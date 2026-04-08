using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SVNFileManager.Services;

namespace SVNFileManager.Views;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly ConfigService _configService;

    [ObservableProperty]
    private int _autoSyncIntervalMinutes = 10;

    [ObservableProperty]
    private string _watchPathFilter = "";

    [ObservableProperty]
    private bool _autoRefreshEnabled = true;

    [ObservableProperty]
    private bool _autoCommitEnabled;

    [ObservableProperty]
    private string _autoCommitMessage = "Auto-sync";

    [ObservableProperty]
    private int _themeIndex; // 0=System, 1=Light, 2=Dark

    public SettingsWindowViewModel(ConfigService configService)
    {
        _configService = configService;
        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        var config = _configService.Config;
        AutoSyncIntervalMinutes = config.AutoRefreshIntervalSeconds / 60;
        AutoRefreshEnabled = config.AutoRefreshEnabled;
        AutoCommitEnabled = config.AutoCommitEnabled;
        AutoCommitMessage = config.AutoCommitMessage;
        // theme would come from config when implemented
    }

    public async Task SaveAsync()
    {
        var config = _configService.Config;
        config.AutoRefreshIntervalSeconds = AutoSyncIntervalMinutes * 60;
        config.AutoRefreshEnabled = AutoRefreshEnabled;
        config.AutoCommitEnabled = AutoCommitEnabled;
        config.AutoCommitMessage = AutoCommitMessage;
        await _configService.SaveAsync();
    }
}
