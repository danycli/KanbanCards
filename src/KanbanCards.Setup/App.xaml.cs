using KanbanCards.Setup.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace KanbanCards.Setup;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        var args = e.Args.Select(a => a.Trim()).ToArray();
        bool isSilent = args.Any(a => a.Equals("/SILENT", StringComparison.OrdinalIgnoreCase) ||
                                      a.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase));
        bool noLaunch = args.Any(a => a.Equals("/NOLAUNCH", StringComparison.OrdinalIgnoreCase) ||
                                      a.Equals("/NORESTART", StringComparison.OrdinalIgnoreCase));

        string? customDir = null;
        var dirArg = args.FirstOrDefault(a => a.StartsWith("/DIR=", StringComparison.OrdinalIgnoreCase));
        if (dirArg != null)
        {
            customDir = dirArg.Substring(5).Trim('\"');
        }

        string currentExe = Environment.ProcessPath ?? "";
        string currentExeName = Path.GetFileName(currentExe).ToLowerInvariant();

        bool isUninstall = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                                         a.Equals("/UNINSTALL", StringComparison.OrdinalIgnoreCase))
                           || currentExeName.Contains("uninstall");

        bool fromTemp = args.Any(a => a.Equals("--from-temp", StringComparison.OrdinalIgnoreCase));
        string? origDir = null;
        var origArg = args.FirstOrDefault(a => a.StartsWith("--orig=", StringComparison.OrdinalIgnoreCase));
        if (origArg != null)
        {
            origDir = origArg.Substring(7).Trim('\"');
        }

        string defaultInstallDir = !string.IsNullOrEmpty(customDir) ? customDir : InstallerEngine.GetDefaultInstallPath();

        // If running as uninstaller from the actual install directory, we MUST clone ourselves
        // to %TEMP% so Windows doesn't lock the uninstall.exe file when we delete the directory.
        if (isUninstall && !fromTemp && !string.IsNullOrEmpty(currentExe))
        {
            string currentDir = Path.GetDirectoryName(currentExe) ?? "";
            if (Directory.Exists(currentDir) &&
                (string.Equals(currentDir, defaultInstallDir, StringComparison.OrdinalIgnoreCase) ||
                 File.Exists(Path.Combine(currentDir, InstallerEngine.AppExecutableName))))
            {
                var tempUninstDir = Path.Combine(Path.GetTempPath(), "KanbanCards_Uninstaller");
                Directory.CreateDirectory(tempUninstDir);
                var tempExe = Path.Combine(tempUninstDir, "uninstall.exe");

                try
                {
                    File.Copy(currentExe, tempExe, true);
                    var childArgs = $"--uninstall --from-temp --orig=\"{currentDir}\"" + (isSilent ? " /SILENT" : "");
                    Process.Start(new ProcessStartInfo(tempExe, childArgs) { UseShellExecute = true });
                    Shutdown(0);
                    return;
                }
                catch { }
            }
        }

        string targetInstallDir = origDir ?? defaultInstallDir;

        // Silent Execution Mode
        if (isSilent)
        {
            var logPath = Path.Combine(Path.GetTempPath(), "kanban_setup_debug.log");
            var exitCode = 0;
            try
            {
                File.AppendAllText(logPath, $"[SILENT START] args={string.Join(" ", args)}, isUninstall={isUninstall}, targetInstallDir={targetInstallDir}\n");
                if (isUninstall)
                {
                    await InstallerEngine.UninstallAsync(targetInstallDir, new UninstallOptions { DeleteUserData = false });
                }
                else
                {
                    var options = new InstallOptions
                    {
                        InstallPath = targetInstallDir,
                        LaunchAfterInstall = !noLaunch,
                        StartWithWindows = File.Exists(Path.Combine(targetInstallDir, InstallerEngine.AppExecutableName))
                            ? InstallerEngine.IsStartupEnabled()
                            : true,
                        CreateDesktopShortcut = !File.Exists(Path.Combine(targetInstallDir, InstallerEngine.AppExecutableName)) ||
                            File.Exists(InstallerEngine.GetDesktopShortcutPath()),
                        CreateStartMenuShortcut = !File.Exists(Path.Combine(targetInstallDir, InstallerEngine.AppExecutableName)) ||
                            File.Exists(InstallerEngine.GetStartMenuShortcutPath())
                    };
                    await InstallerEngine.InstallAsync(options);
                    File.AppendAllText(logPath, "[SILENT INSTALL FINISHED]\n");
                    if (!noLaunch)
                    {
                        var targetExe = Path.Combine(targetInstallDir, InstallerEngine.AppExecutableName);
                        if (File.Exists(targetExe))
                        {
                            Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(logPath, $"[SILENT ERROR] {ex}\n");
                exitCode = 1;
            }
            finally
            {
                Shutdown(exitCode);
            }

            return;
        }

        // Interactive GUI Mode
        var mainWindow = new MainWindow(isUninstall, targetInstallDir);
        mainWindow.Show();
    }
}
