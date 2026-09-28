using System.Windows;
using ELKA.EventLens.App.ViewModels;

namespace ELKA.EventLens.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;
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
