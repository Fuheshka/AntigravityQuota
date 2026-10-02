using System;
using AntigravityQuota.Core;

namespace AntigravityQuota.Tests;

public class TaskbarPositionerTests
{
    [Fact]
    public void DetermineTaskbarEdge_WhenTaskbarAtBottom_ReturnsBottom()
    {
        var monitor = new ScreenRect(0, 0, 1920, 1080);
        var work = new ScreenRect(0, 0, 1920, 1040); // 40px taskbar at bottom

        var edge = TaskbarGeometry.DetermineEdge(monitor, work);
        Assert.Equal(TaskbarEdge.Bottom, edge);
    }

    [Fact]
    public void DetermineTaskbarEdge_WhenTaskbarAtTop_ReturnsTop()
    {
        var monitor = new ScreenRect(0, 0, 1920, 1080);
        var work = new ScreenRect(0, 48, 1920, 1080); // 48px taskbar at top

        var edge = TaskbarGeometry.DetermineEdge(monitor, work);
        Assert.Equal(TaskbarEdge.Top, edge);
    }

    [Fact]
    public void DetermineTaskbarEdge_WhenTaskbarAtLeft_ReturnsLeft()
    {
        var monitor = new ScreenRect(0, 0, 1920, 1080);
        var work = new ScreenRect(60, 0, 1920, 1080);

        var edge = TaskbarGeometry.DetermineEdge(monitor, work);
        Assert.Equal(TaskbarEdge.Left, edge);
    }

    [Fact]
    public void DetermineTaskbarEdge_WhenTaskbarAtRight_ReturnsRight()
    {
        var monitor = new ScreenRect(0, 0, 1920, 1080);
        var work = new ScreenRect(0, 0, 1860, 1080);

        var edge = TaskbarGeometry.DetermineEdge(monitor, work);
        Assert.Equal(TaskbarEdge.Right, edge);
    }

    [Fact]
    public void CalculatePosition_BottomTaskbar_RightAlignedWithMargin()
    {
        var monitor = new ScreenRect(0, 0, 1920, 1080);
        var work = new ScreenRect(0, 0, 1920, 1040);
        double windowWidth = 360;
        double windowHeight = 520;
        double margin = 12;

        var pos = TaskbarGeometry.CalculateFlyoutPosition(
            monitor, work, windowWidth, windowHeight, dpiScaleX: 1.0, dpiScaleY: 1.0, marginDip: margin);

        // Expected X: 1920 - 360 - 12 = 1548
        // Expected Y: 1040 - 520 - 12 = 508
        Assert.Equal(1548, pos.X);
        Assert.Equal(508, pos.Y);
    }

    [Fact]
    public void CalculatePosition_TopTaskbar_RightAlignedWithMargin()
    {
        var monitor = new ScreenRect(0, 0, 1920, 1080);
        var work = new ScreenRect(0, 48, 1920, 1080);
        double windowWidth = 360;
        double windowHeight = 520;
        double margin = 12;

        var pos = TaskbarGeometry.CalculateFlyoutPosition(
            monitor, work, windowWidth, windowHeight, dpiScaleX: 1.0, dpiScaleY: 1.0, marginDip: margin);

        // Expected X: 1920 - 360 - 12 = 1548
        // Expected Y: 48 + 12 = 60
        Assert.Equal(1548, pos.X);
        Assert.Equal(60, pos.Y);
    }

    [Fact]
    public void CalculatePosition_WithDpiScale_TranslatesDIPsCorrectly()
    {
        // 150% scaling (1.5x): physical 2880x1800 screen -> 1920x1200 DIPs
        var monitor = new ScreenRect(0, 0, 2880, 1800);
        var work = new ScreenRect(0, 0, 2880, 1710); // 90 physical px taskbar (60 DIP)
        double windowWidth = 360;
        double windowHeight = 520;
        double margin = 12;

        var pos = TaskbarGeometry.CalculateFlyoutPosition(
            monitor, work, windowWidth, windowHeight, dpiScaleX: 1.5, dpiScaleY: 1.5, marginDip: margin);

        // workRight in DIP = 2880 / 1.5 = 1920
        // workBottom in DIP = 1710 / 1.5 = 1140
        // Expected X: 1920 - 360 - 12 = 1548
        // Expected Y: 1140 - 520 - 12 = 608
        Assert.Equal(1548, pos.X);
        Assert.Equal(608, pos.Y);
    }

    [Fact]
    public void CalculatePosition_CenterOverCursor_ClampsWithinWorkArea()
    {
        var monitor = new ScreenRect(0, 0, 1920, 1080);
        var work = new ScreenRect(0, 0, 1920, 1040);
        double windowWidth = 360;
        double windowHeight = 520;
        double margin = 12;

        // Cursor at physical X = 1800 (DIP = 1800)
        // Center: 1800 - 180 = 1620 -> Clamped to max (1920 - 360 - 12 = 1548)
        var posClamped = TaskbarGeometry.CalculateFlyoutPosition(
            monitor, work, windowWidth, windowHeight, dpiScaleX: 1.0, dpiScaleY: 1.0, marginDip: margin,
            cursorPhysicalX: 1800, centerOverCursor: true);

        Assert.Equal(1548, posClamped.X);

        // Cursor at physical X = 1000 (DIP = 1000)
        // Center: 1000 - 180 = 820 -> within bounds
        var posCentered = TaskbarGeometry.CalculateFlyoutPosition(
            monitor, work, windowWidth, windowHeight, dpiScaleX: 1.0, dpiScaleY: 1.0, marginDip: margin,
            cursorPhysicalX: 1000, centerOverCursor: true);

        Assert.Equal(820, posCentered.X);
    }

    [Fact]
    public void DebounceTimestampGate_WithinCooldown_BlocksReopen()
    {
        var gate = new DebounceGate(TimeSpan.FromMilliseconds(250));
        var now = DateTime.UtcNow;

        gate.RecordDeactivation(now);

        // Click happened 50ms later -> Should NOT open (it's the click that caused deactivation)
        Assert.False(gate.CanToggle(now.AddMilliseconds(50)));

        // Click happened 300ms later -> Should open
        Assert.True(gate.CanToggle(now.AddMilliseconds(300)));
    }
}
