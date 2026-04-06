using System;
using System.Collections.Generic;
using System.IO;

namespace SVNFileManager.Services;

public class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly System.Timers.Timer _debounceTimer;
    private readonly object _lock = new();
    private readonly HashSet<string> _changedFiles = new();
    private int _debounceMs = 300;

    public event EventHandler<string[]>? FilesChanged;

    public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents;

    public FileWatcherService()
    {
        _debounceTimer = new System.Timers.Timer(_debounceMs);
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += OnDebounceTimerElapsed;
    }

    public void StartWatching(string path)
    {
        StopWatching();

        if (!Directory.Exists(path)) return;

        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName |
                          NotifyFilters.DirectoryName |
                          NotifyFilters.LastWrite |
                          NotifyFilters.Size
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnWatcherError;

        _watcher.EnableRaisingEvents = true;
    }

    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public void SetDebounceMs(int ms)
    {
        _debounceMs = ms;
        _debounceTimer.Interval = ms;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            _changedFiles.Add(e.FullPath);
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        lock (_lock)
        {
            _changedFiles.Add(e.OldFullPath);
            _changedFiles.Add(e.FullPath);
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"FileWatcher error: {e.GetException().Message}");
    }

    private void OnDebounceTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        string[] files;
        lock (_lock)
        {
            files = new List<string>(_changedFiles).ToArray();
            _changedFiles.Clear();
        }

        if (files.Length > 0)
        {
            FilesChanged?.Invoke(this, files);
        }
    }

    public void Dispose()
    {
        StopWatching();
        _debounceTimer.Dispose();
    }
}
