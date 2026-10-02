using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AntigravityQuota.App.Services;
using AntigravityQuota.Core;
using AntigravityQuota.Core.ViewModels;
using Wpf.Ui.Controls;

namespace AntigravityQuota.App.Views;

public partial class QuotaHudWindow : FluentWindow
{
    private readonly HudViewModel _viewModel;
    private readonly WindowContextTracker? _tracker;
    private readonly GlobalHotkeyManager? _hotkeyManager;
    private nint _hwnd = nint.Zero;
    private bool _isInitialized = false;
    private bool _isAltPressed = false;
    private bool _deferRestoreClickThrough = false;
    private bool _isFadingOut = false;

    public QuotaHudWindow(
        HudViewModel viewModel,
        WindowContextTracker? tracker = null,
        GlobalHotkeyManager? hotkeyManager = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _tracker = tracker;
        _hotkeyManager = hotkeyManager;
        DataContext = _viewModel;

        UpdateLayoutForMode(_viewModel.IsPillMode);

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(HudViewModel.IsPillMode))
            {
                UpdateLayoutForMode(_viewModel.IsPillMode);
            }
            else if (e.PropertyName == nameof(HudViewModel.ClickThroughEnabled))
            {
                UpdateClickThrough();
            }
            else if (e.PropertyName == nameof(HudViewModel.AutoHideEnabled))
            {
                if (_tracker != null)
                {
                    _tracker.AutoHideEnabled = _viewModel.AutoHideEnabled;
                }
                if (!_viewModel.AutoHideEnabled)
                {
                    AnimateFadeIn();
                }
                else if (_tracker != null && !_tracker.IsTargetActive)
                {
                    AnimateFadeOut();
                }
            }
            else if (e.PropertyName == nameof(HudViewModel.HotkeysEnabled))
            {
                if (_hotkeyManager != null)
                {
                    _hotkeyManager.IsEnabled = _viewModel.HotkeysEnabled;
                }
            }
        };

        if (_tracker != null)
        {
            _tracker.VisibilityChanged += (s, visible) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (visible)
                    {
                        AnimateFadeIn();
                    }
                    else
                    {
                        AnimateFadeOut();
                    }
                });
            };

            _tracker.AltStateChanged += (s, isAlt) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _isAltPressed = isAlt;
                    UpdateClickThrough();
                });
            };
        }
    }

    public HudViewModel ViewModel => _viewModel;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        if (helper.Handle != nint.Zero)
        {
            _hwnd = helper.Handle;

            // 1. Apply Win32 Extended Styles: ToolWindow, TopMost, NoActivate
            Win32Interop.ApplyHudWindowStyles(_hwnd);

            // 2. Apply Win32 DWM Attributes: Mica / Acrylic backdrop and round corners
            Win32Interop.ApplyHudWindowAttributes(_hwnd);

            // 3. Intercept WM_MOUSEACTIVATE to prevent stealing keyboard focus from active IDE
            var source = HwndSource.FromHwnd(_hwnd);
            source?.AddHook(WndProc);

            // 4. Hook global hotkeys (WM_HOTKEY) and register Alt+Shift+Q / M / R
            if (_hotkeyManager != null)
            {
                source?.AddHook(_hotkeyManager.HookCallback);
                if (_viewModel.HotkeysEnabled)
                {
                    _hotkeyManager.RegisterDefaultHotkeys(_hwnd);
                }
            }

            // 5. Initialize click-through state
            UpdateClickThrough();
        }

        // 5. Restore saved position or default to top-right corner
        RestorePosition();
        _isInitialized = true;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Win32Interop.WM_MOUSEACTIVATE)
        {
            // Signal to Windows: do not activate window and do not eat the click
            handled = true;
            return Win32Interop.MA_NOACTIVATE;
        }

        return nint.Zero;
    }

    private void UpdateLayoutForMode(bool isPill)
    {
        if (isPill)
        {
            SizeToContent = SizeToContent.Manual;
            Width = 260;
            Height = 40;
        }
        else
        {
            Width = 320;
            Height = double.NaN;
            SizeToContent = SizeToContent.Height;
        }
    }

    private void RestorePosition()
    {
        double screenLeft = SystemParameters.VirtualScreenLeft;
        double screenTop = SystemParameters.VirtualScreenTop;
        double screenWidth = SystemParameters.VirtualScreenWidth;
        double screenHeight = SystemParameters.VirtualScreenHeight;

        if (_viewModel.WindowX.HasValue && _viewModel.WindowY.HasValue)
        {
            var clamped = HudSettingsManager.ClampPosition(
                _viewModel.WindowX.Value,
                _viewModel.WindowY.Value,
                Width > 0 ? Width : 260,
                Height > 0 ? Height : 40,
                screenLeft,
                screenTop,
                screenWidth,
                screenHeight);

            Left = clamped.X;
            Top = clamped.Y;
        }
        else
        {
            // Default position: top-right corner of primary work area
            Left = SystemParameters.WorkArea.Right - (Width > 0 ? Width : 260) - 24;
            Top = SystemParameters.WorkArea.Top + 36;
            _viewModel.SavePosition(Left, Top);
        }
    }

    private void UpdateClickThrough()
    {
        if (_hwnd == nint.Zero) return;

        if (!_viewModel.ClickThroughEnabled)
        {
            Win32Interop.SetClickThrough(_hwnd, false);
            return;
        }

        if (_isAltPressed)
        {
            // Alt is held down: temporarily remove WS_EX_TRANSPARENT so user can drag or interact
            Win32Interop.SetClickThrough(_hwnd, false);
        }
        else
        {
            // Alt released: if mouse left button is currently pressed (dragging), defer restoring click-through
            if (Mouse.LeftButton == MouseButtonState.Pressed)
            {
                _deferRestoreClickThrough = true;
                return;
            }

            Win32Interop.SetClickThrough(_hwnd, true);
        }
    }

    public void AnimateFadeIn()
    {
        if (!_viewModel.IsEnabled) return;

        _isFadingOut = false;
        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
        }

        var anim = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(OpacityProperty, anim);
    }

    public void AnimateFadeOut()
    {
        if (!_viewModel.IsEnabled) return;

        _isFadingOut = true;
        var anim = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        anim.Completed += (s, e) =>
        {
            if (_isFadingOut && Opacity <= 0.01)
            {
                Visibility = Visibility.Hidden;
            }
        };

        BeginAnimation(OpacityProperty, anim);
    }

    private void OnRootMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // If clicking on a button, allow button click to execute normally
        if (e.OriginalSource is DependencyObject dep && FindVisualParent<ButtonBase>(dep) != null)
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            double startLeft = Left;
            double startTop = Top;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Suppress mouse state race conditions
            }
            finally
            {
                if (_deferRestoreClickThrough && !_isAltPressed)
                {
                    _deferRestoreClickThrough = false;
                    Win32Interop.SetClickThrough(_hwnd, true);
                }
            }

            // If dragged significantly, persist new coordinates
            if (Math.Abs(Left - startLeft) > 2 || Math.Abs(Top - startTop) > 2)
            {
                _viewModel.SavePosition(Left, Top);
            }
            else if (_viewModel.IsPillMode)
            {
                // Single click on the pill without dragging expands back to card mode
                _viewModel.IsPillMode = false;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent) return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Keep window instance cached in memory; hide instead of closing
        e.Cancel = true;
        Hide();
        _viewModel.IsEnabled = false;
    }

    public void ShowHud()
    {
        _viewModel.IsEnabled = true;
        if (!_isInitialized)
        {
            RestorePosition();
        }
        AnimateFadeIn();
    }

    public void HideHud()
    {
        _viewModel.IsEnabled = false;
        AnimateFadeOut();
    }
}
