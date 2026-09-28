using ELKA.EventLens.App.Services;
using ELKA.EventLens.App.ViewModels;
using ELKA.EventLens.App.Views;

namespace ELKA.EventLens.App;

public partial class App : System.Windows.Application
{
    private MainWindowViewModel? _mainViewModel;
    private NotificationService? _notificationService;
    private ActivityLogService? _activityLogService;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        AppStorageService.EnsureStorageFolders();
        _activityLogService = new ActivityLogService();
        _activityLogService.Write($"App started. Storage mode: {(AppStorageService.IsPortable ? "Portable" : "Installed")}. Base: {AppStorageService.AppBaseDirectory}");

        var settingsService = new SettingsService();
        ThemeService.Apply(settingsService.LoadSettings().Appearance);
        WindowThemeService.Initialize();
        var explanationService = new EventExplanationService();
        _notificationService = new NotificationService();
        _notificationService.ShowMainRequested += NotificationService_ShowMainRequested;
        _notificationService.ExitRequested += NotificationService_ExitRequested;

        _mainViewModel = new MainWindowViewModel(
            settingsService,
            new WindowsEventLogService(),
            explanationService,
            new SearchUrlService(),
            _notificationService);

        MainWindow = new MainWindow
        {
            DataContext = _mainViewModel
        };

        MainWindow.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _activityLogService?.Write("App exiting.");

        if (_notificationService is not null)
        {
            _notificationService.ShowMainRequested -= NotificationService_ShowMainRequested;
            _notificationService.ExitRequested -= NotificationService_ExitRequested;
        }

        _mainViewModel?.Dispose();
        base.OnExit(e);
    }

    private void NotificationService_ShowMainRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(ShowMainWindow);
    }

    private void NotificationService_ExitRequested(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.AllowClose();
            }

            Shutdown();
        });
    }

    private void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        if (!MainWindow.IsVisible)
        {
            MainWindow.Show();
        }

        if (MainWindow.WindowState == System.Windows.WindowState.Minimized)
        {
            MainWindow.WindowState = System.Windows.WindowState.Normal;
        }

        MainWindow.Activate();
        MainWindow.Topmost = true;
        MainWindow.Topmost = false;
        MainWindow.Focus();
    }
}

