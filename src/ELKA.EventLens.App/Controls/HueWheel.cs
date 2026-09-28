using ELKA.EventLens.App.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace ELKA.EventLens.App.Controls;

public sealed class HueWheel : FrameworkElement
{
    public static readonly DependencyProperty HueProperty = DependencyProperty.Register(nameof(Hue), typeof(double), typeof(HueWheel),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SwatchProperty = DependencyProperty.Register(nameof(Swatch), typeof(Brush), typeof(HueWheel),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly BitmapSource Wheel = CreateWheel();

    public double Hue { get => (double)GetValue(HueProperty); set => SetValue(HueProperty, value); }
    public Brush Swatch { get => (Brush)GetValue(SwatchProperty); set => SetValue(SwatchProperty, value); }

    public HueWheel()
    {
        Focusable = true;
        Cursor = System.Windows.Input.Cursors.Hand;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight);
        var origin = new Point((ActualWidth - size) / 2, (ActualHeight - size) / 2);
        dc.DrawImage(Wheel, new Rect(origin.X, origin.Y, size, size));
        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        dc.DrawEllipse(Swatch, new Pen(IsKeyboardFocused ? Brushes.White : Brushes.Gray, 1), center, size * 0.25, size * 0.25);
        var angle = (Hue - 90) * Math.PI / 180;
        var marker = new Point(center.X + size * 0.407 * Math.Cos(angle), center.Y + size * 0.407 * Math.Sin(angle));
        dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.Black, 5), marker, 6, 6);
        dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, 2), marker, 6, 6);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var point = e.GetPosition(this);
        var distance = (point - new Point(ActualWidth / 2, ActualHeight / 2)).Length;
        var size = Math.Min(ActualWidth, ActualHeight);
        if (distance < size * 0.30 || distance > size * 0.5) return;
        Focus();
        CaptureMouse();
        SetHue(point);
        e.Handled = true;
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (IsMouseCaptured) SetHue(e.GetPosition(this));
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e) { base.OnLostKeyboardFocus(e); InvalidateVisual(); }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        if (e.Key is Key.Left or Key.Down) SetCurrentValue(HueProperty, HueColor.Normalize(Hue - step));
        else if (e.Key is Key.Right or Key.Up) SetCurrentValue(HueProperty, HueColor.Normalize(Hue + step));
        else return;
        e.Handled = true;
    }

    private void SetHue(Point point) => SetCurrentValue(HueProperty,
        HueColor.Normalize(Math.Atan2(point.Y - ActualHeight / 2, point.X - ActualWidth / 2) * 180 / Math.PI + 90));

    private static BitmapSource CreateWheel()
    {
        const int size = 240;
        var pixels = new byte[size * size * 4];
        var palette = Enumerable.Range(0, 360).Select(h => HueColor.FromLch(0.72, 0.16, h)).ToArray();
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = x + 0.5 - size / 2d; var dy = y + 0.5 - size / 2d;
            var radius = Math.Sqrt(dx * dx + dy * dy);
            var opacity = Math.Clamp(Math.Min(radius - 79, 116 - radius), 0, 1);
            if (opacity == 0) continue;
            var color = palette[(int)HueColor.Normalize(Math.Atan2(dy, dx) * 180 / Math.PI + 90)];
            var offset = (y * size + x) * 4;
            pixels[offset] = color.B; pixels[offset + 1] = color.G; pixels[offset + 2] = color.R;
            pixels[offset + 3] = (byte)(opacity * 255);
        }
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        bitmap.Freeze();
        return bitmap;
    }
}
