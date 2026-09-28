using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace ELKA.EventLens.App.ViewModels;

public sealed class NotificationPopupViewModel : ViewModelBase
{
    private readonly Action _openMain;
    private readonly Action _hide;

    public NotificationPopupViewModel(
        ObservableCollection<NotificationItemViewModel> items,
        Action openMain,
        Action hide)
    {
        Items = items;
        _openMain = openMain;
        _hide = hide;
        OpenMainCommand = new RelayCommand(_ => _openMain());
        HideCommand = new RelayCommand(_ => _hide());
        Items.CollectionChanged += Items_CollectionChanged;
    }

    public ObservableCollection<NotificationItemViewModel> Items { get; }

    public ICommand OpenMainCommand { get; }

    public ICommand HideCommand { get; }

    public bool HasItems => Items.Count > 0;

    public bool IsEmpty => !HasItems;

    public string CountText => Items.Count == 1 ? "1 recent alert" : $"{Items.Count} recent alerts";

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
    }
}
