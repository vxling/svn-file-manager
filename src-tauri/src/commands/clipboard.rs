use log::{debug, error};

/// Get file paths from clipboard
/// Note: This is a placeholder - actual implementation depends on clipboard content type
#[tauri::command]
pub fn get_clipboard_files() -> Result<Vec<String>, String> {
    debug!("Getting files from clipboard");

    // On Linux, we can check for file:// URIs in clipboard
    // This is a simplified implementation
    // A full implementation would need platform-specific clipboard handling

    // For now, return empty - real implementation would need clipboard-parser crate
    Ok(Vec::new())
}

/// Copy path to clipboard
#[tauri::command]
pub fn copy_path_to_clipboard(path: String) -> Result<(), String> {
    debug!("Copying path to clipboard: {}", path);

    // Use tauri-plugin-clipboard-manager through the frontend
    // This command just validates the path
    if path.is_empty() {
        return Err("Path cannot be empty".to_string());
    }

    Ok(())
}
