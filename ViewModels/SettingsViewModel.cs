using System.Windows.Input;
using KanbanCards.Services;

namespace KanbanCards.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly IUpdateService _updateService;
    private AppSettings _settings;
    private string _updateStatusText = "Check for Updates";
    private bool _isCheckingUpdates;

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

    public bool AutoCheckForUpdates
    {
        get => _settings.AutoCheckForUpdates;
        set { _settings.AutoCheckForUpdates = value; Save(); OnPropertyChanged(); }
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set => SetProperty(ref _updateStatusText, value);
    }

    public bool IsCheckingUpdates
    {
        get => _isCheckingUpdates;
        set => SetProperty(ref _isCheckingUpdates, value);
    }

    public string AppVersion => $"v{_updateService.CurrentVersion}";

    public ICommand CheckForUpdatesCommand { get; }

    public SettingsViewModel(ISettingsService settingsService, IStartupService startupService, IUpdateService updateService)
    {
        _settingsService = settingsService;
        _startupService = startupService;
        _updateService = updateService;
        _settings = _settingsService.LoadSettings();

        CheckForUpdatesCommand = new RelayCommand(async _ =>
        {
            if (IsCheckingUpdates) return;

            IsCheckingUpdates = true;
            UpdateStatusText = "Checking...";

            var result = await _updateService.CheckForUpdatesAsync(manual: true);
            IsCheckingUpdates = false;

            if (result != null)
            {
                UpdateStatusText = $"Update Available: v{result.Version}";
                App.ShowUpdateNotice(_updateService, result);
            }
            else
            {
                UpdateStatusText = _updateService.StatusMessage ?? "You're on the latest version.";
                App.ShowUpToDateNotice(_updateService.CurrentVersion, _updateService.StatusMessage);
            }
        });
    }

    private void Save()
    {
        _settingsService.SaveSettings(_settings);
        _startupService.SetStartup(_settings.StartWithWindows);
        OnPropertyChanged(string.Empty);
    }
}
