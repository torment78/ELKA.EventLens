namespace ELKA.EventLens.App.Models;

public sealed class AppSettings
{
    public bool EnableNotifications { get; set; }

    public bool StartMinimized { get; set; }

    public bool MonitorApplication { get; set; } = true;

    public bool MonitorSystem { get; set; } = true;

    public bool MonitorSecurity { get; set; }

    public bool ShowCritical { get; set; } = true;

    public bool ShowErrors { get; set; } = true;

    public bool ShowWarnings { get; set; } = true;

    public int MaxStoredEvents { get; set; } = 500;

    public bool LoadRecentOnStartup { get; set; } = true;

    public int RecentEventsToLoad { get; set; } = 150;

    public string SelectedAiProvider { get; set; } = "ChatGPT";

    public bool WebBrowserDarkMode { get; set; } = true;

    public string SearchEngine { get; set; } = "Bing";

    public bool UseAdvancedAiPrompt { get; set; }

    public string AdvancedAiPrompt { get; set; } = string.Empty;

    public AppearanceSettings Appearance { get; set; } = new();

    public AppSettings Copy()
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
            MaxStoredEvents = MaxStoredEvents,
            LoadRecentOnStartup = LoadRecentOnStartup,
            RecentEventsToLoad = RecentEventsToLoad,
            SelectedAiProvider = SelectedAiProvider,
            WebBrowserDarkMode = WebBrowserDarkMode,
            SearchEngine = SearchEngine,
            UseAdvancedAiPrompt = UseAdvancedAiPrompt,
            AdvancedAiPrompt = AdvancedAiPrompt,
            Appearance = (Appearance ?? new()).Copy()
        };
    }
}

