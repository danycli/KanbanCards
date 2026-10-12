using KanbanCards.Services;
using System.Windows.Input;

namespace KanbanCards.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private AppSettings _settings;

    public bool StartWithWindows
    {
        get => _settings.StartWithWindows;
        set { _settings.StartWithWindows = value; Save(); }
    }

    public bool LaunchMinimizedToTray
    {
        get => _settings.LaunchMinimizedToTray;
        set { _settings.LaunchMinimizedToTray = value; Save(); }
    }

    public bool ShowNotesOnDesktop
    {
        get => _settings.ShowNotesOnDesktop;
        set { _settings.ShowNotesOnDesktop = value; Save(); }
    }

    public bool EnableAnimations
    {
        get => _settings.EnableAnimations;
        set { _settings.EnableAnimations = value; Save(); }
    }

    public int AnimationSpeed
    {
        get => _settings.AnimationSpeed;
        set { _settings.AnimationSpeed = value; Save(); }
    }

    public bool IsStackPositionRight
    {
        get => _settings.StackPosition == "Right";
        set { _settings.StackPosition = value ? "Right" : "Left"; Save(); OnPropertyChanged(); OnPropertyChanged(nameof(IsStackPositionLeft)); }
    }

    public bool IsStackPositionLeft
    {
        get => _settings.StackPosition == "Left";
        set { _settings.StackPosition = value ? "Left" : "Right"; Save(); OnPropertyChanged(); OnPropertyChanged(nameof(IsStackPositionRight)); }
    }

    public string AppVersion
    {
        get
        {
            var version = typeof(SettingsViewModel).Assembly.GetName().Version;
            return version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.1.0";
        }
    }

    public SettingsViewModel(ISettingsService settingsService, IStartupService startupService)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _settings = _settingsService.LoadSettings();
    }

    private void Save()
    {
        _settingsService.SaveSettings(_settings);
        _startupService.SetStartup(_settings.StartWithWindows);
        OnPropertyChanged(string.Empty);
    }
}
