using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;

namespace KanbanCards.Setup.Services;

public class InstallOptions
{
    public string InstallPath { get; set; } = InstallerEngine.GetDefaultInstallPath();
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool CreateStartMenuShortcut { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool LaunchAfterInstall { get; set; } = true;
}

public class UninstallOptions
{
    public bool DeleteUserData { get; set; } = false;
}

public static class InstallerEngine
{
    public const string AppName = "Kanban Cards";
    public const string AppId = "KanbanCards";
    public const string AppExecutableName = "KanbanCards.exe";
    public const string UninstallerExecutableName = "uninstall.exe";
    public const string AppVersion = "1.3.0";
    public const string Publisher = "danycli";
    public const string RepositoryUrl = "https://github.com/danycli/KanbanCards";

    public static string GetDefaultInstallPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Programs", AppId);
    }

    public static string GetUserDataPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, AppId);
    }

    public static string GetDesktopShortcutPath()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return Path.Combine(desktop, $"{AppName}.lnk");
    }

    public static string GetStartMenuShortcutPath()
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        return Path.Combine(programs, $"{AppName}.lnk");
    }

    public static void StopRunningProcesses()
    {
        try
        {
            var processes = Process.GetProcessesByName("KanbanCards");
            foreach (var p in processes)
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(1000);
                }
                catch { }
            }
        }
        catch { }
    }

    public static async Task InstallAsync(InstallOptions options, IProgress<(int percent, string message)>? progress = null)
    {
        progress?.Report((5, "Stopping active Kanban Cards instances..."));
        await Task.Run(StopRunningProcesses);

        var installDir = options.InstallPath;
        Directory.CreateDirectory(installDir);

        progress?.Report((15, "Extracting application files..."));
        await Task.Run(() => ExtractPayload(installDir, progress));

        progress?.Report((75, "Configuring uninstaller..."));
        await Task.Run(() => SetupUninstaller(installDir));

        progress?.Report((85, "Creating shortcuts and system registration..."));
        await Task.Run(() =>
        {
            var exePath = Path.Combine(installDir, AppExecutableName);

            // Desktop shortcut
            if (options.CreateDesktopShortcut)
            {
                ShortcutHelper.CreateShortcut(GetDesktopShortcutPath(), exePath);
            }
            else
            {
                ShortcutHelper.DeleteShortcut(GetDesktopShortcutPath());
            }

            // Start menu shortcut
            if (options.CreateStartMenuShortcut)
            {
                ShortcutHelper.CreateShortcut(GetStartMenuShortcutPath(), exePath);
            }
            else
            {
                ShortcutHelper.DeleteShortcut(GetStartMenuShortcutPath());
            }

            // Startup registration
            SetStartupRunKey(exePath, options.StartWithWindows);

            // Windows Add/Remove Programs registration
            RegisterInAddRemovePrograms(installDir, exePath);
        });

        progress?.Report((100, "Installation completed successfully!"));
    }

    public static async Task UninstallAsync(string installDir, UninstallOptions options, IProgress<(int percent, string message)>? progress = null)
    {
        progress?.Report((10, "Closing running instances..."));
        await Task.Run(StopRunningProcesses);

        progress?.Report((30, "Removing shortcuts..."));
        await Task.Run(() =>
        {
            ShortcutHelper.DeleteShortcut(GetDesktopShortcutPath());
            ShortcutHelper.DeleteShortcut(GetStartMenuShortcutPath());
            SetStartupRunKey(null, false);
            UnregisterFromAddRemovePrograms();
        });

        progress?.Report((60, "Removing application files..."));
        await Task.Run(() =>
        {
            if (Directory.Exists(installDir))
            {
                try
                {
                    // Delete files except current running uninstaller if still running
                    var dirInfo = new DirectoryInfo(installDir);
                    foreach (var file in dirInfo.GetFiles())
                    {
                        try { file.Delete(); } catch { }
                    }
                    foreach (var sub in dirInfo.GetDirectories())
                    {
                        try { sub.Delete(true); } catch { }
                    }
                    try { Directory.Delete(installDir, false); } catch { }
                }
                catch { }
            }
        });

        if (options.DeleteUserData)
        {
            progress?.Report((80, "Removing user settings and database..."));
            await Task.Run(() =>
            {
                var userData = GetUserDataPath();
                if (Directory.Exists(userData))
                {
                    try { Directory.Delete(userData, true); } catch { }
                }
            });
        }

        progress?.Report((100, "Uninstallation completed."));
    }

    private static void ExtractPayload(string targetDir, IProgress<(int percent, string message)>? progress)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = System.Linq.Enumerable.FirstOrDefault(assembly.GetManifestResourceNames(), n => n.EndsWith("app.zip", StringComparison.OrdinalIgnoreCase))
                           ?? "KanbanCards.Setup.app.zip";
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream != null)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            int total = archive.Entries.Count;
            int count = 0;

            foreach (var entry in archive.Entries)
            {
                count++;
                if (string.IsNullOrEmpty(entry.Name)) // Directory entry
                {
                    var dirPath = Path.Combine(targetDir, entry.FullName);
                    Directory.CreateDirectory(dirPath);
                    continue;
                }

                var fullPath = Path.Combine(targetDir, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                entry.ExtractToFile(fullPath, overwrite: true);

                if (total > 0 && count % 5 == 0)
                {
                    int pct = 15 + (count * 55) / total;
                    progress?.Report((pct, $"Extracting: {entry.Name}"));
                }
            }
        }
        else
        {
            // Fallback for development if app.zip is not embedded: copy from published output
            var fallbackSources = new[]
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\bin\Release\net8.0-windows\win-x64\publish")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\bin\Debug\net8.0-windows"))
            };

            foreach (var src in fallbackSources)
            {
                if (Directory.Exists(src) && File.Exists(Path.Combine(src, AppExecutableName)))
                {
                    CopyDirectory(src, targetDir);
                    return;
                }
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
        foreach (var sub in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(destinationDir, Path.GetFileName(sub));
            CopyDirectory(sub, dest);
        }
    }

    private static void SetupUninstaller(string installDir)
    {
        try
        {
            var currentExe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
            {
                var targetUninstaller = Path.Combine(installDir, UninstallerExecutableName);
                if (!string.Equals(currentExe, targetUninstaller, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(currentExe, targetUninstaller, true);
                }
            }
        }
        catch { }
    }

    private static void SetStartupRunKey(string? targetExePath, bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key != null)
            {
                if (enable && !string.IsNullOrEmpty(targetExePath))
                {
                    key.SetValue(AppId, $"\"{targetExePath}\"");
                }
                else
                {
                    key.DeleteValue(AppId, false);
                }
            }
        }
        catch { }
    }

    private static void RegisterInAddRemovePrograms(string installDir, string exePath)
    {
        try
        {
            var uninstallerPath = Path.Combine(installDir, UninstallerExecutableName);
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppId}");
            if (key != null)
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", AppVersion);
                key.SetValue("Publisher", Publisher);
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", $"{exePath},0");
                key.SetValue("UninstallString", $"\"{uninstallerPath}\" --uninstall");
                key.SetValue("QuietUninstallString", $"\"{uninstallerPath}\" --uninstall /SILENT");
                key.SetValue("URLInfoAbout", RepositoryUrl);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    private static void UnregisterFromAddRemovePrograms()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppId}", false);
        }
        catch { }
    }
}
