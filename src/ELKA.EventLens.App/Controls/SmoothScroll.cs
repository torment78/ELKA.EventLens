using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ELKA.EventLens.App.Controls;

public static class SmoothScroll
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached("Enabled", typeof(bool), typeof(SmoothScroll),
        new PropertyMetadata(false, EnabledChanged));
    private static readonly DependencyProperty MotionProperty = DependencyProperty.RegisterAttached("Motion", typeof(ScrollMotion), typeof(SmoothScroll));
    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    private static void EnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not ScrollViewer viewer) return;
        if ((bool)args.NewValue)
        {
            viewer.PreviewMouseWheel += OnWheel;
            viewer.PreviewMouseDown += OnMouseDown;
            viewer.Unloaded += OnUnloaded;
        }
        else
        {
            viewer.PreviewMouseWheel -= OnWheel;
            viewer.PreviewMouseDown -= OnMouseDown;
            viewer.Unloaded -= OnUnloaded;
            Stop(viewer);
        }
    }

    private static DependencyObject? Parent(DependencyObject element) => element is Visual
        ? VisualTreeHelper.GetParent(element) : element is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(element);

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0) return;
        var viewer = (ScrollViewer)sender;
        var current = e.OriginalSource as DependencyObject;
        // The innermost scrollable field consumes the wheel; at an edge the outer list takes over.
        while (current is not null)
        {
            if (current is ScrollViewer candidate && candidate.ScrollableHeight > 0 &&
                (e.Delta > 0 ? candidate.VerticalOffset > 0.1 : candidate.VerticalOffset < candidate.ScrollableHeight - 0.1))
            {
                if (!ReferenceEquals(candidate, viewer)) return;
                var motion = (ScrollMotion?)viewer.GetValue(MotionProperty);
                if (motion is null)
                {
                    motion = new ScrollMotion(viewer);
                    viewer.SetValue(MotionProperty, motion);
                }
                motion.Move(-e.Delta / 120d * 36);
                e.Handled = true;
                return;
            }
            current = Parent(current);
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e) => Stop((ScrollViewer)sender);
    private static void OnUnloaded(object sender, RoutedEventArgs e) => Stop((ScrollViewer)sender);
    private static void Stop(ScrollViewer viewer) => ((ScrollMotion?)viewer.GetValue(MotionProperty))?.Stop();

    private sealed class ScrollMotion : Animatable
    {
        private readonly ScrollViewer _viewer;
        private bool _moving;
        private double _target;
        private static readonly DependencyProperty OffsetProperty = DependencyProperty.Register("Offset", typeof(double), typeof(ScrollMotion),
            new PropertyMetadata(0d, (sender, args) => ((ScrollMotion)sender)._viewer.ScrollToVerticalOffset((double)args.NewValue)));

        public ScrollMotion(ScrollViewer viewer) => _viewer = viewer;
        protected override Freezable CreateInstanceCore() => new ScrollMotion(_viewer);

        public void Move(double delta)
        {
            _target = Math.Clamp((_moving ? _target : _viewer.VerticalOffset) + delta, 0, _viewer.ScrollableHeight);
            var from = _viewer.VerticalOffset;
            if (!SystemParameters.ClientAreaAnimation)
            {
                _viewer.ScrollToVerticalOffset(_target);
                return;
            }
            _moving = true;
            var animation = new DoubleAnimation(from, _target, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            SetValue(OffsetProperty, _target);
            animation.Completed += (_, _) => _moving = false;
            BeginAnimation(OffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        public void Stop()
        {
            SetValue(OffsetProperty, _viewer.VerticalOffset);
            BeginAnimation(OffsetProperty, null);
            _moving = false;
        }
    }
}
