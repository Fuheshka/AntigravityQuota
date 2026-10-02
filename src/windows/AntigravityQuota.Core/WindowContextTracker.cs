using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AntigravityQuota.Core;

/// <summary>
/// Tracks foreground window process changes and Alt modifier key state.
/// Supports context-aware HUD auto-hide (fading in when Antigravity/Code IDE is active)
/// and click-through mode toggling via Alt key.
/// </summary>
public sealed class WindowContextTracker : IDisposable
{
    private readonly Func<string?>? _activeProcessProvider;
    private readonly Func<bool>? _altKeyProvider;
    private readonly Timer? _timer;
    private readonly object _lock = new();

    private bool _autoHideEnabled;
    private bool _isTargetActive;
    private bool _isAltPressed;
    private bool _disposed;

    public const int DefaultPollIntervalMs = 250;
    private const int VK_MENU = 0x12;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public event EventHandler<bool>? TargetActiveChanged;
    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler<bool>? AltStateChanged;

    public WindowContextTracker(
        Func<string?>? activeProcessProvider = null,
        Func<bool>? altKeyProvider = null,
        bool autoHideEnabled = true,
        int pollIntervalMs = DefaultPollIntervalMs)
    {
        _activeProcessProvider = activeProcessProvider;
        _altKeyProvider = altKeyProvider;
        _autoHideEnabled = autoHideEnabled;

        if (pollIntervalMs > 0)
        {
            _timer = new Timer(_ => Poll(), null, pollIntervalMs, pollIntervalMs);
        }
    }

    public bool AutoHideEnabled
    {
        get
        {
            lock (_lock) return _autoHideEnabled;
        }
        set
        {
            lock (_lock)
            {
                if (_autoHideEnabled == value) return;
                _autoHideEnabled = value;

                // When AutoHide is disabled, window should become visible immediately.
                // When AutoHide is enabled, visibility reflects current IsTargetActive state.
                bool visible = !_autoHideEnabled || _isTargetActive;
                VisibilityChanged?.Invoke(this, visible);
            }
        }
    }

    public bool IsTargetActive
    {
        get
        {
            lock (_lock) return _isTargetActive;
        }
    }

    public bool IsAltPressed
    {
        get
        {
            lock (_lock) return _isAltPressed;
        }
    }

    public bool ShouldBeVisible
    {
        get
        {
            lock (_lock) return !_autoHideEnabled || _isTargetActive;
        }
    }

    /// <summary>
    /// Checks if a process name matches supported IDEs (Antigravity, Code / VS Code)
    /// or our own application process.
    /// </summary>
    public static bool IsTargetProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;

        var clean = processName.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^4];
        }

        return clean.Equals("Antigravity", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("Code", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("AntigravityQuota", StringComparison.OrdinalIgnoreCase)
            || clean.Equals("AntigravityQuota.App", StringComparison.OrdinalIgnoreCase);
    }

    public void Poll()
    {
        PollActiveWindow();
        PollAltKey();
    }

    public void PollActiveWindow()
    {
        string? procName = _activeProcessProvider != null
            ? _activeProcessProvider()
            : GetActiveProcessName();

        bool isTarget = IsTargetProcess(procName);

        lock (_lock)
        {
            if (isTarget != _isTargetActive)
            {
                _isTargetActive = isTarget;
                TargetActiveChanged?.Invoke(this, _isTargetActive);

                if (_autoHideEnabled)
                {
                    VisibilityChanged?.Invoke(this, _isTargetActive);
                }
            }
        }
    }

    public void PollAltKey()
    {
        bool isAlt = _altKeyProvider != null
            ? _altKeyProvider()
            : GetIsAltPressed();

        lock (_lock)
        {
            if (isAlt != _isAltPressed)
            {
                _isAltPressed = isAlt;
                AltStateChanged?.Invoke(this, _isAltPressed);
            }
        }
    }

    public static string? GetActiveProcessName()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            nint hwnd = GetForegroundWindow();
            if (hwnd == nint.Zero) return null;

            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return null;

            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    public static bool GetIsAltPressed()
    {
        if (!OperatingSystem.IsWindows()) return false;

        try
        {
            return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
        }
    }
}
