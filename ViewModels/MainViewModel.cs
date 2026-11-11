using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using KanbanCards.Models;
using KanbanCards.Services;

namespace KanbanCards.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly IEditorService _editorService;
    private readonly NoteDataTransferService _transferService;
    private readonly ISettingsService? _settingsService;
    private string _searchText = string.Empty;
    private int _totalNotesCount = 0;
    private bool _isArchiveView;
    private bool _isDataMenuOpen;
    private int _priorityFilter = -1;
    private int _dueFilter;
    private System.Threading.CancellationTokenSource? _searchCts;
    private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);

    public ObservableCollection<Note> FilteredNotes { get; } = new();
    public ObservableCollection<Note> InboxNotes { get; } = new();
    public ObservableCollection<Note> NextNotes { get; } = new();
    public ObservableCollection<Note> DoingNotes { get; } = new();
    public ObservableCollection<Note> DoneNotes { get; } = new();
    public NoteStatus[] StatusOptions { get; } = Enum.GetValues<NoteStatus>();
    public bool CanReorder => string.IsNullOrWhiteSpace(SearchText) && PriorityFilter < 0 && DueFilter == 0 && !IsArchiveView;
    public bool IsArchiveView { get => _isArchiveView; set { if (SetProperty(ref _isArchiveView, value)) { OnPropertyChanged(nameof(ArchiveButtonText)); OnPropertyChanged(nameof(CanReorder)); OnPropertyChanged(nameof(ReorderHint)); OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptyMessage)); _ = LoadNotesAsync(); } } }
    public int PriorityFilter { get => _priorityFilter; set { if (SetProperty(ref _priorityFilter, value)) { OnPropertyChanged(nameof(CanReorder)); OnPropertyChanged(nameof(ReorderHint)); OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptyMessage)); _ = LoadNotesAsync(); } } }
    public int DueFilter { get => _dueFilter; set { if (SetProperty(ref _dueFilter, value)) { OnPropertyChanged(nameof(CanReorder)); OnPropertyChanged(nameof(ReorderHint)); OnPropertyChanged(nameof(EmptyTitle)); OnPropertyChanged(nameof(EmptyMessage)); _ = LoadNotesAsync(); } } }
    public string ArchiveButtonText => IsArchiveView ? "Board" : "Archive";
    public bool IsDataMenuOpen { get => _isDataMenuOpen; set => SetProperty(ref _isDataMenuOpen, value); }
    public bool CompactBoard => _settingsService?.LoadSettings().CompactBoard ?? false;
    public Thickness CardContentMargin => CompactBoard ? new Thickness(8, 8, 8, 7) : new Thickness(12, 12, 12, 10);
    public string ReorderHint => CanReorder
        ? "Drag to rearrange · Ctrl+Up/Down moves the selected card · Click to edit"
        : !string.IsNullOrWhiteSpace(SearchText)
            ? "Clear search to rearrange · Click to edit"
            : "Clear filters and return to the board to rearrange · Click to edit";

    public string InboxCount => InboxNotes.Count.ToString();
    public string NextCount => NextNotes.Count.ToString();
    public string DoingCount => DoingNotes.Count.ToString();
    public string DoneCount => DoneNotes.Count.ToString();

    public bool IsEmpty => FilteredNotes.Count == 0 && string.IsNullOrWhiteSpace(SearchText);
    public bool IsSearchEmpty => FilteredNotes.Count == 0 && !string.IsNullOrWhiteSpace(SearchText);
    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);
    public string EmptyTitle => HasSearchText ? "No cards match" : IsArchiveView ? "Archive is empty" : PriorityFilter >= 0 || DueFilter > 0 ? "No cards in this view" : "No cards yet";
    public string EmptyMessage => HasSearchText ? "Try another keyword or clear your search" : IsArchiveView ? "Archived cards will be kept here until you restore them" : PriorityFilter >= 0 || DueFilter > 0 ? "Adjust a filter to see more cards" : "Click the + button to add your first card";

    public string NoteCountText
    {
        get
        {
            if (_totalNotesCount == 0) return "0 notes";
            if (!string.IsNullOrWhiteSpace(SearchText) || PriorityFilter >= 0 || DueFilter > 0)
                return $"{FilteredNotes.Count} of {_totalNotesCount}";
            return _totalNotesCount == 1 ? "1 note" : $"{_totalNotesCount} notes";
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                OnPropertyChanged(nameof(HasSearchText));
                OnPropertyChanged(nameof(EmptyTitle));
                OnPropertyChanged(nameof(EmptyMessage));
                OnPropertyChanged(nameof(CanReorder));
                OnPropertyChanged(nameof(ReorderHint));
                _searchCts?.Cancel();
                _searchCts?.Dispose();
                _searchCts = new System.Threading.CancellationTokenSource();
                var token = _searchCts.Token;

                Task.Delay(180, token).ContinueWith(t =>
                {
                    if (!t.IsCanceled)
                    {
                        if (System.Windows.Application.Current != null)
                        {
                            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () => await LoadNotesAsync());
                        }
                        else
                        {
                            _ = LoadNotesAsync();
                        }
                    }
                }, TaskScheduler.Default);
            }
        }
    }

    public ICommand LoadNotesCommand { get; }
    public ICommand AddNoteCommand { get; }
    public ICommand EditNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand TogglePinCommand { get; }
    public ICommand ToggleArchiveViewCommand { get; }
    public ICommand ToggleArchiveCommand { get; }
    public ICommand CreateTemplateCommand { get; }
    public ICommand ToggleDataMenuCommand { get; }

    public MainViewModel(INoteService noteService, IEditorService editorService, ISettingsService? settingsService = null)
    {
        _noteService = noteService;
        _editorService = editorService;
        _transferService = new NoteDataTransferService(noteService);
        _settingsService = settingsService;
        if (_settingsService != null) _settingsService.SettingsChanged += () =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                OnPropertyChanged(nameof(CardContentMargin));
                OnPropertyChanged(nameof(CompactBoard));
            });
        };
        
        LoadNotesCommand = new RelayCommand(async _ => await LoadNotesAsync());
        AddNoteCommand = new RelayCommand(_ => AddNote());
        EditNoteCommand = new RelayCommand(note => EditNote((Note)note!));
        DeleteNoteCommand = new RelayCommand(async note =>
        {
            if (note is Note n)
            {
                await _noteService.DeleteNoteAsync(n.Id);
            }
        });
        ClearSearchCommand = new RelayCommand(_ =>
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;
            SearchText = string.Empty;
            _ = LoadNotesAsync();
        });
        TogglePinCommand = new RelayCommand(async note =>
        {
            if (note is Note n)
                await TogglePinAsync(n);
        });
        ToggleArchiveViewCommand = new RelayCommand(_ => IsArchiveView = !IsArchiveView);
        ToggleArchiveCommand = new RelayCommand(async value =>
        {
            if (value is Note note)
                await _noteService.UpdateNoteArchiveAsync(note.Id, !note.IsArchived);
            await LoadNotesAsync();
        });
        CreateTemplateCommand = new RelayCommand(template => CreateFromTemplate(template?.ToString() ?? "idea"));
        ToggleDataMenuCommand = new RelayCommand(_ => IsDataMenuOpen = !IsDataMenuOpen);

        FilteredNotes.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(NoteCountText));
            OnPropertyChanged(nameof(EmptyTitle));
            OnPropertyChanged(nameof(EmptyMessage));
        };

        InboxNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(InboxCount));
        NextNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(NextCount));
        DoingNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(DoingCount));
        DoneNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(DoneCount));

        _noteService.NotesChanged += () =>
        {
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => _ = LoadNotesAsync());
        };

        _ = LoadNotesAsync();
    }

    public async Task LoadNotesAsync()
    {
        await _loadSemaphore.WaitAsync();
        try
        {
            var allNotes = await _noteService.GetAllNotesAsync();
            _totalNotesCount = allNotes.Count(note => note.IsArchived == IsArchiveView);
            
            var filtered = allNotes
                .Where(n => n.IsArchived == IsArchiveView)
                .Where(n => PriorityFilter < 0 || n.Priority == PriorityFilter)
                .Where(n => DueFilter switch
                {
                    1 => n.DueAt?.ToLocalTime().Date == DateTime.Today,
                    2 => n.DueAt?.ToLocalTime().Date > DateTime.Today && n.DueAt?.ToLocalTime().Date <= DateTime.Today.AddDays(7),
                    3 => n.DueAt?.ToLocalTime().Date < DateTime.Today,
                    _ => true
                })
                .Where(n => string.IsNullOrWhiteSpace(SearchText) ||
                    (n.Title != null && n.Title.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)) ||
                    (n.Description != null && n.Description.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)) ||
                    n.TagsJson.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)).ToList();

            // Keep the global search result and each lane reconciled in place so
            // filtering or moving a card doesn't reset every lane's visual tree.
            Reconcile(FilteredNotes, filtered);
            Reconcile(InboxNotes, filtered.Where(n => n.Status == NoteStatus.Inbox).ToList());
            Reconcile(NextNotes, filtered.Where(n => n.Status == NoteStatus.Next).ToList());
            Reconcile(DoingNotes, filtered.Where(n => n.Status == NoteStatus.Doing).ToList());
            Reconcile(DoneNotes, filtered.Where(n => n.Status == NoteStatus.Done).ToList());

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(NoteCountText));
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    public async Task MoveNoteToStatusAsync(Note note, NoteStatus status)
    {
        if (note == null || !Enum.IsDefined(status) || note.Status == status)
            return;

        await _noteService.UpdateNoteStatusAsync(note.Id, status);
        note.Status = status;
        await LoadNotesAsync();
    }

    public async Task TogglePinAsync(Note note)
    {
        if (note == null)
            return;

        var nextPinnedState = !note.IsPinned;
        await _noteService.UpdateNotePinAsync(note.Id, nextPinnedState);
        note.IsPinned = nextPinnedState;
        await LoadNotesAsync();
    }

    public async Task ReorderLaneAsync(NoteStatus status, System.Collections.Generic.IReadOnlyList<int> orderedNoteIds)
    {
        if (!CanReorder || !Enum.IsDefined(status))
            return;

        await _noteService.ReorderNotesAsync(status, orderedNoteIds);
        await LoadNotesAsync();
    }

    public async Task MoveNoteToLaneAsync(Note note, NoteStatus status, int? beforeNoteId = null)
    {
        if (note == null || !Enum.IsDefined(status) || !CanReorder)
            return;

        if (note.Status != status)
            await MoveNoteToStatusAsync(note, status);

        var laneNotes = GetLaneNotes(status);
        laneNotes.RemoveAll(item => item.Id == note.Id);
        var insertIndex = beforeNoteId.HasValue
            ? laneNotes.FindIndex(item => item.Id == beforeNoteId.Value)
            : laneNotes.Count;
        if (insertIndex < 0)
            insertIndex = laneNotes.Count;
        laneNotes.Insert(insertIndex, note);

        await ReorderLaneAsync(status, laneNotes.Select(item => item.Id).ToList());
    }

    private System.Collections.Generic.List<Note> GetLaneNotes(NoteStatus status) => status switch
    {
        NoteStatus.Inbox => InboxNotes.ToList(),
        NoteStatus.Next => NextNotes.ToList(),
        NoteStatus.Doing => DoingNotes.ToList(),
        NoteStatus.Done => DoneNotes.ToList(),
        _ => new System.Collections.Generic.List<Note>()
    };

    public async Task MoveNoteInLaneAsync(Note note, int offset)
    {
        if (note == null || offset == 0 || !CanReorder)
            return;

        var laneNotes = note.Status switch
        {
            NoteStatus.Inbox => InboxNotes.ToList(),
            NoteStatus.Next => NextNotes.ToList(),
            NoteStatus.Doing => DoingNotes.ToList(),
            NoteStatus.Done => DoneNotes.ToList(),
            _ => new System.Collections.Generic.List<Note>()
        };
        var index = laneNotes.FindIndex(item => item.Id == note.Id);
        var targetIndex = index + Math.Sign(offset);
        if (index < 0 || targetIndex < 0 || targetIndex >= laneNotes.Count)
            return;

        (laneNotes[index], laneNotes[targetIndex]) = (laneNotes[targetIndex], laneNotes[index]);
        await ReorderLaneAsync(note.Status, laneNotes.Select(item => item.Id).ToList());
    }

    private static void Reconcile(ObservableCollection<Note> current, System.Collections.Generic.IReadOnlyList<Note> target)
    {
        for (int i = current.Count - 1; i >= 0; i--)
        {
            if (!target.Any(n => n.Id == current[i].Id))
                current.RemoveAt(i);
        }

        for (int i = 0; i < target.Count; i++)
        {
            var targetNote = target[i];
            if (i >= current.Count)
            {
                current.Add(targetNote);
            }
            else if (current[i].Id != targetNote.Id)
            {
                int existingIndex = -1;
                for (int j = i + 1; j < current.Count; j++)
                {
                    if (current[j].Id == targetNote.Id)
                    {
                        existingIndex = j;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    current.Move(existingIndex, i);
                    if (!SameNoteData(current[i], targetNote))
                        current[i] = targetNote;
                }
                else
                {
                    current.Insert(i, targetNote);
                }
            }
            else
            {
                if (!SameNoteData(current[i], targetNote))
                    current[i] = targetNote;
            }
        }
    }

    private static bool SameNoteData(Note left, Note right) =>
        left.Id == right.Id && left.Title == right.Title && left.Description == right.Description &&
        left.Color == right.Color && left.Status == right.Status && left.SortOrder == right.SortOrder &&
        left.IsPinned == right.IsPinned && left.ArchivedAt == right.ArchivedAt && left.Priority == right.Priority &&
        left.DueAt == right.DueAt && left.ReminderEnabled == right.ReminderEnabled &&
        left.ReminderSentAt == right.ReminderSentAt && left.ReminderSnoozedUntil == right.ReminderSnoozedUntil &&
        left.TagsJson == right.TagsJson && left.ChecklistJson == right.ChecklistJson &&
        left.CreatedAt == right.CreatedAt && left.UpdatedAt == right.UpdatedAt;

    private void AddNote()
    {
        _editorService.OpenEditor(new Note(), true);
    }

    public Task ExportJsonAsync(string path) => _transferService.ExportJsonAsync(path);
    public Task ExportMarkdownAsync(string path) => _transferService.ExportMarkdownAsync(path);
    public Task<NoteImportPreview> PreviewImportAsync(string path) => _transferService.PreviewImportAsync(path);
    public async Task<int> ImportJsonAsync(string path)
    {
        var count = await _transferService.ImportJsonAsync(path);
        await LoadNotesAsync();
        return count;
    }

    private void CreateFromTemplate(string template)
    {
        _editorService.OpenEditor(NoteTemplateFactory.Create(template), true);
    }

    private void EditNote(Note note)
    {
        if (note != null)
        {
            _editorService.OpenEditor(note, false);
        }
    }
}
