using System;
using System.Linq;
using System.Threading.Tasks;
using KanbanCards.Models;
using KanbanCards.ViewModels;
using Xunit;

namespace KanbanCards.Tests;

public class AppLogicTests
{
    [Fact]
    public async Task MainViewModel_SearchByTitleAndDescription_FiltersCorrectly()
    {
        var noteService = new MockNoteService();
        var editorService = new MockEditorService();

        noteService.SetNotes(new[]
        {
            new Note { Id = 1, Title = "Grocery shopping", Description = "Buy apples and milk", CreatedAt = DateTime.UtcNow.AddMinutes(1) },
            new Note { Id = 2, Title = "Networking Project", Description = "Configure router lab", CreatedAt = DateTime.UtcNow.AddMinutes(2) },
            new Note { Id = 3, Title = "Study for exam", Description = "Networking principles chapter 5", CreatedAt = DateTime.UtcNow.AddMinutes(3) }
        });

        var vm = new MainViewModel(noteService, editorService);
        await vm.LoadNotesAsync();

        Assert.Equal(3, vm.FilteredNotes.Count);

        // Filter by title
        vm.SearchText = "grocery";
        await vm.LoadNotesAsync();
        Assert.Single(vm.FilteredNotes);
        Assert.Equal("Grocery shopping", vm.FilteredNotes[0].Title);

        // Filter by description
        vm.SearchText = "router";
        await vm.LoadNotesAsync();
        Assert.Single(vm.FilteredNotes);
        Assert.Equal("Networking Project", vm.FilteredNotes[0].Title);

        // Filter matching both title and description
        vm.SearchText = "networking";
        await vm.LoadNotesAsync();
        Assert.Equal(2, vm.FilteredNotes.Count);
        // Newest first order must be preserved: Id 3 (min 3) before Id 2 (min 2)
        Assert.Equal("Study for exam", vm.FilteredNotes[0].Title);
        Assert.Equal("Networking Project", vm.FilteredNotes[1].Title);

        // Clear search restores all
        vm.ClearSearchCommand.Execute(null);
        await vm.LoadNotesAsync();
        Assert.Equal(3, vm.FilteredNotes.Count);
    }

    [Fact]
    public async Task NoteEditorViewModel_UnsavedNote_TitleRequired()
    {
        var noteService = new MockNoteService();
        var note = new Note();
        var editorVm = new NoteEditorViewModel(noteService, note, isNew: true);

        // Execute save with empty title - should not create
        editorVm.OkCommand.Execute(null);
        var notes = await noteService.GetAllNotesAsync();
        Assert.Empty(notes);

        // Set title and save
        editorVm.Title = "Valid Title";
        editorVm.OkCommand.Execute(null);
        notes = await noteService.GetAllNotesAsync();
        Assert.Single(notes);
        Assert.Equal("Valid Title", notes[0].Title);
    }
}
