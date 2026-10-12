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
                _ = LoadNotesAsync();
            }
        }
    }

    public ICommand LoadNotesCommand { get; }
    public ICommand AddNoteCommand { get; }
    public ICommand EditNoteCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public MainViewModel(INoteService noteService, IEditorService editorService)
    {
        _noteService = noteService;
        _editorService = editorService;
        
        LoadNotesCommand = new RelayCommand(async _ => await LoadNotesAsync());
        AddNoteCommand = new RelayCommand(_ => AddNote());
        EditNoteCommand = new RelayCommand(note => EditNote((Note)note!));
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);

        FilteredNotes.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsSearchEmpty));
            OnPropertyChanged(nameof(NoteCountText));
        };

        _noteService.NotesChanged += () =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _ = LoadNotesAsync());
        };

        _ = LoadNotesAsync();
    }

    public async Task LoadNotesAsync()
    {
        var allNotes = await _noteService.GetAllNotesAsync();
        _totalNotesCount = allNotes.Count;
        
        var filtered = string.IsNullOrWhiteSpace(SearchText) 
            ? allNotes 
            : allNotes.Where(n => 
                (n.Title != null && n.Title.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase)) ||
                (n.Description != null && n.Description.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase))).ToList();

        FilteredNotes.Clear();
        foreach (var note in filtered)
        {
            FilteredNotes.Add(note);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(NoteCountText));
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
