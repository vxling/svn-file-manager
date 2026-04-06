// Prevents additional console window on Windows in release
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod commands;
mod watcher;

use commands::{fs, clipboard, drop_handler, svn};
use log::{error, info};
use std::sync::Mutex;
use tauri::Manager;

/// Application state shared across commands
pub struct AppState {
    pub watcher_active: Mutex<bool>,
    pub active_repo_path: Mutex<Option<String>>,
}

impl Default for AppState {
    fn default() -> Self {
        Self {
            watcher_active: Mutex::new(false),
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
            fs::get_workdir_path,
            // SVN commands
            svn::svn_status,
            svn::svn_info,
            svn::svn_update,
            svn::svn_commit,
            svn::svn_add,
            svn::svn_delete,
            svn::svn_revert,
            svn::svn_diff,
            svn::svn_log,
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
