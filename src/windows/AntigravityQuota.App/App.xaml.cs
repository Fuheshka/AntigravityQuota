using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using AntigravityQuota.App.Views;
using AntigravityQuota.Core.ViewModels;
using AntigravityQuota.Core.Models;
using AntigravityQuota.Core;
using Wpf.Ui.Tray.Controls;

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
    private NotifyIcon? _notifyIcon;

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
        _updateChecker = new UpdateChecker();
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
                            _notifyIcon.TooltipText = _viewModel.TrayTooltipText;
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

        // 4. Initialize System Tray Icon
        SetupNotifyIcon(loc);

        // 5. Start background polling loop
        _ = RunPollingLoopAsync(_cts.Token);

        // 6. Start silent background update check after 3 seconds (24h cooldown)
        _ = RunBackgroundUpdateCheckAsync(_cts.Token);
    }

    private void SetupNotifyIcon(LocalizationManager loc)
    {
        _notifyIcon = new NotifyIcon
        {
            FocusOnLeftClick = true,
            MenuOnRightClick = true,
            TooltipText = _viewModel?.TrayTooltipText ?? "AntigravityQuota"
        };

        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/app-icon.ico", UriKind.Absolute);
            _notifyIcon.Icon = BitmapFrame.Create(iconUri);
        }
        catch
        {
            // Fallback if ico resource fails to load
        }

        // Left click on tray toggles the FlyoutWindow
#pragma warning disable CS8622
        _notifyIcon.LeftClick += (s, e) => ToggleFlyout();
#pragma warning restore CS8622

        // Context menu on right click
        var contextMenu = new ContextMenu();

        // 1. Включить HUD (Checkable)
        var hudItem = new MenuItem
        {
            Header = loc.MenuEnableHUD,
            InputGestureText = loc.HotkeyHintToggleHud,
            IsCheckable = true,
            IsChecked = _hudViewModel?.IsEnabled ?? true
        };
        hudItem.Click += (s, e) =>
        {
            if (_hudWindow != null && _hudViewModel != null)
            {
                if (hudItem.IsChecked)
                {
                    _hudWindow.ShowHud();
                }
                else
                {
                    _hudWindow.HideHud();
                }
            }
        };
        contextMenu.Items.Add(hudItem);

        // 2. Режим таблетки (Checkable)
        var pillItem = new MenuItem
        {
            Header = loc.MenuCompactPillMode,
            InputGestureText = loc.HotkeyHintTogglePill,
            IsCheckable = true,
            IsChecked = _hudViewModel?.IsPillMode ?? false
        };
        pillItem.Click += (s, e) =>
        {
            if (_hudViewModel != null)
            {
                _hudViewModel.IsPillMode = pillItem.IsChecked;
            }
        };
        contextMenu.Items.Add(pillItem);

        // 3. Автоскрытие HUD (Checkable)
        var autoHideItem = new MenuItem
        {
            Header = loc.MenuAutoHideHUD,
            IsCheckable = true,
            IsChecked = _hudViewModel?.AutoHideEnabled ?? true
        };
        autoHideItem.Click += (s, e) =>
        {
            if (_hudViewModel != null)
            {
                _hudViewModel.AutoHideEnabled = autoHideItem.IsChecked;
            }
        };
        contextMenu.Items.Add(autoHideItem);

        // 4. Сквозной клик (Checkable)
        var clickThroughItem = new MenuItem
        {
            Header = loc.MenuClickThrough,
            IsCheckable = true,
            IsChecked = _hudViewModel?.ClickThroughEnabled ?? true
        };
        clickThroughItem.Click += (s, e) =>
        {
            if (_hudViewModel != null)
            {
                _hudViewModel.ClickThroughEnabled = clickThroughItem.IsChecked;
            }
        };
        contextMenu.Items.Add(clickThroughItem);

        // 5. Глобальные горячие клавиши (Checkable)
        var hotkeysItem = new MenuItem
        {
            Header = loc.MenuGlobalHotkeys,
            IsCheckable = true,
            IsChecked = _hudViewModel?.HotkeysEnabled ?? true
        };
        hotkeysItem.Click += (s, e) =>
        {
            if (_hudViewModel != null)
            {
                _hudViewModel.HotkeysEnabled = hotkeysItem.IsChecked;
            }
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
                        pillItem.IsChecked = _hudViewModel.IsPillMode;
                    }
                    else if (e.PropertyName == nameof(HudViewModel.IsEnabled))
                    {
                        hudItem.IsChecked = _hudViewModel.IsEnabled;
                    }
                    else if (e.PropertyName == nameof(HudViewModel.AutoHideEnabled))
                    {
                        autoHideItem.IsChecked = _hudViewModel.AutoHideEnabled;
                    }
                    else if (e.PropertyName == nameof(HudViewModel.ClickThroughEnabled))
                    {
                        clickThroughItem.IsChecked = _hudViewModel.ClickThroughEnabled;
                    }
                    else if (e.PropertyName == nameof(HudViewModel.HotkeysEnabled))
                    {
                        hotkeysItem.IsChecked = _hudViewModel.HotkeysEnabled;
                    }
                });
            };
        }

        contextMenu.Items.Add(new Separator());

        // 6. Запускать при старте Windows (Checkable)
        var startupItem = new MenuItem
        {
            Header = loc.MenuLaunchAtStartup,
            IsCheckable = true,
            IsChecked = _startupManager?.IsEnabled() ?? false
        };
        startupItem.Click += (s, e) =>
        {
            if (_startupManager != null)
            {
                bool newState = startupItem.IsChecked;
                bool success = _startupManager.SetEnabled(newState);
                if (!success)
                {
                    // Revert checkbox state if registry write failed
                    startupItem.IsChecked = _startupManager.IsEnabled();
                }
            }
        };
        contextMenu.Items.Add(startupItem);

        contextMenu.Items.Add(new Separator());

        // 7. Обновить квоты
        var refreshItem = new MenuItem
        {
            Header = loc.MenuRefreshNow,
            InputGestureText = loc.HotkeyHintRefresh
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
        var updateItem = new MenuItem
        {
            Header = loc.MenuCheckUpdates
        };
        updateItem.Click += async (s, e) =>
        {
            await CheckForUpdatesManualAsync();
        };
        contextMenu.Items.Add(updateItem);

        // 4. Настройки
        var settingsItem = new MenuItem
        {
            Header = loc.MenuSettings
        };
        settingsItem.Click += (s, e) =>
        {
            ToggleFlyout();
        };
        contextMenu.Items.Add(settingsItem);

        // 5. О программе
        var aboutItem = new MenuItem
        {
            Header = loc.MenuAbout
        };
        aboutItem.Click += (s, e) =>
        {
            ShowAboutDialog();
        };
        contextMenu.Items.Add(aboutItem);

        contextMenu.Items.Add(new Separator());

        // 6. Выход
        var exitItem = new MenuItem
        {
            Header = loc.MenuExit
        };
        exitItem.Click += (s, e) =>
        {
            ShutdownApp();
        };
        contextMenu.Items.Add(exitItem);

        _notifyIcon.Menu = contextMenu;
        _notifyIcon.Register();
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
                            _notifyIcon.TooltipText = _viewModel.TrayTooltipText;
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

    private void ShutdownApp()
    {
        _cts?.Cancel();
        _hotkeyManager?.Dispose();
        _tracker?.Dispose();
        _updateChecker?.Dispose();
        _notifyIcon?.Unregister();
        _flyout?.Close();
        _hudWindow?.Close();
        _aboutWindow?.Close();
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts?.Cancel();
        _hotkeyManager?.Dispose();
        _tracker?.Dispose();
        _updateChecker?.Dispose();
        _notifyIcon?.Unregister();
        _hudWindow?.Close();
        _aboutWindow?.Close();
        base.OnExit(e);
    }
}
