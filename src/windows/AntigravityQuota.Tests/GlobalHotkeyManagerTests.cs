using AntigravityQuota.Core;
using Xunit;

namespace AntigravityQuota.Tests;

public class GlobalHotkeyManagerTests : IDisposable
{
    private readonly List<GlobalHotkeyManager> _managersToDispose = new();

    public void Dispose()
    {
        foreach (var manager in _managersToDispose)
        {
            manager.Dispose();
        }
    }

    private class MockWin32Hotkey
    {
        public List<(nint Hwnd, int Id, uint Modifiers, uint Vk)> Registered { get; } = new();
        public List<(nint Hwnd, int Id)> Unregistered { get; } = new();
        public bool FailOnNoRepeat { get; set; }

        public bool Register(nint hwnd, int id, uint modifiers, uint vk)
        {
            if (FailOnNoRepeat && (modifiers & GlobalHotkeyManager.MOD_NOREPEAT) != 0)
            {
                return false;
            }
            Registered.Add((hwnd, id, modifiers, vk));
            return true;
        }

        public bool Unregister(nint hwnd, int id)
        {
            Unregistered.Add((hwnd, id));
            return true;
        }
    }

    [Fact]
    public void RegisterDefaultHotkeys_RegistersThreeExpectedHotkeys()
    {
        var mock = new MockWin32Hotkey();
        var manager = new GlobalHotkeyManager(
            registerHotKey: mock.Register,
            unregisterHotKey: mock.Unregister);
        _managersToDispose.Add(manager);

        nint fakeHwnd = new nint(12345);
        bool allOk = manager.RegisterDefaultHotkeys(fakeHwnd);

        Assert.True(allOk);
        Assert.Equal(3, mock.Registered.Count);

        // 1. Alt + Shift + Q (Toggle HUD)
        var qHotkey = mock.Registered.FirstOrDefault(r => r.Id == (int)HotkeyAction.ToggleHud);
        Assert.NotEqual(default, qHotkey);
        Assert.Equal(fakeHwnd, qHotkey.Hwnd);
        Assert.Equal(GlobalHotkeyManager.MOD_ALT | GlobalHotkeyManager.MOD_SHIFT | GlobalHotkeyManager.MOD_NOREPEAT, qHotkey.Modifiers);
        Assert.Equal(GlobalHotkeyManager.VK_Q, qHotkey.Vk);

        // 2. Alt + Shift + M (Toggle Pill Mode)
        var mHotkey = mock.Registered.FirstOrDefault(r => r.Id == (int)HotkeyAction.TogglePillMode);
        Assert.NotEqual(default, mHotkey);
        Assert.Equal(fakeHwnd, mHotkey.Hwnd);
        Assert.Equal(GlobalHotkeyManager.MOD_ALT | GlobalHotkeyManager.MOD_SHIFT | GlobalHotkeyManager.MOD_NOREPEAT, mHotkey.Modifiers);
        Assert.Equal(GlobalHotkeyManager.VK_M, mHotkey.Vk);

        // 3. Alt + Shift + R (Refresh Quotas)
        var rHotkey = mock.Registered.FirstOrDefault(r => r.Id == (int)HotkeyAction.RefreshQuotas);
        Assert.NotEqual(default, rHotkey);
        Assert.Equal(fakeHwnd, rHotkey.Hwnd);
        Assert.Equal(GlobalHotkeyManager.MOD_ALT | GlobalHotkeyManager.MOD_SHIFT | GlobalHotkeyManager.MOD_NOREPEAT, rHotkey.Modifiers);
        Assert.Equal(GlobalHotkeyManager.VK_R, rHotkey.Vk);
    }

    [Fact]
    public void Register_WhenNativeFailsWithNoRepeat_FallsBackToWithoutNoRepeat()
    {
        var mock = new MockWin32Hotkey { FailOnNoRepeat = true };
        var manager = new GlobalHotkeyManager(
            registerHotKey: mock.Register,
            unregisterHotKey: mock.Unregister);
        _managersToDispose.Add(manager);

        nint fakeHwnd = new nint(12345);
        bool allOk = manager.RegisterDefaultHotkeys(fakeHwnd);

        Assert.True(allOk);
        Assert.Equal(3, mock.Registered.Count);

        // All should be registered without MOD_NOREPEAT
        foreach (var reg in mock.Registered)
        {
            Assert.Equal(GlobalHotkeyManager.MOD_ALT | GlobalHotkeyManager.MOD_SHIFT, reg.Modifiers);
        }
    }

    [Fact]
    public void UnregisterAll_UnregistersAllRegisteredHotkeys()
    {
        var mock = new MockWin32Hotkey();
        var manager = new GlobalHotkeyManager(
            registerHotKey: mock.Register,
            unregisterHotKey: mock.Unregister);
        _managersToDispose.Add(manager);

        nint fakeHwnd = new nint(12345);
        manager.RegisterDefaultHotkeys(fakeHwnd);
        Assert.Equal(3, mock.Registered.Count);

        manager.UnregisterAll();
        Assert.Equal(3, mock.Unregistered.Count);

        Assert.Contains(mock.Unregistered, u => u.Hwnd == fakeHwnd && u.Id == (int)HotkeyAction.ToggleHud);
        Assert.Contains(mock.Unregistered, u => u.Hwnd == fakeHwnd && u.Id == (int)HotkeyAction.TogglePillMode);
        Assert.Contains(mock.Unregistered, u => u.Hwnd == fakeHwnd && u.Id == (int)HotkeyAction.RefreshQuotas);

        Assert.False(manager.IsRegistered(HotkeyAction.ToggleHud));
        Assert.False(manager.IsRegistered(HotkeyAction.TogglePillMode));
        Assert.False(manager.IsRegistered(HotkeyAction.RefreshQuotas));
    }

    [Fact]
    public void Dispose_UnregistersAllHotkeys()
    {
        var mock = new MockWin32Hotkey();
        var manager = new GlobalHotkeyManager(
            registerHotKey: mock.Register,
            unregisterHotKey: mock.Unregister);

        nint fakeHwnd = new nint(8888);
        manager.RegisterDefaultHotkeys(fakeHwnd);
        Assert.Equal(3, mock.Registered.Count);

        manager.Dispose();
        Assert.Equal(3, mock.Unregistered.Count);
    }

    [Theory]
    [InlineData((int)HotkeyAction.ToggleHud, HotkeyAction.ToggleHud)]
    [InlineData((int)HotkeyAction.TogglePillMode, HotkeyAction.TogglePillMode)]
    [InlineData((int)HotkeyAction.RefreshQuotas, HotkeyAction.RefreshQuotas)]
    public void HookCallback_WhenWmHotkeyReceived_TriggersActionAndSetsHandled(int hotkeyId, HotkeyAction expectedAction)
    {
        var mock = new MockWin32Hotkey();
        var manager = new GlobalHotkeyManager(
            registerHotKey: mock.Register,
            unregisterHotKey: mock.Unregister);
        _managersToDispose.Add(manager);

        nint fakeHwnd = new nint(9999);
        manager.RegisterDefaultHotkeys(fakeHwnd);

        HotkeyAction? triggeredAction = null;
        manager.HotkeyTriggered += (s, action) => triggeredAction = action;

        bool toggleHudCalled = false;
        bool togglePillCalled = false;
        bool refreshCalled = false;

        manager.OnToggleHud = () => toggleHudCalled = true;
        manager.OnTogglePillMode = () => togglePillCalled = true;
        manager.OnRefreshQuotas = () => refreshCalled = true;

        bool handled = false;
        nint result = manager.HookCallback(fakeHwnd, GlobalHotkeyManager.WM_HOTKEY, (nint)hotkeyId, nint.Zero, ref handled);

        Assert.Equal(nint.Zero, result);
        Assert.True(handled);
        Assert.Equal(expectedAction, triggeredAction);

        switch (expectedAction)
        {
            case HotkeyAction.ToggleHud:
                Assert.True(toggleHudCalled);
                Assert.False(togglePillCalled);
                Assert.False(refreshCalled);
                break;
            case HotkeyAction.TogglePillMode:
                Assert.False(toggleHudCalled);
                Assert.True(togglePillCalled);
                Assert.False(refreshCalled);
                break;
            case HotkeyAction.RefreshQuotas:
                Assert.False(toggleHudCalled);
                Assert.False(togglePillCalled);
                Assert.True(refreshCalled);
                break;
        }
    }

    [Fact]
    public void HookCallback_WhenOtherMessageReceived_DoesNotTriggerAndHandledIsFalse()
    {
        var mock = new MockWin32Hotkey();
        var manager = new GlobalHotkeyManager(
            registerHotKey: mock.Register,
            unregisterHotKey: mock.Unregister);
        _managersToDispose.Add(manager);

        nint fakeHwnd = new nint(9999);
        manager.RegisterDefaultHotkeys(fakeHwnd);

        bool eventFired = false;
        manager.HotkeyTriggered += (s, a) => eventFired = true;

        bool handled = false;
        int WM_KEYDOWN = 0x0100;
        nint result = manager.HookCallback(fakeHwnd, WM_KEYDOWN, (nint)HotkeyAction.ToggleHud, nint.Zero, ref handled);

        Assert.Equal(nint.Zero, result);
        Assert.False(handled);
        Assert.False(eventFired);
    }

    [Fact]
    public void HookCallback_WhenUnknownHotkeyId_DoesNotTriggerAndHandledIsFalse()
    {
        var mock = new MockWin32Hotkey();
        var manager = new GlobalHotkeyManager(
            registerHotKey: mock.Register,
            unregisterHotKey: mock.Unregister);
        _managersToDispose.Add(manager);

        nint fakeHwnd = new nint(9999);
        manager.RegisterDefaultHotkeys(fakeHwnd);

        bool eventFired = false;
        manager.HotkeyTriggered += (s, a) => eventFired = true;

        bool handled = false;
        int unknownId = 999;
        nint result = manager.HookCallback(fakeHwnd, GlobalHotkeyManager.WM_HOTKEY, (nint)unknownId, nint.Zero, ref handled);

        Assert.Equal(nint.Zero, result);
        Assert.False(handled);
        Assert.False(eventFired);
    }

    [Fact]
    public void IsEnabled_WhenDisabled_UnregistersHotkeysAndIgnoresMessages()
    {
        var mock = new MockWin32Hotkey();
        var manager = new GlobalHotkeyManager(
            registerHotKey: mock.Register,
            unregisterHotKey: mock.Unregister);
        _managersToDispose.Add(manager);

        nint fakeHwnd = new nint(9999);
        manager.RegisterDefaultHotkeys(fakeHwnd);
        Assert.Equal(3, mock.Registered.Count);

        // Disable
        manager.IsEnabled = false;
        Assert.False(manager.IsEnabled);
        Assert.Equal(3, mock.Unregistered.Count);

        // Trigger should be ignored
        bool handled = false;
        bool eventFired = false;
        manager.HotkeyTriggered += (s, a) => eventFired = true;

        manager.HookCallback(fakeHwnd, GlobalHotkeyManager.WM_HOTKEY, (nint)HotkeyAction.ToggleHud, nint.Zero, ref handled);
        Assert.False(handled);
        Assert.False(eventFired);

        // Re-enable
        manager.IsEnabled = true;
        Assert.True(manager.IsEnabled);
        Assert.Equal(6, mock.Registered.Count); // Registered again
    }
}
