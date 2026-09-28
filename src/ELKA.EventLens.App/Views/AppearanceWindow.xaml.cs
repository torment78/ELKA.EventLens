using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using ELKA.EventLens.App.ViewModels;
using System.Windows;

namespace ELKA.EventLens.App.Views;

public partial class AppearanceWindow : Window
{
    private readonly AppearanceSettings _original = ThemeService.Current.Copy();
    public AppearanceWindow()
    {
        InitializeComponent();
        Closed += (_, _) => { if (DialogResult != true) ThemeService.Apply(_original); };
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
