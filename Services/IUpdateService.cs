using System;
using System.Threading;
using System.Threading.Tasks;
using KanbanCards.Models;

namespace KanbanCards.Services;

/// <summary>
/// Service interface for detecting, downloading, and applying application updates from GitHub Releases.
/// </summary>
public interface IUpdateService
{
    string CurrentVersion { get; }
    string? LatestVersion { get; }
    UpdateInfo? LatestRelease { get; }
    UpdateStep Step { get; }
    int DownloadPercent { get; }
    bool IsBusy { get; }
    bool IsInstalled { get; }
    string? StatusMessage { get; }

    event EventHandler<UpdateInfo>? UpdateAvailable;
    event EventHandler<string>? CheckCompleted;
    event Action? StateChanged;

    void StartBackgroundChecks();
    void StopBackgroundChecks();
    Task<UpdateInfo?> CheckForUpdatesAsync(bool manual = false, CancellationToken cancellationToken = default);
    Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    void OpenReleasePage(string? url = null);
    void DismissUpdate(string version);
}
