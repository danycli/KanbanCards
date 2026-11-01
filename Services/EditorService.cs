using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using KanbanCards.Models;
using KanbanCards.ViewModels;
using KanbanCards.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KanbanCards.Services;

public interface IEditorService
{
    void OpenEditor(Note note, bool isNew);
    bool IsNoteOpen(int noteId);
}

public class EditorService : IEditorService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly List<INoteEditorWindow> _openWindows = new();
    private readonly Func<NoteEditorViewModel, INoteEditorWindow>? _windowFactory;

    public EditorService(IServiceProvider serviceProvider) : this(serviceProvider, null)
    {
    }

    public EditorService(IServiceProvider serviceProvider, Func<NoteEditorViewModel, INoteEditorWindow>? windowFactory)
    {
        _serviceProvider = serviceProvider;
        _windowFactory = windowFactory;
    }

    internal int OpenWindowsCount => _openWindows.Count;

    private void PurgeClosedWindows()
    {
        _openWindows.RemoveAll(w =>
        {
            if (w is Window win)
            {
                return !win.IsLoaded && win.Visibility != Visibility.Visible;
            }
            return false;
        });
    }

    public bool IsNoteOpen(int noteId)
    {
        if (noteId <= 0) return false;
        PurgeClosedWindows();
        return _openWindows.Any(w => !w.ViewModel.IsNew && w.ViewModel.Note.Id == noteId);
    }

    public void OpenEditor(Note note, bool isNew)
    {
        if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(() => OpenEditor(note, isNew));
            return;
        }

        PurgeClosedWindows();

        // 1. Detect if the note is already open
        INoteEditorWindow? existingWindow = null;

        if (!isNew && note.Id > 0)
        {
            existingWindow = _openWindows.FirstOrDefault(w => !w.ViewModel.IsNew && w.ViewModel.Note.Id == note.Id);
        }
        else if (isNew)
        {
            // If already creating a new note and an unsaved editor is open, bring it to focus
            existingWindow = _openWindows.FirstOrDefault(w => w.ViewModel.IsNew);
        }

        // 2. If already open, trigger feedback animation without duplicating or modifying state
        if (existingWindow != null)
        {
            existingWindow.PlayPopAnimation();
            return;
        }

        // 3. Open a new editor instance
        var noteService = _serviceProvider.GetRequiredService<INoteService>();
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        var viewModel = new NoteEditorViewModel(noteService, note, isNew);
        var window = _windowFactory != null ? _windowFactory(viewModel) : new NoteEditorWindow(viewModel);

        var settings = settingsService.LoadSettings();
        var workArea = GetTargetWorkArea();

        // Position adjacent to the edge stack without obstructing it
        if (settings.StackPosition == "Left")
        {
            window.Left = workArea.Left + 330;
        }
        else
        {
            window.Left = Math.Max(workArea.Left, workArea.Left + workArea.Width - 320 - window.Width - 20);
        }

        // Center vertically on screen work area
        window.Top = Math.Max(workArea.Top, workArea.Top + (workArea.Height - window.Height) / 2.0);

        EventHandler? closedHandler = null;
        closedHandler = (s, e) =>
        {
            window.Closed -= closedHandler;
            _openWindows.Remove(window);
        };
        window.Closed += closedHandler;
        _openWindows.Add(window);

        try
        {
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[EDITOR ERROR] Failed to show note editor: {ex.Message}");
            window.Closed -= closedHandler;
            _openWindows.Remove(window);
            try { window.Close(); } catch { }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    internal static Rect GetTargetWorkArea()
    {
        try
        {
            if (GetCursorPos(out POINT pt))
            {
                IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero)
                {
                    MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        double scaleX = 1.0;
                        double scaleY = 1.0;
                        try
                        {
                            if (GetDpiForMonitor(hMonitor, 0, out uint x, out uint y) == 0 && x > 0 && y > 0)
                            {
                                scaleX = x / 96.0;
                                scaleY = y / 96.0;
                            }
                        }
                        catch
                        {
                            // shcore or GetDpiForMonitor unavailable fallback
                        }

                        double width = (mi.rcWork.Right - mi.rcWork.Left) / scaleX;
                        double height = (mi.rcWork.Bottom - mi.rcWork.Top) / scaleY;

                        if (width > 0 && height > 0)
                        {
                            return new Rect(
                                mi.rcWork.Left / scaleX,
                                mi.rcWork.Top / scaleY,
                                width,
                                height);
                        }
                    }
                }
            }
        }
        catch
        {
            // Graceful fallback
        }

        return SystemParameters.WorkArea;
    }
}
