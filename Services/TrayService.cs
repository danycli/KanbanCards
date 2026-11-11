using H.NotifyIcon;
using KanbanCards.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KanbanCards.Services;

public interface ITrayService
{
    void Initialize();
    void Shutdown();
    void OpenMainWindow();
    void OpenSettingsWindow();
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

        var captureClipboardItem = CreateMenuItem("Capture Clipboard", "\uE8C8", "#8B5CF6", (s, e) => CaptureClipboard());
        var templatesItem = CreateMenuItem("New from Template", "\uE8A5", "#0EA5E9", (s, e) => OpenTemplate("idea"));

        // View Notes
        var viewNotesItem = CreateMenuItem("View Notes", "\uE8F1", "#3B82F6", (s, e) =>
        {
            OpenMainWindow();
        });

        // Settings
        var settingsItem = CreateMenuItem("Settings", "\uE713", "#64748B", (s, e) =>
        {
            OpenSettingsWindow();
        });

        // Check for Updates
        var updateItem = CreateMenuItem("Check for Updates", "\uE895", "#3B82F6", async (s, e) =>
        {
            var updateService = _serviceProvider.GetRequiredService<IUpdateService>();
            var result = await updateService.CheckForUpdatesAsync(manual: true);
            if (result != null)
            {
                App.ShowUpdateNotice(updateService, result);
            }
            else
            {
                App.ShowUpToDateNotice(updateService.CurrentVersion, updateService.StatusMessage);
            }
        });

        // Separator
        var sep = new Separator { Style = (Style)Application.Current.FindResource("ModernMenuSeparatorStyle") };

        // Exit
        var exitItem = CreateMenuItem("Exit", "\uE7E8", "#EF4444", (s, e) =>
        {
            Application.Current.Shutdown();
        });

        menu.Items.Add(addNoteItem);
        menu.Items.Add(captureClipboardItem);
        menu.Items.Add(templatesItem);
        menu.Items.Add(viewNotesItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(updateItem);
        menu.Items.Add(sep);
        menu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = menu;
        _taskbarIcon.TrayMouseDoubleClick += (s, e) => OpenMainWindow();
        _taskbarIcon.Visibility = Visibility.Visible;
        try
        {
            _taskbarIcon.ForceCreate(false);
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[TRAY WARNING] ForceCreate non-fatal error: {ex.Message}");
        }
    }

    private void OpenTemplate(string templateId) =>
        _serviceProvider.GetRequiredService<IEditorService>().OpenEditor(NoteTemplateFactory.Create(templateId), true);

    private void CaptureClipboard()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
            {
                System.Windows.MessageBox.Show("There is no text in the clipboard yet.", "Kanban Cards", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }
            var text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text.Length > 20000)
            {
                var answer = System.Windows.MessageBox.Show("This text is long. Capture the first 20,000 characters?", "Kanban Cards", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
                if (answer != System.Windows.MessageBoxResult.Yes) return;
                text = text[..20000];
            }
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var first = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
            if (first < 0) return;
            var title = lines[first].Trim();
            var description = string.Join(Environment.NewLine, lines.Skip(first + 1)).Trim();
            if (title.Length > 100) { description = title[100..] + Environment.NewLine + description; title = title[..100]; }
            _serviceProvider.GetRequiredService<IEditorService>().OpenEditor(new Models.Note { Title = title, Description = description }, true);
        }
        catch (Exception ex) { FullScreenMonitorService.LogAudit($"[CLIPBOARD CAPTURE] {ex.Message}"); }
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

    public void OpenMainWindow()
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

    public void OpenSettingsWindow()
    {
        var existing = Application.Current?.Windows.OfType<Views.SettingsWindow>().FirstOrDefault();
        if (existing != null)
        {
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }
            existing.Activate();
            return;
        }

        var settingsWindow = _serviceProvider.GetRequiredService<Views.SettingsWindow>();
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    public void Shutdown()
    {
        _taskbarIcon?.Dispose();
    }
}
