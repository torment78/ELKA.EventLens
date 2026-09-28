using ELKA.EventLens.App.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ELKA.EventLens.App.Views;

public partial class NotificationPopupWindow : Window
{
    private bool _allowClose;

    public NotificationPopupWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionNearWorkArea();
        SizeChanged += (_, _) => PositionNearWorkArea();
    }

    public void PositionNearWorkArea()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 18;
        Top = workArea.Bottom - ActualHeight - 18;
    }

    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2
            && DataContext is NotificationPopupViewModel viewModel
            && viewModel.OpenMainCommand.CanExecute(null))
        {
            viewModel.OpenMainCommand.Execute(null);
        }
    }

    private void NotificationCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NotificationItemViewModel item }
            && item.ToggleCommand.CanExecute(null))
        {
            item.ToggleCommand.Execute(null);
        }
    }
}
