using AntigravityQuota.Core;
using Xunit;

namespace AntigravityQuota.Tests;

public class WindowContextTrackerTests : IDisposable
{
    private readonly List<WindowContextTracker> _trackersToDispose = new();

    public void Dispose()
    {
        foreach (var tracker in _trackersToDispose)
        {
            tracker.Dispose();
        }
    }

    private WindowContextTracker CreateTracker(
        Func<string?>? processProvider = null,
        Func<bool>? altProvider = null,
        bool autoHideEnabled = true)
    {
        var tracker = new WindowContextTracker(
            activeProcessProvider: processProvider,
            altKeyProvider: altProvider,
            autoHideEnabled: autoHideEnabled,
            pollIntervalMs: 250);
        _trackersToDispose.Add(tracker);
        return tracker;
    }

    [Theory]
    [InlineData("Antigravity", true)]
    [InlineData("Antigravity.exe", true)]
    [InlineData("antigravity", true)]
    [InlineData("antigravity.exe", true)]
    [InlineData("Code", true)]
    [InlineData("Code.exe", true)]
    [InlineData("code", true)]
    [InlineData("code.exe", true)]
    [InlineData("AntigravityQuota", true)]
    [InlineData("AntigravityQuota.exe", true)]
    [InlineData("AntigravityQuota.App", true)]
    [InlineData("AntigravityQuota.App.exe", true)]
    [InlineData("chrome.exe", false)]
    [InlineData("chrome", false)]
    [InlineData("Telegram.exe", false)]
    [InlineData("devenv.exe", false)]
    [InlineData("explorer.exe", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsTargetProcess_IdentifiesSupportedIDEsAndRejectsOthers(string? processName, bool expected)
    {
        bool actual = WindowContextTracker.IsTargetProcess(processName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PollActiveWindow_WhenTargetBecomesActive_FiresEvents()
    {
        string? currentProc = "chrome.exe";
        var tracker = CreateTracker(processProvider: () => currentProc);

        bool? targetActiveEventValue = null;
        bool? visibilityEventValue = null;
        tracker.TargetActiveChanged += (s, isTarget) => targetActiveEventValue = isTarget;
        tracker.VisibilityChanged += (s, isVis) => visibilityEventValue = isVis;

        // 1. Initial poll with non-target
        tracker.PollActiveWindow();
        Assert.False(tracker.IsTargetActive);
        Assert.False(tracker.ShouldBeVisible);

        // 2. Switch to Antigravity
        currentProc = "Antigravity.exe";
        tracker.PollActiveWindow();

        Assert.True(tracker.IsTargetActive);
        Assert.True(tracker.ShouldBeVisible);
        Assert.True(targetActiveEventValue);
        Assert.True(visibilityEventValue);

        // 3. Duplicate poll does not fire redundant events
        targetActiveEventValue = null;
        visibilityEventValue = null;
        tracker.PollActiveWindow();
        Assert.Null(targetActiveEventValue);
        Assert.Null(visibilityEventValue);

        // 4. Switch to browser
        currentProc = "msedge.exe";
        tracker.PollActiveWindow();

        Assert.False(tracker.IsTargetActive);
        Assert.False(tracker.ShouldBeVisible);
        Assert.False(targetActiveEventValue);
        Assert.False(visibilityEventValue);
    }

    [Fact]
    public void AutoHideDisabled_WindowRemainsVisibleRegardlessOfTarget()
    {
        string? currentProc = "chrome.exe";
        var tracker = CreateTracker(processProvider: () => currentProc, autoHideEnabled: false);

        bool? visibilityEventValue = null;
        tracker.VisibilityChanged += (s, isVis) => visibilityEventValue = isVis;

        tracker.PollActiveWindow();

        Assert.False(tracker.IsTargetActive);
        Assert.True(tracker.ShouldBeVisible); // Because AutoHide is false, it should stay visible!

        // Switch to Code.exe
        currentProc = "Code.exe";
        tracker.PollActiveWindow();

        Assert.True(tracker.IsTargetActive);
        Assert.True(tracker.ShouldBeVisible);

        // Switch back to chrome
        currentProc = "chrome.exe";
        tracker.PollActiveWindow();

        Assert.False(tracker.IsTargetActive);
        Assert.True(tracker.ShouldBeVisible);
        Assert.Null(visibilityEventValue); // Visibility was not toggled off
    }

    [Fact]
    public void AutoHideEnabled_TogglingFlag_UpdatesVisibilityState()
    {
        string? currentProc = "slack.exe";
        var tracker = CreateTracker(processProvider: () => currentProc, autoHideEnabled: true);
        tracker.PollActiveWindow();

        Assert.False(tracker.IsTargetActive);
        Assert.False(tracker.ShouldBeVisible);

        bool? visibilityEventValue = null;
        tracker.VisibilityChanged += (s, isVis) => visibilityEventValue = isVis;

        // Disable auto-hide -> Should become visible immediately
        tracker.AutoHideEnabled = false;
        Assert.True(tracker.ShouldBeVisible);
        Assert.True(visibilityEventValue);

        // Re-enable auto-hide while in non-target -> Should hide
        visibilityEventValue = null;
        tracker.AutoHideEnabled = true;
        Assert.False(tracker.ShouldBeVisible);
        Assert.False(visibilityEventValue);
    }

    [Fact]
    public void PollAltKey_DetectsPressAndRelease()
    {
        bool altPressed = false;
        var tracker = CreateTracker(altProvider: () => altPressed);

        bool? altEventValue = null;
        tracker.AltStateChanged += (s, pressed) => altEventValue = pressed;

        tracker.PollAltKey();
        Assert.False(tracker.IsAltPressed);

        // Press Alt
        altPressed = true;
        tracker.PollAltKey();

        Assert.True(tracker.IsAltPressed);
        Assert.True(altEventValue);

        // Alt still down -> No duplicate event
        altEventValue = null;
        tracker.PollAltKey();
        Assert.Null(altEventValue);

        // Release Alt
        altPressed = false;
        tracker.PollAltKey();

        Assert.False(tracker.IsAltPressed);
        Assert.False(altEventValue);
    }

    [Fact]
    public void Poll_ExecutesBothWindowAndAltChecks()
    {
        string? currentProc = "Antigravity.exe";
        bool altPressed = true;
        var tracker = CreateTracker(
            processProvider: () => currentProc,
            altProvider: () => altPressed);

        tracker.Poll();

        Assert.True(tracker.IsTargetActive);
        Assert.True(tracker.IsAltPressed);
        Assert.True(tracker.ShouldBeVisible);
    }
}
