using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using ELKA.EventLens.App.Views;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace ELKA.EventLens.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly WindowsEventLogService _eventLogService;
    private readonly EventExplanationService _explanationService;
    private readonly SearchUrlService _searchUrlService;
    private readonly NotificationService _notificationService;
    private readonly HashSet<string> _seenEvents = new(StringComparer.Ordinal);
    private readonly ObservableCollection<EventCardViewModel> _events = [];
    private readonly Dictionary<string, SearchTabViewModel> _searchTabCache = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loadCancellation;
    private AppSettings _settings;
    private SearchTabViewModel? _selectedSearchTab;
    private bool _initialized;
    private bool _disposed;
    private bool _isLoading;
    private bool _isPaused;
    private bool _isSearchPaneOpen;
    private bool _showErrors;
    private bool _showWarnings;
    private bool _showCritical;
    private bool _monitorApplication;
    private bool _monitorSystem;
    private bool _monitorSecurity;
    private bool _enableNotifications;
    private bool _startMinimized;
    private string _searchText = string.Empty;
    private string _statusText = "Ready";

    public MainWindowViewModel(
        SettingsService settingsService,
        WindowsEventLogService eventLogService,
        EventExplanationService explanationService,
        SearchUrlService searchUrlService,
        NotificationService notificationService)
    {
        _settingsService = settingsService;
        _eventLogService = eventLogService;
        _explanationService = explanationService;
        _searchUrlService = searchUrlService;
        _notificationService = notificationService;
        _settings = _settingsService.LoadSettings();
        ThemeService.Apply(_settings.Appearance);

        ApplySettingsToFields(_settings);

        FilteredEvents = CollectionViewSource.GetDefaultView(_events);
        FilteredEvents.Filter = MatchesFilter;

        LoadRecentCommand = new AsyncRelayCommand(_ => LoadRecentAsync(), _ => !IsLoading);
        ClearListCommand = new RelayCommand(_ => ClearList());
        PauseLiveCommand = new RelayCommand(_ => TogglePause());
        OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        CloseSearchPaneCommand = new RelayCommand(_ => IsSearchPaneOpen = false, _ => SearchTabs.Count > 0);
        CloseSearchTabCommand = new RelayCommand(parameter => CloseSearchTab(parameter as SearchTabViewModel));
        OpenSelectedTabExternalCommand = new RelayCommand(_ => OpenSelectedTabExternal(), _ => SelectedSearchTab is not null);

        _eventLogService.EventReceived += OnEventReceived;
        _eventLogService.StatusChanged += OnStatusChanged;
    }

    public ICollectionView FilteredEvents { get; }

    public ObservableCollection<SearchTabViewModel> SearchTabs { get; } = [];

    public ICommand LoadRecentCommand { get; }

    public ICommand ClearListCommand { get; }

    public ICommand PauseLiveCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand CloseSearchPaneCommand { get; }

    public ICommand CloseSearchTabCommand { get; }

    public ICommand OpenSelectedTabExternalCommand { get; }

    public SearchTabViewModel? SelectedSearchTab
    {
        get => _selectedSearchTab;
        set
        {
            if (SetProperty(ref _selectedSearchTab, value) && OpenSelectedTabExternalCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSearchPaneOpen
    {
        get => _isSearchPaneOpen;
        private set
        {
            if (SetProperty(ref _isSearchPaneOpen, value))
            {
                OnPropertyChanged(nameof(SearchPaneColumnWidth));
                OnPropertyChanged(nameof(SearchPaneSplitterWidth));
                OnPropertyChanged(nameof(SearchPaneVisibility));
            }
        }
    }

    public GridLength SearchPaneColumnWidth => IsSearchPaneOpen && SearchTabs.Count > 0
        ? new GridLength(560)
        : new GridLength(0);

    public GridLength SearchPaneSplitterWidth => IsSearchPaneOpen && SearchTabs.Count > 0
        ? new GridLength(8)
        : new GridLength(0);

    public Visibility SearchPaneVisibility => IsSearchPaneOpen && SearchTabs.Count > 0
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value) && LoadRecentCommand is AsyncRelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                _eventLogService.IsPaused = value;
                OnPropertyChanged(nameof(PauseButtonText));
                StatusText = value ? "Live monitoring paused." : "Live monitoring active.";
            }
        }
    }

    public string PauseButtonText => IsPaused ? "Resume live" : "Pause live";

    public bool ShowErrors
    {
        get => _showErrors;
        set => SetFilterSetting(ref _showErrors, value, setting => setting.ShowErrors = value);
    }

    public bool ShowWarnings
    {
        get => _showWarnings;
        set => SetFilterSetting(ref _showWarnings, value, setting => setting.ShowWarnings = value);
    }

    public bool ShowCritical
    {
        get => _showCritical;
        set => SetFilterSetting(ref _showCritical, value, setting => setting.ShowCritical = value);
    }

    public bool MonitorApplication
    {
        get => _monitorApplication;
        set => SetMonitorSetting(ref _monitorApplication, value, setting => setting.MonitorApplication = value);
    }

    public bool MonitorSystem
    {
        get => _monitorSystem;
        set => SetMonitorSetting(ref _monitorSystem, value, setting => setting.MonitorSystem = value);
    }

    public bool MonitorSecurity
    {
        get => _monitorSecurity;
        set => SetMonitorSetting(ref _monitorSecurity, value, setting => setting.MonitorSecurity = value);
    }

    public bool EnableNotifications
    {
        get => _enableNotifications;
        set
        {
            if (SetProperty(ref _enableNotifications, value))
            {
                _settings.EnableNotifications = value;
                SaveSettings();
            }
        }
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        private set => SetProperty(ref _startMinimized, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilteredEvents.Refresh();
                OnPropertyChanged(nameof(VisibleEventCount));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public int VisibleEventCount => FilteredEvents.Cast<object>().Count();

    public int TotalEventCount => _events.Count;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        LoadHistory();

        if (_settings.LoadRecentOnStartup)
        {
            await LoadRecentAsync();
        }

        RestartWatching();
    }

    public void SaveState()
    {
        SaveSettings();
        _settingsService.SaveHistory(_events.Select(card => card.Entry), _settings.MaxStoredEvents);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _loadCancellation?.Cancel();
        SaveState();
        _eventLogService.EventReceived -= OnEventReceived;
        _eventLogService.StatusChanged -= OnStatusChanged;
        _eventLogService.Dispose();
        _notificationService.Dispose();
        _loadCancellation?.Dispose();
    }

    private async Task LoadRecentAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var token = _loadCancellation.Token;

        try
        {
            IsLoading = true;
            StatusText = "Loading recent events...";

            var entries = await _eventLogService.LoadRecentEventsAsync(
                GetSelectedLogs(),
                GetSelectedSeverities(),
                _settings.RecentEventsToLoad,
                token);

            foreach (var entry in entries.OrderBy(entry => entry.TimeCreated ?? DateTime.MinValue))
            {
                AddEvent(entry, insertAtTop: true);
            }

            StatusText = $"Loaded recent events. Showing {VisibleEventCount} of {_events.Count}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Recent event load canceled.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadHistory()
    {
        foreach (var entry in _settingsService.LoadHistory()
                     .OrderByDescending(entry => entry.TimeCreated ?? DateTime.MinValue)
                     .Take(_settings.MaxStoredEvents))
        {
            AddEvent(entry, insertAtTop: false);
        }
    }

    private void AddEvent(EventLogEntry entry, bool insertAtTop)
    {
        if (!_seenEvents.Add(entry.IdentityKey))
        {
            return;
        }

        var explanation = _explanationService.Explain(entry);
        var card = new EventCardViewModel(entry, explanation, OpenSearchTab, OpenAiTab);

        if (insertAtTop)
        {
            _events.Insert(0, card);
        }
        else
        {
            _events.Add(card);
        }

        EnforceMaxEvents();
        OnPropertyChanged(nameof(TotalEventCount));
        OnPropertyChanged(nameof(VisibleEventCount));
    }

    private void OpenSearchTab(SearchTarget target, EventLogEntry entry)
    {
        var key = _searchUrlService.BuildCacheKey(target, entry, _settings.SearchEngine);

        if (!_searchTabCache.TryGetValue(key, out var tab))
        {
            var url = _searchUrlService.BuildUrl(target, entry, _settings.SearchEngine);
            tab = new SearchTabViewModel(
                key,
                _searchUrlService.BuildTabTitle(target, entry),
                _searchUrlService.GetTargetLabel(target),
                _searchUrlService.BuildQuery(entry),
                url,
                preferDarkWebPages: _settings.WebBrowserDarkMode);

            _searchTabCache[key] = tab;
            SearchTabs.Add(tab);
        }

        SelectedSearchTab = tab;
        IsSearchPaneOpen = true;
        NotifySearchPaneLayoutChanged();
        StatusText = $"Opened {tab.TargetLabel} search tab.";
    }

    private void OpenAiTab(EventLogEntry entry)
    {
        var provider = _settings.SelectedAiProvider;
        var prompt = _searchUrlService.BuildAiPrompt(entry, _settings.UseAdvancedAiPrompt ? _settings.AdvancedAiPrompt : null);
        var key = _searchUrlService.BuildAiCacheKey(provider, entry, prompt);

        if (!_searchTabCache.TryGetValue(key, out var tab))
        {
            var url = _searchUrlService.BuildAiProviderUrl(provider);
            tab = new SearchTabViewModel(
                key,
                _searchUrlService.BuildAiTabTitle(provider, entry),
                _searchUrlService.GetAiProviderLabel(provider),
                prompt,
                url,
                isAiTab: true,
                preferDarkWebPages: _settings.WebBrowserDarkMode,
                autoPrompt: prompt);

            _searchTabCache[key] = tab;
            SearchTabs.Add(tab);
        }

        var copiedPrompt = TryCopyText(prompt);
        SelectedSearchTab = tab;
        IsSearchPaneOpen = true;
        NotifySearchPaneLayoutChanged();
        StatusText = copiedPrompt
            ? $"Opened {tab.TargetLabel}; sending AI prompt automatically."
            : $"Opened {tab.TargetLabel}; clipboard copy was blocked.";
    }

    private void CloseSearchTab(SearchTabViewModel? tab)
    {
        tab ??= SelectedSearchTab;
        if (tab is null)
        {
            return;
        }

        var index = SearchTabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        var wasSelected = ReferenceEquals(SelectedSearchTab, tab);
        SearchTabViewModel? nextSelection = null;

        if (wasSelected && SearchTabs.Count > 1)
        {
            nextSelection = SearchTabs[index == 0 ? 1 : index - 1];
        }

        if (wasSelected)
        {
            SelectedSearchTab = nextSelection;
        }

        _searchTabCache.Remove(tab.Key);
        SearchTabs.Remove(tab);

        if (SearchTabs.Count == 0)
        {
            IsSearchPaneOpen = false;
        }

        NotifySearchPaneLayoutChanged();
    }

    private void OpenSelectedTabExternal()
    {
        if (SelectedSearchTab is not null)
        {
            _searchUrlService.OpenUrl(SelectedSearchTab.Url);
        }
    }

    private void NotifySearchPaneLayoutChanged()
    {
        OnPropertyChanged(nameof(SearchPaneColumnWidth));
        OnPropertyChanged(nameof(SearchPaneSplitterWidth));
        OnPropertyChanged(nameof(SearchPaneVisibility));

        if (CloseSearchPaneCommand is RelayCommand closePaneCommand)
        {
            closePaneCommand.RaiseCanExecuteChanged();
        }
    }

    private void EnforceMaxEvents()
    {
        while (_events.Count > Math.Max(1, _settings.MaxStoredEvents))
        {
            var last = _events[^1];
            _seenEvents.Remove(last.Entry.IdentityKey);
            _events.RemoveAt(_events.Count - 1);
        }
    }

    private void ClearList()
    {
        _events.Clear();
        _seenEvents.Clear();
        StatusText = "Event list cleared.";
        OnPropertyChanged(nameof(TotalEventCount));
        OnPropertyChanged(nameof(VisibleEventCount));
        _settingsService.SaveHistory([], _settings.MaxStoredEvents);
    }

    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }

    private void OpenAiLogin(string provider, bool preferDarkWebPages)
    {
        var label = _searchUrlService.GetAiProviderLabel(provider);
        var window = new AiLoginWindow(label, _searchUrlService.BuildAiLoginUrl(provider), preferDarkWebPages)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        window.ShowDialog();
    }

    private static bool TryCopyText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void RestartWatching()
    {
        if (!_initialized)
        {
            return;
        }

        _eventLogService.IsPaused = IsPaused;
        _eventLogService.StartWatching(GetSelectedLogs(), GetSelectedSeverities());
    }

    private void OpenSettings()
    {
        var viewModel = new SettingsViewModel(
            _settings.Copy(),
            () => _notificationService.ShowTestNotification(),
            OpenAiLogin);
        var window = new SettingsWindow
        {
            Owner = System.Windows.Application.Current.MainWindow,
            DataContext = viewModel
        };

        if (window.ShowDialog() == true)
        {
            _settings = viewModel.ToSettings();
            foreach (var tab in SearchTabs) tab.PreferDarkWebPages = _settings.WebBrowserDarkMode;
            ApplySettingsToFields(_settings);
            FilteredEvents.Refresh();
            EnforceMaxEvents();
            SaveSettings();
            RestartWatching();
            StatusText = "Settings saved.";
            OnPropertyChanged(nameof(TotalEventCount));
            OnPropertyChanged(nameof(VisibleEventCount));
        }
        ThemeService.Apply(_settings.Appearance);
    }

    private bool MatchesFilter(object item)
    {
        if (item is not EventCardViewModel card)
        {
            return false;
        }

        var entry = card.Entry;
        var severityMatches = entry.Severity switch
        {
            EventSeverity.Critical => ShowCritical,
            EventSeverity.Error => ShowErrors,
            EventSeverity.Warning => ShowWarnings,
            _ => false
        };

        if (!severityMatches)
        {
            return false;
        }

        var logMatches = entry.LogName switch
        {
            "Application" => MonitorApplication,
            "System" => MonitorSystem,
            "Security" => MonitorSecurity,
            _ => true
        };

        if (!logMatches)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        var search = SearchText.Trim();
        return entry.ProviderName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.LogName.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.EventId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
            || card.Explanation.ShortSummary.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> GetSelectedLogs()
    {
        var logs = new List<string>();

        if (MonitorApplication)
        {
            logs.Add("Application");
        }

        if (MonitorSystem)
        {
            logs.Add("System");
        }

        if (MonitorSecurity)
        {
            logs.Add("Security");
        }

        return logs;
    }

    private IReadOnlyList<EventSeverity> GetSelectedSeverities()
    {
        var severities = new List<EventSeverity>();

        if (ShowCritical)
        {
            severities.Add(EventSeverity.Critical);
        }

        if (ShowErrors)
        {
            severities.Add(EventSeverity.Error);
        }

        if (ShowWarnings)
        {
            severities.Add(EventSeverity.Warning);
        }

        return severities;
    }

    private void SetFilterSetting(ref bool field, bool value, Action<AppSettings> updateSetting)
    {
        if (!SetProperty(ref field, value))
        {
            return;
        }

        updateSetting(_settings);
        FilteredEvents.Refresh();
        SaveSettings();
        RestartWatching();
        OnPropertyChanged(nameof(VisibleEventCount));
    }

    private void SetMonitorSetting(ref bool field, bool value, Action<AppSettings> updateSetting)
    {
        if (!SetProperty(ref field, value))
        {
            return;
        }

        updateSetting(_settings);
        FilteredEvents.Refresh();
        SaveSettings();
        RestartWatching();
        OnPropertyChanged(nameof(VisibleEventCount));
    }

    private void ApplySettingsToFields(AppSettings settings)
    {
        _enableNotifications = settings.EnableNotifications;
        _startMinimized = settings.StartMinimized;
        _monitorApplication = settings.MonitorApplication;
        _monitorSystem = settings.MonitorSystem;
        _monitorSecurity = settings.MonitorSecurity;
        _showCritical = settings.ShowCritical;
        _showErrors = settings.ShowErrors;
        _showWarnings = settings.ShowWarnings;

        OnPropertyChanged(nameof(EnableNotifications));
        OnPropertyChanged(nameof(StartMinimized));
        OnPropertyChanged(nameof(MonitorApplication));
        OnPropertyChanged(nameof(MonitorSystem));
        OnPropertyChanged(nameof(MonitorSecurity));
        OnPropertyChanged(nameof(ShowCritical));
        OnPropertyChanged(nameof(ShowErrors));
        OnPropertyChanged(nameof(ShowWarnings));
        OnPropertyChanged(nameof(PauseButtonText));
    }

    private void SaveSettings()
    {
        _settingsService.SaveSettings(_settings);
    }

    private void OnEventReceived(object? sender, EventLogEntry entry)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            AddEvent(entry, insertAtTop: true);
            _notificationService.Notify(entry, _explanationService.Explain(entry), EnableNotifications);
            StatusText = $"New {entry.Severity} from {entry.ProviderName}.";
        });
    }

    private void OnStatusChanged(object? sender, string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => StatusText = message);
    }
}









