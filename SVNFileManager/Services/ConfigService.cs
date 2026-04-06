using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SVNFileManager.Models;

namespace SVNFileManager.Services;

public class ConfigService
{
    private readonly string _configDir;
    private readonly string _configPath;
    private AppConfig _config;

    public ConfigService()
    {
        _configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SVNFileManager");
        _configPath = Path.Combine(_configDir, "config.json");
        _config = new AppConfig();
    }

    public string ConfigDir => _configDir;

    public async Task<AppConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath);
                _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading config: {ex.Message}");
        }
        return _config;
    }

    public async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(_configDir);
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving config: {ex.Message}");
        }
    }

    public AppConfig Config => _config;
}
