use log::{debug, info};
use notify::{Config, Event, RecommendedWatcher, RecursiveMode, Watcher};
use serde::{Deserialize, Serialize};
use std::path::Path;
use std::sync::mpsc::channel;

use std::thread;
use std::time::Duration;
use tauri::{AppHandle, Emitter, State};

use crate::AppState;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct FileChangedEvent {
    pub path: String,
    pub kind: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct WatcherConfig {
    pub debounce_ms: u64,
    pub watch_depth: String, // "root", "one_level", "recursive"
}

/// Start watching a directory
#[tauri::command]
pub fn start_watching(
    path: String,
    config: WatcherConfig,
    app_handle: AppHandle,
    state: State<'_, AppState>,
) -> Result<(), String> {
    info!("Starting file watcher for: {} with config: {:?}", path, config);

    // Stop existing watcher if any
    stop_watching_internal(&state)?;

    let watch_path = Path::new(&path);
    if !watch_path.exists() {
        return Err(format!("Path does not exist: {}", path));
    }

    // Create channel for events
    let (tx, rx) = channel();

    // Create watcher
    let mut watcher = RecommendedWatcher::new(
        move |res: Result<Event, notify::Error>| {
            if let Ok(event) = res {
                let _ = tx.send(event);
            }
        },
        Config::default().with_poll_interval(Duration::from_millis(300)),
    )
    .map_err(|e| format!("Failed to create watcher: {}", e))?;

    // Determine watch mode
    let mode = match config.watch_depth.as_str() {
        "one_level" => RecursiveMode::NonRecursive,
        _ => RecursiveMode::Recursive,
    };

    watcher
        .watch(watch_path, mode)
        .map_err(|e| format!("Failed to watch path: {}", e))?;

    // Update state
    *state.watcher_active.lock().unwrap() = true;
    *state.active_repo_path.lock().unwrap() = Some(path.clone());

    // Spawn thread to handle events
    let app_handle_clone = app_handle.clone();
    let debounce_ms = config.debounce_ms;

    thread::spawn(move || {
        let mut last_event: Option<FileChangedEvent> = None;
        let mut last_event_time = std::time::Instant::now();

        loop {
            match rx.recv_timeout(Duration::from_millis(100)) {
                Ok(event) => {
                    for event_path in event.paths {
                        let path_str = event_path.to_string_lossy().to_string();

                        // Skip .svn directory
                        if path_str.contains(".svn") {
                            continue;
                        }

                        let kind = match event.kind {
                            notify::EventKind::Create(_) => "create",
                            notify::EventKind::Modify(_) => "modify",
                            notify::EventKind::Remove(_) => "remove",
                            _ => continue,
                        };

                        let file_event = FileChangedEvent {
                            path: path_str,
                            kind: kind.to_string(),
                        };

                        // Debounce
                        let should_emit = if let Some(last) = &last_event {
                            last.path == file_event.path
                                && last.kind == file_event.kind
                                && last_event_time.elapsed().as_millis() < debounce_ms as u128
                        } else {
                            true
                        };

                        if should_emit {
                            debug!("Emitting file changed event: {:?}", file_event);
                            let _ = app_handle_clone.emit("file-changed", &file_event);
                            last_event = Some(file_event.clone());
                            last_event_time = std::time::Instant::now();
                        }
                    }
                }
                Err(std::sync::mpsc::RecvTimeoutError::Timeout) => {
                    // Continue loop
                }
                Err(std::sync::mpsc::RecvTimeoutError::Disconnected) => {
                    info!("Watcher channel disconnected, stopping");
                    break;
                }
            }
        }
    });

    info!("File watcher started successfully");
    Ok(())
}

/// Stop watching
#[tauri::command]
pub fn stop_watching(state: State<'_, AppState>) -> Result<(), String> {
    info!("Stopping file watcher");
    stop_watching_internal(&state)
}

fn stop_watching_internal(state: &State<'_, AppState>) -> Result<(), String> {
    *state.watcher_active.lock().unwrap() = false;
    *state.active_repo_path.lock().unwrap() = None;
    info!("File watcher stopped");
    Ok(())
}
