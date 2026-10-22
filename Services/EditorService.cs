using System;
using System.Windows;
using KanbanCards.Models;
using KanbanCards.ViewModels;
using KanbanCards.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanCards.Services;

public interface IEditorService
{
    void OpenEditor(Note note, bool isNew);
}

public class EditorService : IEditorService
{
    private readonly IServiceProvider _serviceProvider;

    public EditorService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void OpenEditor(Note note, bool isNew)
    {
        var noteService = _serviceProvider.GetRequiredService<INoteService>();
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var viewModel = new NoteEditorViewModel(noteService, note, isNew);
        var window = new NoteEditorWindow(viewModel);

        var settings = settingsService.LoadSettings();
        var workArea = SystemParameters.WorkArea;

        // Position adjacent to the edge stack without obstructing it
        if (settings.StackPosition == "Left")
        {
            window.Left = workArea.Left + 330;
        }
        else
        {
            window.Left = Math.Max(workArea.Left, workArea.Left + workArea.Width - 320 - window.Width - 20);
        }

        // Center vertically on screen work area
        window.Top = Math.Max(workArea.Top, workArea.Top + (workArea.Height - window.Height) / 2.0);

        window.Show();
        window.Activate();
    }
}
