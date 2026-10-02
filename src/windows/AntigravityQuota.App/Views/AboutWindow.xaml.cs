using System.Windows;
using System.Windows.Input;
using AntigravityQuota.Core.ViewModels;
using Wpf.Ui.Controls;

namespace AntigravityQuota.App.Views;

public partial class AboutWindow : FluentWindow
{
    public AboutViewModel ViewModel { get; }

    public AboutWindow(AboutViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
