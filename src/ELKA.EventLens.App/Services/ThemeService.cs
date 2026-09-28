using ELKA.EventLens.App.Models;
using System.Windows.Media;
using System.Windows.Data;
using System.ComponentModel;
using Binding = System.Windows.Data.Binding;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Application = System.Windows.Application;

namespace ELKA.EventLens.App.Services;

public static class ThemeService
{
    private static readonly Dictionary<string, SolidColorBrush> Brushes = new();
    private static readonly Dictionary<string, PaletteColor> Colors = new();
    public static AppearanceSettings Current { get; private set; } = new();

    public static SolidColorBrush GetBrush(string key) => Brushes[key];
    public static SolidColorBrush SeverityBrush(EventSeverity severity, bool background = false) =>
        GetBrush($"{(severity is EventSeverity.Verbose ? EventSeverity.Unknown : severity)}{(background ? "Background" : "")}Brush");

    public static void Apply(AppearanceSettings? settings)
    {
        Current = (settings ?? new()).Copy();
        var colors = new (string Key, string Hex, double Shift)[]
        {
            ("AppBackground", "#081316", Current.BackgroundHueShift),
            ("Panel", "#0E1B1F", Current.BackgroundHueShift),
            ("PanelAlt", "#132327", Current.BackgroundHueShift),
            ("Input", "#081316", Current.BackgroundHueShift),
            ("Text", "#E7EEF0", Current.BackgroundHueShift),
            ("MutedText", "#8AA0A6", Current.BackgroundHueShift),
            ("Border", "#2C3E43", Current.OutlineHueShift),
            ("BorderHover", "#4C6863", Current.OutlineHueShift),
            ("Accent", "#55C27A", Current.AccentHueShift),
            ("AccentStrong", "#55C27A", Current.AccentHueShift),
            ("AccentHover", "#6DD891", Current.AccentHueShift),
            ("AccentPressed", "#3FA665", Current.AccentHueShift),
            ("AccentText", "#061316", Current.BackgroundHueShift),
            ("AccentWarm", "#E2B84A", Current.HighlightHueShift),
            ("Selection", "#2B6841", Current.AccentHueShift),
            ("Selected", "#102327", Current.BackgroundHueShift),
            ("SelectedHover", "#14392F", Current.AccentHueShift),
            ("ScrollTrack", "#081316", Current.BackgroundHueShift),
            ("ScrollThumb", "#2C3E43", Current.OutlineHueShift),
            ("ScrollThumbHover", "#3F5B55", Current.OutlineHueShift),
            ("Critical", "#F87171", Current.CriticalHueShift),
            ("CriticalBackground", "#451A1A", Current.CriticalHueShift),
            ("Error", "#FB7185", Current.ErrorHueShift),
            ("ErrorBackground", "#4C1D2F", Current.ErrorHueShift),
            ("Warning", "#FBBF24", Current.WarningHueShift),
            ("WarningBackground", "#422006", Current.WarningHueShift),
            ("Information", "#38BDF8", Current.InformationHueShift),
            ("InformationBackground", "#082F49", Current.InformationHueShift),
            ("Unknown", "#94A3B8", Current.BackgroundHueShift),
            ("UnknownBackground", "#1E293B", Current.BackgroundHueShift)
        };

        foreach (var (key, hex, shift) in colors)
        {
            var color = HueColor.Rotate((Color)ColorConverter.ConvertFromString(hex), shift);
            var resourceKey = key + "Brush";
            if (Colors.TryGetValue(resourceKey, out var source)) source.Color = color;
            else
            {
                source = new PaletteColor { Color = color };
                Colors[resourceKey] = source;
                var brush = new SolidColorBrush();
                // A binding keeps shared brushes unfrozen when WPF seals a style using them.
                BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding(nameof(PaletteColor.Color)) { Source = source });
                Brushes[resourceKey] = brush;
            }
            Application.Current.Resources[resourceKey] = Brushes[resourceKey];
            Application.Current.Resources[key + "Color"] = color;
        }

        foreach (System.Windows.Window window in Application.Current.Windows)
            WindowThemeService.Apply(window);
    }

    private sealed class PaletteColor : INotifyPropertyChanged
    {
        private Color _color;
        public event PropertyChangedEventHandler? PropertyChanged;
        public Color Color
        {
            get => _color;
            set { _color = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color))); }
        }
    }
}

// OKLCH hue rotation holds perceptual lightness steady; chroma is reduced only at the sRGB gamut edge.
public static class HueColor
{
    private static (double L, double A, double B) ToLab(Color color)
    {
        static double Linear(byte c) => c / 255d <= 0.04045 ? c / 255d / 12.92 : Math.Pow((c / 255d + 0.055) / 1.055, 2.4);
        var r = Linear(color.R); var g = Linear(color.G); var b = Linear(color.B);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    public static double Normalize(double hue) => double.IsFinite(hue) ? (hue % 360 + 360) % 360 : 0;
    public static double GetHue(Color color)
    {
        var lab = ToLab(color);
        return Normalize(Math.Atan2(lab.B, lab.A) * 180 / Math.PI);
    }

    public static double GetLightness(Color color) => ToLab(color).L;

    public static Color Rotate(Color color, double degrees)
    {
        degrees = Normalize(degrees);
        if (degrees < 0.001) return color;
        var lab = ToLab(color);
        return FromLch(lab.L, Math.Sqrt(lab.A * lab.A + lab.B * lab.B), GetHue(color) + degrees);
    }

    public static Color FromLch(double lightness, double chroma, double hue)
    {
        var angle = hue * Math.PI / 180;
        (double R, double G, double B) Convert(double c)
        {
            var a = c * Math.Cos(angle); var b = c * Math.Sin(angle);
            var l = Math.Pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3);
            var m = Math.Pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3);
            var s = Math.Pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3);
            return (4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
                -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s);
        }
        static bool InGamut((double R, double G, double B) rgb) =>
            rgb.R >= 0 && rgb.R <= 1 && rgb.G >= 0 && rgb.G <= 1 && rgb.B >= 0 && rgb.B <= 1;
        var rgb = Convert(chroma);
        if (!InGamut(rgb))
        {
            var low = 0d; var high = chroma;
            for (var i = 0; i < 20; i++)
            {
                var mid = (low + high) / 2;
                if (InGamut(Convert(mid))) low = mid; else high = mid;
            }
            rgb = Convert(low);
        }
        static byte Srgb(double value) => (byte)Math.Round(255 * Math.Clamp(value <= 0.0031308 ? value * 12.92 : 1.055 * Math.Pow(value, 1 / 2.4) - 0.055, 0, 1));
        return Color.FromRgb(Srgb(rgb.R), Srgb(rgb.G), Srgb(rgb.B));
    }
}
