using System;
using System.IO;
using System.Text.Json;

namespace KanbanCards.Services;

public class AppSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool LaunchMinimizedToTray { get; set; } = true;
    public bool ShowNotesOnDesktop { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public int AnimationSpeed { get; set; } = 50; // 0-100
    public string StackPosition { get; set; } = "Right";
}

public interface ISettingsService
{
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
    event Action? SettingsChanged;
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;

    public event Action? SettingsChanged;

    public SettingsService()
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        _settingsFilePath = Path.Join(path, "KanbanCards", "settings.json");
    }

    public AppSettings LoadSettings()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                // return default
            }
        }
        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        var json = JsonSerializer.Serialize(settings);
        File.WriteAllText(_settingsFilePath, json);
        SettingsChanged?.Invoke();
    }
}
