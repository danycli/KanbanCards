using KanbanCards.Models;
using KanbanCards.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace KanbanCards.ViewModels;

public class NoteItem : ViewModelBase
{
    private Note _note = null!;
    public Note Note
    {
        get => _note;
        set { _note = value; OnPropertyChanged(); }
    }

    private int _zIndex;
    public int ZIndex
    {
        get => _zIndex;
        set { _zIndex = value; OnPropertyChanged(); }
    }

    private System.Windows.Thickness _collapsedMargin;
    public System.Windows.Thickness CollapsedMargin
    {
        get => _collapsedMargin;
        set { _collapsedMargin = value; OnPropertyChanged(); }
    }

    private System.Windows.Thickness _expandedMargin;
    public System.Windows.Thickness ExpandedMargin
    {
        get => _expandedMargin;
        set { _expandedMargin = value; OnPropertyChanged(); }
    }
}

public class EdgeStackViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly IEditorService _editorService;
    private ObservableCollection<Note> _allNotes = new();
    
    public ObservableCollection<NoteItem> VisibleNotes { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    private double _expandedWidth = 30;
    public double ExpandedWidth
    {
        get => _expandedWidth;
        set => SetProperty(ref _expandedWidth, value);
    }

    private Visibility _textVisibility = Visibility.Collapsed;
    public Visibility TextVisibility
    {
        get => _textVisibility;
        set => SetProperty(ref _textVisibility, value);
    }

    private int _scrollOffset = 0;

    public bool ShowAddButton => !_allNotes.Any();
    
    public System.Windows.Thickness CollapsedAddMargin => VisibleNotes.Any() ? new System.Windows.Thickness(0, 2, -(280 - (48 - (VisibleNotes.Count * 4))), 0) : new System.Windows.Thickness(0, 2, -(280 - 48), 0);
    public System.Windows.Thickness ExpandedAddMargin => VisibleNotes.Any() ? new System.Windows.Thickness(0, 2, -(280 - (280 - (VisibleNotes.Count * 16))), 0) : new System.Windows.Thickness(0, 2, 0, 0);

    public ICommand AddNoteCommand { get; }
    public ICommand EditNoteCommand { get; }

    public EdgeStackViewModel(INoteService noteService, IEditorService editorService)
    {
        _noteService = noteService;
        _editorService = editorService;
        AddNoteCommand = new RelayCommand(_ => AddNote());
        EditNoteCommand = new RelayCommand(obj => {
            if (obj is Note note) _editorService.OpenEditor(note, false);
        });
        
        _noteService.NotesChanged += () =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => _ = LoadNotesAsync());
        };
        
        _ = LoadNotesAsync();
    }

    public async Task LoadNotesAsync()
    {
        var notes = await _noteService.GetAllNotesAsync();
        _allNotes = new ObservableCollection<Note>(notes);
        OnPropertyChanged(nameof(ShowAddButton));
        UpdateVisibleNotes();
    }

    public void Scroll(int delta)
    {
        if (_allNotes.Count <= 10) return;
        
        _scrollOffset += delta;
        if (_scrollOffset < 0) _scrollOffset = 0;
        if (_scrollOffset > _allNotes.Count - 10) _scrollOffset = _allNotes.Count - 10;
        
        UpdateVisibleNotes();
    }

    public void ResetScroll()
    {
        _scrollOffset = 0;
        UpdateVisibleNotes();
    }

    private void UpdateVisibleNotes()
    {
        var toShow = _allNotes.Skip(_scrollOffset).Take(10).ToList();
        
        if (VisibleNotes.Count != toShow.Count)
        {
            VisibleNotes.Clear();
            for (int i = 0; i < toShow.Count; i++)
            {
                VisibleNotes.Add(new NoteItem
                {
                    Note = toShow[i],
                    ZIndex = 100 - i, // Top items have higher Z-Index to overlap bottom items
                    CollapsedMargin = new System.Windows.Thickness(0, 2, -(280 - (48 - (i * 4))), 0),
                    ExpandedMargin = new System.Windows.Thickness(0, 2, -(280 - (280 - (i * 16))), 0)
                });
            }
        }
        else
        {
            for (int i = 0; i < toShow.Count; i++)
            {
                VisibleNotes[i].Note = toShow[i];
                VisibleNotes[i].ZIndex = 100 - i;
                VisibleNotes[i].CollapsedMargin = new System.Windows.Thickness(0, 2, -(280 - (48 - (i * 4))), 0);
                VisibleNotes[i].ExpandedMargin = new System.Windows.Thickness(0, 2, -(280 - (280 - (i * 16))), 0);
            }
        }
        
        OnPropertyChanged(nameof(CollapsedAddMargin));
        OnPropertyChanged(nameof(ExpandedAddMargin));
        OnPropertyChanged(nameof(ShowAddButton));
    }

    private void AddNote()
    {
        _editorService.OpenEditor(new Note(), true);
    }
}
