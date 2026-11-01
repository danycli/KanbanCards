using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using KanbanCards.Models;
using KanbanCards.Services;

namespace KanbanCards.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly IEditorService _editorService;
    private string _searchText = string.Empty;
    private int _totalNotesCount = 0;
    private System.Threading.CancellationTokenSource? _searchCts;
    private readonly System.Threading.SemaphoreSlim _loadSemaphore = new(1, 1);

    public ObservableCollection<Note> FilteredNotes { get; } = new();

    public bool IsEmpty => FilteredNotes.Count == 0 && string.IsNullOrWhiteSpace(SearchText);
    public bool IsSearchEmpty => FilteredNotes.Count == 0 && !string.IsNullOrWhiteSpace(SearchText);
    public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

    public string NoteCountText
    {
        get
        {
            if (_totalNotesCount == 0) return "0 notes";
            if (!string.IsNullOrWhiteSpace(SearchText))
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

    public MainViewModel(INoteService noteService, IEditorService editorService)
    {
        _noteService = noteService;
        _editorService = editorService;
        
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

        FilteredNotes.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(NoteCountText));
        };

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
            _totalNotesCount = allNotes.Count;
            
            var filtered = string.IsNullOrWhiteSpace(SearchText) 
                ? allNotes 
                : allNotes.Where(n => 
                    (n.Title != null && n.Title.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)) ||
                    (n.Description != null && n.Description.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase))).ToList();

            // In-place collection reconciliation to prevent visual tree recreation and UI flickering
            for (int i = FilteredNotes.Count - 1; i >= 0; i--)
            {
                if (!filtered.Any(n => n.Id == FilteredNotes[i].Id))
                {
                    FilteredNotes.RemoveAt(i);
                }
            }

            for (int i = 0; i < filtered.Count; i++)
            {
                var targetNote = filtered[i];
                if (i >= FilteredNotes.Count)
                {
                    FilteredNotes.Add(targetNote);
                }
                else if (FilteredNotes[i].Id != targetNote.Id)
                {
                    int existingIndex = -1;
                    for (int j = i + 1; j < FilteredNotes.Count; j++)
                    {
                        if (FilteredNotes[j].Id == targetNote.Id)
                        {
                            existingIndex = j;
                            break;
                        }
                    }

                    if (existingIndex != -1)
                    {
                        FilteredNotes.Move(existingIndex, i);
                        FilteredNotes[i] = targetNote;
                    }
                    else
                    {
                        FilteredNotes.Insert(i, targetNote);
                    }
                }
                else
                {
                    FilteredNotes[i] = targetNote;
                }
            }

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(NoteCountText));
        }
        finally
        {
            _loadSemaphore.Release();
        }
    }

    private void AddNote()
    {
        _editorService.OpenEditor(new Note(), true);
    }

    private void EditNote(Note note)
    {
        if (note != null)
        {
            _editorService.OpenEditor(note, false);
        }
    }
}
