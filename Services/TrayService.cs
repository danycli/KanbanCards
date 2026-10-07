using H.NotifyIcon;
using KanbanCards.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows.Controls;

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
            ToolTipText = "Kanban Cards",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "")
        };

        var menu = new ContextMenu();
        var addNoteItem = new MenuItem { Header = "Add Note" };
        addNoteItem.Click += (s, e) => _serviceProvider.GetRequiredService<IEditorService>().OpenEditor(new Models.Note(), true);
        
        var viewNotesItem = new MenuItem { Header = "View Notes" };
        viewNotesItem.Click += (s, e) => 
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
            mainWindow.Activate();
            var vm = mainWindow.DataContext as MainViewModel;
            if (vm != null) _ = vm.LoadNotesAsync();
        };

        var settingsItem = new MenuItem { Header = "Settings" };
        settingsItem.Click += (s, e) => 
        {
            var settingsWindow = _serviceProvider.GetRequiredService<Views.SettingsWindow>();
            settingsWindow.Show();
        };

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) => System.Windows.Application.Current.Shutdown();

        menu.Items.Add(addNoteItem);
        menu.Items.Add(viewNotesItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = menu;
    }

    public void Shutdown()
    {
        _taskbarIcon?.Dispose();
    }
}
