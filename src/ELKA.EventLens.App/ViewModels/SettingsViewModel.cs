using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using System.Windows.Input;

namespace ELKA.EventLens.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly Action? _testNotification;
    private readonly Action<string, bool>? _openAiLogin;
    private bool _enableNotifications;
    private bool _startMinimized;
    private bool _monitorApplication;
    private bool _monitorSystem;
    private bool _monitorSecurity;
    private bool _showCritical;
    private bool _showErrors;
    private bool _showWarnings;
    private bool _loadRecentOnStartup;
    private int _maxStoredEvents;
    private int _recentEventsToLoad;
    private string _selectedAiProvider;
    private bool _webBrowserDarkMode;
    private string _searchEngine;
    private bool _useAdvancedAiPrompt;
    private string _advancedAiPrompt;
    public AppearanceSettings Appearance { get; set; }

    public SettingsViewModel(
        AppSettings settings,
        Action? testNotification = null,
        Action<string, bool>? openAiLogin = null,
        IAppUpdateService? updateService = null)
    {
        _testNotification = testNotification;
        _openAiLogin = openAiLogin;
        Updates = new UpdateViewModel(updateService ?? new AppUpdateService());
        _enableNotifications = settings.EnableNotifications;
        _startMinimized = settings.StartMinimized;
        _monitorApplication = settings.MonitorApplication;
        _monitorSystem = settings.MonitorSystem;
        _monitorSecurity = settings.MonitorSecurity;
        _showCritical = settings.ShowCritical;
        _showErrors = settings.ShowErrors;
        _showWarnings = settings.ShowWarnings;
        _maxStoredEvents = settings.MaxStoredEvents;
        _loadRecentOnStartup = settings.LoadRecentOnStartup;
        _recentEventsToLoad = settings.RecentEventsToLoad;
        _selectedAiProvider = string.IsNullOrWhiteSpace(settings.SelectedAiProvider)
            ? "ChatGPT"
            : settings.SelectedAiProvider;
        _webBrowserDarkMode = settings.WebBrowserDarkMode;
        _selectedAiProvider = SearchUrlService.NormalizeAiProvider(_selectedAiProvider);
        _searchEngine = SearchUrlService.NormalizeSearchEngine(settings.SearchEngine);
        _useAdvancedAiPrompt = settings.UseAdvancedAiPrompt;
        _advancedAiPrompt = string.IsNullOrWhiteSpace(settings.AdvancedAiPrompt)
            ? SearchUrlService.DefaultAiPromptTemplate : settings.AdvancedAiPrompt;
        Appearance = (settings.Appearance ?? new()).Copy();

        TestNotificationCommand = new RelayCommand(_ => _testNotification?.Invoke());
        OpenAiLoginCommand = new RelayCommand(_ => _openAiLogin?.Invoke(SelectedAiProvider, WebBrowserDarkMode));
        ResetAiPromptCommand = new RelayCommand(_ => AdvancedAiPrompt = SearchUrlService.DefaultAiPromptTemplate);
    }

    public IReadOnlyList<string> AiProviderOptions => SearchUrlService.AiProviders;
    public UpdateViewModel Updates { get; }
    public IReadOnlyList<string> SearchEngineOptions => SearchUrlService.SearchEngines;
    public IReadOnlyList<string> PromptFields { get; } = ["{LogName}", "{Provider}", "{EventId}", "{Level}", "{Severity}", "{Machine}", "{Time}", "{Message}"];
    public ICommand ResetAiPromptCommand { get; }

    public string SearchEngine
    {
        get => _searchEngine;
        set => SetProperty(ref _searchEngine, SearchUrlService.NormalizeSearchEngine(value));
    }

    public bool UseAdvancedAiPrompt
    {
        get => _useAdvancedAiPrompt;
        set => SetProperty(ref _useAdvancedAiPrompt, value);
    }

    public string AdvancedAiPrompt
    {
        get => _advancedAiPrompt;
        set => SetProperty(ref _advancedAiPrompt, value);
    }

    public ICommand TestNotificationCommand { get; }

    public ICommand OpenAiLoginCommand { get; }

    public bool EnableNotifications
    {
        get => _enableNotifications;
        set => SetProperty(ref _enableNotifications, value);
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        set => SetProperty(ref _startMinimized, value);
    }

    public bool MonitorApplication
    {
        get => _monitorApplication;
        set => SetProperty(ref _monitorApplication, value);
    }

    public bool MonitorSystem
    {
        get => _monitorSystem;
        set => SetProperty(ref _monitorSystem, value);
    }

    public bool MonitorSecurity
    {
        get => _monitorSecurity;
        set => SetProperty(ref _monitorSecurity, value);
    }

    public bool ShowCritical
    {
        get => _showCritical;
        set => SetProperty(ref _showCritical, value);
    }

    public bool ShowErrors
    {
        get => _showErrors;
        set => SetProperty(ref _showErrors, value);
    }

    public bool ShowWarnings
    {
        get => _showWarnings;
        set => SetProperty(ref _showWarnings, value);
    }

    public bool LoadRecentOnStartup
    {
        get => _loadRecentOnStartup;
        set => SetProperty(ref _loadRecentOnStartup, value);
    }

    public int MaxStoredEvents
    {
        get => _maxStoredEvents;
        set => SetProperty(ref _maxStoredEvents, value);
    }

    public int RecentEventsToLoad
    {
        get => _recentEventsToLoad;
        set => SetProperty(ref _recentEventsToLoad, value);
    }

    public string SelectedAiProvider
    {
        get => _selectedAiProvider;
        set => SetProperty(ref _selectedAiProvider, string.IsNullOrWhiteSpace(value) ? "ChatGPT" : value);
    }

    public bool WebBrowserDarkMode
    {
        get => _webBrowserDarkMode;
        set => SetProperty(ref _webBrowserDarkMode, value);
    }

    public AppSettings ToSettings()
    {
        return new AppSettings
        {
            EnableNotifications = EnableNotifications,
            StartMinimized = StartMinimized,
            MonitorApplication = MonitorApplication,
            MonitorSystem = MonitorSystem,
            MonitorSecurity = MonitorSecurity,
            ShowCritical = ShowCritical,
            ShowErrors = ShowErrors,
            ShowWarnings = ShowWarnings,
            MaxStoredEvents = Math.Clamp(MaxStoredEvents, 50, 5000),
            LoadRecentOnStartup = LoadRecentOnStartup,
            RecentEventsToLoad = Math.Clamp(RecentEventsToLoad, 25, 1000),
            SelectedAiProvider = SelectedAiProvider,
            WebBrowserDarkMode = WebBrowserDarkMode,
            SearchEngine = SearchEngine,
            UseAdvancedAiPrompt = UseAdvancedAiPrompt,
            AdvancedAiPrompt = AdvancedAiPrompt,
            Appearance = Appearance.Copy()
        };
    }
}


