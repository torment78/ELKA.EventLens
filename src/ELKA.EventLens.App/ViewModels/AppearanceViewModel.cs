using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using System.Windows.Media;
using System.Windows.Input;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace ELKA.EventLens.App.ViewModels;

public sealed class AppearanceViewModel : ViewModelBase
{
    public AppearanceSettings Settings { get; }
    public IReadOnlyList<HueOptionViewModel> Colors { get; }
    public ICommand ResetCommand { get; }

    public AppearanceViewModel(AppearanceSettings settings)
    {
        Settings = settings.Copy();
        Colors =
        [
            new("Background", "#0E1B1F", Settings.BackgroundHueShift, value => Settings.BackgroundHueShift = value, Preview),
            new("Outlines & scrollbars", "#2C3E43", Settings.OutlineHueShift, value => Settings.OutlineHueShift = value, Preview),
            new("Buttons & accents", "#55C27A", Settings.AccentHueShift, value => Settings.AccentHueShift = value, Preview),
            new("Highlights", "#E2B84A", Settings.HighlightHueShift, value => Settings.HighlightHueShift = value, Preview),
            new("Critical", "#F87171", Settings.CriticalHueShift, value => Settings.CriticalHueShift = value, Preview),
            new("Errors", "#FB7185", Settings.ErrorHueShift, value => Settings.ErrorHueShift = value, Preview),
            new("Warnings", "#FBBF24", Settings.WarningHueShift, value => Settings.WarningHueShift = value, Preview),
            new("Information", "#38BDF8", Settings.InformationHueShift, value => Settings.InformationHueShift = value, Preview)
        ];
        ResetCommand = new RelayCommand(_ => { foreach (var color in Colors) color.Reset(); });
    }

    private void Preview() => ThemeService.Apply(Settings);
}

public sealed class HueOptionViewModel : ViewModelBase
{
    private readonly Color _baseColor;
    private readonly double _baseHue;
    private readonly Action<double> _setShift;
    private readonly Action _preview;
    private double _hue;
    public string Label { get; }
    public string Hex => $"#{Swatch.Color.R:X2}{Swatch.Color.G:X2}{Swatch.Color.B:X2}";
    public SolidColorBrush Swatch => new(HueColor.Rotate(_baseColor, Hue - _baseHue));
    public double Hue
    {
        get => _hue;
        set
        {
            if (!SetProperty(ref _hue, HueColor.Normalize(value))) return;
            _setShift(HueColor.Normalize(_hue - _baseHue));
            OnPropertyChanged(nameof(Swatch));
            OnPropertyChanged(nameof(Hex));
            _preview();
        }
    }

    public HueOptionViewModel(string label, string hex, double shift, Action<double> setShift, Action preview)
    {
        Label = label;
        _baseColor = (Color)ColorConverter.ConvertFromString(hex);
        _baseHue = HueColor.GetHue(_baseColor);
        _hue = HueColor.Normalize(_baseHue + shift);
        _setShift = setShift;
        _preview = preview;
    }

    public void Reset() => Hue = _baseHue;
}
