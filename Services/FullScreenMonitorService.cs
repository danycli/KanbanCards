using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace KanbanCards.Services;

/// <summary>
/// Service responsible for monitoring full-screen applications and notifying subscribers
/// when full-screen state transitions occur.
/// </summary>
public interface IFullScreenMonitorService : IDisposable
{
    event EventHandler<bool>? FullScreenChanged;
    bool IsFullScreenActive { get; }
    void Start();
    void Stop();
}

public class FullScreenMonitorService : IFullScreenMonitorService
{
    private readonly System.Timers.Timer _pollTimer;
    private bool _isFullScreenActive;
    private bool _isRunning;
    private IntPtr _winEventHook = IntPtr.Zero;
    private WinEventDelegate? _winEventProc;
    private readonly object _lock = new();
    private int _isChecking = 0;
    private static readonly object _logLock = new();

    public static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KanbanCards", "fullscreen_audit.log");

    public event EventHandler<bool>? FullScreenChanged;

    public bool IsFullScreenActive
    {
        get
        {
            lock (_lock)
            {
                return _isFullScreenActive;
            }
        }
        private set
        {
            bool changed = false;
            lock (_lock)
            {
                if (_isFullScreenActive != value)
                {
                    _isFullScreenActive = value;
                    changed = true;
                }
            }
            if (changed)
            {
                LogAudit($"[STATE TRANSITION] IsFullScreenActive = {value}");
                FullScreenChanged?.Invoke(this, value);
            }
        }
    }

    public double PollInterval => _pollTimer.Interval;

    public FullScreenMonitorService(double pollIntervalMs = 2500)
    {
        _pollTimer = new System.Timers.Timer(pollIntervalMs);
        _pollTimer.AutoReset = true;
        _pollTimer.Elapsed += (s, e) => CheckAndUpdateState();
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
        }

        LogAudit("[SERVICE START] FullScreenMonitorService starting...");

        // Initial check
        CheckAndUpdateState();

        // Start periodic verification timer
        _pollTimer.Start();

        // Register Windows event hook for instantaneous detection on active window switch
        try
        {
            _winEventProc = new WinEventDelegate(WinEventCallback);
            const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
            const uint WINEVENT_OUTOFCONTEXT = 0x0000;
            const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

            _winEventHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

            LogAudit($"[HOOK REGISTERED] SetWinEventHook = 0x{_winEventHook.ToInt64():X}");
        }
        catch (Exception ex)
        {
            LogAudit($"[HOOK ERROR] SetWinEventHook failed: {ex.Message}");
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;
        }

        LogAudit("[SERVICE STOP] FullScreenMonitorService stopping...");
        _pollTimer.Stop();

        if (_winEventHook != IntPtr.Zero)
        {
            try
            {
                UnhookWinEvent(_winEventHook);
                LogAudit("[HOOK UNHOOKED] WinEventHook successfully unhooked.");
            }
            catch (Exception ex)
            {
                LogAudit($"[HOOK ERROR] UnhookWinEvent failed: {ex.Message}");
            }
            _winEventHook = IntPtr.Zero;
        }
        _winEventProc = null;
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        CheckAndUpdateState();
    }

    public void CheckAndUpdateState()
    {
        if (System.Threading.Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
        {
            return;
        }

        try
        {
            bool? isFs = CheckIsFullScreen();
            if (isFs.HasValue)
            {
                IsFullScreenActive = isFs.Value;
            }
        }
        catch (Exception ex)
        {
            LogAudit($"[EXCEPTION] CheckAndUpdateState failed: {ex}");
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _isChecking, 0);
        }
    }

    public const uint MONITORINFOF_PRIMARY = 0x00000001;

    public bool PrimaryMonitorOnly { get; set; } = true;

    public static bool EvaluateFullScreen(
        RECT rect,
        RECT dwmRect,
        MONITORINFO mi,
        int windowStyle,
        bool hasMaxPlacement,
        bool isD3dOrPresentation = false,
        bool primaryMonitorOnly = true)
    {
        bool isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
        if (primaryMonitorOnly && !isPrimary)
        {
            // The foreground window is on a secondary monitor; do not consider primary monitor obscured.
            return false;
        }

        if (isD3dOrPresentation)
        {
            return true;
        }

        const int WS_MAXIMIZE = 0x01000000;
        const int WS_CAPTION = 0x00C00000;

        bool hasMaxStyle = (windowStyle & WS_MAXIMIZE) != 0;
        bool hasCaption = (windowStyle & WS_CAPTION) == WS_CAPTION;

        // An ordinary maximized window retains its title bar / caption (WS_CAPTION) and is maximized
        bool isOrdinaryMaximized = (hasMaxPlacement || hasMaxStyle) && hasCaption;
        if (isOrdinaryMaximized)
        {
            return false;
        }

        // Check if window bounds cover the physical monitor (with 2px tolerance for multi-monitor / DPI)
        bool coversMonitor = (rect.Left <= mi.rcMonitor.Left + 2 &&
                              rect.Top <= mi.rcMonitor.Top + 2 &&
                              rect.Right >= mi.rcMonitor.Right - 2 &&
                              rect.Bottom >= mi.rcMonitor.Bottom - 2)
                             ||
                             (dwmRect.Left <= mi.rcMonitor.Left + 2 &&
                              dwmRect.Top <= mi.rcMonitor.Top + 2 &&
                              dwmRect.Right >= mi.rcMonitor.Right - 2 &&
                              dwmRect.Bottom >= mi.rcMonitor.Bottom - 2);

        return coversMonitor;
    }

    public bool? CheckIsFullScreen()
    {
        // 1. Direct3D exclusive full-screen / presentation mode check via Shell32
        bool isD3dOrPresentation = false;
        try
        {
            if (SHQueryUserNotificationState(out var state) == 0) // S_OK
            {
                if (state == QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN ||
                    state == QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE)
                {
                    isD3dOrPresentation = true;
                }
            }
        }
        catch (Exception ex)
        {
            LogAudit($"[EXCEPTION] SHQueryUserNotificationState failed: {ex.Message}");
        }

        // 2. Retrieve active foreground window
        IntPtr hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero)
        {
            // Transition in progress - maintain current state if no D3D mode
            return isD3dOrPresentation ? true : null;
        }

        if (hWnd == GetDesktopWindow() || hWnd == GetShellWindow())
        {
            return false;
        }

        // Ignore KanbanCards' own windows (maintain current state)
        uint processId = 0;
        try
        {
            GetWindowThreadProcessId(hWnd, out processId);
            if (processId == Environment.ProcessId)
            {
                return _isFullScreenActive;
            }
        }
        catch { }

        // Exclude desktop shell classes (desktop wallpaper surface, taskbar, secondary taskbar)
        var sbClass = new StringBuilder(256);
        string className = "";
        if (GetClassName(hWnd, sbClass, 256) > 0)
        {
            className = sbClass.ToString();
            if (className == "Progman" ||
                className == "WorkerW" ||
                className == "Shell_TrayWnd" ||
                className == "Shell_SecondaryTrayWnd")
            {
                return false;
            }
        }

        // Exclude minimized or hidden windows
        if (IsIconic(hWnd) || !IsWindowVisible(hWnd))
        {
            return false;
        }

        // 3. Obtain monitor associated with the foreground window
        const uint MONITOR_DEFAULTTONEAREST = 2;
        IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
        if (hMonitor == IntPtr.Zero)
        {
            return isD3dOrPresentation;
        }

        // 4. Retrieve the monitor's physical bounds and work area
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref mi))
        {
            return isD3dOrPresentation;
        }

        // 5. Obtain window bounds
        if (!GetWindowRect(hWnd, out RECT rect))
        {
            return isD3dOrPresentation;
        }

        RECT dwmRect;
        bool hasDwm = DwmGetWindowAttribute(hWnd, 9, out dwmRect, Marshal.SizeOf<RECT>()) == 0;
        if (!hasDwm)
        {
            dwmRect = rect;
        }

        // 6. Inspect Window Placement and Styles
        const int SW_SHOWMAXIMIZED = 3;
        const int GWL_STYLE = -16;

        WINDOWPLACEMENT wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        bool hasMaxPlacement = GetWindowPlacement(hWnd, ref wp) && wp.showCmd == SW_SHOWMAXIMIZED;

        int style = GetWindowLong(hWnd, GWL_STYLE);

        bool isFullScreen = EvaluateFullScreen(rect, dwmRect, mi, style, hasMaxPlacement, isD3dOrPresentation, PrimaryMonitorOnly);

        if (isFullScreen && !_isFullScreenActive)
        {
            var sbTitle = new StringBuilder(256);
            GetWindowText(hWnd, sbTitle, 256);
            string title = sbTitle.ToString();

            string procName = "Unknown";
            try
            {
                if (processId > 0)
                {
                    using var proc = Process.GetProcessById((int)processId);
                    procName = proc.ProcessName;
                }
            }
            catch { }

            LogAudit($"[FULLSCREEN DETECTED] Proc: '{procName}' | Title: '{title}' | Rect: [{rect.Left},{rect.Top},{rect.Right},{rect.Bottom}] | Mon: [{mi.rcMonitor.Left},{mi.rcMonitor.Top},{mi.rcMonitor.Right},{mi.rcMonitor.Bottom}] | Style: 0x{style:X8}");
        }

        return isFullScreen;
    }

    public static void LogAudit(string message)
    {
        try
        {
            lock (_logLock)
            {
                string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);

                // Rotate log if it exceeds 1MB to prevent disk exhaustion
                var fileInfo = new FileInfo(LogFilePath);
                if (fileInfo.Exists && fileInfo.Length > 1024 * 1024)
                {
                    string oldPath = LogFilePath + ".old";
                    if (File.Exists(oldPath)) File.Delete(oldPath);
                    File.Move(LogFilePath, oldPath);
                }

                File.AppendAllText(LogFilePath, line + Environment.NewLine);
                Debug.WriteLine(line);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        _pollTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Win32 Interop

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    private static int GetWindowLong(IntPtr hWnd, int nIndex)
    {
        if (IntPtr.Size == 8)
            return (int)GetWindowLongPtr64(hWnd, nIndex).ToInt64();
        else
            return GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    public enum QUERY_USER_NOTIFICATION_STATE
    {
        QUNS_NOT_PRESENT = 1,
        QUNS_BUSY = 2,
        QUNS_RUNNING_D3D_FULL_SCREEN = 3,
        QUNS_PRESENTATION_MODE = 4,
        QUNS_ACCEPTS_NOTIFICATIONS = 5,
        QUNS_QUIET_TIME = 6,
        QUNS_APP = 7
    }

    #endregion
}
