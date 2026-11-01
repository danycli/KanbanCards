using System;
using System.Collections.Generic;
using System.Windows.Input;
using KanbanCards.Models;
using KanbanCards.Services;

namespace KanbanCards.ViewModels;

public class UpdateNoticeViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;
    private readonly UpdateInfo _updateInfo;
    private int _downloadPercent;
    private bool _isDownloading;
    private string? _statusText;

    public string Title => "A new Kanban Cards update is available.";
    public string Version => $"v{_updateInfo.Version}";
    public string Description => "A new version of Kanban Cards is ready to install.";
    public IReadOnlyList<string> Changes => _updateInfo.Summary;
    public string PrimaryButtonText => "Update";

    public int DownloadPercent
    {
        get => _downloadPercent;
        set => SetProperty(ref _downloadPercent, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        set
        {
            if (SetProperty(ref _isDownloading, value))
            {
                OnPropertyChanged(nameof(CanInteract));
            }
        }
    }

    public bool CanInteract => !IsDownloading;

    public string? StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand UpdateCommand { get; }
    public ICommand DismissCommand { get; }
    public ICommand ViewReleaseNotesCommand { get; }

    public event Action? RequestClose;

    public UpdateNoticeViewModel(IUpdateService updateService, UpdateInfo updateInfo)
    {
        _updateService = updateService;
        _updateInfo = updateInfo;

        UpdateCommand = new RelayCommand(async _ =>
        {
            if (_updateInfo.InstallerAsset == null && string.IsNullOrWhiteSpace(_updateInfo.InstallerAsset?.DownloadUrl))
            {
                _updateService.OpenReleasePage(_updateInfo.HtmlUrl);
                RequestClose?.Invoke();
                return;
            }

            IsDownloading = true;
            DownloadPercent = 0;
            StatusText = "Downloading update... 0%";

            var progress = new Progress<int>(percent =>
            {
                DownloadPercent = percent;
                StatusText = percent < 100 ? $"Downloading update... {percent}%" : "Verifying update...";
            });

            var success = await _updateService.DownloadAndInstallUpdateAsync(_updateInfo, progress);
            if (!success && _updateService.Step != UpdateStep.Cancelled)
            {
                IsDownloading = false;
                StatusText = _updateService.StatusMessage ?? "Update failed.";
            }
            else if (_updateService.Step == UpdateStep.Cancelled)
            {
                IsDownloading = false;
                StatusText = "Update cancelled.";
            }
            else if (success)
            {
                StatusText = "Installing update...";
            }
        }, _ => !IsDownloading);

        DismissCommand = new RelayCommand(_ =>
        {
            _updateService.DismissUpdate(_updateInfo.Version);
            RequestClose?.Invoke();
        });

        ViewReleaseNotesCommand = new RelayCommand(_ =>
        {
            _updateService.OpenReleasePage(_updateInfo.HtmlUrl);
        });
    }
}
