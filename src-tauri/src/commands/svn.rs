use log::{debug, error, info};
use quick_xml::events::Event;
use quick_xml::Reader;
use serde::{Deserialize, Serialize};
use std::process::Command;

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct SvnStatusEntry {
    pub path: String,
    pub status: String,
    pub props_status: String,
    pub revision: Option<u32>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct SvnStatusResult {
    pub entries: Vec<SvnStatusEntry>,
    pub wc_root: Option<String>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct SvnInfo {
    pub url: String,
    pub revision: u32,
    pub kind: String,
    pub repository_root: String,
    pub wc_path: String,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct SvnLogEntry {
    pub revision: u32,
    pub author: Option<String>,
    pub date: Option<String>,
    pub message: Option<String>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct SvnUpdateResult {
    pub revision: u32,
    pub updated: Vec<String>,
    pub skipped: Vec<String>,
}

/// Run svn command and return output
fn run_svn(args: &[&str], working_dir: Option<&str>) -> Result<String, String> {
    debug!("Running svn {:?}", args);

    let output = Command::new("svn")
        .args(args)
        .current_dir(working_dir.unwrap_or("."))
        .output()
        .map_err(|e| format!("Failed to execute svn: {}", e))?;

    let stdout = String::from_utf8_lossy(&output.stdout).to_string();
    let stderr = String::from_utf8_lossy(&output.stderr).to_string();

    if !output.status.success() {
        error!("svn command failed: {}", stderr);
        return Err(format!("svn command failed: {}", stderr));
    }

    debug!("svn output: {}", stdout);
    Ok(stdout)
}

/// Parse svn status XML output
fn parse_status_xml(xml: &str) -> Result<SvnStatusResult, String> {
    let mut entries = Vec::new();
    let mut reader = Reader::from_str(xml);
    reader.config_mut().trim_text(true);

    let mut buf = Vec::new();
    let mut current_entry: Option<SvnStatusEntry> = None;
    let mut current_field: Option<String> = None;

    loop {
        match reader.read_event_into(&mut buf) {
            Ok(Event::Start(ref e)) | Ok(Event::Empty(ref e)) => {
                let name = String::from_utf8_lossy(e.name().as_ref()).to_string();

                match name.as_str() {
                    "entry" => {
                        current_entry = Some(SvnStatusEntry {
                            path: String::new(),
                            status: String::new(),
                            props_status: String::new(),
                            revision: None,
                        });
                    }
                    "wc-status" | "repo-status" => {
                        if let Some(ref mut entry) = current_entry {
                            for attr in e.attributes().flatten() {
                                let key = String::from_utf8_lossy(attr.key.as_ref()).to_string();
                                let value = String::from_utf8_lossy(&attr.value).to_string();
                                if key == "item" {
                                    entry.status = value;
                                } else if key == "props" {
                                    entry.props_status = value;
                                } else if key == "revision" {
                                    entry.revision = value.parse().ok();
                                }
                            }
                        }
                    }
                    "path" | "commit" | "author" | "date" | "revision" => {
                        current_field = Some(name);
                    }
                    _ => {}
                }
            }
            Ok(Event::Text(e)) => {
                if let Some(ref mut entry) = current_entry {
                    if let Some(field) = &current_field {
                        let text = e.unescape().unwrap_or_default().to_string();
                        match field.as_str() {
                            "path" => entry.path = text,
                            "author" => entry.author = Some(text),
                            "date" => entry.date = Some(text),
                            "revision" => {
                                entry.revision = text.parse().ok();
                            }
                            _ => {}
                        }
                    }
                }
            }
            Ok(Event::End(ref e)) => {
                let name = String::from_utf8_lossy(e.name().as_ref()).to_string();
                if name == "entry" {
                    if let Some(entry) = current_entry.take() {
                        entries.push(entry);
                    }
                } else if name == "wc-status" || name == "repo-status" || name == "path" {
                    current_field = None;
                }
            }
            Ok(Event::Eof) => break,
            Err(e) => return Err(format!("XML parse error: {}", e)),
            _ => {}
        }
        buf.clear();
    }

    Ok(SvnStatusResult {
        entries,
        wc_root: None,
    })
}

/// Get SVN status for a path
#[tauri::command]
pub fn svn_status(path: String) -> Result<SvnStatusResult, String> {
    info!("Getting SVN status for: {}", path);

    let output = run_svn(&["status", "--xml", "--no-ignore", "--ignore-externals"], Some(&path))?;

    parse_status_xml(&output)
}

/// Get SVN info for a path
#[tauri::command]
pub fn svn_info(path: String) -> Result<SvnInfo, String> {
    info!("Getting SVN info for: {}", path);

    let output = run_svn(&["info", "--xml"], Some(&path))?;

    let mut reader = Reader::from_str(&output);
    reader.config_mut().trim_text(true);

    let mut buf = Vec::new();
    let mut info = SvnInfo {
        url: String::new(),
        revision: 0,
        kind: String::new(),
        repository_root: String::new(),
        wc_path: path.clone(),
    };
    let mut current_field: Option<String> = None;

    loop {
        match reader.read_event_into(&mut buf) {
            Ok(Event::Start(ref e)) | Ok(Event::Empty(ref e)) => {
                let name = String::from_utf8_lossy(e.name().as_ref()).to_string();

                match name.as_str() {
                    "entry" => {
                        for attr in e.attributes().flatten() {
                            let key = String::from_utf8_lossy(attr.key.as_ref()).to_string();
                            let value = String::from_utf8_lossy(&attr.value).to_string();
                            if key == "kind" {
                                info.kind = value;
                            } else if key == "revision" {
                                info.revision = value.parse().unwrap_or(0);
                            }
                        }
                    }
                    "url" | "repository" | "wc" | "commit" | "author" | "date" => {
                        current_field = Some(name);
                    }
                    _ => {}
                }
            }
            Ok(Event::Text(e)) => {
                if let Some(field) = &current_field {
                    let text = e.unescape().unwrap_or_default().to_string();
                    match field.as_str() {
                        "url" => info.url = text,
                        "root" => info.repository_root = text,
                        _ => {}
                    }
                }
            }
            Ok(Event::End(ref e)) => {
                let name = String::from_utf8_lossy(e.name().as_ref()).to_string();
                if name == "url" || name == "repository" || name == "wc" || name == "commit" {
                    current_field = None;
                }
            }
            Ok(Event::Eof) => break,
            Err(e) => return Err(format!("XML parse error: {}", e)),
            _ => {}
        }
        buf.clear();
    }

    Ok(info)
}

/// Update working copy
#[tauri::command]
pub fn svn_update(path: String) -> Result<SvnUpdateResult, String> {
    info!("Updating SVN working copy: {}", path);

    let output = run_svn(&["update", "--xml"], Some(&path))?;

    let mut reader = Reader::from_str(&output);
    reader.config_mut().trim_text(true);

    let mut buf = Vec::new();
    let mut result = SvnUpdateResult {
        revision: 0,
        updated: Vec::new(),
        skipped: Vec::new(),
    };
    let mut current_field: Option<String> = None;

    loop {
        match reader.read_event_into(&mut buf) {
            Ok(Event::Start(ref e)) | Ok(Event::Empty(ref e)) => {
                let name = String::from_utf8_lossy(e.name().as_ref()).to_string();

                match name.as_str() {
                    "update" => {
                        for attr in e.attributes().flatten() {
                            let key = String::from_utf8_lossy(attr.key.as_ref()).to_string();
                            let value = String::from_utf8_lossy(&attr.value).to_string();
                            if key == "revision" {
                                result.revision = value.parse().unwrap_or(0);
                            }
                        }
                    }
                    "target" | "path" | "action" => {
                        current_field = Some(name);
                    }
                    _ => {}
                }
            }
            Ok(Event::Text(e)) => {
                if let Some(field) = &current_field {
                    let text = e.unescape().unwrap_or_default().to_string();
                    if field.as_str() == "path" && !text.is_empty() {
                        // Determine if this was updated or skipped based on context
                        result.updated.push(text);
                    }
                }
            }
            Ok(Event::End(ref e)) => {
                let name = String::from_utf8_lossy(e.name().as_ref()).to_string();
                if name == "target" || name == "path" || name == "action" {
                    current_field = None;
                }
            }
            Ok(Event::Eof) => break,
            Err(e) => return Err(format!("XML parse error: {}", e)),
            _ => {}
        }
        buf.clear();
    }

    info!("Update completed, revision: {}", result.revision);
    Ok(result)
}

/// Commit changes
#[tauri::command]
pub fn svn_commit(path: String, message: String) -> Result<u32, String> {
    info!("Committing changes in: {} with message: {}", path, message);

    let output = run_svn(&["commit", "-m", &message], Some(&path))?;

    // Parse revision from output
    let revision = if output.contains("Committed revision") {
        output
            .lines()
            .find(|l| l.contains("Committed revision"))
            .and_then(|l| {
                l.split("Committed revision")
                    .nth(1)?
                    .trim()
                    .split_whitespace()
                    .next()?
                    .trim_end_matches('.')
                    .parse()
                    .ok()
            })
            .unwrap_or(0)
    } else {
        0
    };

    info!("Commit completed, revision: {}", revision);
    Ok(revision)
}

/// Add file to version control
#[tauri::command]
pub fn svn_add(path: String) -> Result<(), String> {
    info!("Adding to SVN: {}", path);

    run_svn(&["add", &path], None)?;

    info!("Added successfully");
    Ok(())
}

/// Delete from version control (keeps local file)
#[tauri::command]
pub fn svn_delete(path: String) -> Result<(), String> {
    info!("Deleting from SVN: {}", path);

    run_svn(&["delete", &path], None)?;

    info!("Deleted successfully");
    Ok(())
}

/// Revert local changes
#[tauri::command]
pub fn svn_revert(path: String) -> Result<(), String> {
    info!("Reverting: {}", path);

    run_svn(&["revert", &path], None)?;

    info!("Reverted successfully");
    Ok(())
}

/// Get diff for a file
#[tauri::command]
pub fn svn_diff(path: String) -> Result<String, String> {
    info!("Getting diff for: {}", path);

    let output = run_svn(&["diff", &path], None)?;

    Ok(output)
}

/// Get log entries
#[tauri::command]
pub fn svn_log(path: String, limit: u32) -> Result<Vec<SvnLogEntry>, String> {
    info!("Getting log for: {} with limit: {}", path, limit);

    let output = run_svn(
        &["log", "--xml", "-l", &limit.to_string()],
        Some(&path),
    )?;

    let mut reader = Reader::from_str(&output);
    reader.config_mut().trim_text(true);

    let mut buf = Vec::new();
    let mut entries = Vec::new();
    let mut current_entry: Option<SvnLogEntry> = None;
    let mut current_field: Option<String> = None;

    loop {
        match reader.read_event_into(&mut buf) {
            Ok(Event::Start(ref e)) | Ok(Event::Empty(ref e)) => {
                let name = String::from_utf8_lossy(e.name().as_ref()).to_string();

                match name.as_str() {
                    "logentry" => {
                        current_entry = Some(SvnLogEntry {
                            revision: 0,
                            author: None,
                            date: None,
                            message: None,
                        });
                        for attr in e.attributes().flatten() {
                            let key = String::from_utf8_lossy(attr.key.as_ref()).to_string();
                            let value = String::from_utf8_lossy(&attr.value).to_string();
                            if key == "revision" {
                                current_entry.as_mut().unwrap().revision = value.parse().unwrap_or(0);
                            }
                        }
                    }
                    "author" | "date" | "msg" => {
                        current_field = Some(name);
                    }
                    _ => {}
                }
            }
            Ok(Event::Text(e)) => {
                if let Some(ref mut entry) = current_entry {
                    if let Some(field) = &current_field {
                        let text = e.unescape().unwrap_or_default().to_string();
                        match field.as_str() {
                            "author" => entry.author = Some(text),
                            "date" => entry.date = Some(text),
                            "msg" => entry.message = Some(text),
                            _ => {}
                        }
                    }
                }
            }
            Ok(Event::End(ref e)) => {
                let name = String::from_utf8_lossy(e.name().as_ref()).to_string();
                if name == "logentry" {
                    if let Some(entry) = current_entry.take() {
                        entries.push(entry);
                    }
                } else if name == "author" || name == "date" || name == "msg" {
                    current_field = None;
                }
            }
            Ok(Event::Eof) => break,
            Err(e) => return Err(format!("XML parse error: {}", e)),
            _ => {}
        }
        buf.clear();
    }

    Ok(entries)
}
