using KanbanCards.Models;
using KanbanCards.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace KanbanCards.ViewModels;

public class NoteItem : ViewModelBase
{
    private Note _note = null!;
    public Note Note
    {
        get => _note;
        set { _note = value; OnPropertyChanged(); }
    }

    private int _slotIndex;
    public int SlotIndex
    {
        get => _slotIndex;
        set { _slotIndex = value; OnPropertyChanged(); }
    }

    private int _zIndex;
    public int ZIndex
    {
        get => _zIndex;
        set { _zIndex = value; OnPropertyChanged(); }
    }
}

public class EdgeStackViewModel : ViewModelBase
{
    private readonly INoteService _noteService;
    private readonly IEditorService _editorService;
    private readonly ISettingsService _settingsService;
    private ObservableCollection<Note> _allNotes = new();
    private string _stackPosition = "Right";
    private int _scrollOffset = 0;
    private bool _isExpanded;

    public ObservableCollection<NoteItem> VisibleNotes { get; } = new();

    public event Action<int>? ScrollShifted;
    public event Action? NotesUpdated;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public int ScrollOffset => _scrollOffset;

    public bool ShowAddButton => !_allNotes.Any();

    public bool IsLeft => _stackPosition == "Left";
    public HorizontalAlignment StackAlignment => IsLeft ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    public CornerRadius NoteCornerRadius => IsLeft ? new CornerRadius(0, 12, 12, 0) : new CornerRadius(12, 0, 0, 12);
    public Thickness NoteBorderThickness => IsLeft ? new Thickness(0, 1, 1, 1) : new Thickness(1, 1, 0, 1);
    public Point RenderTransformOrigin => IsLeft ? new Point(0, 0.5) : new Point(1, 0.5);
    public HorizontalAlignment PlusHorizontalAlignment => IsLeft ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Thickness PlusMargin => IsLeft ? new Thickness(0, -4, 12, 0) : new Thickness(12, -4, 0, 0);
    public Thickness TitleMargin => IsLeft ? new Thickness(20, 0, 16, 0) : new Thickness(16, 0, 20, 0);

    public ICommand AddNoteCommand { get; }
    public ICommand EditNoteCommand { get; }

    public EdgeStackViewModel(INoteService noteService, IEditorService editorService, ISettingsService settingsService)
    {
        _noteService = noteService;
        _editorService = editorService;
        _settingsService = settingsService;
        _stackPosition = _settingsService.LoadSettings().StackPosition;

        AddNoteCommand = new RelayCommand(_ => AddNote());
        EditNoteCommand = new RelayCommand(obj =>
        {
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
        OnPropertyChanged(nameof(TitleMargin));
        UpdateVisibleNotes();
        NotesUpdated?.Invoke();
    }

    public async Task LoadNotesAsync()
    {
        var notes = await _noteService.GetAllNotesAsync();
        _allNotes = new ObservableCollection<Note>(notes);

        int maxOffset = Math.Max(0, _allNotes.Count - 10);
        if (_scrollOffset > maxOffset)
        {
            _scrollOffset = maxOffset;
        }

        OnPropertyChanged(nameof(ShowAddButton));
        UpdateVisibleNotes();
        NotesUpdated?.Invoke();
    }

    public bool Scroll(int delta)
    {
        if (_allNotes.Count <= 10) return false;

        int maxOffset = _allNotes.Count - 10;
        int newOffset = Math.Clamp(_scrollOffset + delta, 0, maxOffset);
        if (newOffset == _scrollOffset) return false;

        _scrollOffset = newOffset;
        UpdateVisibleNotes();
        ScrollShifted?.Invoke(delta > 0 ? 1 : -1);
        return true;
    }

    public bool ResetScroll()
    {
        if (_scrollOffset == 0) return false;
        int oldOffset = _scrollOffset;
        _scrollOffset = 0;
        UpdateVisibleNotes();
        ScrollShifted?.Invoke(-oldOffset);
        return true;
    }

    public void UpdateVisibleNotes()
    {
        var toShow = _allNotes.Skip(_scrollOffset).Take(10).ToList();

        while (VisibleNotes.Count > toShow.Count)
        {
            VisibleNotes.RemoveAt(VisibleNotes.Count - 1);
        }

        for (int i = 0; i < toShow.Count; i++)
        {
            if (i < VisibleNotes.Count)
            {
                VisibleNotes[i].Note = toShow[i];
                VisibleNotes[i].SlotIndex = i;
                VisibleNotes[i].ZIndex = 100 - i;
            }
            else
            {
                VisibleNotes.Add(new NoteItem
                {
                    Note = toShow[i],
                    SlotIndex = i,
                    ZIndex = 100 - i
                });
            }
        }

        OnPropertyChanged(nameof(ShowAddButton));
    }

    private void AddNote()
    {
        _editorService.OpenEditor(new Note(), true);
    }
}
