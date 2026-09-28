using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ELKA.EventLens.App.Services;

public static class WindowThemeService
{
    public static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => Apply((Window)sender)));
        EventManager.RegisterClassHandler(typeof(Window), System.Windows.Input.Keyboard.GotKeyboardFocusEvent,
            new System.Windows.Input.KeyboardFocusChangedEventHandler((sender, _) => Apply((Window)sender)));
    }

    public static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || window.WindowStyle == WindowStyle.None) return;
        Set(handle, 20, 1);
        Set(handle, 35, ColorRef("PanelBrush"));
        Set(handle, 36, ColorRef("TextBrush"));
        Set(handle, 34, ColorRef("BorderBrush"));
    }

    private static int ColorRef(string key)
    {
        var color = ThemeService.GetBrush(key).Color;
        return color.R | color.G << 8 | color.B << 16;
    }

    private static void Set(IntPtr handle, int attribute, int value) =>
        DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
