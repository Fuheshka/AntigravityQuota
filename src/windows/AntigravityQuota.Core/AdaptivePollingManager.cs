using System;
using System.Threading;
using System.Threading.Tasks;

namespace AntigravityQuota.Core;

/// <summary>
/// Operating state of adaptive polling.
/// </summary>
public enum AdaptivePollState
{
    Active,
    Idle,
    Offline
}

/// <summary>
/// Manages polling intervals adaptively:
/// 20s during active coding / foreground,
/// 60s during idle / background,
/// 120s when offline / Antigravity is closed.
/// </summary>
public class AdaptivePollingManager
{
    public TimeSpan ActiveInterval { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan IdleInterval { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan OfflineInterval { get; set; } = TimeSpan.FromSeconds(120);
    public TimeSpan IdleInactivityThreshold { get; set; } = TimeSpan.FromMinutes(2);

    public AdaptivePollState CurrentState { get; private set; }
    public TimeSpan CurrentInterval => IntervalForState(CurrentState);
    public DateTimeOffset LastActivityTime { get; private set; }

    public event Action<AdaptivePollState, TimeSpan>? StateChanged;

    public AdaptivePollingManager(AdaptivePollState initialState = AdaptivePollState.Active)
    {
        CurrentState = initialState;
        LastActivityTime = DateTimeOffset.UtcNow;
    }

    public TimeSpan IntervalForState(AdaptivePollState state) => state switch
    {
        AdaptivePollState.Active => ActiveInterval,
        AdaptivePollState.Idle => IdleInterval,
        AdaptivePollState.Offline => OfflineInterval,
        _ => IdleInterval
    };

    public static AdaptivePollState DetermineState(bool isAntigravityRunning, bool isAntigravityForeground)
    {
        if (!isAntigravityRunning)
        {
            return AdaptivePollState.Offline;
        }

        return isAntigravityForeground ? AdaptivePollState.Active : AdaptivePollState.Idle;
    }

    public void SetState(AdaptivePollState newState)
    {
        if (CurrentState == newState)
        {
            return;
        }

        CurrentState = newState;
        StateChanged?.Invoke(CurrentState, CurrentInterval);
    }

    public bool UpdateState(bool isAntigravityRunning, bool isAntigravityForeground)
    {
        var newState = DetermineState(isAntigravityRunning, isAntigravityForeground);
        if (newState != CurrentState)
        {
            SetState(newState);
            return true;
        }

        return false;
    }

    public void RecordActivity(DateTimeOffset? timestamp = null)
    {
        LastActivityTime = timestamp ?? DateTimeOffset.UtcNow;
        if (CurrentState != AdaptivePollState.Active)
        {
            SetState(AdaptivePollState.Active);
        }
    }

    public bool CheckInactivity(DateTimeOffset? now = null)
    {
        if (CurrentState != AdaptivePollState.Active)
        {
            return false;
        }

        var currentTime = now ?? DateTimeOffset.UtcNow;
        if (currentTime - LastActivityTime >= IdleInactivityThreshold)
        {
            SetState(AdaptivePollState.Idle);
            return true;
        }

        return false;
    }

    public async Task TriggerImmediatePollAsync(
        Func<CancellationToken, Task> pollAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pollAction);
        await pollAction(cancellationToken).ConfigureAwait(false);
    }

    public async Task RunAsync(
        Func<CancellationToken, Task> pollAction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pollAction);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await pollAction(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // Isolate callback failures from terminating the polling loop
            }

            try
            {
                await Task.Delay(CurrentInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
