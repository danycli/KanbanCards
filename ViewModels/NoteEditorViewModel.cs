using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using KanbanCards.Models;
using KanbanCards.Services;

namespace KanbanCards.ViewModels;

public class NoteEditorViewModel : ViewModelBase, IDisposable
{
    private readonly INoteService _noteService;
    private Note _note;
    private bool _isNew;

    public Note Note => _note;
    public bool IsNew => _isNew;

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
        CloseCommand = new RelayCommand(_ => CloseEditor());
    }

    public async Task FlushAutosaveAsync()
    {
        if (_autosaveCts != null)
        {
            _autosaveCts.Cancel();
            _autosaveCts.Dispose();
            _autosaveCts = null;
        }

        if (string.IsNullOrWhiteSpace(Title))
            return;

        try
        {
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
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[AUTOSAVE EXCEPTION] Failed to flush note: {ex.Message}");
        }
    }

    private async Task AutosaveAsync()
    {
        // Ghost note prevention: newly created notes are only committed when confirmed by user
        if (_isNew) return;

        if (_autosaveCts != null)
        {
            _autosaveCts.Cancel();
            _autosaveCts.Dispose();
            _autosaveCts = null;
        }
        _autosaveCts = new System.Threading.CancellationTokenSource();
        var token = _autosaveCts.Token;

        try
        {
            await Task.Delay(500, token);
            
            if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(Title)) 
                return;

            await _noteService.UpdateNoteAsync(_note);
        }
        catch (TaskCanceledException)
        {
            // Ignored
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[AUTOSAVE EXCEPTION] Background autosave failed: {ex.Message}");
        }
    }

    private async void SaveAndClose()
    {
        await FlushAutosaveAsync();
        RequestClose?.Invoke();
    }

    private async void CloseEditor()
    {
        if (!_isNew)
        {
            await FlushAutosaveAsync();
        }
        else
        {
            if (_autosaveCts != null)
            {
                _autosaveCts.Cancel();
                _autosaveCts.Dispose();
                _autosaveCts = null;
            }
        }
        RequestClose?.Invoke();
    }

    private async void DeleteNote()
    {
        if (_autosaveCts != null)
        {
            _autosaveCts.Cancel();
            _autosaveCts.Dispose();
            _autosaveCts = null;
        }

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

    public void Dispose()
    {
        if (_autosaveCts != null)
        {
            _autosaveCts.Cancel();
            _autosaveCts.Dispose();
            _autosaveCts = null;
        }
    }
}
