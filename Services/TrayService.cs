using H.NotifyIcon;
using KanbanCards.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KanbanCards.Services;

public interface ITrayService
{
    void Initialize();
    void Shutdown();
}

public class TrayService : ITrayService
{
    private TaskbarIcon? _taskbarIcon;
    private readonly IServiceProvider _serviceProvider;

    public TrayService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Initialize()
    {
        _taskbarIcon = new TaskbarIcon
        {
            ToolTipText = "Kanban Cards"
        };

        // 1. Try loading icon from pack resource
        try
        {
            var iconUri = new Uri("pack://application:,,,/AppIcon.ico", UriKind.RelativeOrAbsolute);
            _taskbarIcon.IconSource = new BitmapImage(iconUri);
        }
        catch { }

        // 2. If IconSource is null or fails, try loading directly from file in BaseDirectory
        if (_taskbarIcon.IconSource == null)
        {
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    _taskbarIcon.Icon = new System.Drawing.Icon(iconPath);
                }
            }
            catch { }
        }

        // 3. Fallback to process executable icon
        if (_taskbarIcon.IconSource == null && _taskbarIcon.Icon == null)
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    _taskbarIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }
            }
            catch { }
        }

        var menu = new ContextMenu
        {
            Style = (Style)Application.Current.FindResource("ModernContextMenuStyle")
        };

        // Add Note
        var addNoteItem = CreateMenuItem("Add Note", "\uE710", "#6366F1", (s, e) =>
        {
            _serviceProvider.GetRequiredService<IEditorService>().OpenEditor(new Models.Note(), true);
        });

        // View Notes
        var viewNotesItem = CreateMenuItem("View Notes", "\uE8F1", "#3B82F6", (s, e) =>
        {
            OpenMainWindow();
        });

        // Settings
        var settingsItem = CreateMenuItem("Settings", "\uE713", "#64748B", (s, e) =>
        {
            var settingsWindow = _serviceProvider.GetRequiredService<Views.SettingsWindow>();
            settingsWindow.Show();
        });

        // Separator
        var sep = new Separator { Style = (Style)Application.Current.FindResource("ModernMenuSeparatorStyle") };

        // Exit
        var exitItem = CreateMenuItem("Exit", "\uE7E8", "#EF4444", (s, e) =>
        {
            Application.Current.Shutdown();
        });

        menu.Items.Add(addNoteItem);
        menu.Items.Add(viewNotesItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(sep);
        menu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = menu;
        _taskbarIcon.TrayMouseDoubleClick += (s, e) => OpenMainWindow();
        _taskbarIcon.Visibility = Visibility.Visible;
        _taskbarIcon.ForceCreate(false);
    }

    private MenuItem CreateMenuItem(string title, string glyph, string iconColorHex, RoutedEventHandler onClick)
    {
        var converter = new BrushConverter();
        var fg = (Brush)converter.ConvertFromString(iconColorHex)!;

        var iconBlock = new TextBlock
        {
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            Text = glyph,
            FontSize = 12,
            Foreground = fg,
            Width = 16,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var item = new MenuItem
        {
            Header = title,
            Style = (Style)Application.Current.FindResource("ModernMenuItemStyle"),
            Icon = iconBlock
        };
        item.Click += onClick;
        return item;
    }

    private void OpenMainWindow()
    {
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
        mainWindow.Activate();
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }
        var vm = mainWindow.DataContext as MainViewModel;
        if (vm != null) _ = vm.LoadNotesAsync();
    }

    public void Shutdown()
    {
        _taskbarIcon?.Dispose();
    }
}
