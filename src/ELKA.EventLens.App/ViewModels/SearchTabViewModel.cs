namespace ELKA.EventLens.App.ViewModels;

public sealed class SearchTabViewModel : ViewModelBase
{
    private int _autoPromptState;
    private bool _isPromptExpanded;
    private bool _preferDarkWebPages;
    private string _promptStatus = "AI prompt";

    public SearchTabViewModel(
        string key,
        string title,
        string targetLabel,
        string query,
        string url,
        bool isAiTab = false,
        bool preferDarkWebPages = true,
        string? autoPrompt = null)
    {
        Key = key;
        Title = title;
        TargetLabel = targetLabel;
        Query = query;
        Url = url;
        Source = new Uri(url);
        IsAiTab = isAiTab;
        PreferDarkWebPages = preferDarkWebPages;
        AutoPrompt = autoPrompt;
        _isPromptExpanded = isAiTab;
        CopyPromptCommand = new RelayCommand(_ =>
        {
            try { System.Windows.Clipboard.SetText(Query); }
            catch { PromptStatus = "Clipboard unavailable"; }
        });
    }

    public string Key { get; }

    public string Title { get; }

    public string TargetLabel { get; }

    public string Query { get; }

    public string Url { get; }

    public Uri Source { get; }

    public bool IsAiTab { get; }

    public bool PreferDarkWebPages
    {
        get => _preferDarkWebPages;
        set => SetProperty(ref _preferDarkWebPages, value);
    }

    public bool IsPromptExpanded
    {
        get => _isPromptExpanded;
        set => SetProperty(ref _isPromptExpanded, value);
    }

    public string PromptStatus
    {
        get => _promptStatus;
        set => SetProperty(ref _promptStatus, value);
    }

    public System.Windows.Input.ICommand CopyPromptCommand { get; }

    public string? AutoPrompt { get; }

    public bool ShouldAutoSubmitPrompt => IsAiTab
        && !string.IsNullOrWhiteSpace(AutoPrompt)
        && System.Threading.Volatile.Read(ref _autoPromptState) != 2;

    public bool TryBeginAutoSubmitPrompt()
    {
        if (!IsAiTab || string.IsNullOrWhiteSpace(AutoPrompt))
        {
            return false;
        }

        return System.Threading.Interlocked.CompareExchange(ref _autoPromptState, 1, 0) == 0;
    }

    public void MarkAutoPromptCompleted()
    {
        System.Threading.Interlocked.Exchange(ref _autoPromptState, 2);
    }

    public void ResetAutoPromptAttempt()
    {
        System.Threading.Interlocked.CompareExchange(ref _autoPromptState, 0, 1);
    }
}
