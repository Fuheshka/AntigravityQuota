using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AntigravityQuota.App.Views;
using AntigravityQuota.Core.ViewModels;
using AntigravityQuota.Core.Models;
using AntigravityQuota.Core;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using FormsToolStripSeparator = System.Windows.Forms.ToolStripSeparator;
using FormsMouseButtons = System.Windows.Forms.MouseButtons;

namespace AntigravityQuota.App;

public partial class App : Application
{
    private static readonly DebounceGate DebounceGate = new(TimeSpan.FromMilliseconds(250));

    private FlyoutWindow? _flyout;
    private FlyoutViewModel? _viewModel;
    private QuotaHudWindow? _hudWindow;
    private HudViewModel? _hudViewModel;
    private AboutWindow? _aboutWindow;
    private AboutViewModel? _aboutViewModel;
    private QuotaSnapshot? _latestSnapshot;
    private WindowContextTracker? _tracker;
    private GlobalHotkeyManager? _hotkeyManager;
    private FormsNotifyIcon? _notifyIcon;

    private QuotaClient? _client;
    private QuotaHistoryTracker? _history;
    private AdaptivePollingManager? _pollingManager;
    private StartupManager? _startupManager;
    private UpdateChecker? _updateChecker;
    private CancellationTokenSource? _cts;

    public static void RecordFlyoutDeactivation()
    {
        DebounceGate.RecordDeactivation();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var loc = LocalizationManager.Instance;
        _startupManager = new StartupManager();
        _updateChecker = new UpdateChecker(currentVersion: UpdateChecker.ResolveCurrentVersion());
        _client = new QuotaClient();
        _history = new QuotaHistoryTracker();
        _pollingManager = new AdaptivePollingManager();
        _cts = new CancellationTokenSource();

        Func<Task> triggerRefresh = async () =>
        {
            if (_pollingManager != null && _client != null)
            {
                await _pollingManager.TriggerImmediatePollAsync(async ct =>
                {
                    var snap = await _client.FetchSnapshotAsync(forceRefresh: true, ct);
                    Dispatcher.Invoke(() =>
                    {
                        _latestSnapshot = snap;
                        _viewModel?.UpdateFromSnapshot(snap);
                        _hudViewModel?.UpdateFromSnapshot(snap);
                        _aboutViewModel?.UpdateState(_client.CachedEndpoint, snap, snap?.UpdatedAt);
                        if (_notifyIcon != null && _viewModel != null)
                        {
                            _notifyIcon.Text = TruncateTooltip(_viewModel.TrayTooltipText);
                        }
                    });
                });
            }
        };

        // 1. Initialize Flyout ViewModel
        _viewModel = new FlyoutViewModel(
            client: _client,
            history: _history,
            loc: loc,
            triggerRefreshCallback: triggerRefresh);

        // 2. Initialize About ViewModel
        _aboutViewModel = new AboutViewModel(
            loc: loc,
            clipboardSetter: text => Dispatcher.Invoke(() =>
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch
                {
                    // Fail gracefully if clipboard is busy
                }
            }),
            endpointResolver: () => _client?.CachedEndpoint ?? ServerDiscovery.DiscoverActiveServer(),
            snapshotResolver: () => _latestSnapshot);

        _viewModel.OnShowAboutRequested += () => Dispatcher.Invoke(ShowAboutDialog);

        // 3. Initialize HUD ViewModel, Window Context Tracker and Window
        var hudSettings = HudSettingsManager.Load();
        _hudViewModel = new HudViewModel(
            settings: hudSettings,
            client: _client,
            history: _history,
            loc: loc,
            triggerRefreshCallback: triggerRefresh);

        _hotkeyManager = new GlobalHotkeyManager
        {
            IsEnabled = hudSettings.HotkeysEnabled,
            OnToggleHud = () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_hudWindow != null && _hudViewModel != null)
                    {
                        if (_hudViewModel.IsEnabled)
                        {
                            _hudWindow.HideHud();
                        }
                        else
                        {
                            _hudWindow.ShowHud();
                        }
                    }
                });
            },
            OnTogglePillMode = () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_hudViewModel != null && _hudWindow != null)
                    {
                        if (!_hudViewModel.IsEnabled)
                        {
                            _hudWindow.ShowHud();
                        }
                        _hudViewModel.IsPillMode = !_hudViewModel.IsPillMode;
                    }
                });
            },
            OnRefreshQuotas = () =>
            {
                Dispatcher.Invoke(async () =>
                {
                    if (_viewModel != null)
                    {
                        await _viewModel.RefreshAsync();
                    }
                });
            }
        };

        _tracker = new WindowContextTracker(autoHideEnabled: hudSettings.AutoHideEnabled);
        _hudWindow = new QuotaHudWindow(_hudViewModel, _tracker, _hotkeyManager);

        // Ensure HWND is created so HwndSource and global hotkeys are registered even if HUD is initially hidden
        new WindowInteropHelper(_hudWindow).EnsureHandle();

        if (hudSettings.IsEnabled)
        {
            _hudWindow.ShowHud();
        }

        // 3. Initialize FlyoutWindow (hidden by default)
        _flyout = new FlyoutWindow(_viewModel);
        var flyoutHelper = new WindowInteropHelper(_flyout);
        flyoutHelper.EnsureHandle();
        MainWindow = _flyout;

        // 4. Initialize System Tray Icon
        SetupNotifyIcon(loc);

        // 5. Start background polling loop
        _ = RunPollingLoopAsync(_cts.Token);

        // 6. Start silent background update check after 3 seconds (24h cooldown)
        _ = RunBackgroundUpdateCheckAsync(_cts.Token);
    }

    private static string TruncateTooltip(string text)
    {
        if (string.IsNullOrEmpty(text)) return "AntigravityQuota";
        return text.Length > 63 ? text.Substring(0, 60) + "..." : text;
    }

    private void SetupNotifyIcon(LocalizationManager loc)
    {
        _notifyIcon = new FormsNotifyIcon
        {
            Visible = true,
            Text = TruncateTooltip(_viewModel?.TrayTooltipText ?? "AntigravityQuota")
        };

        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            }
        }
        catch
        {
            // Fallback handled below
        }

        if (_notifyIcon.Icon == null)
        {
            try
            {
                var sri = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute))
                       ?? Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app-icon.ico", UriKind.Absolute));
                if (sri != null)
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(sri.Stream);
                }
            }
            catch
            {
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
        }

        // Left click on tray toggles the FlyoutWindow
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == FormsMouseButtons.Left)
            {
                Dispatcher.Invoke(() => ToggleFlyout());
            }
        };

        // Context menu on right click
        var contextMenu = new FormsContextMenuStrip();

        // 1. Включить HUD (Checkable)
        var hudItem = new FormsToolStripMenuItem(loc.MenuEnableHUD)
        {
            CheckOnClick = true,
            Checked = _hudViewModel?.IsEnabled ?? true,
            ShortcutKeyDisplayString = loc.HotkeyHintToggleHud
        };
        hudItem.Click += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_hudWindow != null && _hudViewModel != null)
                {
                    if (hudItem.Checked)
                    {
                        _hudWindow.ShowHud();
                    }
                    else
                    {
                        _hudWindow.HideHud();
                    }
                }
            });
        };
        contextMenu.Items.Add(hudItem);

        // 2. Режим таблетки (Checkable)
        var pillItem = new FormsToolStripMenuItem(loc.MenuCompactPillMode)
        {
            CheckOnClick = true,
            Checked = _hudViewModel?.IsPillMode ?? false,
            ShortcutKeyDisplayString = loc.HotkeyHintTogglePill
        };
        pillItem.Click += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_hudViewModel != null)
                {
                    _hudViewModel.IsPillMode = pillItem.Checked;
                }
            });
        };
        contextMenu.Items.Add(pillItem);

        // 3. Автоскрытие HUD (Checkable)
        var autoHideItem = new FormsToolStripMenuItem(loc.MenuAutoHideHUD)
        {
            CheckOnClick = true,
            Checked = _hudViewModel?.AutoHideEnabled ?? true
        };
        autoHideItem.Click += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_hudViewModel != null)
                {
                    _hudViewModel.AutoHideEnabled = autoHideItem.Checked;
                }
            });
        };
        contextMenu.Items.Add(autoHideItem);

        // 4. Сквозной клик (Checkable)
        var clickThroughItem = new FormsToolStripMenuItem(loc.MenuClickThrough)
        {
            CheckOnClick = true,
            Checked = _hudViewModel?.ClickThroughEnabled ?? false
        };
        clickThroughItem.Click += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_hudViewModel != null)
                {
                    _hudViewModel.ClickThroughEnabled = clickThroughItem.Checked;
                }
            });
        };
        contextMenu.Items.Add(clickThroughItem);

        // 5. Глобальные горячие клавиши (Checkable)
        var hotkeysItem = new FormsToolStripMenuItem(loc.MenuGlobalHotkeys)
        {
            CheckOnClick = true,
            Checked = _hudViewModel?.HotkeysEnabled ?? true
        };
        hotkeysItem.Click += (s, e) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_hudViewModel != null)
                {
                    _hudViewModel.HotkeysEnabled = hotkeysItem.Checked;
                }
            });
        };
        contextMenu.Items.Add(hotkeysItem);

        if (_hudViewModel != null)
        {
            _hudViewModel.PropertyChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (e.PropertyName == nameof(HudViewModel.IsPillMode))
                    {
                        pillItem.Checked = _hudViewModel.IsPillMode;
                    }
                    else if (e.PropertyName == nameof(HudViewModel.IsEnabled))
                    {
                        hudItem.Checked = _hudViewModel.IsEnabled;
                    }
                    else if (e.PropertyName == nameof(HudViewModel.AutoHideEnabled))
                    {
                        autoHideItem.Checked = _hudViewModel.AutoHideEnabled;
                    }
                    else if (e.PropertyName == nameof(HudViewModel.ClickThroughEnabled))
                    {
                        clickThroughItem.Checked = _hudViewModel.ClickThroughEnabled;
                    }
                    else if (e.PropertyName == nameof(HudViewModel.HotkeysEnabled))
                    {
                        hotkeysItem.Checked = _hudViewModel.HotkeysEnabled;
                    }
                });
            };
        }

        contextMenu.Items.Add(new FormsToolStripSeparator());

        // 6. Запускать при старте Windows (Checkable)
        var startupItem = new FormsToolStripMenuItem(loc.MenuLaunchAtStartup)
        {
            CheckOnClick = true,
            Checked = _startupManager?.IsEnabled() ?? false
        };
        startupItem.Click += (s, e) =>
        {
            if (_startupManager != null)
            {
                bool newState = startupItem.Checked;
                bool success = _startupManager.SetEnabled(newState);
                if (!success)
                {
                    startupItem.Checked = _startupManager.IsEnabled();
                }
            }
        };
        contextMenu.Items.Add(startupItem);

        contextMenu.Items.Add(new FormsToolStripSeparator());

        // 7. Обновить квоты
        var refreshItem = new FormsToolStripMenuItem(loc.MenuRefreshNow)
        {
            ShortcutKeyDisplayString = loc.HotkeyHintRefresh
        };
        refreshItem.Click += async (s, e) =>
        {
            if (_viewModel != null)
            {
                await _viewModel.RefreshAsync();
            }
        };
        contextMenu.Items.Add(refreshItem);

        // 8. Проверить обновления...
        var updateItem = new FormsToolStripMenuItem(loc.MenuCheckUpdates);
        updateItem.Click += async (s, e) =>
        {
            await CheckForUpdatesManualAsync();
        };
        contextMenu.Items.Add(updateItem);

        // 9. Настройки
        var settingsItem = new FormsToolStripMenuItem(loc.MenuSettings);
        settingsItem.Click += (s, e) =>
        {
            Dispatcher.Invoke(() => ToggleFlyout());
        };
        contextMenu.Items.Add(settingsItem);

        // 10. О программе
        var aboutItem = new FormsToolStripMenuItem(loc.MenuAbout);
        aboutItem.Click += (s, e) =>
        {
            Dispatcher.Invoke(() => ShowAboutDialog());
        };
        contextMenu.Items.Add(aboutItem);

        contextMenu.Items.Add(new FormsToolStripSeparator());

        // 11. Выход
        var exitItem = new FormsToolStripMenuItem(loc.MenuExit);
        exitItem.Click += (s, e) =>
        {
            Dispatcher.Invoke(() => ShutdownApp());
        };
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    public void ToggleFlyout()
    {
        if (_flyout == null) return;

        // Prevent toggle-flicker race condition
        if (!DebounceGate.CanToggle())
        {
            return;
        }

        if (_flyout.IsVisible)
        {
            _flyout.Hide();
        }
        else
        {
            _flyout.ShowFlyout();
        }
    }

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        if (_pollingManager == null || _client == null) return;

        try
        {
            await _pollingManager.RunAsync(async ct =>
            {
                try
                {
                    var snapshot = await _client.FetchSnapshotAsync(forceRefresh: false, ct);
                    Dispatcher.Invoke(() =>
                    {
                        _latestSnapshot = snapshot;
                        _viewModel?.UpdateFromSnapshot(snapshot);
                        _hudViewModel?.UpdateFromSnapshot(snapshot);
                        _aboutViewModel?.UpdateState(_client.CachedEndpoint, snapshot, snapshot?.UpdatedAt);
                        if (_notifyIcon != null && _viewModel != null)
                        {
                            _notifyIcon.Text = TruncateTooltip(_viewModel.TrayTooltipText);
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    // Clean cancellation
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Polling Error]: {ex.Message}");
                }
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on application shutdown
        }
    }

    private void ShowAboutDialog()
    {
        if (_aboutViewModel == null) return;

        var endpoint = _client?.CachedEndpoint ?? ServerDiscovery.DiscoverActiveServer();
        _aboutViewModel.UpdateState(endpoint, _latestSnapshot, _latestSnapshot?.UpdatedAt);

        if (_aboutWindow == null || !_aboutWindow.IsLoaded)
        {
            _aboutWindow = new AboutWindow(_aboutViewModel);
            _aboutWindow.Closed += (s, e) => _aboutWindow = null;
            _aboutWindow.Show();
        }
        else
        {
            if (_aboutWindow.WindowState == WindowState.Minimized)
            {
                _aboutWindow.WindowState = WindowState.Normal;
            }
            _aboutWindow.Activate();
            _aboutWindow.Focus();
        }
    }

    private async Task RunBackgroundUpdateCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            if (cancellationToken.IsCancellationRequested || _updateChecker == null) return;

            var result = await _updateChecker.CheckForUpdatesAsync(force: false, cancellationToken);
            if (result.IsUpdateAvailable && !string.IsNullOrWhiteSpace(result.LatestVersion))
            {
                Dispatcher.Invoke(() =>
                {
                    PromptUpdateAvailable(result);
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on application shutdown
        }
        catch (Exception ex)
        {
            // Fail silently in background
            Debug.WriteLine($"[UpdateChecker Background Error]: {ex.Message}");
        }
    }

    public async Task CheckForUpdatesManualAsync()
    {
        if (_updateChecker == null) return;

        try
        {
            var result = await _updateChecker.CheckForUpdatesAsync(force: true);
            Dispatcher.Invoke(() =>
            {
                var loc = LocalizationManager.Instance;
                if (result.IsUpdateAvailable && !string.IsNullOrWhiteSpace(result.LatestVersion))
                {
                    PromptUpdateAvailable(result);
                }
                else if (result.IsUpToDate)
                {
                    MessageBox.Show(
                        loc.FormatUpToDateMessage(result.CurrentVersion),
                        loc.UpdateDialogTitleUpToDate,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        loc.UpdateDialogFailedMessage,
                        loc.UpdateDialogTitleFailed,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            });
        }
        catch
        {
            var loc = LocalizationManager.Instance;
            MessageBox.Show(
                loc.UpdateDialogFailedMessage,
                loc.UpdateDialogTitleFailed,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void PromptUpdateAvailable(UpdateCheckResult result)
    {
        var loc = LocalizationManager.Instance;
        var newVer = result.LatestVersion ?? "Unknown";
        var message = loc.FormatUpdateAvailableMessage(newVer);
        var res = MessageBox.Show(
            message,
            loc.UpdateDialogTitleAvailable,
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (res == MessageBoxResult.Yes)
        {
            string url = !string.IsNullOrWhiteSpace(result.DownloadUrl)
                ? result.DownloadUrl
                : (!string.IsNullOrWhiteSpace(result.ReleasePageUrl) ? result.ReleasePageUrl : "https://github.com/Fuheshka/AntigravityQuota/releases");

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateChecker Open Browser Error]: {ex.Message}");
            }
        }
    }

    public static void ShutdownApplication()
    {
        if (Current is App app)
        {
            app.ShutdownApp();
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private void ShutdownApp()
    {
        _cts?.Cancel();
        _hotkeyManager?.Dispose();
        _tracker?.Dispose();
        _updateChecker?.Dispose();
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        if (_flyout != null)
        {
            _flyout.IsExplicitShutdown = true;
            _flyout.Close();
        }
        if (_hudWindow != null)
        {
            _hudWindow.IsExplicitShutdown = true;
            _hudWindow.Close();
        }
        _aboutWindow?.Close();
        Current.Shutdown();
        Environment.Exit(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts?.Cancel();
        _hotkeyManager?.Dispose();
        _tracker?.Dispose();
        _updateChecker?.Dispose();
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        _hudWindow?.Close();
        _aboutWindow?.Close();
        base.OnExit(e);
    }
}
