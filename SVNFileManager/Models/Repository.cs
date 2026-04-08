using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SVNFileManager.Models;

public partial class Repository : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _path = "";

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private DateTime _lastSync;

    public Repository() { }

    public Repository(string name, string path, string url = "")
    {
        Name = name;
        Path = path;
        Url = url;
        IsActive = false;
    }
}

public class AppConfig
{
    [JsonPropertyName("repositories")]
    public List<Repository> Repositories { get; set; } = new();

    [JsonPropertyName("activeRepository")]
    public string? ActiveRepositoryPath { get; set; }

    [JsonPropertyName("autoRefreshIntervalSeconds")]
    public int AutoRefreshIntervalSeconds { get; set; } = 10;

    [JsonPropertyName("autoRefreshEnabled")]
    public bool AutoRefreshEnabled { get; set; } = true;

    [JsonPropertyName("theme")]
    public int Theme { get; set; } = 0; // 0=System, 1=Light, 2=Dark

    [JsonPropertyName("watchPathFilter")]
    public string WatchPathFilter { get; set; } = "";

    [JsonPropertyName("autoCommitEnabled")]
    public bool AutoCommitEnabled { get; set; } = false;

    [JsonPropertyName("autoCommitMessage")]
    public string AutoCommitMessage { get; set; } = "Auto-sync";
}
