using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KanbanCards.Models;
using KanbanCards.Services;
using KanbanCards.ViewModels;
using Xunit;

namespace KanbanCards.Tests;

public class MockNoteService : INoteService
{
    private readonly List<Note> _notes = new();
    public event Action? NotesChanged;

    public void SetNotes(IEnumerable<Note> notes)
    {
        _notes.Clear();
        _notes.AddRange(notes);
    }

    public Task<List<Note>> GetAllNotesAsync()
    {
        return Task.FromResult(_notes.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToList());
    }

    public Task<Note?> GetNoteAsync(int id) => Task.FromResult(_notes.FirstOrDefault(n => n.Id == id));

    public Task<Note> CreateNoteAsync(Note note)
    {
        note.Id = _notes.Count > 0 ? _notes.Max(n => n.Id) + 1 : 1;
        _notes.Insert(0, note);
        NotesChanged?.Invoke();
        return Task.FromResult(note);
    }

    public Task UpdateNoteAsync(Note note)
    {
        var existing = _notes.FirstOrDefault(n => n.Id == note.Id);
        if (existing != null)
        {
            existing.Title = note.Title;
            existing.Description = note.Description;
            existing.Color = note.Color;
        }
        NotesChanged?.Invoke();
        return Task.CompletedTask;
    }

    public Task DeleteNoteAsync(int id)
    {
        _notes.RemoveAll(n => n.Id == id);
        NotesChanged?.Invoke();
        return Task.CompletedTask;
    }
}

public class MockSettingsService : ISettingsService
{
    public AppSettings Settings { get; set; } = new();
    public event Action? SettingsChanged;

    public AppSettings LoadSettings() => Settings;
    public void SaveSettings(AppSettings settings)
    {
        Settings = settings;
        SettingsChanged?.Invoke();
    }
}

public class MockEditorService : IEditorService
{
    public Note? LastOpenedNote { get; private set; }
    public bool LastOpenedIsNew { get; private set; }

    public void OpenEditor(Note note, bool isNew)
    {
        LastOpenedNote = note;
        LastOpenedIsNew = isNew;
    }
}

public class MotionAndStackTests
{
    [Fact]
    public void MotionSystem_Coordinates_RightDock_StaircaseIsCorrect()
    {
        // Collapsed on Right:
        // Slot 0 peeks 48px -> hidden = 280 - 48 = 232px
        // Slot 1 peeks 44px -> hidden = 280 - 44 = 236px
        // Slot 9 peeks 12px -> hidden = 280 - 12 = 268px
        Assert.Equal(232.0, MotionSystem.GetCardTargetX(0, isExpanded: false, isLeft: false));
        Assert.Equal(236.0, MotionSystem.GetCardTargetX(1, isExpanded: false, isLeft: false));
        Assert.Equal(268.0, MotionSystem.GetCardTargetX(9, isExpanded: false, isLeft: false));

        // Expanded on Right:
        // Slot 0 full 280px -> hidden = 0px
        // Slot 1 shows 264px -> hidden = 16px
        // Slot 9 shows 136px -> hidden = 144px
        Assert.Equal(0.0, MotionSystem.GetCardTargetX(0, isExpanded: true, isLeft: false));
        Assert.Equal(16.0, MotionSystem.GetCardTargetX(1, isExpanded: true, isLeft: false));
        Assert.Equal(144.0, MotionSystem.GetCardTargetX(9, isExpanded: true, isLeft: false));
    }

    [Fact]
    public void MotionSystem_Coordinates_LeftDock_StaircaseIsMirroredCorrectly()
    {
        // Collapsed on Left: mirrored negative values
        Assert.Equal(-232.0, MotionSystem.GetCardTargetX(0, isExpanded: false, isLeft: true));
        Assert.Equal(-236.0, MotionSystem.GetCardTargetX(1, isExpanded: false, isLeft: true));
        Assert.Equal(-268.0, MotionSystem.GetCardTargetX(9, isExpanded: false, isLeft: true));

        // Expanded on Left: mirrored negative values
        Assert.Equal(0.0, MotionSystem.GetCardTargetX(0, isExpanded: true, isLeft: true));
        Assert.Equal(-16.0, MotionSystem.GetCardTargetX(1, isExpanded: true, isLeft: true));
        Assert.Equal(-144.0, MotionSystem.GetCardTargetX(9, isExpanded: true, isLeft: true));
    }

    [Fact]
    public void MotionSystem_AddButtonCoordinate_MatchesSlotCount()
    {
        // With 5 cards, Add button is at slot 5
        double cardTarget = MotionSystem.GetCardTargetX(5, isExpanded: false, isLeft: false);
        double addTarget = MotionSystem.GetAddButtonTargetX(5, isExpanded: false, isLeft: false);
        Assert.Equal(cardTarget, addTarget);
    }

    [Fact]
    public void MotionSystem_Durations_RespectSettings()
    {
        var settingsDisabled = new AppSettings { EnableAnimations = false };
        Assert.Equal(TimeSpan.Zero, MotionSystem.HoverExpandDuration(settingsDisabled));
        Assert.Equal(TimeSpan.Zero, MotionSystem.ScrollShiftDuration(settingsDisabled));

        var settingsNormal = new AppSettings { EnableAnimations = true, AnimationSpeed = 50 };
        var normalDuration = MotionSystem.HoverExpandDuration(settingsNormal);
        Assert.InRange(normalDuration.TotalMilliseconds, 180, 240);

        var settingsGentle = new AppSettings { EnableAnimations = true, AnimationSpeed = 0 };
        var gentleDuration = MotionSystem.HoverExpandDuration(settingsGentle);
        Assert.True(gentleDuration > normalDuration, "Gentle mode must have longer duration");

        var settingsInstant = new AppSettings { EnableAnimations = true, AnimationSpeed = 100 };
        var instantDuration = MotionSystem.HoverExpandDuration(settingsInstant);
        Assert.True(instantDuration < normalDuration, "Instant mode must have shorter duration");
    }

    [Fact]
    public async Task EdgeStackViewModel_With10OrFewerNotes_DoesNotScroll()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        var notes = Enumerable.Range(1, 10).Select(i => new Note
        {
            Id = i,
            Title = $"Note {i}",
            CreatedAt = DateTime.UtcNow.AddMinutes(i)
        }).ToList();
        noteService.SetNotes(notes);

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        Assert.Equal(10, vm.VisibleNotes.Count);
        Assert.Equal(0, vm.ScrollOffset);

        // Scrolling attempts should return false and not change offset
        bool scrolled = vm.Scroll(1);
        Assert.False(scrolled);
        Assert.Equal(0, vm.ScrollOffset);

        scrolled = vm.Scroll(-1);
        Assert.False(scrolled);
        Assert.Equal(0, vm.ScrollOffset);
    }

    [Fact]
    public async Task EdgeStackViewModel_WithMoreThan10Notes_ScrollsOneNoteAtATime()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        // 15 notes total
        var notes = Enumerable.Range(1, 15).Select(i => new Note
        {
            Id = i,
            Title = $"Note {i}",
            CreatedAt = DateTime.UtcNow.AddMinutes(i) // i=15 is newest
        }).ToList();
        noteService.SetNotes(notes);

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        // Initial viewport: exactly 10 visible notes (newest first: Note 15 down to Note 6)
        Assert.Equal(10, vm.VisibleNotes.Count);
        Assert.Equal("Note 15", vm.VisibleNotes[0].Note.Title);
        Assert.Equal("Note 6", vm.VisibleNotes[9].Note.Title);
        Assert.Equal(0, vm.ScrollOffset);

        // One step down (+1)
        bool scrolled = vm.Scroll(1);
        Assert.True(scrolled);
        Assert.Equal(1, vm.ScrollOffset);
        Assert.Equal("Note 14", vm.VisibleNotes[0].Note.Title);
        Assert.Equal("Note 5", vm.VisibleNotes[9].Note.Title);

        // Another step down (+1)
        scrolled = vm.Scroll(1);
        Assert.True(scrolled);
        Assert.Equal(2, vm.ScrollOffset);
        Assert.Equal("Note 13", vm.VisibleNotes[0].Note.Title);

        // One step up (-1)
        scrolled = vm.Scroll(-1);
        Assert.True(scrolled);
        Assert.Equal(1, vm.ScrollOffset);
        Assert.Equal("Note 14", vm.VisibleNotes[0].Note.Title);
    }

    [Fact]
    public async Task EdgeStackViewModel_RapidScrolling_ClampsStrictlyAtBounds()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        // 30 notes total. Max offset is 30 - 10 = 20.
        var notes = Enumerable.Range(1, 30).Select(i => new Note
        {
            Id = i,
            Title = $"Note {i}",
            CreatedAt = DateTime.UtcNow.AddMinutes(i)
        }).ToList();
        noteService.SetNotes(notes);

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        // Rapid scroll 25 steps down (past max of 20)
        for (int i = 0; i < 25; i++)
        {
            vm.Scroll(1);
        }

        Assert.Equal(20, vm.ScrollOffset);
        Assert.Equal(10, vm.VisibleNotes.Count);
        // Note 1 is oldest, note 10 is 10th oldest
        Assert.Equal("Note 10", vm.VisibleNotes[0].Note.Title);
        Assert.Equal("Note 1", vm.VisibleNotes[9].Note.Title);

        // Rapid scroll 35 steps up (past 0)
        for (int i = 0; i < 35; i++)
        {
            vm.Scroll(-1);
        }

        Assert.Equal(0, vm.ScrollOffset);
        Assert.Equal("Note 30", vm.VisibleNotes[0].Note.Title);
    }

    [Fact]
    public async Task EdgeStackViewModel_ResetScroll_RestoresNewestNotes()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        var notes = Enumerable.Range(1, 20).Select(i => new Note
        {
            Id = i,
            Title = $"Note {i}",
            CreatedAt = DateTime.UtcNow.AddMinutes(i)
        }).ToList();
        noteService.SetNotes(notes);

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        // Scroll down 5 steps
        vm.Scroll(5);
        Assert.Equal(5, vm.ScrollOffset);

        // Reset scroll (as occurs on mouse leave)
        bool reset = vm.ResetScroll();
        Assert.True(reset);
        Assert.Equal(0, vm.ScrollOffset);
        Assert.Equal("Note 20", vm.VisibleNotes[0].Note.Title);
    }

    [Fact]
    public async Task EdgeStackViewModel_SlotIndicesAndZIndexes_AreDeterministic()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        var notes = Enumerable.Range(1, 8).Select(i => new Note
        {
            Id = i,
            Title = $"Note {i}",
            CreatedAt = DateTime.UtcNow.AddMinutes(i)
        }).ToList();
        noteService.SetNotes(notes);

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        Assert.Equal(8, vm.VisibleNotes.Count);
        for (int i = 0; i < vm.VisibleNotes.Count; i++)
        {
            Assert.Equal(i, vm.VisibleNotes[i].SlotIndex);
            Assert.Equal(100 - i, vm.VisibleNotes[i].ZIndex);
        }
    }

    [Fact]
    public async Task EdgeStackViewModel_SingleNote_ShowsSingleNoteAndNoScroll()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        noteService.SetNotes(new[] { new Note { Id = 1, Title = "Only Note", CreatedAt = DateTime.UtcNow } });

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        Assert.Single(vm.VisibleNotes);
        Assert.Equal("Only Note", vm.VisibleNotes[0].Note.Title);
        Assert.Equal(0, vm.ScrollOffset);
        Assert.False(vm.Scroll(1));
        Assert.False(vm.Scroll(-1));
    }

    [Fact]
    public async Task EdgeStackViewModel_ZeroNotes_ShowsEmptyNotes()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        Assert.Empty(vm.VisibleNotes);
        Assert.True(vm.ShowAddButton);
        Assert.False(vm.Scroll(1));
    }

    [Fact]
    public async Task EdgeStackViewModel_CreateNote_InsertsAtTop()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        noteService.SetNotes(new[] { new Note { Id = 1, Title = "First Note", CreatedAt = DateTime.UtcNow } });

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        Assert.Single(vm.VisibleNotes);

        await noteService.CreateNoteAsync(new Note { Title = "Brand New Note", CreatedAt = DateTime.UtcNow.AddMinutes(5) });
        await vm.LoadNotesAsync();

        Assert.Equal(2, vm.VisibleNotes.Count);
        Assert.Equal("Brand New Note", vm.VisibleNotes[0].Note.Title);
        Assert.Equal("First Note", vm.VisibleNotes[1].Note.Title);
    }

    [Fact]
    public async Task EdgeStackViewModel_DeleteNote_AdjustsScrollOffsetGracefully()
    {
        var noteService = new MockNoteService();
        var settingsService = new MockSettingsService();
        var editorService = new MockEditorService();

        // 12 notes: max offset is 12 - 10 = 2
        var notes = Enumerable.Range(1, 12).Select(i => new Note
        {
            Id = i,
            Title = $"Note {i}",
            CreatedAt = DateTime.UtcNow.AddMinutes(i)
        }).ToList();
        noteService.SetNotes(notes);

        var vm = new EdgeStackViewModel(noteService, editorService, settingsService);
        await vm.LoadNotesAsync();

        vm.Scroll(2);
        Assert.Equal(2, vm.ScrollOffset);

        // Delete 2 notes -> total 10 notes -> max offset becomes 0
        await noteService.DeleteNoteAsync(1);
        await noteService.DeleteNoteAsync(2);
        await vm.LoadNotesAsync();

        Assert.Equal(0, vm.ScrollOffset);
        Assert.Equal(10, vm.VisibleNotes.Count);
    }
}
