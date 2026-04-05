use log::{debug, error, info};
use serde::{Deserialize, Serialize};
use std::fs;
use std::path::{Path, PathBuf};
use walkdir::WalkDir;

#[derive(Debug, Serialize, Deserialize)]
pub struct CopyResult {
    pub copied_count: u32,
    pub failed_paths: Vec<String>,
}

/// Copy files to workdir
#[tauri::command]
pub fn copy_files_to_workdir(
    source_paths: Vec<String>,
    target_dir: String,
) -> Result<CopyResult, String> {
    info!(
        "Copying {} files to workdir: {}",
        source_paths.len(),
        target_dir
    );

    let target = Path::new(&target_dir);

    // Ensure target directory exists
    if !target.exists() {
        fs::create_dir_all(target)
            .map_err(|e| format!("Failed to create target directory: {}", e))?;
    }

    let mut copied_count = 0u32;
    let mut failed_paths = Vec::new();

    for source_path in &source_paths {
        let source = Path::new(source_path);

        if !source.exists() {
            error!("Source path does not exist: {}", source_path);
            failed_paths.push(source_path.clone());
            continue;
        }

        // Determine target path
        let file_name = source
            .file_name()
            .ok_or_else(|| format!("Invalid source path: {}", source_path))?;

        let dest_path = target.join(file_name);

        match copy_path(source, &dest_path) {
            Ok(_) => {
                copied_count += 1;
                info!("Copied: {} -> {:?}", source_path, dest_path);
            }
            Err(e) => {
                error!("Failed to copy {}: {}", source_path, e);
                failed_paths.push(source_path.clone());
            }
        }
    }

    info!(
        "Copy complete: {} succeeded, {} failed",
        copied_count,
        failed_paths.len()
    );

    Ok(CopyResult {
        copied_count,
        failed_paths,
    })
}

/// Copy file or directory to destination
fn copy_path(source: &Path, dest: &Path) -> Result<(), String> {
    if source.is_dir() {
        copy_dir(source, dest)
    } else {
        copy_file(source, dest)
    }
}

/// Copy a single file
fn copy_file(source: &Path, dest: &Path) -> Result<(), String> {
    if dest.exists() {
        // Optionally handle overwrite
        std::fs::remove_file(dest)
            .map_err(|e| format!("Failed to remove existing file: {}", e))?;
    }

    fs::copy(source, dest).map_err(|e| format!("Failed to copy file: {}", e))?;

    // Preserve metadata
    if let Ok(metadata) = fs::metadata(source) {
        if let Ok(mtime) = metadata.modified() {
            let _ = filetime::set_file_mtime(dest, filetime::FileTime::from(mtime));
        }
    }

    Ok(())
}

/// Copy directory recursively
fn copy_dir(source: &Path, dest: &Path) -> Result<(), String> {
    if !dest.exists() {
        fs::create_dir_all(dest)
            .map_err(|e| format!("Failed to create directory: {}", e))?;
    }

    for entry in WalkDir::new(source)
        .into_iter()
        .filter_map(|e| e.ok())
    {
        let relative_path = entry
            .path()
            .strip_prefix(source)
            .map_err(|e| format!("Path error: {}", e))?;

        let target_path = dest.join(relative_path);

        if entry.path().is_dir() {
            if !target_path.exists() {
                fs::create_dir_all(&target_path)
                    .map_err(|e| format!("Failed to create directory: {}", e))?;
            }
        } else {
            // Copy file
            let parent = target_path.parent().unwrap_or(dest);
            if !parent.exists() {
                fs::create_dir_all(parent)
                    .map_err(|e| format!("Failed to create directory: {}", e))?;
            }

            fs::copy(entry.path(), &target_path)
                .map_err(|e| format!("Failed to copy file: {}", e))?;
        }
    }

    Ok(())
}
