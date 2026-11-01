using System;
using System.IO;
using System.Threading.Tasks;
using KanbanCards.Setup.Services;
using Xunit;

namespace KanbanCards.Tests;

public class SetupAndInstallerTests
{
    [Fact]
    public void InstallerEngine_DefaultPaths_AreValidAndFormatted()
    {
        var defaultPath = InstallerEngine.GetDefaultInstallPath();
        Assert.NotNull(defaultPath);
        Assert.Contains("KanbanCards", defaultPath);
        Assert.Contains("Programs", defaultPath);

        var userDataPath = InstallerEngine.GetUserDataPath();
        Assert.NotNull(userDataPath);
        Assert.Contains("KanbanCards", userDataPath);

        var desktopShortcut = InstallerEngine.GetDesktopShortcutPath();
        Assert.EndsWith(".lnk", desktopShortcut);

        var startMenuShortcut = InstallerEngine.GetStartMenuShortcutPath();
        Assert.EndsWith(".lnk", startMenuShortcut);
    }

    [Fact]
    public void ShortcutHelper_CreateAndDeleteShortcut_WorksCleanly()
    {
        var tempShortcut = Path.Combine(Path.GetTempPath(), $"kanban_test_shortcut_{Guid.NewGuid()}.lnk");
        try
        {
            var targetExe = Environment.ProcessPath ?? "notepad.exe";
            ShortcutHelper.CreateShortcut(tempShortcut, targetExe, "Test Shortcut");

            Assert.True(File.Exists(tempShortcut));

            ShortcutHelper.DeleteShortcut(tempShortcut);
            Assert.False(File.Exists(tempShortcut));
        }
        finally
        {
            if (File.Exists(tempShortcut)) File.Delete(tempShortcut);
        }
    }

    [Fact]
    public void InstallAndUninstallOptions_Defaults_AreSafe()
    {
        var installOpts = new InstallOptions();
        Assert.True(installOpts.CreateDesktopShortcut);
        Assert.True(installOpts.CreateStartMenuShortcut);
        Assert.True(installOpts.StartWithWindows);
        Assert.True(installOpts.LaunchAfterInstall);

        var uninstallOpts = new UninstallOptions();
        // User notes and personal settings must NOT be deleted by default
        Assert.False(uninstallOpts.DeleteUserData);
    }
}
