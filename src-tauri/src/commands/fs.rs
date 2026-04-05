use serde::{Deserialize, Serialize};
use std::fs;
use std::path::Path;
use log::{debug, error};

#[derive(Debug, Serialize, Deserialize)]
pub struct FileEntry {
    pub name: String,
    pub path: String,
    pub is_directory: bool,
    pub size: u64,
    pub modified_at: Option<u64>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct FileInfo {
    pub name: String,
    pub path: String,
    pub is_directory: bool,
    pub size: u64,
    pub modified_at: Option<u64>,
    pub created_at: Option<u64>,
}

/// Read directory contents
#[tauri::command]
pub fn read_directory(path: String) -> Result<Vec<FileEntry>, String> {
    debug!("Reading directory: {}", path);

    let dir_path = Path::new(&path);

    if !dir_path.exists() {
        return Err(format!("Directory does not exist: {}", path));
    }

    if !dir_path.is_dir() {
        return Err(format!("Path is not a directory: {}", path));
    }

    let entries = fs::read_dir(dir_path)
        .map_err(|e| format!("Failed to read directory: {}", e))?;

    let mut result: Vec<FileEntry> = Vec::new();

    for entry in entries {
        match entry {
            Ok(entry) => {
                let metadata = entry.metadata().ok();
                let file_name = entry.file_name().to_string_lossy().to_string();
                let file_path = entry.path().to_string_lossy().to_string();

                // Skip .svn directories
                if file_name == ".svn" {
                    continue;
                }

                let is_dir = entry.path().is_dir();
                let size = metadata.as_ref().map(|m| m.len()).unwrap_or(0);
                let modified_at = metadata
                    .as_ref()
                    .and_then(|m| m.modified().ok())
                    .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
                    .map(|d| d.as_secs());

                result.push(FileEntry {
                    name: file_name,
                    path: file_path,
                    is_directory: is_dir,
                    size,
                    modified_at,
                });
            }
            Err(e) => {
                error!("Failed to read directory entry: {}", e);
            }
        }
    }

    // Sort: directories first, then by name
    result.sort_by(|a, b| {
        if a.is_directory != b.is_directory {
            b.is_directory.cmp(&a.is_directory)
        } else {
            a.name.to_lowercase().cmp(&b.name.to_lowercase())
        }
    });

    debug!("Found {} entries in {}", result.len(), path);
    Ok(result)
}

/// Get file information
#[tauri::command]
pub fn get_file_info(path: String) -> Result<FileInfo, String> {
    debug!("Getting file info: {}", path);

    let file_path = Path::new(&path);

    if !file_path.exists() {
        return Err(format!("File does not exist: {}", path));
    }

    let metadata = fs::metadata(&path)
        .map_err(|e| format!("Failed to get metadata: {}", e))?;

    let name = file_path
        .file_name()
        .map(|n| n.to_string_lossy().to_string())
        .unwrap_or_default();

    let modified_at = metadata
        .modified()
        .ok()
        .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
        .map(|d| d.as_secs());

    let created_at = metadata
        .created()
        .ok()
        .and_then(|t| t.duration_since(std::time::UNIX_EPOCH).ok())
        .map(|d| d.as_secs());

    Ok(FileInfo {
        name,
        path: path.clone(),
        is_directory: metadata.is_dir(),
        size: metadata.len(),
        modified_at,
        created_at,
    })
}

/// Reveal file in system file browser (opens folder and selects file)
#[tauri::command]
pub fn reveal_in_file_browser(path: String) -> Result<(), String> {
    debug!("Revealing in file browser: {}", path);

    let file_path = Path::new(&path);

    if !file_path.exists() {
        return Err(format!("Path does not exist: {}", path));
    }

    #[cfg(target_os = "windows")]
    {
        std::process::Command::new("explorer")
            .args(["/select,", &path])
            .spawn()
            .map_err(|e| format!("Failed to open explorer: {}", e))?;
    }

    #[cfg(target_os = "macos")]
    {
        std::process::Command::new("open")
            .args(["-R", &path])
            .spawn()
            .map_err(|e| format!("Failed to open Finder: {}", e))?;
    }

    #[cfg(target_os = "linux")]
    {
        let parent = file_path.parent().unwrap_or(file_path);
        std::process::Command::new("xdg-open")
            .arg(parent)
            .spawn()
            .map_err(|e| format!("Failed to open file manager: {}", e))?;
    }

    Ok(())
}

/// Get workdir path for a repository name
#[tauri::command]
pub fn get_workdir_path(name: String) -> Result<String, String> {
    let data_dir = dirs::data_dir()
        .ok_or_else(|| "Failed to get data directory".to_string())?;
    let workcopies = data_dir.join(".svnfilemanager").join("workcopies").join(&name);
    
    // Create directory if it doesn't exist
    if !workcopies.exists() {
        std::fs::create_dir_all(&workcopies)
            .map_err(|e| format!("Failed to create workcopies directory: {}", e))?;
    }
    
    Ok(workcopies.to_string_lossy().to_string())
}
