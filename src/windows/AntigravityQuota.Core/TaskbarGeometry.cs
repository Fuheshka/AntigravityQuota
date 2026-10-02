using System;

namespace AntigravityQuota.Core;

public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right,
    None
}

public readonly record struct ScreenRect(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
}

public readonly record struct ScreenPoint(double X, double Y);

public static class TaskbarGeometry
{
    public static TaskbarEdge DetermineEdge(ScreenRect monitor, ScreenRect work)
    {
        if (work.Bottom < monitor.Bottom) return TaskbarEdge.Bottom;
        if (work.Top > monitor.Top) return TaskbarEdge.Top;
        if (work.Left > monitor.Left) return TaskbarEdge.Left;
        if (work.Right < monitor.Right) return TaskbarEdge.Right;
        return TaskbarEdge.None;
    }

    public static ScreenPoint CalculateFlyoutPosition(
        ScreenRect monitorPhysical,
        ScreenRect workPhysical,
        double windowWidthDip,
        double windowHeightDip,
        double dpiScaleX = 1.0,
        double dpiScaleY = 1.0,
        double marginDip = 12.0,
        double? cursorPhysicalX = null,
        bool centerOverCursor = false)
    {
        double scaleX = dpiScaleX <= 0 ? 1.0 : dpiScaleX;
        double scaleY = dpiScaleY <= 0 ? 1.0 : dpiScaleY;

        double workLeftDip = workPhysical.Left / scaleX;
        double workTopDip = workPhysical.Top / scaleY;
        double workRightDip = workPhysical.Right / scaleX;
        double workBottomDip = workPhysical.Bottom / scaleY;

        var edge = DetermineEdge(monitorPhysical, workPhysical);

        double x;
        double y;

        // Vertical calculation
        if (edge == TaskbarEdge.Top)
        {
            y = workTopDip + marginDip;
        }
        else // Bottom, Left, Right or None default to bottom alignment
        {
            y = workBottomDip - windowHeightDip - marginDip;
        }

        // Horizontal calculation
        if (edge == TaskbarEdge.Left)
        {
            x = workLeftDip + marginDip;
        }
        else if (centerOverCursor && cursorPhysicalX.HasValue)
        {
            double cursorXDip = cursorPhysicalX.Value / scaleX;
            x = cursorXDip - (windowWidthDip / 2.0);
            double minX = workLeftDip + marginDip;
            double maxX = workRightDip - windowWidthDip - marginDip;
            if (minX <= maxX)
            {
                x = Math.Clamp(x, minX, maxX);
            }
        }
        else
        {
            x = workRightDip - windowWidthDip - marginDip;
        }

        return new ScreenPoint(x, y);
    }
}

/// <summary>
/// Debounce gate to prevent the classic "toggle-flicker" race condition
/// where clicking the tray icon deactivates the flyout, and then the tray click handler
/// sees the flyout as hidden and immediately reopens it.
/// </summary>
public class DebounceGate
{
    private readonly TimeSpan _cooldown;
    private DateTime _lastDeactivatedUtc = DateTime.MinValue;

    public DebounceGate(TimeSpan? cooldown = null)
    {
        _cooldown = cooldown ?? TimeSpan.FromMilliseconds(250);
    }

    public void RecordDeactivation(DateTime? nowUtc = null)
    {
        _lastDeactivatedUtc = nowUtc ?? DateTime.UtcNow;
    }

    public bool CanToggle(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        return (now - _lastDeactivatedUtc) >= _cooldown;
    }
}
