using System;
using System.Threading.Tasks;
using System.Windows;
using KanbanCards.Models;
using KanbanCards.Services;
using KanbanCards.ViewModels;
using KanbanCards.Views;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KanbanCards.Tests;

public class MockNoteEditorWindow : INoteEditorWindow
{
    public NoteEditorViewModel ViewModel { get; }
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; } = 340;
    public double Height { get; } = 400;

    public int PopAnimationCallCount { get; private set; }
    public int GrabAnimationCallCount { get; private set; }
    public bool LastGrabState { get; private set; }
    public int ActivateCallCount { get; private set; }
    public int ShowCallCount { get; private set; }
    public int CloseCallCount { get; private set; }
    public bool IsClosed { get; private set; }

    public event EventHandler? Closed;

    public MockNoteEditorWindow(NoteEditorViewModel viewModel)
    {
        ViewModel = viewModel;
    }

    public void PlayPopAnimation()
    {
        PopAnimationCallCount++;
    }

    public void AnimateGrabState(bool isGrabbed)
    {
        GrabAnimationCallCount++;
        LastGrabState = isGrabbed;
    }

    public bool Activate()
    {
        ActivateCallCount++;
        return true;
    }

    public virtual void Show()
    {
        ShowCallCount++;
    }

    public void Close()
    {
        CloseCallCount++;
        IsClosed = true;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}

public class NoteReselectionTests
{
    private (EditorService service, ServiceProvider provider, Func<NoteEditorViewModel, MockNoteEditorWindow> factory, Func<int> getCreatedCount) CreateTestContext()
    {
        var services = new ServiceCollection();
        var mockNoteService = new MockNoteService();
        var mockSettingsService = new MockSettingsService();

        services.AddSingleton<INoteService>(mockNoteService);
        services.AddSingleton<ISettingsService>(mockSettingsService);

        var provider = services.BuildServiceProvider();
        int createdWindowCount = 0;

        MockNoteEditorWindow WindowFactory(NoteEditorViewModel vm)
        {
            createdWindowCount++;
            return new MockNoteEditorWindow(vm);
        }

        var editorService = new EditorService(provider, WindowFactory);
        return (editorService, provider, WindowFactory, () => createdWindowCount);
    }

    [Fact]
    public void OpenEditor_FirstTime_OpensWindowNormally()
    {
        var (editorService, _, _, getCreatedCount) = CreateTestContext();
        var note = new Note { Id = 1, Title = "Test Note", Description = "Test Desc" };

        editorService.OpenEditor(note, isNew: false);

        Assert.Equal(1, getCreatedCount());
        Assert.True(editorService.IsNoteOpen(1));
    }

    [Fact]
    public void OpenEditor_AlreadyOpenNote_TriggersPopAnimationAndNoDuplicateInstance()
    {
        var (editorService, _, _, getCreatedCount) = CreateTestContext();
        var note = new Note { Id = 42, Title = "Important Task", Description = "Details" };

        // 1. Open note initially
        editorService.OpenEditor(note, isNew: false);
        Assert.Equal(1, getCreatedCount());

        // 2. Click the same note again while open
        editorService.OpenEditor(note, isNew: false);

        // Verify: No duplicate window was created
        Assert.Equal(1, getCreatedCount());
        Assert.True(editorService.IsNoteOpen(42));
    }

    [Fact]
    public void OpenEditor_RepeatedClicks_RetriggersPopAnimationEveryTime()
    {
        MockNoteEditorWindow? capturedWindow = null;
        var services = new ServiceCollection();
        services.AddSingleton<INoteService>(new MockNoteService());
        services.AddSingleton<ISettingsService>(new MockSettingsService());
        var provider = services.BuildServiceProvider();

        int createdCount = 0;
        var editorService = new EditorService(provider, vm =>
        {
            createdCount++;
            capturedWindow = new MockNoteEditorWindow(vm);
            return capturedWindow;
        });

        var note = new Note { Id = 7, Title = "Meeting Notes" };

        // First click opens
        editorService.OpenEditor(note, isNew: false);
        Assert.NotNull(capturedWindow);
        Assert.Equal(0, capturedWindow.PopAnimationCallCount);
        Assert.Equal(1, createdCount);

        // 4 repeated clicks on already open note
        for (int i = 1; i <= 4; i++)
        {
            editorService.OpenEditor(note, isNew: false);
            Assert.Equal(i, capturedWindow.PopAnimationCallCount);
            Assert.Equal(1, createdCount); // Still only 1 window
        }
    }

    [Fact]
    public void OpenEditor_MultipleDifferentNotes_HandledIndependently()
    {
        MockNoteEditorWindow? windowA = null;
        MockNoteEditorWindow? windowB = null;

        var services = new ServiceCollection();
        services.AddSingleton<INoteService>(new MockNoteService());
        services.AddSingleton<ISettingsService>(new MockSettingsService());
        var provider = services.BuildServiceProvider();

        int createdCount = 0;
        var editorService = new EditorService(provider, vm =>
        {
            createdCount++;
            var win = new MockNoteEditorWindow(vm);
            if (vm.Note.Id == 101) windowA = win;
            if (vm.Note.Id == 102) windowB = win;
            return win;
        });

        var note1 = new Note { Id = 101, Title = "Note 1" };
        var note2 = new Note { Id = 102, Title = "Note 2" };

        editorService.OpenEditor(note1, isNew: false);
        editorService.OpenEditor(note2, isNew: false);

        Assert.Equal(2, createdCount);
        Assert.True(editorService.IsNoteOpen(101));
        Assert.True(editorService.IsNoteOpen(102));

        // Reselect note 1
        editorService.OpenEditor(note1, isNew: false);
        Assert.Equal(1, windowA!.PopAnimationCallCount);
        Assert.Equal(0, windowB!.PopAnimationCallCount);

        // Reselect note 2
        editorService.OpenEditor(note2, isNew: false);
        Assert.Equal(1, windowA.PopAnimationCallCount);
        Assert.Equal(1, windowB.PopAnimationCallCount);
    }

    [Fact]
    public void OpenEditor_ClosedNote_OpensFreshWindow()
    {
        MockNoteEditorWindow? capturedWindow = null;
        var services = new ServiceCollection();
        services.AddSingleton<INoteService>(new MockNoteService());
        services.AddSingleton<ISettingsService>(new MockSettingsService());
        var provider = services.BuildServiceProvider();

        int createdCount = 0;
        var editorService = new EditorService(provider, vm =>
        {
            createdCount++;
            capturedWindow = new MockNoteEditorWindow(vm);
            return capturedWindow;
        });

        var note = new Note { Id = 5, Title = "Shopping List" };

        // 1. Open
        editorService.OpenEditor(note, isNew: false);
        Assert.Equal(1, createdCount);
        Assert.True(editorService.IsNoteOpen(5));

        // 2. User closes the window
        capturedWindow!.Close();
        Assert.False(editorService.IsNoteOpen(5));

        // 3. Reopening opens a new window instance
        editorService.OpenEditor(note, isNew: false);
        Assert.Equal(2, createdCount);
        Assert.True(editorService.IsNoteOpen(5));
    }

    [Fact]
    public void OpenEditor_PreservesEditingStateOnReselection()
    {
        MockNoteEditorWindow? capturedWindow = null;
        var services = new ServiceCollection();
        services.AddSingleton<INoteService>(new MockNoteService());
        services.AddSingleton<ISettingsService>(new MockSettingsService());
        var provider = services.BuildServiceProvider();

        var editorService = new EditorService(provider, vm =>
        {
            capturedWindow = new MockNoteEditorWindow(vm);
            return capturedWindow;
        });

        var note = new Note { Id = 12, Title = "Original Title", Description = "Original Desc" };
        editorService.OpenEditor(note, isNew: false);

        // User typed edits in the window
        capturedWindow!.ViewModel.Title = "Edited In-Memory Title";
        capturedWindow.ViewModel.Description = "Edited In-Memory Desc";

        // User clicks note in the side stack again
        editorService.OpenEditor(note, isNew: false);

        // State remains untouched
        Assert.Equal("Edited In-Memory Title", capturedWindow.ViewModel.Title);
        Assert.Equal("Edited In-Memory Desc", capturedWindow.ViewModel.Description);
        Assert.Equal(1, capturedWindow.PopAnimationCallCount);
    }

    [Fact]
    public void OpenEditor_NewNoteReselection_BringsExistingNewNoteToFocus()
    {
        MockNoteEditorWindow? capturedWindow = null;
        var services = new ServiceCollection();
        services.AddSingleton<INoteService>(new MockNoteService());
        services.AddSingleton<ISettingsService>(new MockSettingsService());
        var provider = services.BuildServiceProvider();

        int createdCount = 0;
        var editorService = new EditorService(provider, vm =>
        {
            createdCount++;
            capturedWindow = new MockNoteEditorWindow(vm);
            return capturedWindow;
        });

        // Click Add Note
        editorService.OpenEditor(new Note(), isNew: true);
        Assert.Equal(1, createdCount);
        Assert.Equal(0, capturedWindow!.PopAnimationCallCount);

        // Click Add Note again while first new note is still open
        editorService.OpenEditor(new Note(), isNew: true);
        Assert.Equal(1, createdCount); // No duplicate empty window
        Assert.Equal(1, capturedWindow.PopAnimationCallCount);
    }

    [Fact]
    public void OpenEditor_WindowPresentationFailure_CleansUpWindowReferenceImmediately()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INoteService>(new MockNoteService());
        services.AddSingleton<ISettingsService>(new MockSettingsService());
        var provider = services.BuildServiceProvider();

        var editorService = new EditorService(provider, vm =>
        {
            var win = new FailingMockNoteEditorWindow(vm);
            return win;
        });

        // Presentation throws inside Show()
        editorService.OpenEditor(new Note { Id = 42, Title = "Failed Note" }, isNew: false);

        // Window reference must not be retained in _openWindows
        Assert.False(editorService.IsNoteOpen(42));
        Assert.Equal(0, editorService.OpenWindowsCount);
    }

    [Fact]
    public void EditorService_GetTargetWorkArea_ReturnsValidScreenArea()
    {
        var targetArea = EditorService.GetTargetWorkArea();
        Assert.True(targetArea.Width > 0);
        Assert.True(targetArea.Height > 0);
    }

    [Fact]
    public void OpenEditor_PositionsWindowRelative_ToTargetWorkArea()
    {
        MockNoteEditorWindow? capturedWindow = null;
        var settingsService = new MockSettingsService();
        var services = new ServiceCollection();
        services.AddSingleton<INoteService>(new MockNoteService());
        services.AddSingleton<ISettingsService>(settingsService);
        var provider = services.BuildServiceProvider();

        var editorService = new EditorService(provider, vm =>
        {
            capturedWindow = new MockNoteEditorWindow(vm);
            return capturedWindow;
        });

        var workArea = EditorService.GetTargetWorkArea();

        // 1. Test Right StackPosition (default)
        settingsService.Settings.StackPosition = "Right";
        editorService.OpenEditor(new Note { Id = 101, Title = "Right Dock Note" }, isNew: false);
        Assert.NotNull(capturedWindow);
        var expectedLeftRight = Math.Max(workArea.Left, workArea.Left + workArea.Width - 320 - capturedWindow.Width - 20);
        var expectedTop = Math.Max(workArea.Top, workArea.Top + (workArea.Height - capturedWindow.Height) / 2.0);
        Assert.Equal(expectedLeftRight, capturedWindow.Left);
        Assert.Equal(expectedTop, capturedWindow.Top);

        // 2. Test Left StackPosition
        capturedWindow = null;
        settingsService.Settings.StackPosition = "Left";
        editorService.OpenEditor(new Note { Id = 102, Title = "Left Dock Note" }, isNew: false);
        Assert.NotNull(capturedWindow);
        var expectedLeftLeft = workArea.Left + 330;
        Assert.Equal(expectedLeftLeft, capturedWindow.Left);
        Assert.Equal(expectedTop, capturedWindow.Top);
    }

    [Fact]
    public void NoteEditorWindow_InitializesAndHandlesLoadedEventWithoutCrashing()
    {
        var thread = new System.Threading.Thread(() =>
        {
            var noteService = new MockNoteService();
            var vm = new NoteEditorViewModel(noteService, new Note { Title = "Initial Focus" }, isNew: false);
            var window = new NoteEditorWindow(vm, initializeComponent: false);
            
            // Raise Loaded event to verify safe focus handling
            var loadedArgs = new RoutedEventArgs(System.Windows.FrameworkElement.LoadedEvent);
            window.RaiseEvent(loadedArgs);

            Assert.Equal("Initial Focus", window.ViewModel.Title);
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private class FailingMockNoteEditorWindow : MockNoteEditorWindow
    {
        public FailingMockNoteEditorWindow(NoteEditorViewModel viewModel) : base(viewModel) { }

        public override void Show()
        {
            throw new InvalidOperationException("Simulated monitor disconnected during Show()");
        }
    }
}
