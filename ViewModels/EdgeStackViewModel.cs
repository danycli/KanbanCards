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
    private readonly ISettingsService _settingsService;
    private ObservableCollection<Note> _allNotes = new();
    private string _stackPosition = "Right";
    
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

    public bool IsLeft => _stackPosition == "Left";
    public HorizontalAlignment StackAlignment => IsLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    public CornerRadius NoteCornerRadius => IsLeft ? new CornerRadius(0, 12, 12, 0) : new CornerRadius(12, 0, 0, 12);
    public Thickness NoteBorderThickness => IsLeft ? new Thickness(0, 1, 1, 1) : new Thickness(1, 1, 0, 1);
    public Point RenderTransformOrigin => IsLeft ? new Point(0, 0.5) : new Point(1, 0.5);
    public HorizontalAlignment PlusHorizontalAlignment => IsLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Thickness PlusMargin => IsLeft ? new Thickness(0, -4, 12, 0) : new Thickness(12, -4, 0, 0);
    public double TitleCanvasLeft => IsLeft ? 20 : 16;
    
    public Thickness CollapsedAddMargin
    {
        get
        {
            int count = VisibleNotes.Count;
            int offset = count > 0 ? (48 - (count * 4)) : 48;
            return IsLeft
                ? new Thickness(-(280 - offset), 2, 0, 0)
                : new Thickness(0, 2, -(280 - offset), 0);
        }
    }

    public Thickness ExpandedAddMargin
    {
        get
        {
            int count = VisibleNotes.Count;
            int offset = count > 0 ? (280 - (count * 16)) : 280;
            return IsLeft
                ? new Thickness(-(280 - offset), 2, 0, 0)
                : new Thickness(0, 2, -(280 - offset), 0);
        }
    }

    public ICommand AddNoteCommand { get; }
    public ICommand EditNoteCommand { get; }

    public EdgeStackViewModel(INoteService noteService, IEditorService editorService, ISettingsService settingsService)
    {
        _noteService = noteService;
        _editorService = editorService;
        _settingsService = settingsService;
        _stackPosition = _settingsService.LoadSettings().StackPosition;

        AddNoteCommand = new RelayCommand(_ => AddNote());
        EditNoteCommand = new RelayCommand(obj => {
            if (obj is Note note) _editorService.OpenEditor(note, false);
        });
        
        _noteService.NotesChanged += () =>
        {
            Application.Current?.Dispatcher.Invoke(() => _ = LoadNotesAsync());
        };

        _settingsService.SettingsChanged += () =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var settings = _settingsService.LoadSettings();
                ApplySettings(settings.StackPosition);
            });
        };
        
        _ = LoadNotesAsync();
    }

    public void ApplySettings(string stackPosition)
    {
        _stackPosition = stackPosition;
        OnPropertyChanged(nameof(IsLeft));
        OnPropertyChanged(nameof(StackAlignment));
        OnPropertyChanged(nameof(NoteCornerRadius));
        OnPropertyChanged(nameof(NoteBorderThickness));
        OnPropertyChanged(nameof(RenderTransformOrigin));
        OnPropertyChanged(nameof(PlusHorizontalAlignment));
        OnPropertyChanged(nameof(PlusMargin));
        OnPropertyChanged(nameof(TitleCanvasLeft));
        UpdateVisibleNotes();
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

    private Thickness GetCollapsedMargin(int index) =>
        IsLeft
            ? new Thickness(-(280 - (48 - (index * 4))), 2, 0, 0)
            : new Thickness(0, 2, -(280 - (48 - (index * 4))), 0);

    private Thickness GetExpandedMargin(int index) =>
        IsLeft
            ? new Thickness(-(280 - (280 - (index * 16))), 2, 0, 0)
            : new Thickness(0, 2, -(280 - (280 - (index * 16))), 0);

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
                    CollapsedMargin = GetCollapsedMargin(i),
                    ExpandedMargin = GetExpandedMargin(i)
                });
            }
        }
        else
        {
            for (int i = 0; i < toShow.Count; i++)
            {
                VisibleNotes[i].Note = toShow[i];
                VisibleNotes[i].ZIndex = 100 - i;
                VisibleNotes[i].CollapsedMargin = GetCollapsedMargin(i);
                VisibleNotes[i].ExpandedMargin = GetExpandedMargin(i);
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
