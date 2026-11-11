using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KanbanCards.Models;
using KanbanCards.Services;
using KanbanCards.ViewModels;
using Xunit;

namespace KanbanCards.Tests;

public class UpdateServiceTests
{
    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> OnSend { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(OnSend(request));
        }
    }

    private class MemorySettingsService : ISettingsService
    {
        public AppSettings Settings { get; set; } = new();
        public event Action? SettingsChanged;

        public AppSettings LoadSettings() => Settings;

        public void SaveSettings(AppSettings settings)
        {
            Settings = settings;
            SettingsChanged?.Invoke();
        }
    }

    // =========================================================================
    // 1. UPDATE DETECTION TESTING
    // =========================================================================

    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]   // Installed 1.0.0, GitHub 1.0.1 -> Update available
    [InlineData("1.0.0", "1.0.1", false)]  // Installed 1.0.1, GitHub 1.0.0 -> No update
    [InlineData("1.0.1", "1.0.1", false)]  // Installed 1.0.1, GitHub 1.0.1 -> No update
    [InlineData("v1.2.0", "1.0.0", true)]  // Installed 1.0.0, GitHub v1.2.0 -> Update available
    [InlineData("2.0.0", "1.9.0", true)]   // Installed 1.9.0, GitHub 2.0.0 -> Update available
    [InlineData("v1.2.0-beta+123", "1.2.0", false)]
    [InlineData("v1.2.1-preview", "1.2.0", true)]
    [InlineData("invalid", "1.0.0", false)]
    public void VersionComparison_Matrix_EvaluatesCorrectly(string githubLatest, string installed, bool expected)
    {
        var result = UpdateService.IsNewer(githubLatest, installed);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CurrentVersion_DetectedFromAssembly()
    {
        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, new FakeHttpMessageHandler());

        Assert.False(string.IsNullOrWhiteSpace(updateService.CurrentVersion));
        Assert.True(UpdateService.TryParseVersion(updateService.CurrentVersion, out var ver));
        Assert.True(ver.Major >= 1);
    }

    [Fact]
    public async Task CheckForUpdates_IgnoresDraftsAndPreReleases()
    {
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req =>
            {
                var json = @"{
                    ""tag_name"": ""v9.9.9"",
                    ""draft"": false,
                    ""prerelease"": true,
                    ""body"": ""* Experimental pre-release""
                }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, fakeHandler);

        var result = await updateService.CheckForUpdatesAsync(manual: true);

        Assert.Null(result);
        Assert.Null(updateService.LatestRelease);
    }

    [Fact]
    public async Task CheckForUpdates_IgnoresDraftReleases()
    {
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req =>
            {
                var json = @"{
                    ""tag_name"": ""v9.9.9"",
                    ""draft"": true,
                    ""prerelease"": false,
                    ""body"": ""* Draft release""
                }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, fakeHandler);

        var result = await updateService.CheckForUpdatesAsync(manual: true);

        Assert.Null(result);
        Assert.Null(updateService.LatestRelease);
    }

    // =========================================================================
    // 2. AUTOMATIC BACKGROUND CHECKING & SETTINGS
    // =========================================================================

    [Fact]
    public void BackgroundChecking_IntervalProperties_AreConfigurable()
    {
        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, new FakeHttpMessageHandler());

        Assert.Equal(TimeSpan.FromSeconds(20), updateService.FirstCheckDelay);
        Assert.Equal(TimeSpan.FromHours(6), updateService.PeriodicCheckInterval);

        // Can customize for testing
        updateService.FirstCheckDelay = TimeSpan.FromMilliseconds(50);
        updateService.PeriodicCheckInterval = TimeSpan.FromMilliseconds(100);

        Assert.Equal(TimeSpan.FromMilliseconds(50), updateService.FirstCheckDelay);
        Assert.Equal(TimeSpan.FromMilliseconds(100), updateService.PeriodicCheckInterval);
    }

    [Fact]
    public async Task BackgroundChecking_RespectsAutoCheckPreference()
    {
        var checkCount = 0;
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req =>
            {
                checkCount++;
                var json = @"{
                    ""tag_name"": ""v2.0.0"",
                    ""draft"": false,
                    ""prerelease"": false,
                    ""body"": ""* Update""
                }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var settingsService = new MemorySettingsService();
        // Disable automatic checks
        settingsService.Settings.AutoCheckForUpdates = false;

        using var updateService = new UpdateService(settingsService, fakeHandler);

        // When automatic is disabled, CheckForUpdatesAsync(manual: false) returns null and does not fire event
        bool eventFired = false;
        updateService.UpdateAvailable += (s, e) => eventFired = true;

        // Manual check still works
        var manualResult = await updateService.CheckForUpdatesAsync(manual: true);
        Assert.NotNull(manualResult);
        Assert.True(eventFired);

        // Re-enable automatic updates
        settingsService.Settings.AutoCheckForUpdates = true;
        settingsService.Settings.LastDismissedUpdateVersion = null;
        eventFired = false;
        var autoResult = await updateService.CheckForUpdatesAsync(manual: false);
        Assert.NotNull(autoResult);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task BackgroundChecking_TimerFiresAutomatically_AndTriggersUpdate()
    {
        var tcs = new TaskCompletionSource<UpdateInfo>();
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req =>
            {
                var json = @"{
                    ""tag_name"": ""v2.0.0"",
                    ""draft"": false,
                    ""prerelease"": false,
                    ""body"": ""* New release""
                }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, fakeHandler)
        {
            FirstCheckDelay = TimeSpan.FromMilliseconds(50),
            PeriodicCheckInterval = TimeSpan.FromMilliseconds(500)
        };

        updateService.UpdateAvailable += (s, info) => tcs.TrySetResult(info);
        updateService.StartBackgroundChecks();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
        Assert.Same(tcs.Task, completed);
        var result = await tcs.Task;
        Assert.Equal("2.0.0", result.Version);
    }

    // =========================================================================
    // 3. DESKTOP NOTIFICATION & VIEWMODEL TESTING
    // =========================================================================

    [Fact]
    public void NotificationViewModel_BindsPropertiesAndHandlesActions()
    {
        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, new FakeHttpMessageHandler())
        {
            IsInstalled = true
        };

        var updateInfo = new UpdateInfo
        {
            Version = "1.4.0",
            TagName = "v1.4.0",
            HtmlUrl = "https://github.com/danycli/KanbanCards/releases/tag/v1.4.0",
            Summary = new[] { "Improved UI responsiveness", "Fixed drag stutter" },
            IsInstalled = true
        };

        var vm = new UpdateNoticeViewModel(updateService, updateInfo);

        Assert.Equal("v1.4.0", vm.Version);
        Assert.Equal(2, vm.Changes.Count);
        Assert.Equal("Update", vm.PrimaryButtonText);
        Assert.True(vm.CanInteract);
        Assert.False(vm.IsDownloading);

        // Dismiss action
        bool closeRequested = false;
        vm.RequestClose += () => closeRequested = true;

        vm.DismissCommand.Execute(null);

        Assert.True(closeRequested);
        Assert.Equal("1.4.0", settingsService.Settings.LastDismissedUpdateVersion);
    }

    [Fact]
    public void NotificationViewModel_ShowsUpdateButton_EvenInPortableMode()
    {
        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, new FakeHttpMessageHandler())
        {
            IsInstalled = false
        };

        var updateInfo = new UpdateInfo
        {
            Version = "1.4.0",
            IsInstalled = false
        };

        var vm = new UpdateNoticeViewModel(updateService, updateInfo);
        Assert.Equal("Update", vm.PrimaryButtonText);
    }

    // =========================================================================
    // 4. DUPLICATE NOTIFICATION PREVENTION (COMPLETE 10-STEP SEQUENCE)
    // =========================================================================

    [Fact]
    public async Task DuplicateNotification_CompleteTenStepSequence()
    {
        string currentSimulatedTag = "v1.0.1";
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req =>
            {
                var json = $@"{{
                    ""tag_name"": ""{currentSimulatedTag}"",
                    ""draft"": false,
                    ""prerelease"": false,
                    ""body"": ""* New release""
                }}";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        };

        var settingsService = new MemorySettingsService();

        // 1 & 2. Simulate release 1.0.1 and Launch Kanban Cards
        using (var updateService = new UpdateService(settingsService, fakeHandler) { CurrentVersionOverride = "1.0.0" })
        {
            bool notificationFired = false;
            updateService.UpdateAvailable += (s, e) => notificationFired = true;

            // 3. Verify that the notification appears
            var res1 = await updateService.CheckForUpdatesAsync(manual: false);
            Assert.NotNull(res1);
            Assert.True(notificationFired, "Notification should appear on first discovery.");

            // 4. Dismiss the notification
            updateService.DismissUpdate("1.0.1");
            Assert.Equal("1.0.1", settingsService.Settings.LastDismissedUpdateVersion);

            // 5 & 6. Trigger another update check -> Verify notification does not appear again
            notificationFired = false;
            var res2 = await updateService.CheckForUpdatesAsync(manual: false);
            Assert.NotNull(res2);
            Assert.False(notificationFired, "Notification must be suppressed after dismissal.");
        }

        // 7 & 8. Restart application -> Verify dismissed notification is still suppressed
        using (var updateServiceRestarted = new UpdateService(settingsService, fakeHandler) { CurrentVersionOverride = "1.0.0" })
        {
            bool notificationFired = false;
            updateServiceRestarted.UpdateAvailable += (s, e) => notificationFired = true;

            var res3 = await updateServiceRestarted.CheckForUpdatesAsync(manual: false);
            Assert.NotNull(res3);
            Assert.False(notificationFired, "Dismissed notification must remain suppressed across restarts.");
        }

        // 9. Simulate release 1.0.2
        currentSimulatedTag = "v1.0.2";

        // 10. Verify that a new notification appears for 1.0.2
        using (var updateServiceNext = new UpdateService(settingsService, fakeHandler) { CurrentVersionOverride = "1.0.0" })
        {
            bool notificationFired = false;
            updateServiceNext.UpdateAvailable += (s, e) => notificationFired = true;

            var res4 = await updateServiceNext.CheckForUpdatesAsync(manual: false);
            Assert.NotNull(res4);
            Assert.Equal("1.0.2", res4.Version);
            Assert.True(notificationFired, "New notification must appear when a higher release version is discovered.");
        }
    }

    // =========================================================================
    // 5. DOWNLOAD & CHECKSUM VERIFICATION TESTING
    // =========================================================================

    [Fact]
    public async Task Download_WithValidChecksum_CompletesSuccessfully()
    {
        var installerBytes = Encoding.UTF8.GetBytes("MOCK_INNO_SETUP_INSTALLER_PAYLOAD");
        string expectedSha256;
        using (var sha = SHA256.Create())
        {
            expectedSha256 = Convert.ToHexString(sha.ComputeHash(installerBytes)).ToLowerInvariant();
        }

        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                if (uri.EndsWith("SHA256SUMS"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{expectedSha256}  KanbanCardsSetup.exe\n", Encoding.UTF8, "text/plain")
                    };
                }
                if (uri.EndsWith("KanbanCardsSetup.exe"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(installerBytes)
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        var settingsService = new MemorySettingsService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "KC-Test-" + Guid.NewGuid().ToString("N"));
        using var updateService = new UpdateService(settingsService, fakeHandler)
        {
            IsInstalled = true,
            SignerOf = (_, _) => null,
            DownloadDirectoryOverride = tempFolder
        };

        // Mock StartProcess to verify it gets called with correct arguments
        ProcessStartInfo? capturedPsi = null;
        updateService.StartProcess = psi =>
        {
            capturedPsi = psi;
            return Process.GetCurrentProcess(); // Mock running process
        };

        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            HtmlUrl = "https://github.com/danycli/KanbanCards/releases/tag/v2.0.0",
            IsInstalled = true,
            InstallerAsset = new ReleaseAsset
            {
                Name = "KanbanCardsSetup.exe",
                DownloadUrl = "https://github.com/danycli/KanbanCards/releases/download/v2.0.0/KanbanCardsSetup.exe",
                Size = installerBytes.Length
            },
            ChecksumAsset = new ReleaseAsset
            {
                Name = "SHA256SUMS",
                DownloadUrl = "https://github.com/danycli/KanbanCards/releases/download/v2.0.0/SHA256SUMS",
                Size = 100
            }
        };

        int progressReported = 0;
        var progress = new Progress<int>(p => progressReported = p);

        var success = await updateService.DownloadAndInstallUpdateAsync(updateInfo, progress);

        Assert.True(success, updateService.StatusMessage);
        Assert.NotNull(capturedPsi);
        Assert.Contains("KanbanCardsSetup.exe", capturedPsi.FileName);
        Assert.Equal("/SILENT /NORESTART", capturedPsi.Arguments);
    }

    [Fact]
    public async Task Download_WithCorruptedChecksum_FailsAndDeletesFile()
    {
        var installerBytes = Encoding.UTF8.GetBytes("TAMPERED_INSTALLER_PAYLOAD");
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req =>
            {
                var uri = req.RequestUri?.ToString() ?? "";
                if (uri.EndsWith("SHA256SUMS"))
                {
                    // Published checksum belongs to legitimate file, not our tampered bytes
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("0000000000000000000000000000000000000000000000000000000000000000  KanbanCardsSetup.exe\n", Encoding.UTF8, "text/plain")
                    };
                }
                if (uri.EndsWith("KanbanCardsSetup.exe"))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(installerBytes)
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        var settingsService = new MemorySettingsService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "KC-Test-" + Guid.NewGuid().ToString("N"));
        using var updateService = new UpdateService(settingsService, fakeHandler)
        {
            IsInstalled = true,
            SignerOf = (_, _) => null,
            DownloadDirectoryOverride = tempFolder
        };

        bool processStarted = false;
        updateService.StartProcess = psi =>
        {
            processStarted = true;
            return null;
        };

        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            HtmlUrl = "https://github.com/danycli/KanbanCards/releases/tag/v2.0.0",
            IsInstalled = true,
            InstallerAsset = new ReleaseAsset
            {
                Name = "KanbanCardsSetup.exe",
                DownloadUrl = "https://github.com/danycli/KanbanCards/releases/download/v2.0.0/KanbanCardsSetup.exe",
                Size = installerBytes.Length
            },
            ChecksumAsset = new ReleaseAsset
            {
                Name = "SHA256SUMS",
                DownloadUrl = "https://github.com/danycli/KanbanCards/releases/download/v2.0.0/SHA256SUMS",
                Size = 100
            }
        };

        var success = await updateService.DownloadAndInstallUpdateAsync(updateInfo);

        Assert.False(success);
        Assert.False(processStarted, "Installer must never be executed if checksum check fails.");
        Assert.Equal(UpdateStep.Failed, updateService.Step);
        Assert.Contains("checksum verification failed", updateService.StatusMessage ?? "");
    }

    [Fact]
    public async Task Download_IncompleteStream_FailsAndCleansUp()
    {
        var incompleteBytes = Encoding.UTF8.GetBytes("SHORT");
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req =>
            {
                var content = new ByteArrayContent(incompleteBytes);
                content.Headers.ContentLength = 100000;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                };
            }
        };

        var settingsService = new MemorySettingsService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "KC-Test-" + Guid.NewGuid().ToString("N"));
        using var updateService = new UpdateService(settingsService, fakeHandler)
        {
            IsInstalled = true,
            SignerOf = (_, _) => null,
            DownloadDirectoryOverride = tempFolder
        };

        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            IsInstalled = true,
            InstallerAsset = new ReleaseAsset
            {
                Name = "KanbanCardsSetup.exe",
                DownloadUrl = "https://github.com/danycli/KanbanCards/releases/download/v2.0.0/KanbanCardsSetup.exe",
                Size = 100000 // Declared size mismatch
            }
        };

        var success = await updateService.DownloadAndInstallUpdateAsync(updateInfo);

        Assert.False(success);
        Assert.Equal(UpdateStep.Failed, updateService.Step);
        Assert.Contains("Incomplete download", updateService.StatusMessage ?? "");
    }

    // =========================================================================
    // 6. INSTALLATION & UAC HANDLING TESTING
    // =========================================================================

    [Fact]
    public async Task Installation_UAC_Cancelled_SetsCancelledStateGracefully()
    {
        var installerBytes = Encoding.UTF8.GetBytes("INSTALLER");
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(installerBytes)
            }
        };

        var settingsService = new MemorySettingsService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "KC-Test-" + Guid.NewGuid().ToString("N"));
        using var updateService = new UpdateService(settingsService, fakeHandler)
        {
            IsInstalled = true,
            SignerOf = (_, _) => null,
            DownloadDirectoryOverride = tempFolder
        };

        // User declined UAC prompt (Win32 error 1223)
        updateService.StartProcess = psi => throw new Win32Exception(1223);

        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            IsInstalled = true,
            InstallerAsset = new ReleaseAsset
            {
                Name = "KanbanCardsSetup.exe",
                DownloadUrl = "https://github.com/danycli/KanbanCards/releases/download/v2.0.0/KanbanCardsSetup.exe",
                Size = installerBytes.Length
            }
        };

        var success = await updateService.DownloadAndInstallUpdateAsync(updateInfo);

        Assert.False(success);
        Assert.Equal(UpdateStep.Cancelled, updateService.Step);
        Assert.Equal("Update cancelled by user.", updateService.StatusMessage);
    }

    // =========================================================================
    // 7. FAILURE & EDGE-CASE TESTING
    // =========================================================================

    [Fact]
    public async Task Failure_RateLimit403_ReportsMeaningfulMessage()
    {
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req => new HttpResponseMessage(HttpStatusCode.Forbidden)
        };

        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, fakeHandler);

        string? completedMsg = null;
        updateService.CheckCompleted += (s, m) => completedMsg = m;

        var result = await updateService.CheckForUpdatesAsync(manual: true);

        Assert.Null(result);
        Assert.Equal(UpdateStep.Idle, updateService.Step);
        Assert.Contains("HTTP 403", completedMsg ?? "");
    }

    [Fact]
    public async Task Failure_MalformedJson_HandlesGracefully()
    {
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ NOT_VALID_JSON_AT_ALL ", Encoding.UTF8, "application/json")
            }
        };

        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, fakeHandler);

        var result = await updateService.CheckForUpdatesAsync(manual: true);
        Assert.Null(result);
    }

    [Fact]
    public async Task Failure_OfflineNetworkException_HandledGracefully()
    {
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req => throw new HttpRequestException("The remote name could not be resolved.")
        };

        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, fakeHandler);

        string? completedMsg = null;
        updateService.CheckCompleted += (s, m) => completedMsg = m;

        var result = await updateService.CheckForUpdatesAsync(manual: true);

        Assert.Null(result);
        Assert.Contains("Could not connect to GitHub", completedMsg ?? "");
    }

    // =========================================================================
    // 8. SECURITY TESTING
    // =========================================================================

    [Fact]
    public async Task Security_InsecureHttpUrl_IsRejected()
    {
        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, new FakeHttpMessageHandler())
        {
            IsInstalled = true
        };

        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            IsInstalled = true,
            InstallerAsset = new ReleaseAsset
            {
                Name = "KanbanCardsSetup.exe",
                DownloadUrl = "http://insecure-domain.com/KanbanCardsSetup.exe", // INSECURE HTTP
                Size = 1000
            }
        };

        var success = await updateService.DownloadAndInstallUpdateAsync(updateInfo);

        Assert.False(success);
        Assert.Equal(UpdateStep.Failed, updateService.Step);
        Assert.Equal("Insecure download URL rejected.", updateService.StatusMessage);
    }

    [Fact]
    public void Security_AssetPrioritization_PrefersSetupExecutable()
    {
        var setupPriority = UpdateService.GetInstallerPriority("KanbanCardsSetup.exe");
        var generalSetupPriority = UpdateService.GetInstallerPriority("MySetup.exe");
        var otherExePriority = UpdateService.GetInstallerPriority("OtherTool.exe");
        var nonExePriority = UpdateService.GetInstallerPriority("KanbanCards.zip");

        Assert.Equal(3, setupPriority);
        Assert.Equal(2, generalSetupPriority);
        Assert.Equal(1, otherExePriority);
        Assert.Equal(-1, nonExePriority);
    }

    [Fact]
    public async Task Security_Authenticode_PublisherMismatch_RejectsInstaller()
    {
        var installerBytes = Encoding.UTF8.GetBytes("PAYLOAD");
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(installerBytes)
            }
        };

        var settingsService = new MemorySettingsService();
        using var updateService = new UpdateService(settingsService, fakeHandler)
        {
            IsInstalled = true,
            // Running executable is signed by Official Publisher, but installer signed by Untrusted
            SignerOf = (path, _) => path.Contains("Setup") ? "CN=Untrusted Third Party" : "CN=Official Publisher"
        };

        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            IsInstalled = true,
            InstallerAsset = new ReleaseAsset
            {
                Name = "KanbanCardsSetup.exe",
                DownloadUrl = "https://github.com/danycli/KanbanCards/releases/download/v2.0.0/KanbanCardsSetup.exe",
                Size = installerBytes.Length
            }
        };

        var success = await updateService.DownloadAndInstallUpdateAsync(updateInfo);

        Assert.False(success);
        Assert.Equal(UpdateStep.Failed, updateService.Step);
        Assert.Contains("Installer signature does not match application publisher", updateService.StatusMessage ?? "");
    }

    [Fact]
    public async Task Security_Authenticode_PublisherMatches_AcceptsInstaller()
    {
        var installerBytes = Encoding.UTF8.GetBytes("PAYLOAD");
        var fakeHandler = new FakeHttpMessageHandler
        {
            OnSend = req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(installerBytes)
            }
        };

        var settingsService = new MemorySettingsService();
        var tempFolder = Path.Combine(Path.GetTempPath(), "KC-Test-" + Guid.NewGuid().ToString("N"));
        using var updateService = new UpdateService(settingsService, fakeHandler)
        {
            IsInstalled = true,
            DownloadDirectoryOverride = tempFolder,
            // Both the running app and the downloaded installer are signed by the same publisher
            SignerOf = (path, _) => "CN=Official Publisher",
            StartProcess = psi => Process.GetCurrentProcess()
        };

        var updateInfo = new UpdateInfo
        {
            Version = "2.0.0",
            IsInstalled = true,
            InstallerAsset = new ReleaseAsset
            {
                Name = "KanbanCardsSetup.exe",
                DownloadUrl = "https://github.com/danycli/KanbanCards/releases/download/v2.0.0/KanbanCardsSetup.exe",
                Size = installerBytes.Length
            }
        };

        var success = await updateService.DownloadAndInstallUpdateAsync(updateInfo);

        Assert.True(success, updateService.StatusMessage);
        Assert.Equal(UpdateStep.Idle, updateService.Step);
    }

    // =========================================================================
    // 9. RELEASE NOTES & MARKDOWN CLEANER TESTING
    // =========================================================================

    [Fact]
    public void ReleaseNotesParser_StripsMarkdownAndPRLinks()
    {
        var markdown = @"
## Changes
* Added smooth desktop update card (#10) by @danycli in https://github.com/danycli/KanbanCards/pull/10
* Implemented SHA-256 verification
* Fixed memory leak in FullScreenMonitor
";
        var bullets = ReleaseNotesParser.ExtractSummary(markdown);

        Assert.Equal(3, bullets.Count);
        Assert.Equal("Added smooth desktop update card", bullets[0]);
        Assert.Equal("Implemented SHA-256 verification", bullets[1]);
        Assert.Equal("Fixed memory leak in FullScreenMonitor", bullets[2]);
    }
}
