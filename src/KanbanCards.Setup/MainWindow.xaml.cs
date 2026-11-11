using KanbanCards.Setup.Services;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KanbanCards.Setup;

public partial class MainWindow : Window
{
    private readonly bool _isUninstallMode;
    private readonly string _targetDir;
    private const double MaxProgressBarWidth = 380.0;

    public MainWindow(bool isUninstallMode, string targetDir)
    {
        InitializeComponent();
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.4.0";
        VersionText.Text = $"v{version}";
        _isUninstallMode = isUninstallMode;
        _targetDir = targetDir;

        if (isUninstallMode)
        {
            WindowTitleBlock.Text = "Kanban Cards Uninstaller";
            InstallConfigView.Visibility = Visibility.Collapsed;
            UninstallConfirmView.Visibility = Visibility.Visible;
        }
        else
        {
            WindowTitleBlock.Text = "Kanban Cards Setup";
            InstallPathBox.Text = targetDir;
            InstallConfigView.Visibility = Visibility.Visible;
            UninstallConfirmView.Visibility = Visibility.Collapsed;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Installation Folder",
            InitialDirectory = InstallPathBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            InstallPathBox.Text = dialog.FolderName;
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        string path = InstallPathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            path = InstallerEngine.GetDefaultInstallPath();
        }

        var options = new InstallOptions
        {
            InstallPath = path,
            StartWithWindows = StartWithWindowsCheck.IsChecked ?? true,
            CreateDesktopShortcut = DesktopShortcutCheck.IsChecked ?? true,
            CreateStartMenuShortcut = StartMenuShortcutCheck.IsChecked ?? true,
            LaunchAfterInstall = true
        };

        InstallConfigView.Visibility = Visibility.Collapsed;
        ProgressView.Visibility = Visibility.Visible;
        ProgressTitleBlock.Text = "Installing Kanban Cards...";

        var progress = new Progress<(int percent, string message)>(update =>
        {
            ProgressStatusBlock.Text = update.message;
            ProgressPercentBlock.Text = $"{update.percent}%";
            ProgressBarFill.Width = (update.percent / 100.0) * MaxProgressBarWidth;
        });

        try
        {
            await InstallerEngine.InstallAsync(options, progress);
            await Task.Delay(300); // Brief visual confirmation
            ShowFinishedInstall();
        }
        catch (Exception ex)
        {
            ProgressStatusBlock.Text = "Error: " + ex.Message;
            MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var options = new UninstallOptions
        {
            DeleteUserData = DeleteUserDataCheck.IsChecked ?? false
        };

        UninstallConfirmView.Visibility = Visibility.Collapsed;
        ProgressView.Visibility = Visibility.Visible;
        ProgressTitleBlock.Text = "Uninstalling Kanban Cards...";

        var progress = new Progress<(int percent, string message)>(update =>
        {
            ProgressStatusBlock.Text = update.message;
            ProgressPercentBlock.Text = $"{update.percent}%";
            ProgressBarFill.Width = (update.percent / 100.0) * MaxProgressBarWidth;
        });

        try
        {
            await InstallerEngine.UninstallAsync(_targetDir, options, progress);
            await Task.Delay(300);
            ShowFinishedUninstall();
        }
        catch (Exception ex)
        {
            ProgressStatusBlock.Text = "Error: " + ex.Message;
            MessageBox.Show($"Uninstallation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void ShowFinishedInstall()
    {
        ProgressView.Visibility = Visibility.Collapsed;
        FinishedView.Visibility = Visibility.Visible;

        FinishedTitleBlock.Text = "Kanban Cards is Ready!";
        FinishedMessageBlock.Text = "Kanban Cards has been successfully installed on your computer.";
        LaunchAfterFinishCheck.Visibility = Visibility.Visible;
        FinishActionButton.Content = "Launch";
    }

    private void ShowFinishedUninstall()
    {
        ProgressView.Visibility = Visibility.Collapsed;
        FinishedView.Visibility = Visibility.Visible;

        FinishedTitleBlock.Text = "Uninstallation Complete";
        FinishedMessageBlock.Text = "Kanban Cards and its shortcuts have been cleanly removed from your computer.";
        LaunchAfterFinishCheck.Visibility = Visibility.Collapsed;
        FinishActionButton.Content = "Done";
    }

    private void FinishAction_Click(object sender, RoutedEventArgs e)
    {
        if (!_isUninstallMode)
        {
            if (LaunchAfterFinishCheck.IsChecked == true)
            {
                var targetExe = Path.Combine(InstallPathBox.Text.Trim(), InstallerEngine.AppExecutableName);
                if (File.Exists(targetExe))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(targetExe) { UseShellExecute = true });
                    }
                    catch { }
                }
            }
        }
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current?.Shutdown(0);
    }
}
