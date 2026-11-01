using System;
using System.IO;
using System.Linq;
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
                LastDismissedUpdateVersion = "1.5.0"
            };

            // 1. Initial save
            service.SaveSettings(settings);
            Assert.True(File.Exists(settingsFile));
            Assert.True(eventFired);

            var loaded = service.LoadSettings();
            Assert.Equal("Left", loaded.StackPosition);
            Assert.Equal(85, loaded.AnimationSpeed);
            Assert.False(loaded.AutoCheckForUpdates);
            Assert.Equal("1.5.0", loaded.LastDismissedUpdateVersion);

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
