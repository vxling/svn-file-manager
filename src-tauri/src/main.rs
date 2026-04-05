// Prevents additional console window on Windows in release
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod commands;
mod svn;
mod watcher;

use commands::{fs, svn as svn_cmd, clipboard, drop_handler};
use log::{error, info};
use std::sync::Mutex;
use tauri::{Manager, State};
use watcher::FileWatcher;

/// Application state shared across commands
pub struct AppState {
    pub watcher: Mutex<Option<FileWatcher>>,
    pub active_repo_path: Mutex<Option<String>>,
}

impl Default for AppState {
    fn default() -> Self {
        Self {
            watcher: Mutex::new(None),
            active_repo_path: Mutex::new(None),
        }
    }
}

fn main() {
    // Initialize logger
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("info"))
        .format_timestamp_millis()
        .init();

    info!("Starting SVN File Manager v{}", env!("CARGO_PKG_VERSION"));

    let result = tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .plugin(tauri_plugin_clipboard_manager::init())
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_fs::init())
        .plugin(tauri_plugin_notification::init())
        .manage(AppState::default())
        .invoke_handler(tauri::generate_handler![
            // File system commands
            fs::read_directory,
            fs::get_file_info,
            fs::reveal_in_file_browser,
            // SVN commands
            svn_cmd::svn_status,
            svn_cmd::svn_info,
            svn_cmd::svn_update,
            svn_cmd::svn_commit,
            svn_cmd::svn_add,
            svn_cmd::svn_delete,
            svn_cmd::svn_revert,
            svn_cmd::svn_diff,
            svn_cmd::svn_log,
            // Clipboard commands
            clipboard::get_clipboard_files,
            clipboard::copy_path_to_clipboard,
            // Drop handler
            drop_handler::copy_files_to_workdir,
            // File watcher
            watcher::start_watching,
            watcher::stop_watching,
        ])
        .setup(|app| {
            info!("Application setup complete");

            // Ensure data directory exists
            if let Some(data_dir) = app.path().app_data_dir().ok() {
                let workcopies = data_dir.join("workcopies");
                if !workcopies.exists() {
                    if let Err(e) = std::fs::create_dir_all(&workcopies) {
                        error!("Failed to create workcopies directory: {}", e);
                    } else {
                        info!("Created workcopies directory at {:?}", workcopies);
                    }
                }
            }

            Ok(())
        })
        .run(tauri::generate_context!());

    if let Err(e) = result {
        error!("Application error: {}", e);
        std::process::exit(1);
    }
}
