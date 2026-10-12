using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KanbanCards.Models;
using KanbanCards.Services;

namespace KanbanCards.ViewModels;

public class NoteEditorViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private Note _note;
    private bool _isNew;

    private System.Threading.CancellationTokenSource? _autosaveCts;
    
    public Action? RequestClose { get; set; }

    public string Title
    {
        get => _note.Title;
        set
        {
            _note.Title = value;
            OnPropertyChanged();
            _ = AutosaveAsync();
        }
    }

    public string Description
    {
        get => _note.Description;
        set
        {
            _note.Description = value;
            OnPropertyChanged();
            _ = AutosaveAsync();
        }
    }

    public string Color
    {
        get => _note.Color;
        set
        {
            _note.Color = value;
            OnPropertyChanged();
            _ = AutosaveAsync();
        }
    }

    public ICommand OkCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CloseCommand { get; }

    public NoteEditorViewModel(INoteService noteService, Note note, bool isNew)
    {
        _noteService = noteService;
        _note = note;
        _isNew = isNew;

        OkCommand = new RelayCommand(_ => SaveAndClose());
        DeleteCommand = new RelayCommand(_ => DeleteNote());
        CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
    }

    private async Task AutosaveAsync()
    {
        _autosaveCts?.Cancel();
        _autosaveCts = new System.Threading.CancellationTokenSource();
        var token = _autosaveCts.Token;

        try
        {
            await Task.Delay(500, token);
            
            if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(Title)) 
                return;

            if (_isNew)
            {
                await _noteService.CreateNoteAsync(_note);
                _isNew = false;
            }
            else
            {
                await _noteService.UpdateNoteAsync(_note);
            }
        }
        catch (TaskCanceledException)
        {
            // Ignored
        }
    }

    private async void SaveAndClose()
    {
        _autosaveCts?.Cancel();
        if (string.IsNullOrWhiteSpace(Title))
            return; 

        if (_isNew)
        {
            await _noteService.CreateNoteAsync(_note);
            _isNew = false;
        }
        else
        {
            await _noteService.UpdateNoteAsync(_note);
        }

        RequestClose?.Invoke();
    }

    private async void DeleteNote()
    {
        if (!_isNew)
        {
            Window? activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                                   ?? Application.Current.MainWindow;

            bool confirmed = Views.ConfirmDeleteDialog.ShowConfirmation(activeWindow);
            if (!confirmed)
            {
                return;
            }

            await _noteService.DeleteNoteAsync(_note.Id);
        }
        RequestClose?.Invoke();
    }
}
