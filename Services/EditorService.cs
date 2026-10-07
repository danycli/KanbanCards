using KanbanCards.Models;
using KanbanCards.ViewModels;
using KanbanCards.Views;
using Microsoft.Extensions.DependencyInjection;
using System;

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
        var viewModel = new NoteEditorViewModel(noteService, note, isNew);
        var window = new NoteEditorWindow(viewModel);
        window.Show();
    }
}
