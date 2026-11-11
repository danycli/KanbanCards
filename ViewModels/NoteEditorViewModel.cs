using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly ISettingsService? _settingsService;
    private string _saveError = string.Empty;

    public string SaveError { get => _saveError; private set => SetProperty(ref _saveError, value); }

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
            if (!string.IsNullOrWhiteSpace(value)) SaveError = string.Empty;
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

    public int Priority
    {
        get => _note.Priority;
        set { if (_note.Priority == value || value is < 0 or > 2) return; _note.Priority = value; OnPropertyChanged(); _ = AutosaveAsync(); }
    }

    public DateTime? DueDate
    {
        get => _note.DueAt?.ToLocalTime();
        set
        {
            _note.DueAt = value.HasValue ? DateTime.SpecifyKind(value.Value.Date.AddHours(9), DateTimeKind.Local).ToUniversalTime() : null;
            _note.ReminderSentAt = null;
            _note.ReminderSnoozedUntil = null;
            OnPropertyChanged(); OnPropertyChanged(nameof(ReminderEnabled)); _ = AutosaveAsync();
        }
    }

    public bool ReminderEnabled
    {
        get => _note.ReminderEnabled;
        set { if (_note.ReminderEnabled == value) return; _note.ReminderEnabled = value; _note.ReminderSentAt = null; _note.ReminderSnoozedUntil = null; OnPropertyChanged(); _ = AutosaveAsync(); }
    }

    public string TagsText
    {
        get
        {
            try { return string.Join(", ", (JsonSerializer.Deserialize<string[]>(_note.TagsJson) ?? Array.Empty<string>())); }
            catch { return string.Empty; }
        }
        set
        {
            var tags = (value ?? string.Empty).Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
            _note.TagsJson = JsonSerializer.Serialize(tags);
            OnPropertyChanged(); _ = AutosaveAsync();
        }
    }

    public string ChecklistText
    {
        get => string.Join(Environment.NewLine, ChecklistItems.Select(item => $"[{(item.IsDone ? "x" : " ")}] {item.Text}"));
        set
        {
            ChecklistItems.Clear();
            foreach (var line in (value ?? string.Empty).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Take(30))
            {
                var content = line.Trim();
                bool done = content.StartsWith("[x]", StringComparison.OrdinalIgnoreCase);
                if (content.StartsWith("[x]", StringComparison.OrdinalIgnoreCase) || content.StartsWith("[ ]")) content = content[3..].TrimStart();
                AddChecklistItem(content, done);
            }
            PersistChecklist();
        }
    }

    public ObservableCollection<ChecklistItemViewModel> ChecklistItems { get; } = new();
    private string _newChecklistItemText = string.Empty;
    public string NewChecklistItemText { get => _newChecklistItemText; set => SetProperty(ref _newChecklistItemText, value); }
    public ICommand AddChecklistItemCommand { get; }
    public ICommand RemoveChecklistItemCommand { get; }

    public ICommand OkCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CloseCommand { get; }

    public NoteEditorViewModel(INoteService noteService, Note note, bool isNew, ISettingsService? settingsService = null)
    {
        _noteService = noteService;
        _settingsService = settingsService;
        _note = note;
        _isNew = isNew;

        try
        {
            var rows = JsonSerializer.Deserialize<string[]>(_note.ChecklistJson) ?? Array.Empty<string>();
            foreach (var row in rows)
            {
                var content = row.Trim();
                bool done = content.StartsWith("[x]", StringComparison.OrdinalIgnoreCase);
                if (content.StartsWith("[x]", StringComparison.OrdinalIgnoreCase) || content.StartsWith("[ ]")) content = content[3..].TrimStart();
                AddChecklistItem(content, done);
            }
        }
        catch { }
        AddChecklistItemCommand = new RelayCommand(_ =>
        {
            var text = NewChecklistItemText.Trim();
            if (text.Length == 0 || ChecklistItems.Count >= 30) return;
            AddChecklistItem(text, false);
            NewChecklistItemText = string.Empty;
            PersistChecklist();
        });
        RemoveChecklistItemCommand = new RelayCommand(item =>
        {
            if (item is ChecklistItemViewModel row) { ChecklistItems.Remove(row); PersistChecklist(); }
        });

        OkCommand = new RelayCommand(_ => SaveAndClose());
        DeleteCommand = new RelayCommand(_ => DeleteNote());
        CloseCommand = new RelayCommand(_ => CloseEditor());
    }

    private void AddChecklistItem(string text, bool done)
    {
        var item = new ChecklistItemViewModel { Text = text, IsDone = done };
        item.PropertyChanged += ChecklistItemChanged;
        ChecklistItems.Add(item);
    }

    private void ChecklistItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChecklistItemViewModel.Text) or nameof(ChecklistItemViewModel.IsDone)) PersistChecklist();
    }

    private void PersistChecklist()
    {
        _note.ChecklistJson = JsonSerializer.Serialize(ChecklistItems.Select(item => $"[{(item.IsDone ? "x" : " ")}] {item.Text}"));
        OnPropertyChanged(nameof(ChecklistText));
        OnPropertyChanged(nameof(Note));
        _ = AutosaveAsync();
    }

    public double EditorFontSize => Math.Clamp(_settingsService?.LoadSettings().EditorFontSize ?? 15, 13, 22);
    public double TitleFontSize => EditorFontSize + 5;

    public async Task<bool> FlushAutosaveAsync()
    {
        if (_autosaveCts != null)
        {
            _autosaveCts.Cancel();
            _autosaveCts.Dispose();
            _autosaveCts = null;
        }

        if (string.IsNullOrWhiteSpace(Title))
        {
            if (_isNew) return true;
            SaveError = "Add a title before saving this card.";
            return false;
        }

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
            SaveError = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[AUTOSAVE EXCEPTION] Failed to flush note: {ex.Message}");
            SaveError = "Could not save this card. Please try again.";
            return false;
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
        if (!await FlushAutosaveAsync()) return;
        RequestClose?.Invoke();
    }

    private async void CloseEditor()
    {
        if (!_isNew)
        {
            if (!await FlushAutosaveAsync()) return;
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

    internal void ShowTitleRequiredError() => SaveError = "Add a title before saving this card.";

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
