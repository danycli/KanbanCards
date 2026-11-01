using System;
using System.Threading;

namespace KanbanCards.Services;

public interface ISingleInstanceService : IDisposable
{
    bool TryAcquire();
    void SignalExistingInstance();
    void RegisterWakeupHandler(Action onWakeup);
}

public class SingleInstanceService : ISingleInstanceService
{
    public const string DefaultMutexName = @"Local\KanbanCards_SingleInstance_Mutex";
    public const string DefaultEventName = @"Local\KanbanCards_ShowEvent";

    private readonly string _mutexName;
    private readonly string _eventName;
    private Mutex? _mutex;
    private EventWaitHandle? _eventWaitHandle;
    private RegisteredWaitHandle? _registeredWaitHandle;
    private bool _hasOwnership;
    private bool _isDisposed;

    public SingleInstanceService(string? mutexName = null, string? eventName = null)
    {
        _mutexName = !string.IsNullOrWhiteSpace(mutexName) ? mutexName : DefaultMutexName;
        _eventName = !string.IsNullOrWhiteSpace(eventName) ? eventName : DefaultEventName;
    }

    public bool TryAcquire()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SingleInstanceService));

        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            _hasOwnership = createdNew;
            return createdNew;
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[SINGLE INSTANCE ERROR] Failed to create mutex '{_mutexName}': {ex.Message}");
            return false;
        }
    }

    public void SignalExistingInstance()
    {
        try
        {
            using var showHandle = EventWaitHandle.OpenExisting(_eventName);
            showHandle.Set();
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[SINGLE INSTANCE] Could not signal existing instance handle '{_eventName}': {ex.Message}");
        }
    }

    public void RegisterWakeupHandler(Action onWakeup)
    {
        if (_isDisposed || !_hasOwnership) return;

        try
        {
            _eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName);
            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _eventWaitHandle,
                (state, timedOut) =>
                {
                    if (!timedOut)
                    {
                        onWakeup();
                    }
                },
                null,
                -1,
                false);
        }
        catch (Exception ex)
        {
            FullScreenMonitorService.LogAudit($"[SINGLE INSTANCE ERROR] Failed to register wakeup handler for '{_eventName}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _registeredWaitHandle?.Unregister(null);
        _registeredWaitHandle = null;

        _eventWaitHandle?.Dispose();
        _eventWaitHandle = null;

        if (_mutex != null)
        {
            if (_hasOwnership)
            {
                try { _mutex.ReleaseMutex(); } catch { }
            }
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
