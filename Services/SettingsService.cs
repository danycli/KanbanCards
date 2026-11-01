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
    public bool AutoCheckForUpdates { get; set; } = true;
    public string? LastDismissedUpdateVersion { get; set; } = null;
    public DateTime? LastUpdateCheckTime { get; set; } = null;
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
    private readonly object _fileLock = new();

    public event Action? SettingsChanged;

    public SettingsService() : this(Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "KanbanCards", "settings.json"))
    {
    }

    public SettingsService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public AppSettings LoadSettings()
    {
        lock (_fileLock)
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
                    // Check if a backup file is available if target file was corrupted
                    var backupPath = _settingsFilePath + ".bak";
                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            var backupJson = File.ReadAllText(backupPath);
                            return JsonSerializer.Deserialize<AppSettings>(backupJson) ?? new AppSettings();
                        }
                        catch { }
                    }
                }
            }
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        lock (_fileLock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
            var tempFile = _settingsFilePath + ".tmp";
            var backupFile = _settingsFilePath + ".bak";
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(tempFile, json);

            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    File.Copy(_settingsFilePath, backupFile, overwrite: true);
                }
                catch { }
            }

            File.Move(tempFile, _settingsFilePath, overwrite: true);
            SettingsChanged?.Invoke();
        }
    }
}
