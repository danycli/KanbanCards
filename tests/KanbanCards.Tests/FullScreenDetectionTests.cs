using System;
using KanbanCards.Services;
using Xunit;

namespace KanbanCards.Tests;

public class MockFullScreenMonitorService : IFullScreenMonitorService
{
    private bool _isFullScreenActive;
    public event EventHandler<bool>? FullScreenChanged;

    public bool IsFullScreenActive
    {
        get => _isFullScreenActive;
        set
        {
            if (_isFullScreenActive != value)
            {
                _isFullScreenActive = value;
                FullScreenChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;
    public void Stop() => IsRunning = false;
    public void Dispose() => Stop();
}

public class FullScreenMonitorTests
{
    [Fact]
    public void FullScreenMonitorService_DefaultInterval_IsEnergyEfficientHeartbeat()
    {
        using var service = new FullScreenMonitorService();
        // Default interval must be >= 2000ms to conserve CPU and battery instead of aggressive 200ms polling
        Assert.Equal(2500, service.PollInterval);
    }

    [Fact]
    public void FullScreenMonitorService_StartAndStop_LifecycleIsManaged()
    {
        using var service = new FullScreenMonitorService(pollIntervalMs: 100);
        Assert.False(service.IsFullScreenActive);

        service.Start();
        // Verify service runs without throwing
        service.Stop();
    }

    [Fact]
    public void MockFullScreenMonitorService_TogglingFullScreen_FiresEventProperly()
    {
        var service = new MockFullScreenMonitorService();
        int eventCount = 0;
        bool lastState = false;

        service.FullScreenChanged += (sender, isFs) =>
        {
            eventCount++;
            lastState = isFs;
        };

        // Transition: Normal -> Full-screen (overlay hides)
        service.IsFullScreenActive = true;
        Assert.Equal(1, eventCount);
        Assert.True(lastState);

        // Setting same value again must not duplicate event triggers
        service.IsFullScreenActive = true;
        Assert.Equal(1, eventCount);

        // Transition: Full-screen -> Normal (overlay restores)
        service.IsFullScreenActive = false;
        Assert.Equal(2, eventCount);
        Assert.False(lastState);
    }

    [Fact]
    public void EvaluateFullScreen_PrimaryMonitor_FullscreenApp_ReturnsTrue()
    {
        var rect = new FullScreenMonitorService.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };
        var mi = new FullScreenMonitorService.MONITORINFO
        {
            rcMonitor = new FullScreenMonitorService.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            dwFlags = FullScreenMonitorService.MONITORINFOF_PRIMARY
        };

        // Frameless fullscreen window without WS_CAPTION
        int style = 0;
        bool isFs = FullScreenMonitorService.EvaluateFullScreen(rect, rect, mi, style, hasMaxPlacement: false, isD3dOrPresentation: false, primaryMonitorOnly: true);

        Assert.True(isFs);
    }

    [Fact]
    public void EvaluateFullScreen_SecondaryMonitor_DoesNotHideEdgeStackWhenPrimaryOnly()
    {
        // Monitor 2 coordinates: 1920 to 3840
        var rect = new FullScreenMonitorService.RECT { Left = 1920, Top = 0, Right = 3840, Bottom = 1080 };
        var mi = new FullScreenMonitorService.MONITORINFO
        {
            rcMonitor = new FullScreenMonitorService.RECT { Left = 1920, Top = 0, Right = 3840, Bottom = 1080 },
            dwFlags = 0 // NOT primary
        };

        int style = 0;
        // When primaryMonitorOnly is true, fullscreen on secondary display must NOT trigger full-screen hide
        bool isFs = FullScreenMonitorService.EvaluateFullScreen(rect, rect, mi, style, hasMaxPlacement: false, isD3dOrPresentation: false, primaryMonitorOnly: true);

        Assert.False(isFs);

        // When primaryMonitorOnly is false, secondary fullscreen is reported
        bool isFsAll = FullScreenMonitorService.EvaluateFullScreen(rect, rect, mi, style, hasMaxPlacement: false, isD3dOrPresentation: false, primaryMonitorOnly: false);
        Assert.True(isFsAll);
    }

    [Fact]
    public void EvaluateFullScreen_OrdinaryMaximizedWindowWithCaption_ReturnsFalse()
    {
        var rect = new FullScreenMonitorService.RECT { Left = -8, Top = -8, Right = 1928, Bottom = 1088 };
        var mi = new FullScreenMonitorService.MONITORINFO
        {
            rcMonitor = new FullScreenMonitorService.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            dwFlags = FullScreenMonitorService.MONITORINFOF_PRIMARY
        };

        const int WS_MAXIMIZE = 0x01000000;
        const int WS_CAPTION = 0x00C00000;
        int style = WS_MAXIMIZE | WS_CAPTION;

        bool isFs = FullScreenMonitorService.EvaluateFullScreen(rect, rect, mi, style, hasMaxPlacement: true, isD3dOrPresentation: false, primaryMonitorOnly: true);

        Assert.False(isFs);
    }
}
