using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using System.Windows.Input;

namespace ELKA.EventLens.App.ViewModels;

public sealed class NotificationItemViewModel : ViewModelBase
{
    private EventLogEntry _entry;
    private EventExplanation _explanation;
    private int _repeatCount;
    private DateTimeOffset _lastSeenUtc;
    private bool _isExpanded;

    public NotificationItemViewModel(EventLogEntry entry, EventExplanation explanation, int repeatCount)
    {
        _entry = entry;
        _explanation = explanation;
        _repeatCount = Math.Max(1, repeatCount);
        _lastSeenUtc = DateTimeOffset.UtcNow;
        ToggleCommand = new RelayCommand(_ => IsExpanded = !IsExpanded);
    }

    public EventLogEntry Entry => _entry;

    public EventExplanation Explanation => _explanation;

    public ICommand ToggleCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string RepeatKey => _entry.RepeatKey;

    public string Title => $"{_entry.Severity}: {_entry.ProviderName}";

    public string SourceText => $"{_entry.LogName} / Event ID {_entry.EventId}";

    public string SummaryText => _explanation.ShortSummary;

    public string DetailText => BuildDetailText();

    public string TimeText => _entry.TimeCreated?.ToLocalTime().ToString("HH:mm:ss") ?? _lastSeenUtc.ToLocalTime().ToString("HH:mm:ss");

    public string CountText => _repeatCount > 1 ? $"x{_repeatCount}" : string.Empty;

    public bool HasRepeats => _repeatCount > 1;

    public System.Windows.Media.SolidColorBrush AccentBrush => ThemeService.SeverityBrush(_entry.Severity);

    public System.Windows.Media.SolidColorBrush SeverityBackgroundBrush => ThemeService.SeverityBrush(_entry.Severity, background: true);

    public void Update(EventLogEntry entry, EventExplanation explanation, int repeatCount)
    {
        _entry = entry;
        _explanation = explanation;
        _repeatCount = Math.Max(1, repeatCount);
        _lastSeenUtc = DateTimeOffset.UtcNow;

        OnPropertyChanged(nameof(Entry));
        OnPropertyChanged(nameof(Explanation));
        OnPropertyChanged(nameof(RepeatKey));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(HasRepeats));
        OnPropertyChanged(nameof(AccentBrush));
        OnPropertyChanged(nameof(SeverityBackgroundBrush));
    }

    private string BuildDetailText()
    {
        var firstStep = _explanation.RecommendedNextSteps.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstStep))
        {
            return _explanation.SeverityAdvice;
        }

        return $"{_explanation.SeverityAdvice}\nNext: {firstStep}";
    }
}
