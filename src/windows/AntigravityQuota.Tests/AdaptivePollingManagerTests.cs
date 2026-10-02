using System;
using System.Threading;
using System.Threading.Tasks;
using AntigravityQuota.Core;
using AntigravityQuota.Core.Models;
using Xunit;

namespace AntigravityQuota.Tests;

public class AdaptivePollingManagerTests
{
    [Fact]
    public void DefaultIntervals_MatchSpecification()
    {
        var manager = new AdaptivePollingManager();

        Assert.Equal(TimeSpan.FromSeconds(20), manager.ActiveInterval);
        Assert.Equal(TimeSpan.FromSeconds(60), manager.IdleInterval);
        Assert.Equal(TimeSpan.FromSeconds(120), manager.OfflineInterval);
    }

    [Theory]
    [InlineData(AdaptivePollState.Active, 20)]
    [InlineData(AdaptivePollState.Idle, 60)]
    [InlineData(AdaptivePollState.Offline, 120)]
    public void IntervalForState_ReturnsCorrectTimeSpan(AdaptivePollState state, int expectedSeconds)
    {
        var manager = new AdaptivePollingManager();
        manager.SetState(state);

        Assert.Equal(state, manager.CurrentState);
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), manager.CurrentInterval);
    }

    [Fact]
    public void DetermineState_WithVariousApplicationStates_ComputesExpectedState()
    {
        // Antigravity running & in foreground -> Active (20s)
        var stateActive = AdaptivePollingManager.DetermineState(
            isAntigravityRunning: true,
            isAntigravityForeground: true);
        Assert.Equal(AdaptivePollState.Active, stateActive);

        // Antigravity running but in background / idle -> Idle (60s)
        var stateIdle = AdaptivePollingManager.DetermineState(
            isAntigravityRunning: true,
            isAntigravityForeground: false);
        Assert.Equal(AdaptivePollState.Idle, stateIdle);

        // Antigravity not running -> Offline (120s)
        var stateOffline = AdaptivePollingManager.DetermineState(
            isAntigravityRunning: false,
            isAntigravityForeground: false);
        Assert.Equal(AdaptivePollState.Offline, stateOffline);
    }

    [Fact]
    public void UpdateState_ChangesIntervalAndFiresEvent()
    {
        var manager = new AdaptivePollingManager(AdaptivePollState.Offline);
        bool eventFired = false;
        AdaptivePollState reportedState = AdaptivePollState.Offline;
        TimeSpan reportedInterval = TimeSpan.Zero;

        manager.StateChanged += (newState, newInterval) =>
        {
            eventFired = true;
            reportedState = newState;
            reportedInterval = newInterval;
        };

        bool changed = manager.UpdateState(isAntigravityRunning: true, isAntigravityForeground: true);

        Assert.True(changed);
        Assert.True(eventFired);
        Assert.Equal(AdaptivePollState.Active, manager.CurrentState);
        Assert.Equal(AdaptivePollState.Active, reportedState);
        Assert.Equal(TimeSpan.FromSeconds(20), manager.CurrentInterval);
        Assert.Equal(TimeSpan.FromSeconds(20), reportedInterval);
    }

    [Fact]
    public void UpdateState_WhenStateUnchanged_ReturnsFalseAndDoesNotFireEvent()
    {
        var manager = new AdaptivePollingManager(AdaptivePollState.Active);
        int eventCount = 0;
        manager.StateChanged += (_, _) => eventCount++;

        bool changed = manager.UpdateState(isAntigravityRunning: true, isAntigravityForeground: true);

        Assert.False(changed);
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public void RecordActivity_ResetsInactivityAndSetsStateToActive()
    {
        var manager = new AdaptivePollingManager(AdaptivePollState.Idle);
        var baseTime = new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero);

        manager.RecordActivity(baseTime);

        Assert.Equal(AdaptivePollState.Active, manager.CurrentState);
        Assert.Equal(TimeSpan.FromSeconds(20), manager.CurrentInterval);
        Assert.Equal(baseTime, manager.LastActivityTime);
    }

    [Fact]
    public void CheckInactivityTimeout_TransitionsActiveToIdle_WhenThresholdExceeded()
    {
        var baseTime = new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero);
        var manager = new AdaptivePollingManager(AdaptivePollState.Active)
        {
            IdleInactivityThreshold = TimeSpan.FromMinutes(2)
        };
        manager.RecordActivity(baseTime);

        // 1 minute later: still active
        bool transitioned1 = manager.CheckInactivity(baseTime.AddMinutes(1));
        Assert.False(transitioned1);
        Assert.Equal(AdaptivePollState.Active, manager.CurrentState);

        // 2 minutes and 1 second later: transitions to Idle (60s)
        bool transitioned2 = manager.CheckInactivity(baseTime.AddSeconds(121));
        Assert.True(transitioned2);
        Assert.Equal(AdaptivePollState.Idle, manager.CurrentState);
        Assert.Equal(TimeSpan.FromSeconds(60), manager.CurrentInterval);
    }

    [Fact]
    public async Task PollingLoop_InvokesCallbackAndHandlesCancellation()
    {
        int pollCount = 0;
        var manager = new AdaptivePollingManager(AdaptivePollState.Active)
        {
            ActiveInterval = TimeSpan.FromMilliseconds(50),
            IdleInterval = TimeSpan.FromMilliseconds(100)
        };

        using var cts = new CancellationTokenSource();

        var pollTask = manager.RunAsync(async ct =>
        {
            Interlocked.Increment(ref pollCount);
            if (pollCount >= 3)
            {
                cts.Cancel();
            }
            await Task.CompletedTask;
        }, cts.Token);

        await Task.WhenAny(pollTask, Task.Delay(2000));

        Assert.True(pollCount >= 3);
    }

    [Fact]
    public async Task TriggerImmediatePollAsync_ExecutesCallbackImmediately()
    {
        bool polled = false;
        var manager = new AdaptivePollingManager();

        await manager.TriggerImmediatePollAsync(async ct =>
        {
            polled = true;
            await Task.CompletedTask;
        });

        Assert.True(polled);
    }
}
