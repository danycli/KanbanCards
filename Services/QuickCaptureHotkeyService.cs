using KanbanCards.Models;
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace KanbanCards.Services;

/// <summary>Registers Ctrl+Alt+N while the tray app runs; registration failure leaves tray capture available.</summary>
public sealed class QuickCaptureHotkeyService : IDisposable
{
    private const int HotkeyId = 0x4B43;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkN = 0x4E;
    private readonly IEditorService _editor;
    private HwndSource? _source;
    private bool _registered;

    public QuickCaptureHotkeyService(IEditorService editor) => _editor = editor;
    public void Start()
    {
        if (_source != null) return;
        var parameters = new HwndSourceParameters("Kanban Cards quick capture") { Width = 0, Height = 0, ParentWindow = new IntPtr(-3) };
        _source = new HwndSource(parameters);
        _source.AddHook(WindowProc);
        _registered = RegisterHotKey(_source.Handle, HotkeyId, ModControl | ModAlt, VkN);
        if (!_registered) FullScreenMonitorService.LogAudit("[HOTKEY] Ctrl+Alt+N is already registered; tray capture is still available.");
    }
    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _editor.OpenEditor(new Note(), true);
            handled = true;
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (_registered && _source != null) UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
        _source?.RemoveHook(WindowProc);
        _source?.Dispose();
        _source = null;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
