using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SVNFileManager.Models;
using SVNFileManager.Services;

namespace SVNFileManager.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly SvnService _svnService;
    private readonly ConfigService _configService;
    private readonly FileWatcherService _fileWatcher;

    [ObservableProperty]
    private ObservableCollection<FileItem> _files = new();

    [ObservableProperty]
    private ObservableCollection<Repository> _repositories = new();

    [ObservableProperty]
    private Repository? _selectedRepository;

    [ObservableProperty]
    private FileItem? _selectedFile;

    [ObservableProperty]
    private string _currentPath = "";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private bool _autoRefreshEnabled = true;

    [ObservableProperty]
    private bool _autoCommitEnabled;

    [ObservableProperty]
    private string _autoCommitMessage = "Auto-sync";

    private System.Timers.Timer? _autoRefreshTimer;
    private readonly string _configDir;

    public MainWindowViewModel()
    {
        _svnService = new SvnService();
        _configService = new ConfigService();
        _fileWatcher = new FileWatcherService();
        _fileWatcher.FilesChanged += OnFilesChanged;
        _configDir = _configService.ConfigDir;
    }

    public async Task InitializeAsync()
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            StatusText = "Loading configuration...";
        });

        try
        {
            await _configService.LoadAsync();
            var config = _configService.Config;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                AutoCommitEnabled = config.AutoCommitEnabled;
                AutoCommitMessage = config.AutoCommitMessage;
            });

            foreach (var repo in config.Repositories)
            {
                Repositories.Add(repo);
            }

            if (!string.IsNullOrEmpty(config.ActiveRepositoryPath))
            {
                var active = Repositories.FirstOrDefault(r => r.Path == config.ActiveRepositoryPath);
                if (active != null)
                {
                    SelectedRepository = active;
                }
            }

            if (SelectedRepository != null && Directory.Exists(SelectedRepository.Path))
            {
                await LoadDirectoryAsync(SelectedRepository.Path);
                StartWatching(SelectedRepository.Path);
            }

            var svnAvailable = await _svnService.IsSvnAvailableAsync();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = svnAvailable ? "Ready" : "SVN not found - some features may not work";
                IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"Error: {ex.Message}";
                IsLoading = false;
            });
        }
    }

    partial void OnSelectedRepositoryChanged(Repository? value)
    {
        if (value != null && Directory.Exists(value.Path))
        {
            foreach (var repo in Repositories)
            {
                repo.IsActive = repo.Path == value.Path;
            }

            var config = _configService.Config;
            config.ActiveRepositoryPath = value.Path;
            _ = _configService.SaveAsync();

            _ = LoadDirectoryAsync(value.Path);
            StartWatching(value.Path);
        }
    }



    public async Task LoadDirectoryAsync(string path)
    {
        Debug.WriteLine($"[DEBUG] LoadDirectoryAsync entered: {path}");
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            Debug.WriteLine($"[DEBUG] LoadDirectoryAsync: path invalid or not exists, returning. path=[{path}]");
            return;
        }

        // Marshal all UI updates to UI thread
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            StatusText = $"Loading {path}...";
        });

        try
        {
            Debug.WriteLine($"[DEBUG] LoadDirectoryAsync: calling GetStatusAsync for {path}");
            var statuses = await _svnService.GetStatusAsync(path);
            Debug.WriteLine($"[DEBUG] LoadDirectoryAsync: GetStatusAsync returned {statuses.Count} statuses");
            var items = new List<FileItem>();

            var dirInfo = new DirectoryInfo(path);
            var parentPath = dirInfo.Parent?.FullName;

            if (parentPath != null)
            {
                items.Add(new FileItem { Name = "..", FullPath = parentPath, IsDirectory = true, SvnStatus = SvnStatus.Normal });
            }

            foreach (var dir in dirInfo.GetDirectories())
            {
                if (dir.Name.StartsWith(".")) continue;

                var dirPath = dir.FullName;
                var status = statuses.TryGetValue(dirPath, out var s) ? s : SvnStatus.Normal;

                items.Add(new FileItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    SvnStatus = status
                });
            }

            foreach (var file in dirInfo.GetFiles())
            {
                if (file.Name.StartsWith(".")) continue;

                var status = statuses.TryGetValue(file.FullName, out var s) ? s : SvnStatus.Normal;

                items.Add(new FileItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    FileSize = file.Length,
                    LastModified = file.LastWriteTime,
                    SvnStatus = status
                });
            }

            // Replace entire collection on UI thread (ensures compiled bindings refresh)
            Debug.WriteLine($"[DEBUG] LoadDirectoryAsync: marshalling to UI thread, {items.Count} items");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Debug.WriteLine($"[DEBUG] LoadDirectoryAsync: on UI thread, replacing Files with {items.Count} items");
                Files = new ObservableCollection<FileItem>(items);
                OnPropertyChanged(nameof(Files));  // Force compiled binding to refresh
                CurrentPath = path;
                StatusText = $"{path} - {items.Count} items";
                IsLoading = false;
                Debug.WriteLine($"[DEBUG] LoadDirectoryAsync: UI update complete, Files count={Files.Count}");
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DEBUG] LoadDirectoryAsync EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[DEBUG] Stack: {ex.StackTrace}");
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"Error: {ex.Message}";
                IsLoading = false;
            });
        }
    }

    // Public method for direct invocation from code-behind
    public async void OpenItem(FileItem? item)
    {
        Debug.WriteLine($"[DEBUG] OpenItem called: {(item != null ? item.Name : "null")}");
        if (item == null)
        {
            Debug.WriteLine("[DEBUG] OpenItem: item is null, returning");
            return;
        }

        var isDir = item.IsDirectory || Directory.Exists(item.FullPath);
        Debug.WriteLine($"[DEBUG] OpenItem: isDir={isDir}, FullPath={item.FullPath}");

        if (isDir)
        {
            string? targetPath = null;

            if (item.Name == "..")
            {
                Debug.WriteLine("[DEBUG] OpenItem: handling '..' parent directory");
                var parent = Directory.GetParent(item.FullPath);
                if (parent != null)
                    targetPath = parent.FullName;
                else
                    Debug.WriteLine("[DEBUG] OpenItem: parent is null!");
            }
            else if (Directory.Exists(item.FullPath))
            {
                targetPath = item.FullPath;
            }
            else
            {
                Debug.WriteLine($"[DEBUG] OpenItem: directory does not exist: {item.FullPath}");
            }

            if (targetPath != null)
            {
                Debug.WriteLine($"[DEBUG] OpenItem: stopping watcher before navigation");
                StopWatching();  // Stop old watcher before navigating
                Debug.WriteLine($"[DEBUG] OpenItem: calling LoadDirectoryAsync for: {targetPath}");
                await LoadDirectoryAsync(targetPath);
                Debug.WriteLine($"[DEBUG] OpenItem: LoadDirectoryAsync returned for: {targetPath}");
            }
        }
        else
        {
            Debug.WriteLine($"[DEBUG] OpenItem: opening file: {item.FullPath}");
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                });
                Debug.WriteLine($"[DEBUG] OpenItem: file opened successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] OpenItem: file open error: {ex.Message}");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText = $"Error opening file: {ex.Message}";
                });
            }
        }
    }

    [RelayCommand]
    private async Task OpenItemAsyncInternal(FileItem? item)
    {
        if (item == null) return;

        var isDir = item.IsDirectory || Directory.Exists(item.FullPath);

        if (isDir)
        {
            string? targetPath = null;

            if (item.Name == "..")
            {
                var parent = Directory.GetParent(item.FullPath);
                if (parent != null)
                    targetPath = parent.FullName;
            }
            else if (Directory.Exists(item.FullPath))
            {
                targetPath = item.FullPath;
            }

            if (targetPath != null)
            {
                await LoadDirectoryAsync(targetPath);
            }
        }
        else
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    StatusText = $"Error opening file: {ex.Message}";
                });
            }
        }
    }

    [RelayCommand]
    private async Task AddRepositoryAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select SVN Working Copy"
            };

            var result = await dialog.ShowAsync(desktop.MainWindow!);
            if (!string.IsNullOrEmpty(result) && Directory.Exists(result))
            {
                var url = await _svnService.GetRepoUrlAsync(result);
                var name = Path.GetFileName(result);

                var repo = new Repository
                {
                    Name = name,
                    Path = result,
                    Url = url,
                    IsActive = true
                };

                foreach (var r in Repositories)
                {
                    r.IsActive = false;
                }

                Repositories.Add(repo);
                SelectedRepository = repo;

                var config = _configService.Config;
                config.Repositories = Repositories.ToList();
                await _configService.SaveAsync();

                StatusText = $"Added repository: {name}";
            }
        }
    }

    [RelayCommand]
    private async Task RemoveRepositoryAsync(Repository? repo)
    {
        if (repo == null) return;

        Repositories.Remove(repo);

        var config = _configService.Config;
        config.Repositories = Repositories.ToList();
        await _configService.SaveAsync();

        if (SelectedRepository == repo)
        {
            SelectedRepository = Repositories.FirstOrDefault();
        }

        StatusText = $"Removed repository: {repo.Name}";
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!string.IsNullOrEmpty(CurrentPath))
        {
            await LoadDirectoryAsync(CurrentPath);
        }
    }

    [RelayCommand]
    private async Task CommitAllAsync()
    {
        if (SelectedRepository == null) return;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            StatusText = "Committing all changes...";
        });

        try
        {
            var result = await _svnService.CommitAsync(SelectedRepository.Path, AutoCommitMessage);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = result ? "Commit successful" : "Commit failed";
            });
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"Commit error: {ex.Message}";
            });
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
            });
        }
    }

    [RelayCommand]
    private async Task UpdateRepositoryAsync()
    {
        if (SelectedRepository == null) return;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            StatusText = "Updating repository...";
        });

        try
        {
            var result = await _svnService.UpdateAsync(SelectedRepository.Path);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = result ? "Update successful" : "Update failed";
            });
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"Update error: {ex.Message}";
            });
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
            });
        }
    }

    [RelayCommand]
    private async Task SvnAddAsync()
    {
        if (SelectedRepository == null) return;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsLoading = true;
            StatusText = "Adding unversioned files...";
        });

        try
        {
            var result = await _svnService.SvnAddRecursiveAsync(SelectedRepository.Path);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = result.exitCode == 0 ? "Files added" : $"Add failed: {result.output}";
            });
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = $"Add error: {ex.Message}";
            });
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
            });
        }
    }

    private void StopWatching()
    {
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;
        _fileWatcher.StopWatching();
    }

    private void StartWatching(string path)
    {
        // Stop existing watcher and timer first
        _autoRefreshTimer?.Dispose();
        _fileWatcher.StopWatching();

        _fileWatcher.StartWatching(path);

        _autoRefreshTimer = new System.Timers.Timer(10000);
        _autoRefreshTimer.Elapsed += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await RefreshAsync();
            });
        };
        _autoRefreshTimer.Start();
    }

    private async void OnFilesChanged(object? sender, string[] files)
    {
        // Don't call RefreshAsync (it restarts watcher) - just reload current directory
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (!string.IsNullOrEmpty(CurrentPath))
            {
                await LoadDirectoryAsync(CurrentPath);
            }
        });
    }

    partial void OnAutoCommitEnabledChanged(bool value)
    {
        var config = _configService.Config;
        config.AutoCommitEnabled = value;
        _ = _configService.SaveAsync();
    }

    partial void OnAutoCommitMessageChanged(string value)
    {
        var config = _configService.Config;
        config.AutoCommitMessage = value;
        _ = _configService.SaveAsync();
    }

    public void Dispose()
    {
        _fileWatcher.Dispose();
        _autoRefreshTimer?.Dispose();
    }
}
