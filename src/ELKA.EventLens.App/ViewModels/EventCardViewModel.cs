using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using System.Windows.Input;

namespace ELKA.EventLens.App.ViewModels;

public sealed class EventCardViewModel : ViewModelBase
{
    private readonly Action<SearchTarget, EventLogEntry> _openSearch;
    private readonly Action<EventLogEntry> _openAi;
    private bool _isExpanded;

    public EventCardViewModel(
        EventLogEntry entry,
        EventExplanation explanation,
        Action<SearchTarget, EventLogEntry> openSearch,
        Action<EventLogEntry> openAi)
    {
        Entry = entry;
        Explanation = explanation;
        _openSearch = openSearch;
        _openAi = openAi;

        SearchWebCommand = new RelayCommand(_ => Open(SearchTarget.Web));
        SearchRedditCommand = new RelayCommand(_ => Open(SearchTarget.Reddit));
        SearchStackOverflowCommand = new RelayCommand(_ => Open(SearchTarget.StackOverflow));
        SearchMicrosoftCommand = new RelayCommand(_ => Open(SearchTarget.Microsoft));
        OpenMicrosoftHelpCommand = new RelayCommand(_ => Open(SearchTarget.Microsoft));
        UseAiCommand = new RelayCommand(_ => _openAi(Entry));
    }

    public EventLogEntry Entry { get; }

    public EventExplanation Explanation { get; }

    public ICommand SearchWebCommand { get; }

    public ICommand SearchRedditCommand { get; }

    public ICommand SearchStackOverflowCommand { get; }

    public ICommand SearchMicrosoftCommand { get; }

    public ICommand OpenMicrosoftHelpCommand { get; }

    public ICommand UseAiCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string Title => $"{Entry.ProviderName} event {Entry.EventId}";

    public string SeverityLabel => Entry.Severity.ToString();

    public string TimeText => Entry.TimeCreated?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown time";

    public string SourceText => $"{Entry.LogName} / {Entry.ProviderName}";

    public string EventIdText => $"Event ID {Entry.EventId}";

    public string MessagePreview => Trim(Entry.Message, 260);

    public System.Windows.Media.SolidColorBrush AccentBrush => ThemeService.SeverityBrush(Entry.Severity);

    public System.Windows.Media.SolidColorBrush SeverityBackgroundBrush => ThemeService.SeverityBrush(Entry.Severity, background: true);

    public string TechnicalDetails =>
        $"Log: {Entry.LogName}\n" +
        $"Source: {Entry.ProviderName}\n" +
        $"Event ID: {Entry.EventId}\n" +
        $"Level: {Entry.LevelDisplayName}\n" +
        $"Record ID: {Entry.RecordId}\n" +
        $"Machine: {Entry.MachineName}\n" +
        $"Task: {Entry.TaskDisplayName}\n" +
        $"Opcode: {Entry.OpcodeDisplayName}\n" +
        $"Keywords: {Entry.KeywordsDisplayNames}";

    private void Open(SearchTarget target)
    {
        _openSearch(target, Entry);
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No message text is available for this event.";
        }

        var cleaned = value.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        return cleaned.Length <= maxLength ? cleaned : $"{cleaned[..(maxLength - 3)]}...";
    }
}

