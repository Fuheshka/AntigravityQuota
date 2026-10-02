using System.Runtime.InteropServices;

namespace AntigravityQuota.Core;

/// <summary>
/// Identifiers for supported global hotkey actions.
/// </summary>
public enum HotkeyAction
{
    ToggleHud = 1,
    TogglePillMode = 2,
    RefreshQuotas = 3
}

/// <summary>
/// Manages global system hotkeys via Win32 RegisterHotKey / UnregisterHotKey.
/// Intercepts WM_HOTKEY messages via WndProc / HwndSourceHook without stealing keyboard focus.
/// Follows Ponytail simplicity and zero external dependencies.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    public const int WM_HOTKEY = 0x0312;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const uint VK_Q = 0x51;
    public const uint VK_M = 0x4D;
    public const uint VK_R = 0x52;

    public delegate bool RegisterHotKeyDelegate(nint hWnd, int id, uint fsModifiers, uint vk);
    public delegate bool UnregisterHotKeyDelegate(nint hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKeyNative(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKeyNative(nint hWnd, int id);

    private readonly RegisterHotKeyDelegate _registerHotKey;
    private readonly UnregisterHotKeyDelegate _unregisterHotKey;
    private readonly Dictionary<HotkeyAction, (nint Hwnd, int Id, uint Modifiers, uint Vk)> _registered = new();
    private readonly object _lock = new();

    private nint _lastHwnd = nint.Zero;
    private bool _isEnabled = true;
    private bool _disposed;

    public event EventHandler<HotkeyAction>? HotkeyTriggered;

    public Action? OnToggleHud { get; set; }
    public Action? OnTogglePillMode { get; set; }
    public Action? OnRefreshQuotas { get; set; }

    public GlobalHotkeyManager(
        RegisterHotKeyDelegate? registerHotKey = null,
        UnregisterHotKeyDelegate? unregisterHotKey = null)
    {
        _registerHotKey = registerHotKey ?? RegisterHotKeyNative;
        _unregisterHotKey = unregisterHotKey ?? UnregisterHotKeyNative;
    }

    public bool IsEnabled
    {
        get
        {
            lock (_lock) return _isEnabled;
        }
        set
        {
            lock (_lock)
            {
                if (_isEnabled == value) return;
                _isEnabled = value;

                if (!_isEnabled)
                {
                    UnregisterAllInternal();
                }
                else if (_lastHwnd != nint.Zero)
                {
                    RegisterDefaultHotkeysInternal(_lastHwnd);
                }
            }
        }
    }

    /// <summary>
    /// Registers the default application hotkey combinations:
    /// - Alt + Shift + Q: Toggle HUD overlay visibility
    /// - Alt + Shift + M: Toggle compact pill and expanded card mode
    /// - Alt + Shift + R: Force refresh quota snapshots immediately
    /// </summary>
    public bool RegisterDefaultHotkeys(nint hwnd)
    {
        lock (_lock)
        {
            _lastHwnd = hwnd;
            if (!_isEnabled) return false;
            return RegisterDefaultHotkeysInternal(hwnd);
        }
    }

    private bool RegisterDefaultHotkeysInternal(nint hwnd)
    {
        bool ok = true;
        ok &= RegisterInternal(hwnd, HotkeyAction.ToggleHud, MOD_ALT | MOD_SHIFT, VK_Q);
        ok &= RegisterInternal(hwnd, HotkeyAction.TogglePillMode, MOD_ALT | MOD_SHIFT, VK_M);
        ok &= RegisterInternal(hwnd, HotkeyAction.RefreshQuotas, MOD_ALT | MOD_SHIFT, VK_R);
        return ok;
    }

    /// <summary>
    /// Registers a single hotkey action for the specified HWND.
    /// Attempts registration with MOD_NOREPEAT first, falling back without MOD_NOREPEAT if unsupported.
    /// </summary>
    public bool Register(nint hwnd, HotkeyAction action, uint modifiers, uint vk)
    {
        lock (_lock)
        {
            _lastHwnd = hwnd;
            if (!_isEnabled) return false;
            return RegisterInternal(hwnd, action, modifiers, vk);
        }
    }

    private bool RegisterInternal(nint hwnd, HotkeyAction action, uint modifiers, uint vk)
    {
        int id = (int)action;

        // Unregister existing if re-registering
        if (_registered.TryGetValue(action, out var existing))
        {
            try
            {
                _unregisterHotKey(existing.Hwnd, existing.Id);
            }
            catch
            {
                // Ignore failure during unregistration
            }
            _registered.Remove(action);
        }

        // 1. Try with MOD_NOREPEAT to prevent repetitive repeat-key floods
        bool success = false;
        try
        {
            success = _registerHotKey(hwnd, id, modifiers | MOD_NOREPEAT, vk);
        }
        catch
        {
            success = false;
        }

        // 2. Fall back to standard modifiers if MOD_NOREPEAT is rejected by OS / driver
        if (!success)
        {
            try
            {
                success = _registerHotKey(hwnd, id, modifiers, vk);
            }
            catch
            {
                success = false;
            }
        }

        if (success)
        {
            _registered[action] = (hwnd, id, modifiers, vk);
        }

        return success;
    }

    /// <summary>
    /// Unregisters a specific hotkey action.
    /// </summary>
    public bool Unregister(HotkeyAction action)
    {
        lock (_lock)
        {
            if (_registered.TryGetValue(action, out var existing))
            {
                bool ok = false;
                try
                {
                    ok = _unregisterHotKey(existing.Hwnd, existing.Id);
                }
                catch
                {
                    ok = false;
                }
                _registered.Remove(action);
                return ok;
            }
            return false;
        }
    }

    /// <summary>
    /// Unregisters all currently registered global hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        lock (_lock)
        {
            UnregisterAllInternal();
        }
    }

    private void UnregisterAllInternal()
    {
        foreach (var (_, (hwnd, id, _, _)) in _registered)
        {
            try
            {
                _unregisterHotKey(hwnd, id);
            }
            catch
            {
                // Suppress errors during cleanup
            }
        }
        _registered.Clear();
    }

    public bool IsRegistered(HotkeyAction action)
    {
        lock (_lock)
        {
            return _registered.ContainsKey(action);
        }
    }

    /// <summary>
    /// Window procedure hook callback matching WPF HwndSourceHook delegate signature:
    /// (nint hwnd, int msg, nint wParam, nint lParam, ref bool handled) -> nint
    /// Intercepts WM_HOTKEY (0x0312) and dispatches action callbacks.
    /// </summary>
    public nint HookCallback(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY)
        {
            return nint.Zero;
        }

        lock (_lock)
        {
            if (!_isEnabled || _disposed)
            {
                return nint.Zero;
            }

            int id = (int)wParam;
            if (Enum.IsDefined(typeof(HotkeyAction), id))
            {
                var action = (HotkeyAction)id;
                if (_registered.ContainsKey(action))
                {
                    handled = true;
                    DispatchAction(action);
                }
            }
        }

        return nint.Zero;
    }

    private void DispatchAction(HotkeyAction action)
    {
        HotkeyTriggered?.Invoke(this, action);

        switch (action)
        {
            case HotkeyAction.ToggleHud:
                OnToggleHud?.Invoke();
                break;
            case HotkeyAction.TogglePillMode:
                OnTogglePillMode?.Invoke();
                break;
            case HotkeyAction.RefreshQuotas:
                OnRefreshQuotas?.Invoke();
                break;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            UnregisterAllInternal();
        }
    }
}
