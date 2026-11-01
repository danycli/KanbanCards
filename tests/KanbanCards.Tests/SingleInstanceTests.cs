using System;
using System.Threading;
using KanbanCards.Services;
using Xunit;

namespace KanbanCards.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void SingleInstanceService_FirstInstance_AcquiresSuccessfully()
    {
        var mutexName = $"Local\\KanbanCards_TestMutex_{Guid.NewGuid()}";
        var eventName = $"Local\\KanbanCards_TestEvent_{Guid.NewGuid()}";

        using var service = new SingleInstanceService(mutexName, eventName);
        Assert.True(service.TryAcquire());
    }

    [Fact]
    public void SingleInstanceService_SecondInstance_FailsToAcquire()
    {
        var mutexName = $"Local\\KanbanCards_TestMutex_{Guid.NewGuid()}";
        var eventName = $"Local\\KanbanCards_TestEvent_{Guid.NewGuid()}";

        using var primary = new SingleInstanceService(mutexName, eventName);
        Assert.True(primary.TryAcquire());

        using var secondary = new SingleInstanceService(mutexName, eventName);
        Assert.False(secondary.TryAcquire());
    }

    [Fact]
    public void SingleInstanceService_SignalWakeup_NotifiesPrimaryInstance()
    {
        var mutexName = $"Local\\KanbanCards_TestMutex_{Guid.NewGuid()}";
        var eventName = $"Local\\KanbanCards_TestEvent_{Guid.NewGuid()}";

        using var primary = new SingleInstanceService(mutexName, eventName);
        Assert.True(primary.TryAcquire());

        bool wakeupTriggered = false;
        using var signalReceivedEvent = new ManualResetEventSlim(false);

        primary.RegisterWakeupHandler(() =>
        {
            wakeupTriggered = true;
            signalReceivedEvent.Set();
        });

        using var secondary = new SingleInstanceService(mutexName, eventName);
        Assert.False(secondary.TryAcquire());
        secondary.SignalExistingInstance();

        // Wait up to 3 seconds for kernel EventWaitHandle thread pool dispatch
        bool received = signalReceivedEvent.Wait(3000);
        Assert.True(received);
        Assert.True(wakeupTriggered);
    }

    [Fact]
    public void SingleInstanceService_DisposedInstance_ReleasesMutexForSubsequentAcquisition()
    {
        var mutexName = $"Local\\KanbanCards_TestMutex_{Guid.NewGuid()}";
        var eventName = $"Local\\KanbanCards_TestEvent_{Guid.NewGuid()}";

        var first = new SingleInstanceService(mutexName, eventName);
        Assert.True(first.TryAcquire());
        first.Dispose();

        using var second = new SingleInstanceService(mutexName, eventName);
        Assert.True(second.TryAcquire());
    }
}
