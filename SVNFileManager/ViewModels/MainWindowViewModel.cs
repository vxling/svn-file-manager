using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        IsLoading = true;
        StatusText = "Loading configuration...";

        try
        {
            await _configService.LoadAsync();
            var config = _configService.Config;

            AutoCommitEnabled = config.AutoCommitEnabled;
            AutoCommitMessage = config.AutoCommitMessage;

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
            StatusText = svnAvailable ? "Ready" : "SVN not found - some features may not work";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedRepositoryChanged(Repository? value)
    {
        if (value != null && Directory.Exists(value.Path))
        {
            _ = LoadDirectoryAsync(value.Path);
            StartWatching(value.Path);

            foreach (var repo in Repositories)
            {
                repo.IsActive = repo.Path == value.Path;
            }

            var config = _configService.Config;
            config.ActiveRepositoryPath = value.Path;
            _ = _configService.SaveAsync();
        }
    }

    partial void OnCurrentPathChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
        {
            _ = LoadDirectoryAsync(value);
        }
    }

    public async Task LoadDirectoryAsync(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

        IsLoading = true;
        StatusText = $"Loading {path}...";
        CurrentPath = path;

        try
        {
            var statuses = await _svnService.GetStatusAsync(path);
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

            // Replace entire collection to trigger UI refresh
            Files = new ObservableCollection<FileItem>(items);
            OnPropertyChanged(nameof(Files));

            StatusText = $"{path} - {items.Count} items";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task OpenItemAsync(FileItem? item)
    {
        if (item == null) return Task.CompletedTask;

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
                // Setting CurrentPath will trigger OnCurrentPathChanged which calls LoadDirectoryAsync
                CurrentPath = targetPath;
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
                StatusText = $"Error opening file: {ex.Message}";
            }
        }

        return Task.CompletedTask;
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

        IsLoading = true;
        StatusText = "Committing all changes...";

        try
        {
            var result = await _svnService.CommitAsync(SelectedRepository.Path, AutoCommitMessage);
            StatusText = result ? "Commit successful" : "Commit failed";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Commit error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UpdateRepositoryAsync()
    {
        if (SelectedRepository == null) return;

        IsLoading = true;
        StatusText = "Updating repository...";

        try
        {
            var result = await _svnService.UpdateAsync(SelectedRepository.Path);
            StatusText = result ? "Update successful" : "Update failed";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Update error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SvnAddAsync()
    {
        if (SelectedRepository == null) return;

        IsLoading = true;
        StatusText = "Adding unversioned files...";

        try
        {
            var result = await _svnService.SvnAddRecursiveAsync(SelectedRepository.Path);
            StatusText = result.exitCode == 0 ? "Files added" : $"Add failed: {result.output}";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Add error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void StartWatching(string path)
    {
        _fileWatcher.StartWatching(path);

        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = new System.Timers.Timer(10000);
        _autoRefreshTimer.Elapsed += async (_, _) => await RefreshAsync();
        _autoRefreshTimer.Start();
    }

    private async void OnFilesChanged(object? sender, string[] files)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await RefreshAsync();
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
