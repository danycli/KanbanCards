// Portions derived from Filee (MIT License - Copyright (c) KnifeLemon).
// Adapted for KanbanCards WPF architecture and Inno Setup distribution.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using KanbanCards.Models;

namespace KanbanCards.Services;

public class UpdateService : IUpdateService, IDisposable
{
    private const string GitHubOwner = "danycli";
    private const string GitHubRepo = "KanbanCards";
    public const string DefaultRepositoryUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}";
    public const string LatestReleaseApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
    private const string InstallerArguments = "/SILENT /NORESTART";
    private const int Win32ErrorCancelled = 1223; // User declined UAC prompt
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PeriodicInterval = TimeSpan.FromHours(6);

    public TimeSpan FirstCheckDelay { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan PeriodicCheckInterval { get; set; } = TimeSpan.FromHours(6);
    public string? ApiUrlOverride { get; set; }
    public string EffectiveApiUrl => !string.IsNullOrWhiteSpace(ApiUrlOverride) ? ApiUrlOverride : LatestReleaseApiUrl;
    public string? CurrentVersionOverride { get; set; }
    public string? DownloadDirectoryOverride { get; set; }
    private string DownloadDirectory => DownloadDirectoryOverride ?? Path.Combine(Path.GetTempPath(), "KanbanCards-update");

    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly string _detectedVersion;
    private System.Timers.Timer? _periodicTimer;
    private bool _isDisposed;
    private bool? _isInstalledOverride;

    internal Func<ProcessStartInfo, Process?> StartProcess { get; set; } = psi => Process.Start(psi);
    internal Func<string, bool, string?> SignerOf { get; set; } = Authenticode.TrustedSigner;
    internal string? OwnProgram { get; set; } = Environment.ProcessPath;

    public string CurrentVersion => CurrentVersionOverride ?? _detectedVersion;
    public string? LatestVersion { get; private set; }
    public UpdateInfo? LatestRelease { get; private set; }
    public UpdateStep Step { get; private set; } = UpdateStep.Idle;
    public int DownloadPercent { get; private set; }
    public bool IsBusy => Step is UpdateStep.Checking or UpdateStep.Downloading or UpdateStep.Installing;
    public string? StatusMessage { get; private set; }

    public static bool DefaultIsInstalled =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe")) ||
        File.Exists(Path.Combine(AppContext.BaseDirectory, "uninstall.exe"));

    public bool IsInstalled
    {
        get => _isInstalledOverride ?? DefaultIsInstalled;
        internal set => _isInstalledOverride = value;
    }

    bool IUpdateService.IsInstalled => IsInstalled;

    public event EventHandler<UpdateInfo>? UpdateAvailable;
    public event EventHandler<string>? CheckCompleted;
    public event Action? StateChanged;

    public UpdateService(ISettingsService settingsService, HttpMessageHandler? handler = null)
    {
        _settingsService = settingsService;
        _detectedVersion = GetCurrentVersion();

        var httpHandler = handler ?? new HttpClientHandler();
        _httpClient = new HttpClient(httpHandler)
        {
            Timeout = TimeSpan.FromSeconds(25)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KanbanCards", CurrentVersion));
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    private static string GetCurrentVersion()
    {
        var assembly = typeof(UpdateService).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            var raw = infoVersion.Split('+')[0];
            if (TryParseVersion(raw, out var parsedVer))
            {
                return $"{parsedVer.Major}.{parsedVer.Minor}.{Math.Max(0, parsedVer.Build)}";
            }
        }
        var version = assembly.GetName().Version;
        if (version != null && version.Major > 0)
        {
            return $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        }

        // Check Windows registry DisplayVersion for installed app
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KanbanCards_is1")
                         ?? Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KanbanCards_is1")
                         ?? Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\KanbanCards_is1");
            if (key?.GetValue("DisplayVersion") is string disp && TryParseVersion(disp, out var dispVer))
            {
                return $"{dispVer.Major}.{dispVer.Minor}.{Math.Max(0, dispVer.Build)}";
            }
        }
        catch { }

        return "1.3.0";
    }

    public void StartBackgroundChecks()
    {
        if (_periodicTimer != null)
            return;

        FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] Starting background checks. FirstCheckDelay={FirstCheckDelay.TotalSeconds:F0}s, PeriodicInterval={PeriodicCheckInterval.TotalMinutes:F0}m");

        _periodicTimer = new System.Timers.Timer
        {
            Interval = Math.Max(50, FirstCheckDelay.TotalMilliseconds),
            AutoReset = false
        };

        _periodicTimer.Elapsed += async (_, _) =>
        {
            FullScreenMonitorService.LogAudit("[UPDATE SERVICE] Periodic timer elapsed. Triggering update check...");
            try
            {
                var settings = _settingsService.LoadSettings();
                if (settings.AutoCheckForUpdates)
                {
                    await CheckForUpdatesAsync(manual: false);
                }
                else
                {
                    FullScreenMonitorService.LogAudit("[UPDATE SERVICE] AutoCheckForUpdates is disabled in settings. Skipping check.");
                }
            }
            catch (Exception ex)
            {
                FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] Background check error: {ex.Message}");
            }
            finally
            {
                if (_periodicTimer != null && !_isDisposed)
                {
                    try
                    {
                        _periodicTimer.Interval = Math.Max(500, PeriodicCheckInterval.TotalMilliseconds);
                        _periodicTimer.Start();
                    }
                    catch { }
                }
            }
        };

        _periodicTimer.Start();
    }

    public void StopBackgroundChecks()
    {
        if (_periodicTimer != null)
        {
            FullScreenMonitorService.LogAudit("[UPDATE SERVICE] Stopping background checks.");
            _periodicTimer.Stop();
            _periodicTimer.Dispose();
            _periodicTimer = null;
        }
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool manual = false, CancellationToken cancellationToken = default)
    {
        if (IsBusy && Step != UpdateStep.Checking)
            return LatestRelease;

        FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] CheckForUpdatesAsync called (manual={manual}, url={EffectiveApiUrl})");
        SetState(UpdateStep.Checking, 0, "Checking for updates...");

        try
        {
            using var response = await _httpClient.GetAsync(EffectiveApiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var msg = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "No public releases published on GitHub yet."
                    : $"Update check failed (HTTP {(int)response.StatusCode}).";
                SetState(UpdateStep.Idle, 0, msg);
                RaiseCheckCompleted(msg);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = json.RootElement;

            // Ignore drafts and prereleases
            if (root.TryGetProperty("draft", out var isDraft) && isDraft.GetBoolean())
            {
                SetState(UpdateStep.Idle, 0, "Latest release is a draft.");
                RaiseCheckCompleted("No release updates available.");
                return null;
            }

            if (root.TryGetProperty("prerelease", out var isPrerelease) && isPrerelease.GetBoolean())
            {
                SetState(UpdateStep.Idle, 0, "Latest release is a prerelease.");
                RaiseCheckCompleted("No release updates available.");
                return null;
            }

            var tagName = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
            var releaseTitle = root.TryGetProperty("name", out var n) ? n.GetString() ?? tagName : tagName;
            var htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? DefaultRepositoryUrl : DefaultRepositoryUrl;
            var body = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            DateTime? publishedAt = null;
            if (root.TryGetProperty("published_at", out var pub) && pub.TryGetDateTime(out var dt))
            {
                publishedAt = dt;
            }

            if (!TryParseVersion(tagName, out var latestParsedVersion))
            {
                SetState(UpdateStep.Idle, 0, "Could not parse version tag.");
                RaiseCheckCompleted("Could not determine version.");
                return null;
            }

            var latestVersionStr = $"{latestParsedVersion.Major}.{latestParsedVersion.Minor}.{Math.Max(0, latestParsedVersion.Build)}";
            LatestVersion = latestVersionStr;

            var settings = _settingsService.LoadSettings();
            settings.LastUpdateCheckTime = DateTime.UtcNow;
            _settingsService.SaveSettings(settings);

            if (!IsNewer(tagName, CurrentVersion))
            {
                FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] App is up to date: Current={CurrentVersion}, Latest={tagName}. No update needed.");
                SetState(UpdateStep.Idle, 0, $"Kanban Cards is up to date (v{CurrentVersion}).");
                RaiseCheckCompleted($"Kanban Cards is up to date (v{CurrentVersion}).");
                return null;
            }

            // An update is available!
            var updateInfo = new UpdateInfo
            {
                Version = latestVersionStr,
                TagName = tagName,
                Title = string.IsNullOrWhiteSpace(releaseTitle) ? $"Kanban Cards v{latestVersionStr}" : releaseTitle,
                HtmlUrl = htmlUrl,
                RawBody = body,
                Summary = ReleaseNotesParser.ExtractSummary(body),
                PublishedAt = publishedAt,
                IsInstalled = IsInstalled
            };

            // Parse release assets with installer prioritization
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                int bestInstallerPriority = 0;
                foreach (var asset in assets.EnumerateArray())
                {
                    var assetName = asset.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
                    var assetUrl = asset.TryGetProperty("browser_download_url", out var au) ? au.GetString() ?? "" : "";
                    var assetSize = asset.TryGetProperty("size", out var asz) ? asz.GetInt64() : 0L;
                    var contentType = asset.TryGetProperty("content_type", out var act) ? act.GetString() : null;

                    var item = new ReleaseAsset
                    {
                        Name = assetName,
                        DownloadUrl = assetUrl,
                        Size = assetSize,
                        ContentType = contentType
                    };

                    int priority = GetInstallerPriority(assetName);
                    if (priority > bestInstallerPriority)
                    {
                        bestInstallerPriority = priority;
                        updateInfo.InstallerAsset = item;
                    }

                    if (updateInfo.ChecksumAsset == null && IsChecksumAsset(assetName))
                    {
                        updateInfo.ChecksumAsset = item;
                    }
                }
            }

            LatestRelease = updateInfo;
            SetState(UpdateStep.Idle, 0, $"Update available: v{latestVersionStr}");
            RaiseCheckCompleted($"Update available: v{latestVersionStr}");

            // Duplicate notification prevention
            bool shouldNotify = manual || !string.Equals(settings.LastDismissedUpdateVersion, latestVersionStr, StringComparison.OrdinalIgnoreCase);
            FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] Check complete: v{latestVersionStr} available. shouldNotify={shouldNotify} (manual={manual}, dismissed={settings.LastDismissedUpdateVersion})");

            if (shouldNotify)
            {
                DispatchOnUI(() =>
                {
                    FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] Invoking UpdateAvailable event for v{latestVersionStr}");
                    UpdateAvailable?.Invoke(this, updateInfo);
                });
            }

            return updateInfo;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
        {
            FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] Network/IO error checking updates: {ex.Message}");
            SetState(UpdateStep.Idle, 0, "Could not connect to GitHub.");
            RaiseCheckCompleted("Could not connect to GitHub.");
            return null;
        }
    }

    public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsBusy && Step != UpdateStep.Idle)
            return false;

        // Check if a newer release has appeared in the meantime
        if (LatestRelease != null && IsNewer(LatestRelease.Version, update.Version))
        {
            update = LatestRelease;
        }

        var installer = update.InstallerAsset;
        if (installer == null || string.IsNullOrWhiteSpace(installer.DownloadUrl))
        {
            // Fallback to opening release page only if no installer asset is found
            OpenReleasePage(update.HtmlUrl);
            return true;
        }

        // HTTPS verification
        if (!installer.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            SetState(UpdateStep.Failed, 0, "Insecure download URL rejected.");
            return false;
        }

        SetState(UpdateStep.Downloading, 0, "Starting download...");

        var tempDir = DownloadDirectory;
        Directory.CreateDirectory(tempDir);

        var safeFileName = Path.GetFileName(installer.Name);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            safeFileName = "KanbanCardsSetup.exe";
        }
        var targetFile = Path.Combine(tempDir, safeFileName);
        var partFile = targetFile + ".part";

        try
        {
            // 1. Download checksums if available via HTTPS
            string? expectedChecksum = null;
            if (update.ChecksumAsset != null &&
                !string.IsNullOrWhiteSpace(update.ChecksumAsset.DownloadUrl) &&
                update.ChecksumAsset.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var checksumRequest = new HttpRequestMessage(HttpMethod.Get, update.ChecksumAsset.DownloadUrl);
                    using var checksumResponse = await _httpClient.SendAsync(checksumRequest, cancellationToken);
                    if (checksumResponse.IsSuccessStatusCode)
                    {
                        var checksumText = await checksumResponse.Content.ReadAsStringAsync(cancellationToken);
                        expectedChecksum = ParseChecksum(checksumText, installer.Name);
                    }
                }
                catch
                {
                    // If checksum file cannot be downloaded, proceed with signature verification
                }
            }

            // 2. Download installer
            if (File.Exists(partFile))
            {
                try { File.Delete(partFile); } catch { }
            }

            using (var stallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                stallCts.CancelAfter(StallTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Get, installer.DownloadUrl);
                request.Headers.Accept.ParseAdd("application/octet-stream");

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stallCts.Token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? installer.Size;
                await using var sourceStream = await response.Content.ReadAsStreamAsync(stallCts.Token);
                await using var fileStream = new FileStream(partFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int lastReportedPercent = -1;
                int bytesRead;

                while ((bytesRead = await sourceStream.ReadAsync(buffer, stallCts.Token)) > 0)
                {
                    stallCts.CancelAfter(StallTimeout); // Reset stall timeout on each packet
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;

                    int percent = totalBytes > 0 ? (int)Math.Min(100, (totalRead * 100) / totalBytes) : 0;
                    if (percent != lastReportedPercent)
                    {
                        lastReportedPercent = percent;
                        SetState(UpdateStep.Downloading, percent, $"Downloading... {percent}%");
                        progress?.Report(percent);
                    }
                }

                var expectedTotal = installer.Size > 0 ? installer.Size : (response.Content.Headers.ContentLength ?? 0);
                if (expectedTotal > 0 && totalRead < expectedTotal)
                {
                    try { File.Delete(partFile); } catch { }
                    throw new IOException($"Incomplete download: received {totalRead} of {expectedTotal} bytes");
                }
            }

            // 3. Verify SHA256 checksum if available
            if (!string.IsNullOrWhiteSpace(expectedChecksum))
            {
                var actualChecksum = await ComputeSha256Async(partFile, cancellationToken);
                if (!string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(partFile); } catch { }
                    SetState(UpdateStep.Failed, 0, "Downloaded file checksum verification failed.");
                    return false;
                }
            }

            // 4. Verify Authenticode digital signature if the running application is signed
            var ownProcessPath = OwnProgram;
            if (!string.IsNullOrWhiteSpace(ownProcessPath) && File.Exists(ownProcessPath))
            {
                var ownSigner = SignerOf(ownProcessPath, false);
                if (!string.IsNullOrWhiteSpace(ownSigner))
                {
                    var installerSigner = SignerOf(partFile, false);
                    if (!string.Equals(ownSigner, installerSigner, StringComparison.Ordinal))
                    {
                        try { File.Delete(partFile); } catch { }
                        SetState(UpdateStep.Failed, 0, "Installer signature does not match application publisher.");
                        return false;
                    }
                }
            }

            // Move completed download into place
            if (File.Exists(targetFile))
            {
                try { File.Delete(targetFile); } catch { }
            }
            File.Move(partFile, targetFile, overwrite: true);

            // 5. Execute installer
            SetState(UpdateStep.Installing, 100, "Launching installer...");

            var psi = new ProcessStartInfo(targetFile, InstallerArguments)
            {
                UseShellExecute = true
            };

            Process? setupProcess;
            try
            {
                setupProcess = StartProcess(psi);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == Win32ErrorCancelled)
            {
                SetState(UpdateStep.Cancelled, 0, "Update cancelled by user.");
                return false;
            }
            catch (Exception ex)
            {
                SetState(UpdateStep.Failed, 0, "Failed to start installer: " + ex.Message);
                return false;
            }

            if (setupProcess != null)
            {
                // Inno Setup's PrepareToInstall script automatically shuts down KanbanCards.exe,
                // upgrades the files, and completes cleanly.
                SetState(UpdateStep.Idle, 0, "Installer running.");
                FullScreenMonitorService.LogAudit("[UPDATE SERVICE] Installer launched successfully. Initiating clean application shutdown...");

                // Spawn a detached watcher to guarantee the updated application is automatically launched
                // immediately after the installer process finishes.
                try
                {
                    var targetExe = GetInstalledAppExecutablePath();
                    var watcherScript = $"Wait-Process -Id {setupProcess.Id}; Start-Sleep -Seconds 1; if (-not (Get-Process KanbanCards -ErrorAction SilentlyContinue)) {{ Start-Process '{targetExe}' }}";
                    var watcherPsi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{watcherScript}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(watcherPsi);
                    FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] Auto-relaunch watcher scheduled for '{targetExe}' once PID {setupProcess.Id} exits.");
                }
                catch (Exception ex)
                {
                    FullScreenMonitorService.LogAudit($"[UPDATE SERVICE] Failed to schedule auto-relaunch watcher: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    DispatchOnUI(() =>
                    {
                        Application.Current?.Shutdown();
                    });
                });

                return true;
            }

            SetState(UpdateStep.Failed, 0, "Failed to start installer.");
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            try { if (File.Exists(partFile)) File.Delete(partFile); } catch { }
            SetState(UpdateStep.Failed, 0, "Download failed: " + ex.Message);
            return false;
        }
    }

    public void OpenReleasePage(string? url = null)
    {
        var targetUrl = !string.IsNullOrWhiteSpace(url) ? url : (LatestRelease?.HtmlUrl ?? DefaultRepositoryUrl);
        try
        {
            Process.Start(new ProcessStartInfo(targetUrl) { UseShellExecute = true });
        }
        catch { }
    }

    public void DismissUpdate(string version)
    {
        var settings = _settingsService.LoadSettings();
        settings.LastDismissedUpdateVersion = version;
        _settingsService.SaveSettings(settings);
    }

    private string GetInstalledAppExecutablePath()
    {
        // 1. Check registry install location (HKLM / HKCU)
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KanbanCards_is1")
                         ?? Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\KanbanCards_is1")
                         ?? Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\KanbanCards_is1");
            if (key != null)
            {
                var loc = key.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(loc))
                {
                    var exe = Path.Combine(loc, "KanbanCards.exe");
                    if (File.Exists(exe))
                        return exe;
                }
            }
        }
        catch { }

        // 2. Check standard Program Files location
        var defaultProgFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KanbanCards", "KanbanCards.exe");
        if (File.Exists(defaultProgFiles))
            return defaultProgFiles;

        // 3. Fallback to own process path if it exists
        if (!string.IsNullOrWhiteSpace(OwnProgram) && File.Exists(OwnProgram))
            return OwnProgram;

        return defaultProgFiles;
    }

    private void SetState(UpdateStep step, int percent, string? message)
    {
        Step = step;
        DownloadPercent = percent;
        StatusMessage = message;
        DispatchOnUI(() => StateChanged?.Invoke());
    }

    private void RaiseCheckCompleted(string message)
    {
        DispatchOnUI(() => CheckCompleted?.Invoke(this, message));
    }

    private static void DispatchOnUI(Action action)
    {
        var app = Application.Current;
        if (app == null || app.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            app.Dispatcher.InvokeAsync(action);
        }
    }

    public static int GetInstallerPriority(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;
        var lower = name.ToLowerInvariant();
        if (!lower.EndsWith(".exe") || lower.Contains("sha256")) return -1;
        if (lower == "kanbancardssetup.exe") return 3;
        if (lower.Contains("setup")) return 2;
        return 1;
    }

    private static bool IsInstallerAsset(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var lower = name.ToLowerInvariant();
        return lower.EndsWith(".exe") && !lower.Contains("sha256");
    }

    private static bool IsChecksumAsset(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var lower = name.ToLowerInvariant();
        return lower == "sha256sums" || lower == "checksums.txt" || lower.EndsWith(".sha256");
    }

    public static string? ParseChecksum(string checksumText, string filename)
    {
        if (string.IsNullOrWhiteSpace(checksumText)) return null;

        var targetName = Path.GetFileName(filename);
        foreach (var raw in checksumText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var hashCandidate = parts[0];
                var fileCandidate = Path.GetFileName(parts[1].TrimStart('*'));
                if (hashCandidate.Length == 64 && string.Equals(fileCandidate, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return hashCandidate.ToLowerInvariant();
                }
            }
            else if (parts.Length == 1 && parts[0].Length == 64)
            {
                return parts[0].ToLowerInvariant();
            }
        }
        return null;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hash = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim().TrimStart('v', 'V');
        var core = trimmed.Split(new[] { '-', '+' }, StringSplitOptions.RemoveEmptyEntries)[0];

        if (!core.Contains('.'))
            core += ".0";

        if (!Version.TryParse(core, out var parsed))
            return false;

        version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build));
        return true;
    }

    public static bool IsNewer(string? releaseTag, string currentVersion) =>
        TryParseVersion(releaseTag, out var releaseVer) &&
        TryParseVersion(currentVersion, out var installedVer) &&
        releaseVer > installedVer;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopBackgroundChecks();
        _httpClient.Dispose();
    }
}
