using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KanbanCards.Models;
using KanbanCards.Services;
using KanbanCards.ViewModels;
using Xunit;

namespace KanbanCards.Tests;

public class EndToEndUpdateTests
{
    private class FakeHttpServer : HttpMessageHandler
    {
        public byte[] InstallerPayload { get; set; } = Encoding.UTF8.GetBytes("KANBAN_CARDS_UPGRADE_BINARY_v1.3.0_NEW_VERSION");
        public string ChecksumText { get; set; } = string.Empty;
        public string ReleaseJson { get; set; } = string.Empty;

        public FakeHttpServer()
        {
            using var sha = SHA256.Create();
            var hashHex = Convert.ToHexString(sha.ComputeHash(InstallerPayload)).ToLowerInvariant();
            ChecksumText = $"{hashHex}  KanbanCardsSetup.exe\n";

            ReleaseJson = @"
{
  ""tag_name"": ""v1.3.0"",
  ""name"": ""Kanban Cards v1.3.0"",
  ""html_url"": ""https://github.com/danycli/KanbanCards/releases/tag/v1.3.0"",
  ""draft"": false,
  ""prerelease"": false,
  ""published_at"": ""2026-11-01T12:00:00Z"",
  ""body"": ""## What's Changed\n* Fixed dragging stutter on note grab by @danycli\n* Added subtle drop shadow under open note\n* Full screen awareness"",
  ""assets"": [
    {
      ""name"": ""KanbanCardsSetup.exe"",
      ""browser_download_url"": ""https://github.com/danycli/KanbanCards/releases/download/v1.3.0/KanbanCardsSetup.exe"",
      ""size"": " + InstallerPayload.Length + @"
    },
    {
      ""name"": ""SHA256SUMS"",
      ""browser_download_url"": ""https://github.com/danycli/KanbanCards/releases/download/v1.3.0/SHA256SUMS"",
      ""size"": " + ChecksumText.Length + @"
    }
  ]
}";
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? "";

            if (uri.EndsWith("/releases/latest") || uri.EndsWith("releases/latest"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ReleaseJson, Encoding.UTF8, "application/json")
                });
            }
            if (uri.EndsWith("SHA256SUMS"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ChecksumText, Encoding.UTF8, "text/plain")
                });
            }
            if (uri.EndsWith("KanbanCardsSetup.exe"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(InstallerPayload)
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    [Fact]
    public async Task EndToEnd_CompleteUpdatePipeline_FromDetectionToVerification()
    {
        // 1. Setup Isolated Environment
        var isolatedDir = Path.Combine(Path.GetTempPath(), "KanbanCards-E2E-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolatedDir);

        var isolatedSettingsPath = Path.Combine(isolatedDir, "settings.json");
        var initialSettings = new AppSettings
        {
            StartWithWindows = false,
            LaunchMinimizedToTray = true,
            ShowNotesOnDesktop = true,
            AutoCheckForUpdates = true,
            LastDismissedUpdateVersion = null
        };
        File.WriteAllText(isolatedSettingsPath, JsonSerializer.Serialize(initialSettings));

        // Create unins000.exe marker so app recognizes it as an installed copy
        File.WriteAllText(Path.Combine(isolatedDir, "unins000.exe"), "MOCK_UNINSTALLER_MARKER");

        var settingsService = new SettingsService();
        // Point settings path to isolated folder via reflection
        var field = typeof(SettingsService).GetField("_settingsFilePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(settingsService, isolatedSettingsPath);

        // 2. Setup Mock Server with Release v1.3.0
        var fakeServer = new FakeHttpServer();
        using var updateService = new UpdateService(settingsService, fakeServer)
        {
            CurrentVersionOverride = "1.2.0",
            IsInstalled = true,
            SignerOf = (_, _) => null // Unsigned dev build test
        };

        // 3. Step: Detect new release
        UpdateInfo? detectedUpdate = null;
        updateService.UpdateAvailable += (s, e) => detectedUpdate = e;

        var checkResult = await updateService.CheckForUpdatesAsync(manual: false);

        Assert.NotNull(checkResult);
        Assert.NotNull(detectedUpdate);
        Assert.Equal("1.3.0", detectedUpdate.Version);
        Assert.Equal("Kanban Cards v1.3.0", detectedUpdate.Title);
        Assert.NotEmpty(detectedUpdate.Summary);
        Assert.Contains(detectedUpdate.Summary, s => s.Contains("Fixed dragging stutter"));

        // 4. Step: Verify Desktop Notification ViewModel
        var noticeVm = new UpdateNoticeViewModel(updateService, detectedUpdate);
        Assert.Equal("A new Kanban Cards update is available.", noticeVm.Title);
        Assert.Equal("v1.3.0", noticeVm.Version);
        Assert.Equal("Update", noticeVm.PrimaryButtonText);
        Assert.True(noticeVm.CanInteract);
        Assert.False(noticeVm.IsDownloading);

        // 5. Step: Execute Update Download and verify SHA256 integrity
        var progressList = new List<int>();
        var progress = new Progress<int>(pct => progressList.Add(pct));

        bool installerLaunched = false;
        ProcessStartInfo? capturedPsi = null;
        updateService.StartProcess = psi =>
        {
            installerLaunched = true;
            capturedPsi = psi;
            return Process.GetCurrentProcess();
        };

        var downloadSuccess = await updateService.DownloadAndInstallUpdateAsync(detectedUpdate, progress);

        Assert.True(downloadSuccess);
        Assert.True(installerLaunched);
        Assert.NotNull(capturedPsi);
        Assert.EndsWith("KanbanCardsSetup.exe", capturedPsi.FileName);
        Assert.Equal("/SILENT /NORESTART", capturedPsi.Arguments);

        // 6. Verify User Settings & Data Remain Preserved
        var reloadedSettings = settingsService.LoadSettings();
        Assert.True(reloadedSettings.ShowNotesOnDesktop);
        Assert.True(reloadedSettings.AutoCheckForUpdates);

        // 7. Step: Post-Update Verification on Upgraded Version 1.3.0
        updateService.CurrentVersionOverride = "1.3.0";
        var postUpdateCheck = await updateService.CheckForUpdatesAsync(manual: false);

        Assert.Null(postUpdateCheck); // No update available
        Assert.Contains("up to date", updateService.StatusMessage ?? "");

        // Cleanup isolated temp folder
        try
        {
            if (Directory.Exists(isolatedDir))
                Directory.Delete(isolatedDir, true);
        }
        catch { }
    }
}
