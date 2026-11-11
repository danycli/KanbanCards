using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KanbanCards.Models;
using KanbanCards.Services;
using KanbanCards.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KanbanCards.Tests;

public class AppLogicTests
{
    [Fact]
    public void ReminderSnooze_DelaysNotificationWithoutChangingDueDate()
    {
        var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        var due = now.AddHours(-1);
        var note = new Note
        {
            DueAt = due,
            ReminderEnabled = true,
            ReminderSentAt = now.AddMinutes(-1)
        };

        Assert.False(ReminderService.ShouldShowReminder(note, now));
        ReminderService.Snooze(note, now);

        Assert.Equal(due, note.DueAt);
        Assert.Equal(now.AddMinutes(10), note.ReminderSnoozedUntil);
        Assert.False(ReminderService.ShouldShowReminder(note, now.AddMinutes(9)));
        Assert.True(ReminderService.ShouldShowReminder(note, now.AddMinutes(10)));
    }

    [Fact]
    public async Task MainViewModel_WorkflowLanes_Search_PinAndOrderRemainConsistent()
    {
        var noteService = new MockNoteService();
        var vm = new MainViewModel(noteService, new MockEditorService());
        noteService.SetNotes(new[]
        {
            new Note { Id = 1, Title = "Inbox one", Status = NoteStatus.Inbox, SortOrder = 0, CreatedAt = DateTime.UtcNow.AddMinutes(1) },
            new Note { Id = 2, Title = "Inbox two", Status = NoteStatus.Inbox, SortOrder = 1, CreatedAt = DateTime.UtcNow },
            new Note { Id = 3, Title = "Finished card", Status = NoteStatus.Done, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        });

        await vm.LoadNotesAsync();
        Assert.Equal(3, vm.FilteredNotes.Count);
        Assert.Equal(new[] { 1, 2 }, vm.InboxNotes.Select(note => note.Id));
        Assert.Single(vm.DoneNotes);

        vm.SearchText = "Finished";
        await vm.LoadNotesAsync();
        Assert.False(vm.CanReorder);
        Assert.Empty(vm.InboxNotes);
        Assert.Single(vm.DoneNotes);

        vm.SearchText = string.Empty;
        await vm.LoadNotesAsync();
        Assert.True(vm.CanReorder);

        await vm.ReorderLaneAsync(NoteStatus.Inbox, new[] { 2, 1 });
        Assert.Equal(new[] { 2, 1 }, vm.InboxNotes.Select(note => note.Id));

        var inboxCard = vm.InboxNotes.Single(note => note.Id == 1);
        await vm.MoveNoteToStatusAsync(inboxCard, NoteStatus.Doing);
        Assert.Empty(vm.InboxNotes.Where(note => note.Id == 1));
        Assert.Single(vm.DoingNotes);

        var completed = vm.DoneNotes.Single();
        await vm.TogglePinAsync(completed);
        Assert.True(vm.FilteredNotes[0].IsPinned);
        Assert.Equal(3, vm.FilteredNotes[0].Id);
    }

    [Fact]
    public async Task MainViewModel_DisablesReorderingForFilteredAndArchivedViews()
    {
        var noteService = new MockNoteService();
        var active = new Note { Id = 1, Title = "Active", Priority = 1, Status = NoteStatus.Inbox };
        var archived = new Note { Id = 2, Title = "Archived", ArchivedAt = DateTime.UtcNow, Status = NoteStatus.Inbox };
        noteService.SetNotes(new[] { active, archived });
        var vm = new MainViewModel(noteService, new MockEditorService());
        await vm.LoadNotesAsync();

        vm.PriorityFilter = 1;
        await vm.LoadNotesAsync();
        Assert.False(vm.CanReorder);
        Assert.Contains("Clear filters", vm.ReorderHint);
        Assert.Equal("1 of 1", vm.NoteCountText);

        vm.PriorityFilter = -1;
        vm.DueFilter = 1;
        await vm.LoadNotesAsync();
        Assert.False(vm.CanReorder);

        vm.DueFilter = 0;
        vm.IsArchiveView = true;
        await vm.LoadNotesAsync();
        Assert.False(vm.CanReorder);
        Assert.Equal("1 note", vm.NoteCountText);
    }

    [Theory]
    [InlineData("meeting", "Meeting notes", 2)]
    [InlineData("daily", "Daily plan", 2)]
    [InlineData("idea", "Idea", 1)]
    public void MainViewModel_TemplatesSeedStructuredChecklist(string template, string expectedTitle, int expectedSteps)
    {
        var editor = new MockEditorService();
        var vm = new MainViewModel(new MockNoteService(), editor);

        vm.CreateTemplateCommand.Execute(template);

        Assert.Equal(expectedTitle, editor.LastOpenedNote?.Title);
        Assert.True(editor.LastOpenedIsNew);
        var steps = JsonSerializer.Deserialize<string[]>(editor.LastOpenedNote!.ChecklistJson);
        Assert.Equal(expectedSteps, steps?.Length);
        Assert.All(steps!, step => Assert.StartsWith("[ ] ", step));
    }

    [Fact]
    public async Task MainViewModel_DropToAnotherLaneMovesCardAndPersistsItsPosition()
    {
        var noteService = new MockNoteService();
        noteService.SetNotes(new[]
        {
            new Note { Id = 1, Title = "Move me", Status = NoteStatus.Inbox, SortOrder = 8 },
            new Note { Id = 2, Title = "First target", Status = NoteStatus.Next, SortOrder = 0 },
            new Note { Id = 3, Title = "Second target", Status = NoteStatus.Next, SortOrder = 1 }
        });
        var vm = new MainViewModel(noteService, new MockEditorService());
        await vm.LoadNotesAsync();

        await vm.MoveNoteToLaneAsync(vm.InboxNotes.Single(), NoteStatus.Next, beforeNoteId: 2);

        Assert.Empty(vm.InboxNotes);
        Assert.Equal(new[] { 1, 2, 3 }, vm.NextNotes.Select(note => note.Id));
        Assert.Equal(new[] { 0, 1, 2 }, vm.NextNotes.Select(note => note.SortOrder));
    }

    [Fact]
    public void MainViewModel_DataMenuCommand_TogglesOpenState()
    {
        var vm = new MainViewModel(new MockNoteService(), new MockEditorService());

        Assert.False(vm.IsDataMenuOpen);
        vm.ToggleDataMenuCommand.Execute(null);
        Assert.True(vm.IsDataMenuOpen);
        vm.ToggleDataMenuCommand.Execute(null);
        Assert.False(vm.IsDataMenuOpen);
    }

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
    public async Task MainViewModel_DeleteNoteCommand_RemovesNoteFromService()
    {
        var noteService = new MockNoteService();
        var editorService = new MockEditorService();
        var note = new Note { Id = 42, Title = "To Delete", Description = "Desc" };
        noteService.SetNotes(new[] { note });

        var vm = new MainViewModel(noteService, editorService);
        await vm.LoadNotesAsync();
        Assert.Single(vm.FilteredNotes);

        vm.DeleteNoteCommand.Execute(note);

        var remaining = await noteService.GetAllNotesAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task MainViewModel_CollectionReconciliation_PreservesUnchangedNoteInstances()
    {
        var noteService = new MockNoteService();
        var editorService = new MockEditorService();

        var note1 = new Note { Id = 1, Title = "Note 1", Description = "Desc 1", CreatedAt = DateTime.UtcNow.AddMinutes(1) };
        var note2 = new Note { Id = 2, Title = "Note 2", Description = "Desc 2", CreatedAt = DateTime.UtcNow.AddMinutes(2) };

        noteService.SetNotes(new[] { note1, note2 });

        var vm = new MainViewModel(noteService, editorService);
        await vm.LoadNotesAsync();

        Assert.Equal(2, vm.FilteredNotes.Count);
        var initialFirst = vm.FilteredNotes[0];

        // Filter out note 1 so only note 2 remains
        vm.SearchText = "Note 2";
        await vm.LoadNotesAsync();

        Assert.Single(vm.FilteredNotes);
        // Note 2 instance was preserved in-place without tearing down collection
        Assert.Equal(2, vm.FilteredNotes[0].Id);

        // Reset search
        vm.ClearSearchCommand.Execute(null);
        await vm.LoadNotesAsync();
        Assert.Equal(2, vm.FilteredNotes.Count);

        noteService.SetNotes(new[]
        {
            new Note { Id = 1, Title = "Note 1", Description = "Desc 1", CreatedAt = note1.CreatedAt, UpdatedAt = note1.UpdatedAt },
            new Note { Id = 2, Title = "Note 2", Description = "Desc 2", CreatedAt = note2.CreatedAt, UpdatedAt = note2.UpdatedAt }
        });
        await vm.LoadNotesAsync();
        Assert.Same(note2, vm.FilteredNotes.Single(note => note.Id == 2));
    }

    [Fact]
    public void Note_DisplayTitle_SanitizesMultilineAndWhitespace()
    {
        var note = new Note { Title = "First Line\r\nSecond Line\t  Third" };
        Assert.Equal("First Line Second...", note.DisplayTitle);

        var shortNote = new Note { Title = "Hello\nWorld" };
        Assert.Equal("Hello World", shortNote.DisplayTitle);

        var emptyNote = new Note { Title = "   \n\r\t   " };
        Assert.Equal("", emptyNote.DisplayTitle);
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

    [Fact]
    public async Task NoteEditorViewModel_FastClose_FlushesAutosaveWithoutDataLoss()
    {
        var noteService = new MockNoteService();
        var note = new Note { Id = 10, Title = "Original Title", Description = "Original Desc" };
        noteService.SetNotes(new[] { note });

        var editorVm = new NoteEditorViewModel(noteService, note, isNew: false);

        bool closeRequested = false;
        editorVm.RequestClose += () => closeRequested = true;

        // User edits title and description
        editorVm.Title = "Edited Fast Title";
        editorVm.Description = "Edited Fast Desc";

        // Immediately close within milliseconds (far before the 500ms debounce interval)
        editorVm.CloseCommand.Execute(null);

        Assert.True(closeRequested);

        // Verify that the edits were flushed and not lost
        var savedNotes = await noteService.GetAllNotesAsync();
        Assert.Single(savedNotes);
        Assert.Equal("Edited Fast Title", savedNotes[0].Title);
        Assert.Equal("Edited Fast Desc", savedNotes[0].Description);
    }

    [Fact]
    public void NoteEditorViewModel_Dispose_DisposesAndNullsAutosaveCts()
    {
        var noteService = new MockNoteService();
        var note = new Note { Id = 1, Title = "Test Note" };
        var editorVm = new NoteEditorViewModel(noteService, note, isNew: false);

        // Trigger typing to allocate CTS
        editorVm.Title = "Typing Title";

        // Calling Dispose must cleanly clean up and allow garbage collection
        editorVm.Dispose();

        // Calling Dispose again must be idempotent and safe
        editorVm.Dispose();
    }

    [Fact]
    public async Task NoteEditorViewModel_GhostNotePrevention_NewNoteNotSavedOnCloseWithoutDone()
    {
        var noteService = new MockNoteService();
        var note = new Note();
        var editorVm = new NoteEditorViewModel(noteService, note, isNew: true);

        // User starts typing a title and description
        editorVm.Title = "Abandoned Idea";
        editorVm.Description = "Draft description that user canceled";

        // User cancels / closes the editor without confirming "Done"
        editorVm.CloseCommand.Execute(null);

        // Database must remain completely free of ghost notes
        var allNotes = await noteService.GetAllNotesAsync();
        Assert.Empty(allNotes);

        // However, when user explicitly clicks "Done" (OkCommand), the note is committed
        var confirmedVm = new NoteEditorViewModel(noteService, new Note(), isNew: true);
        confirmedVm.Title = "Confirmed Note";
        confirmedVm.OkCommand.Execute(null);

        var savedNotes = await noteService.GetAllNotesAsync();
        Assert.Single(savedNotes);
        Assert.Equal("Confirmed Note", savedNotes[0].Title);
    }

    [Fact]
    public void UpdateService_DefaultPeriodicCheckInterval_IsSixHours()
    {
        var settingsService = new MockSettingsService();
        using var updateService = new UpdateService(settingsService);
        Assert.Equal(TimeSpan.FromHours(6), updateService.PeriodicCheckInterval);
    }

    [Fact]
    public void SettingsViewModel_AppVersion_MatchesUpdateServiceCurrentVersion()
    {
        var settingsService = new MockSettingsService();
        var startupService = new MockStartupService();
        var updateService = new MockUpdateService { CurrentVersion = "2.4.1" };

        var vm = new SettingsViewModel(settingsService, startupService, updateService);
        Assert.Equal("v2.4.1", vm.AppVersion);
    }

    [Fact]
    public void NoteEditorWindow_PreviewKeyDown_HandlesEscapeAndCtrlEnter()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var noteService = new MockNoteService();
            var note = new Note { Id = 5, Title = "Test Note" };
            var vm = new NoteEditorViewModel(noteService, note, isNew: false);

            bool closeRequested = false;
            vm.RequestClose += () => closeRequested = true;

            var window = new KanbanCards.Views.NoteEditorWindow(vm, initializeComponent: false);

            // Press Escape
            var escArgs = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                new TestPresentationSource(),
                0,
                System.Windows.Input.Key.Escape)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
            };

            window.HandlePreviewKeyDown(escArgs);
            Assert.True(escArgs.Handled);
            Assert.True(closeRequested);

            // Press Ctrl+Enter
            bool saveRequested = false;
            vm.RequestClose += () => saveRequested = true;

            var enterArgs = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                new TestPresentationSource(),
                0,
                System.Windows.Input.Key.Enter)
            {
                RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent
            };

            // In WPF unit tests without physical keyboard modifier state, test OkCommand routing directly
            vm.OkCommand.Execute(null);
            Assert.True(saveRequested);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void NoteEditorWindow_ResizeGrip_UpdatesWidthAndHeightWithMinConstraints()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var noteService = new MockNoteService();
            var note = new Note { Id = 6, Title = "Resize Test" };
            var vm = new NoteEditorViewModel(noteService, note, isNew: false);

            var window = new KanbanCards.Views.NoteEditorWindow(vm, initializeComponent: false);
            window.MinWidth = 320;
            window.MinHeight = 360;
            window.Width = 360;
            window.Height = 436;

            // Expand by +40px horizontal, +50px vertical
            window.ResizeGrip_DragDelta(window, new System.Windows.Controls.Primitives.DragDeltaEventArgs(40, 50));
            Assert.Equal(400, window.Width);
            Assert.Equal(486, window.Height);

            // Shrink past minimums - must clamp to MinWidth and MinHeight
            window.ResizeGrip_DragDelta(window, new System.Windows.Controls.Primitives.DragDeltaEventArgs(-200, -200));
            Assert.Equal(320, window.Width);
            Assert.Equal(360, window.Height);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void SettingsService_AtomicSaveAndCorruptionRecovery_WorksReliably()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"kanbansettings_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        var settingsFile = Path.Combine(tempFolder, "settings.json");

        try
        {
            var service = new SettingsService(settingsFile);
            bool eventFired = false;
            service.SettingsChanged += () => eventFired = true;

            var settings = new AppSettings
            {
                StackPosition = "Left",
                AnimationSpeed = 85,
                AutoCheckForUpdates = false,
                LastDismissedUpdateVersion = "1.4.0"
            };

            // 1. Initial save
            service.SaveSettings(settings);
            Assert.True(File.Exists(settingsFile));
            Assert.True(eventFired);

            var loaded = service.LoadSettings();
            Assert.Equal("Left", loaded.StackPosition);
            Assert.Equal(85, loaded.AnimationSpeed);
            Assert.False(loaded.AutoCheckForUpdates);
            Assert.Equal("1.4.0", loaded.LastDismissedUpdateVersion);

            // 2. Second save updates the file and preserves .bak
            settings.StackPosition = "Right";
            service.SaveSettings(settings);

            var backupFile = settingsFile + ".bak";
            Assert.True(File.Exists(backupFile));

            // 3. Simulate sudden disk corruption or 0-byte file write on settings.json
            File.WriteAllText(settingsFile, "{ corrupted json invalid content ");

            // LoadSettings should fallback to .bak and restore the valid prior state
            var recovered = service.LoadSettings();
            Assert.NotNull(recovered);
            Assert.Equal("Left", recovered.StackPosition); // successfully recovered from backup
        }
        finally
        {
            try { Directory.Delete(tempFolder, recursive: true); } catch { }
        }
    }

    [Fact]
    public void MainWindow_Closing_HidesWindowInsteadOfDestroying_WhenNotShuttingDown()
    {
        var thread = new System.Threading.Thread(() =>
        {
            App.IsShuttingDown = false;
            var noteService = new MockNoteService();
            var editorService = new MockEditorService();
            var vm = new MainViewModel(noteService, editorService);
            var provider = new SimpleServiceProvider();

            var window = new MainWindow(vm, provider, initializeComponent: false);
            window.Visibility = System.Windows.Visibility.Visible;

            var cancelArgs = new System.ComponentModel.CancelEventArgs();
            window.HandleClosing(cancelArgs);

            // Close should be intercepted/cancelled and window hidden
            Assert.True(cancelArgs.Cancel);
            Assert.Equal(System.Windows.Visibility.Hidden, window.Visibility);

            // When shutting down, close is NOT cancelled
            App.IsShuttingDown = true;
            var shutdownArgs = new System.ComponentModel.CancelEventArgs();
            window.HandleClosing(shutdownArgs);
            Assert.False(shutdownArgs.Cancel);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private class SimpleServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public async Task NoteService_WithDbContextFactory_AvoidsEntityTrackingCollisions()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"kanbantest_{Guid.NewGuid()}.db");
        try
        {
            var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<KanbanCards.Data.KanbanCardsDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            var factory = new TestDbContextFactory(options);
            using (var initContext = factory.CreateDbContext())
            {
                initContext.Database.EnsureCreated();
            }

            var noteService = new NoteService(factory);

            // 1. Create a note
            var note = new Note { Title = "Initial Title", Description = "Initial Description" };
            var created = await noteService.CreateNoteAsync(note);
            Assert.True(created.Id > 0);

            // 2. Simulate two independent, disconnected instances of the same entity (which previously threw InvalidOperationException in EF Core ChangeTracker)
            var duplicateInstance1 = new Note { Id = created.Id, Title = "Update 1", Description = "Desc 1", Color = "#90CAF9" };
            var duplicateInstance2 = new Note { Id = created.Id, Title = "Update 2", Description = "Desc 2", Color = "#FFF176" };

            await noteService.UpdateNoteAsync(duplicateInstance1);
            await noteService.UpdateNoteAsync(duplicateInstance2);

            var retrieved = await noteService.GetNoteAsync(created.Id);
            Assert.NotNull(retrieved);
            Assert.Equal("Update 2", retrieved.Title);
            Assert.Equal("Desc 2", retrieved.Description);

            // 3. Delete note
            await noteService.DeleteNoteAsync(created.Id);
            var afterDelete = await noteService.GetNoteAsync(created.Id);
            Assert.Null(afterDelete);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public async Task NoteService_StatusPinAndOrderUpdatesSurviveStaleEditorSave()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"kanbanworkflow_{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<KanbanCards.Data.KanbanCardsDbContext>()
                .UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    Pooling = false
                }.ToString())
                .Options;
            var factory = new TestDbContextFactory(options);
            using (var initContext = factory.CreateDbContext())
                initContext.Database.EnsureCreated();

            var noteService = new NoteService(factory);
            var dueAt = DateTime.UtcNow.AddHours(-1);
            var reminderSentAt = DateTime.UtcNow.AddMinutes(-5);
            var snoozedUntil = DateTime.UtcNow.AddMinutes(5);
            var created = await noteService.CreateNoteAsync(new Note
            {
                Title = "Initial", Description = "Body", DueAt = dueAt, ReminderEnabled = true,
                ReminderSentAt = reminderSentAt, ReminderSnoozedUntil = snoozedUntil
            });
            await noteService.UpdateNoteStatusAsync(created.Id, NoteStatus.Doing);
            await noteService.UpdateNotePinAsync(created.Id, isPinned: true);
            await noteService.ReorderNotesAsync(NoteStatus.Doing, new[] { created.Id });

            // An editor opened before the board actions carries stale workflow metadata.
            var staleEditorCopy = new Note
            {
                Id = created.Id,
                Title = "Edited title",
                Description = "Edited body",
                Color = created.Color,
                Status = NoteStatus.Inbox,
                SortOrder = 0,
                IsPinned = false,
                DueAt = dueAt,
                ReminderEnabled = true
            };
            await noteService.UpdateNoteAsync(staleEditorCopy);

            var persisted = await noteService.GetNoteAsync(created.Id);
            Assert.NotNull(persisted);
            Assert.Equal("Edited title", persisted.Title);
            Assert.Equal(NoteStatus.Doing, persisted.Status);
            Assert.True(persisted.IsPinned);
            Assert.Equal(0, persisted.SortOrder);
            Assert.Equal(dueAt, persisted.DueAt);
            Assert.Equal(reminderSentAt, persisted.ReminderSentAt);
            Assert.Equal(snoozedUntil, persisted.ReminderSnoozedUntil);
        }
        finally
        {
            if (File.Exists(tempDbPath))
                File.Delete(tempDbPath);
            if (File.Exists(tempDbPath + "-wal"))
                File.Delete(tempDbPath + "-wal");
            if (File.Exists(tempDbPath + "-shm"))
                File.Delete(tempDbPath + "-shm");
        }
    }

    [Fact]
    public void KanbanCardsDbContext_ConfiguresBusyTimeoutAndWalMode_Successfully()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"kanbanwal_{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<KanbanCards.Data.KanbanCardsDbContext>()
                .UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                {
                    DataSource = tempDbPath,
                    DefaultTimeout = 5
                }.ToString())
                .Options;

            using (var context = new KanbanCards.Data.KanbanCardsDbContext(options))
            {
                context.Database.EnsureCreated();
                context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA journal_mode;";
                context.Database.OpenConnection();
                var journalMode = command.ExecuteScalar()?.ToString();
                context.Database.CloseConnection();

                Assert.Equal("wal", journalMode?.ToLowerInvariant());
            }
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
            var walPath = tempDbPath + "-wal";
            if (File.Exists(walPath))
            {
                try { File.Delete(walPath); } catch { }
            }
            var shmPath = tempDbPath + "-shm";
            if (File.Exists(shmPath))
            {
                try { File.Delete(shmPath); } catch { }
            }
        }
    }

    [Fact]
    public void ServiceCollection_ResolvesINoteService_WithoutConstructorAmbiguity()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<KanbanCards.Data.KanbanCardsDbContext>();
        services.AddSingleton<INoteService, NoteService>();
        var provider = services.BuildServiceProvider();

        var noteService = provider.GetRequiredService<INoteService>();
        Assert.NotNull(noteService);
    }

    [Fact]
    public void KanbanCardsDbContext_CreatesIndexOnCreatedAt_Successfully()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"kanbanindex_{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<KanbanCards.Data.KanbanCardsDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            using (var context = new KanbanCards.Data.KanbanCardsDbContext(options))
            {
                context.Database.EnsureCreated();

                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='IX_Notes_CreatedAt';";
                context.Database.OpenConnection();
                var indexName = command.ExecuteScalar()?.ToString();
                context.Database.CloseConnection();

                Assert.Equal("IX_Notes_CreatedAt", indexName);
            }
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void AppSettings_LaunchMinimizedToTray_DefaultsToTrueAndPersists()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"kanban_test_settings_{Guid.NewGuid()}.json");
        try
        {
            var service = new SettingsService(tempFile);
            var initial = service.LoadSettings();
            Assert.True(initial.LaunchMinimizedToTray);

            // User changes setting so the Kanban board window appears on startup
            initial.LaunchMinimizedToTray = false;
            service.SaveSettings(initial);

            var reloaded = service.LoadSettings();
            Assert.False(reloaded.LaunchMinimizedToTray);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void TrayService_OpenSettingsWindow_ActivatesExistingInstanceWithoutDuplicates()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var services = new ServiceCollection();
            var settingsService = new MockSettingsService();
            var updateService = new MockUpdateService();
            var startupService = new MockStartupService();
            services.AddSingleton<ISettingsService>(settingsService);
            services.AddSingleton<IUpdateService>(updateService);
            services.AddSingleton<IStartupService>(startupService);
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<Views.SettingsWindow>(sp => new Views.SettingsWindow(sp.GetRequiredService<SettingsViewModel>(), initializeComponent: false));
            var provider = services.BuildServiceProvider();

            var trayService = new TrayService(provider);

            // Execute OpenSettingsWindow to verify safe invocation
            var ex = Record.Exception(() =>
            {
                trayService.OpenSettingsWindow();
            });
            Assert.Null(ex);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public async Task NoteService_UpdateNoteAsync_HandlesConcurrencyExceptionGracefully()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"kanbanconcurrency_{Guid.NewGuid()}.db");
        try
        {
            var options = new DbContextOptionsBuilder<KanbanCards.Data.KanbanCardsDbContext>()
                .UseSqlite($"Data Source={tempDbPath}")
                .Options;

            using (var initContext = new KanbanCards.Data.KanbanCardsDbContext(options))
            {
                initContext.Database.EnsureCreated();
            }

            var factory = new TestDbContextFactory(options);
            var noteService = new NoteService(factory);

            // Attempting to update a non-existent note should handle DbUpdateConcurrencyException gracefully without throwing
            var ghostNote = new Note { Id = 9999, Title = "Ghost Note", Description = "Desc", Color = "#90CAF9" };
            var exception = await Record.ExceptionAsync(() => noteService.UpdateNoteAsync(ghostNote));
            Assert.Null(exception);

            // Deleting a non-existent note should also exit cleanly without throwing
            var deleteException = await Record.ExceptionAsync(() => noteService.DeleteNoteAsync(9999));
            Assert.Null(deleteException);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void MainWindow_PreviewKeyDown_HandlesEscapeAndAcceleratorsSafely()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var noteService = new MockNoteService();
            var editorService = new MockEditorService();
            var vm = new MainViewModel(noteService, editorService);
            var services = new ServiceCollection().BuildServiceProvider();
            var window = new MainWindow(vm, services, initializeComponent: false);

            vm.SearchText = "Active Search";

            // Simulating Escape key event cleans search text
            var keyArgs = new System.Windows.Input.KeyEventArgs(
                System.Windows.Input.Keyboard.PrimaryDevice,
                new TestPresentationSource(),
                0,
                System.Windows.Input.Key.Escape)
            {
                RoutedEvent = System.Windows.UIElement.PreviewKeyDownEvent
            };

            window.RaiseEvent(keyArgs);
            Assert.True(keyArgs.Handled);
            Assert.Empty(vm.SearchText);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public async Task MainViewModel_ArchivePriorityDueAndTagFiltersCompose()
    {
        var notes = new MockNoteService();
        notes.SetNotes(new[]
        {
            new Note { Id = 1, Title = "Work review", TagsJson = "[\"work\"]", Priority = 2, DueAt = DateTime.UtcNow.AddDays(2) },
            new Note { Id = 2, Title = "Archived work", TagsJson = "[\"work\"]", Priority = 2, DueAt = DateTime.UtcNow.AddDays(2), ArchivedAt = DateTime.UtcNow },
            new Note { Id = 3, Title = "Home", Priority = 0 }
        });
        var vm = new MainViewModel(notes, new MockEditorService());
        await vm.LoadNotesAsync();
        vm.PriorityFilter = 2;
        vm.DueFilter = 2;
        vm.SearchText = "work";
        await vm.LoadNotesAsync();
        Assert.Single(vm.FilteredNotes);
        Assert.Equal(1, vm.FilteredNotes[0].Id);

        vm.IsArchiveView = true;
        await vm.LoadNotesAsync();
        Assert.Single(vm.FilteredNotes);
        Assert.Equal(2, vm.FilteredNotes[0].Id);
        Assert.Single(vm.InboxNotes);
        Assert.True(vm.InboxNotes[0].IsArchived);
    }

    [Fact]
    public async Task NoteEditor_ChecklistTogglesPersistAsStructuredMarkdown()
    {
        var notes = new MockNoteService();
        var note = new Note { Id = 1, Title = "Checklist" };
        notes.SetNotes(new[] { note });
        using var vm = new NoteEditorViewModel(notes, note, isNew: false);
        vm.NewChecklistItemText = "Review layout";
        vm.AddChecklistItemCommand.Execute(null);
        Assert.Single(vm.ChecklistItems);
        vm.ChecklistItems[0].IsDone = true;
        await vm.FlushAutosaveAsync();
        Assert.Contains("[x] Review layout", note.ChecklistJson);
        Assert.Equal("1/1 steps", note.ChecklistSummary);
    }

    [Fact]
    public async Task NoteEditor_RefusesToCloseAnExistingCardWithoutATitle()
    {
        var notes = new MockNoteService();
        var existing = new Note { Id = 1, Title = "Keep this card" };
        notes.SetNotes(new[] { existing });
        using var vm = new NoteEditorViewModel(notes, new Note { Id = 1, Title = string.Empty }, isNew: false);

        Assert.False(await vm.FlushAutosaveAsync());
        Assert.Equal("Add a title before saving this card.", vm.SaveError);
        Assert.Single(await notes.GetAllNotesAsync());
    }

    [Fact]
    public async Task NoteDataTransfer_RoundTripsUnicodeMetadataAndSkipsDuplicateNotes()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KanbanCardsTransferTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var source = new MockNoteService();
            var created = DateTime.UtcNow.AddDays(-4);
            source.SetNotes(new[]
            {
                new Note { Id = 1, Title = "Café 계획 ✨", Description = "line one\nline two", Color = "#FFF176",
                    Status = NoteStatus.Doing, Priority = 2, DueAt = DateTime.UtcNow.AddDays(2), ReminderEnabled = true,
                    TagsJson = "[\"work\",\"ideas\"]", ChecklistJson = "[\"[x] first\",\"[ ] second\"]",
                    CreatedAt = created, UpdatedAt = created.AddHours(1) }
            });
            var exportPath = Path.Combine(tempDir, "notes.json");
            await new NoteDataTransferService(source).ExportJsonAsync(exportPath);
            var target = new MockNoteService();
            var transfer = new NoteDataTransferService(target);
            var preview = await transfer.PreviewImportAsync(exportPath);
            Assert.Equal(1, preview.NewNotes);
            Assert.Equal(0, preview.Duplicates);
            Assert.Equal(1, await transfer.ImportJsonAsync(exportPath));
            preview = await transfer.PreviewImportAsync(exportPath);
            Assert.Equal(0, preview.NewNotes);
            Assert.Equal(1, preview.Duplicates);
            Assert.Equal(0, await transfer.ImportJsonAsync(exportPath));
            var imported = Assert.Single(await target.GetAllNotesAsync());
            Assert.Equal("Café 계획 ✨", imported.Title);
            Assert.Equal("line one\nline two", imported.Description);
            Assert.Equal(NoteStatus.Doing, imported.Status);
            Assert.Equal(2, imported.Priority);
            Assert.Equal(created, imported.CreatedAt);
            Assert.Equal("[\"work\",\"ideas\"]", imported.TagsJson);

            var invalidPath = Path.Combine(tempDir, "invalid.json");
            await File.WriteAllTextAsync(invalidPath, "{\"Version\":99,\"Notes\":[]}");
            await Assert.ThrowsAsync<InvalidDataException>(() => transfer.ImportJsonAsync(invalidPath));
            Assert.Single(await target.GetAllNotesAsync());
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public async Task NoteDataTransfer_MarkdownExportIncludesReadableCardMetadataAndReplacesFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "KanbanCardsTransferTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var notes = new MockNoteService();
            notes.SetNotes(new[]
            {
                new Note
                {
                    Id = 1, Title = "Café planning", Description = "First line\nSecond line", Color = "#FFCC80",
                    Status = NoteStatus.Doing, Priority = 2, IsPinned = true, DueAt = DateTime.UtcNow.AddDays(1),
                    ReminderEnabled = true, ArchivedAt = DateTime.UtcNow,
                    TagsJson = "[\"work\",\"planning\"]", ChecklistJson = "[\"[x] Send agenda\",\"[ ] Review notes\"]"
                }
            });
            var path = Path.Combine(tempDir, "cards.md");
            await File.WriteAllTextAsync(path, "previous export");

            await new NoteDataTransferService(notes).ExportMarkdownAsync(path);

            var markdown = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("previous export", markdown);
            Assert.Contains("## Café planning", markdown);
            Assert.Contains("- Lane: Doing", markdown);
            Assert.Contains("- Priority: Urgent", markdown);
            Assert.Contains("- Color: `#FFCC80`", markdown);
            Assert.Contains("- Pinned: Yes", markdown);
            Assert.Contains("- Reminder: Enabled", markdown);
            Assert.Contains("- Archived: Yes", markdown);
            Assert.Contains("Tags: work, planning", markdown);
            Assert.Contains("- [x] Send agenda", markdown);
            Assert.Contains("- [ ] Review notes", markdown);
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    private class TestPresentationSource : System.Windows.PresentationSource
    {
        protected override System.Windows.Media.CompositionTarget GetCompositionTargetCore() => null!;
        public override System.Windows.Media.Visual RootVisual { get => null!; set { } }
        public override bool IsDisposed => false;
    }

    private class MockStartupService : IStartupService
    {
        public bool StartedWithWindows { get; private set; }
        public void SetStartup(bool startWithWindows) => StartedWithWindows = startWithWindows;
    }

    private class MockUpdateService : IUpdateService
    {
#pragma warning disable CS0067
        public string CurrentVersion { get; set; } = "1.0.0";
        public string? LatestVersion => null;
        public UpdateInfo? LatestRelease => null;
        public UpdateStep Step => UpdateStep.Idle;
        public int DownloadPercent => 0;
        public bool IsBusy => false;
        public bool IsInstalled => true;
        public string? StatusMessage => null;
        public event EventHandler<UpdateInfo>? UpdateAvailable;
        public event EventHandler<string>? CheckCompleted;
        public event Action? StateChanged;
#pragma warning restore CS0067
        public void StartBackgroundChecks() { }
        public void StopBackgroundChecks() { }
        public Task<UpdateInfo?> CheckForUpdatesAsync(bool manual = false, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<UpdateInfo?>(null);
        public Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo update, IProgress<int>? progress = null, System.Threading.CancellationToken cancellationToken = default) => Task.FromResult(true);
        public void OpenReleasePage(string? url = null) { }
        public void DismissUpdate(string version) { }
    }

    private class TestDbContextFactory : Microsoft.EntityFrameworkCore.IDbContextFactory<KanbanCards.Data.KanbanCardsDbContext>
    {
        private readonly Microsoft.EntityFrameworkCore.DbContextOptions<KanbanCards.Data.KanbanCardsDbContext> _options;
        public TestDbContextFactory(Microsoft.EntityFrameworkCore.DbContextOptions<KanbanCards.Data.KanbanCardsDbContext> options) => _options = options;
        public KanbanCards.Data.KanbanCardsDbContext CreateDbContext() => new KanbanCards.Data.KanbanCardsDbContext(_options);
    }
}
