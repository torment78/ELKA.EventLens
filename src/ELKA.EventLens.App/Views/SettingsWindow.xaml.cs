using System.Windows;
using System.Diagnostics;
using System.Windows.Threading;
using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using ELKA.EventLens.App.ViewModels;

namespace ELKA.EventLens.App.Views;

public partial class SettingsWindow : Window
{
    private bool _closed;
    public SettingsWindow()
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SettingsViewModel previous) { previous.Updates.Cancel(); previous.Updates.CompleteDownloadAsync = null; }
            if (e.NewValue is SettingsViewModel settings) settings.Updates.CompleteDownloadAsync = CompleteUpdateAsync;
        };
        Closed += (_, _) =>
        {
            _closed = true;
            if (DataContext is SettingsViewModel settings) settings.Updates.Cancel();
        };
    }

    private async Task CompleteUpdateAsync(DownloadedUpdate download, CancellationToken cancellationToken)
    {
        if (_closed) return;
        if (download.Release.IsPortable)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{download.Path}\"") { UseShellExecute = true });
            return;
        }

        if (System.Windows.MessageBox.Show(this,
            $"Install EventLens {download.Release.Version} now? EventLens will save and exit, then reopen after installation. Windows may ask for administrator permission.",
            "Install update", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        await AppUpdateService.StartInstallerAsync(download, cancellationToken);
        // Return from ShowDialog so the owner commits pending Settings before shutdown.
        DialogResult = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (System.Windows.Application.Current is App app) app.ShutdownForUpdate();
        }));
    }

    private void Appearance_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel settings) return;
        var viewModel = new AppearanceViewModel(settings.Appearance);
        var window = new AppearanceWindow { Owner = this, DataContext = viewModel };
        if (window.ShowDialog() == true) settings.Appearance = viewModel.Settings.Copy();
    }

    private void InsertPromptField_Click(object sender, RoutedEventArgs e)
    {
        if (PromptFieldPicker.SelectedItem is not string field) return;
        var caret = PromptEditor.SelectionStart;
        PromptEditor.SelectedText = field;
        PromptEditor.CaretIndex = caret + field.Length;
        PromptEditor.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
