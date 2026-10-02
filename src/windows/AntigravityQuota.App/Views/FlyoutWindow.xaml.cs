using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using AntigravityQuota.App.Services;
using AntigravityQuota.Core.ViewModels;
using Wpf.Ui.Controls;

namespace AntigravityQuota.App.Views;

public partial class FlyoutWindow : FluentWindow
{
    private readonly FlyoutViewModel _viewModel;

    public FlyoutWindow(FlyoutViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Deactivated += OnDeactivated;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        App.RecordFlyoutDeactivation();
        Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Intercept close and hide instead, keeping visual state and resources cached
        e.Cancel = true;
        Hide();
    }

    public void UpdatePosition()
    {
        var pos = TaskbarPositioner.CalculatePosition(this, centerOverCursor: true);
        Left = pos.X;
        Top = pos.Y;
    }

    public void ShowFlyout()
    {
        UpdatePosition();
        Show();

        // Ensure window grabs foreground focus over other apps
        var helper = new WindowInteropHelper(this);
        if (helper.Handle != nint.Zero)
        {
            Win32Interop.SetForegroundWindow(helper.Handle);
        }

        Activate();
        Focus();
    }
}
