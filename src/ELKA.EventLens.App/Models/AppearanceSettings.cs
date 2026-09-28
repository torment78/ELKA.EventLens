namespace ELKA.EventLens.App.Models;

public sealed class AppearanceSettings
{
    public double BackgroundHueShift { get; set; }
    public double OutlineHueShift { get; set; }
    public double AccentHueShift { get; set; }
    public double HighlightHueShift { get; set; }
    public double CriticalHueShift { get; set; }
    public double ErrorHueShift { get; set; }
    public double WarningHueShift { get; set; }
    public double InformationHueShift { get; set; }

    public AppearanceSettings Copy() => (AppearanceSettings)MemberwiseClone();
}
