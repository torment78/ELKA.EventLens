using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;

namespace ELKA.EventLens.App.ViewModels;

public sealed class UpdateViewModel : ViewModelBase
{
    private readonly IAppUpdateService _service;
    private CancellationTokenSource? _cancellation;
    private UpdateRelease? _available;
    private DownloadedUpdate? _downloaded;
    private bool _busy;
    private bool _checking;
    private double _progress;
    private string _status = "Not checked yet.";

    public UpdateViewModel(IAppUpdateService service)
    {
        _service = service;
        CheckCommand = new AsyncRelayCommand(_ => CheckAsync(), _ => !IsBusy);
        DownloadCommand = new AsyncRelayCommand(_ => DownloadAsync(), _ => !IsBusy && _available is not null);
        CancelCommand = new RelayCommand(_ => Cancel(), _ => IsBusy);
    }

    public string VersionLabel => "Installed version: " + _service.CurrentVersion;
    public string DownloadLabel => _available?.IsPortable == true ? "Download portable ZIP" : "Download and install";
    public bool HasUpdate => _available is not null;
    public bool IsBusy { get => _busy; private set { SetProperty(ref _busy, value); RefreshCommands(); } }
    public bool IsChecking { get => _checking; private set => SetProperty(ref _checking, value); }
    public double Progress { get => _progress; private set => SetProperty(ref _progress, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public AsyncRelayCommand CheckCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }
    public RelayCommand CancelCommand { get; }
    public Func<DownloadedUpdate, CancellationToken, Task>? CompleteDownloadAsync { get; set; }

    internal async Task CheckAsync()
    {
        if (IsBusy) return;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _available = null;
        _downloaded = null;
        NotifyReleaseChanged();
        IsBusy = true;
        IsChecking = true;
        Progress = 0;
        Status = "Checking GitHub releases...";
        try
        {
            var candidate = await _service.CheckAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            _available = candidate;
            Status = _available is null ? "You are up to date." : $"Version {_available.Version} is available ({_available.Size / 1_000_000d:F1} MB).";
            NotifyReleaseChanged();
        }
        catch (OperationCanceledException) { Status = cancellation.IsCancellationRequested ? "Update check cancelled." : "Update check timed out. Please retry."; }
        catch (Exception error) { Status = "Could not check for updates: " + error.Message; }
        finally { _cancellation = null; IsChecking = false; IsBusy = false; }
    }

    internal async Task DownloadAsync()
    {
        if (IsBusy || _available is null) return;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsBusy = true;
        Progress = 0;
        Status = "Downloading update...";
        try
        {
            _downloaded ??= await _service.DownloadAsync(_available, new Progress<double>(value => Progress = value), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            Progress = 100;
            Status = "Download verified.";
            if (CompleteDownloadAsync is not null) await CompleteDownloadAsync(_downloaded, cancellation.Token);
            if (_available.IsPortable) Status = "Portable ZIP downloaded. Extract it after exiting EventLens; keep your Saves, Logs and WebView2Profile folders.";
        }
        catch (OperationCanceledException) { Status = cancellation.IsCancellationRequested ? "Update download cancelled." : "Update download timed out. Please retry."; }
        catch (Exception error) { Status = "Update stopped: " + error.Message; }
        finally { _cancellation = null; IsBusy = false; }
    }

    public void Cancel() => _cancellation?.Cancel();

    private void NotifyReleaseChanged()
    {
        OnPropertyChanged(nameof(HasUpdate));
        OnPropertyChanged(nameof(DownloadLabel));
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        CheckCommand.RaiseCanExecuteChanged();
        DownloadCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }
}
