using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.ViewModels;
using ELKA.EventLens.App.Views;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ELKA.EventLens.App.Services;

public sealed class NotificationService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon _trayIcon;
    private readonly Dictionary<string, RepeatState> _repeatStates = new(StringComparer.Ordinal);
    private readonly ObservableCollection<NotificationItemViewModel> _recentNotifications = [];
    private readonly NotificationPopupViewModel _popupViewModel;
    private NotificationPopupWindow? _popupWindow;
    private bool _disposed;

    public NotificationService()
    {
        _popupViewModel = new NotificationPopupViewModel(
            _recentNotifications,
            RequestShowMain,
            HideNotifications);

        _trayIcon = LoadTrayIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            // Icon = SystemIcons.Application,
            Icon = _trayIcon,
            Text = "ELKA EventLens",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => RequestShowMain();
    }

    public event EventHandler? ShowMainRequested;

    public event EventHandler? ExitRequested;

    public void Notify(EventLogEntry entry, EventExplanation explanation, bool enabled)
    {
        if (!enabled || entry.Severity is not (EventSeverity.Critical or EventSeverity.Error))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var key = entry.RepeatKey;

        if (!_repeatStates.TryGetValue(key, out var state))
        {
            state = new RepeatState();
            _repeatStates[key] = state;
        }

        state.Count++;
        var shouldShowPopup = !state.LastNotificationUtc.HasValue
            || now - state.LastNotificationUtc.Value >= TimeSpan.FromSeconds(90);

        if (shouldShowPopup)
        {
            state.LastNotificationUtc = now;
        }

        DispatchToUi(() =>
        {
            AddOrUpdateNotification(entry, explanation, state.Count);

            if (shouldShowPopup)
            {
                ShowNotifications();
            }
        });
    }

    public void ShowTestNotification()
    {
        var entry = new EventLogEntry
        {
            TimeCreated = DateTime.Now,
            LogName = "System",
            ProviderName = "Kernel-Power",
            EventId = 41,
            RecordId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Severity = EventSeverity.Critical,
            LevelDisplayName = "Critical",
            Message = "Test notification preview. The system rebooted without cleanly shutting down first.",
            MachineName = Environment.MachineName
        };

        var explanation = new EventExplanation
        {
            MatchedPattern = "Test notification",
            ShortSummary = "Test preview: unexpected shutdown style alert.",
            SeverityAdvice = "This is only a test, but real Kernel-Power 41 events are worth reviewing if they repeat.",
            PossibleCauses =
            [
                "Power loss or forced power-off",
                "Crash, driver, hardware, or thermal instability"
            ],
            RecommendedNextSteps =
            [
                "Open the main app to review the full event card.",
                "Check nearby events around the same timestamp."
            ]
        };

        DispatchToUi(() =>
        {
            AddOrUpdateNotification(entry, explanation, 1);
            ShowNotifications();
        });
    }

    public void ShowNotifications()
    {
        DispatchToUi(() =>
        {
            var window = EnsurePopupWindow();
            window.PositionNearWorkArea();
            window.Show();
            window.Activate();
        });
    }

    public void HideNotifications()
    {
        DispatchToUi(() => _popupWindow?.Hide());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon.Dispose();

        DispatchToUi(() =>
        {
            _popupWindow?.CloseForShutdown();
            _popupWindow = null;
        });
    }

    private static Icon LoadTrayIcon()
    {
        var uri = new Uri("pack://application:,,,/ELKA.EventLens.App;component/Assets/EventLens-v1.ico");
        using var stream = System.Windows.Application.GetResourceStream(uri)!.Stream;
        using var icon = new Icon(stream, Forms.SystemInformation.SmallIconSize);
        return (Icon)icon.Clone();
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip
        {
            BackColor = ElkaTrayMenuRenderer.PanelColor,
            ForeColor = ElkaTrayMenuRenderer.TextColor,
            Renderer = new ElkaTrayMenuRenderer(),
            ShowCheckMargin = false,
            ShowImageMargin = false,
            Padding = new Forms.Padding(6),
            Font = new Font(new FontFamily("Segoe UI"), 10F, System.Drawing.FontStyle.Regular)
        };

        var showMainItem = CreateTrayMenuItem("Show ELKA EventLens");
        showMainItem.Click += (_, _) => RequestShowMain();

        var showNotificationsItem = CreateTrayMenuItem("Show notifications");
        showNotificationsItem.Click += (_, _) => ShowNotifications();

        var exitItem = CreateTrayMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);

        menu.Items.Add(showMainItem);
        menu.Items.Add(showNotificationsItem);
        menu.Items.Add(new Forms.ToolStripSeparator { Margin = new Forms.Padding(4, 5, 4, 5) });
        menu.Items.Add(exitItem);

        return menu;
    }

    private static Forms.ToolStripMenuItem CreateTrayMenuItem(string text)
    {
        return new Forms.ToolStripMenuItem(text)
        {
            BackColor = ElkaTrayMenuRenderer.PanelColor,
            ForeColor = ElkaTrayMenuRenderer.TextColor,
            AutoSize = true,
            Padding = new Forms.Padding(12, 7, 22, 7),
            Margin = new Forms.Padding(0, 1, 0, 1)
        };
    }

    private NotificationPopupWindow EnsurePopupWindow()
    {
        if (_popupWindow is not null)
        {
            return _popupWindow;
        }

        _popupWindow = new NotificationPopupWindow
        {
            DataContext = _popupViewModel
        };

        return _popupWindow;
    }

    private void AddOrUpdateNotification(EventLogEntry entry, EventExplanation explanation, int count)
    {
        var existing = _recentNotifications.FirstOrDefault(item => item.RepeatKey == entry.RepeatKey);
        if (existing is not null)
        {
            existing.Update(entry, explanation, count);
            var oldIndex = _recentNotifications.IndexOf(existing);
            if (oldIndex > 0)
            {
                _recentNotifications.Move(oldIndex, 0);
            }
        }
        else
        {
            _recentNotifications.Insert(0, new NotificationItemViewModel(entry, explanation, count));
        }

        while (_recentNotifications.Count > 5)
        {
            _recentNotifications.RemoveAt(_recentNotifications.Count - 1);
        }
    }

    private void RequestShowMain()
    {
        HideNotifications();
        ShowMainRequested?.Invoke(this, EventArgs.Empty);
    }

    private static void DispatchToUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private sealed class ElkaTrayMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        public static Color PanelColor => Palette("PanelBrush");
        public static Color BorderColor => Palette("BorderBrush");
        public static Color AccentColor => Palette("AccentBrush");
        public static Color AccentWarmColor => Palette("AccentWarmBrush");
        public static Color TextColor => Palette("TextBrush");

        private static Color Palette(string key)
        {
            var color = ThemeService.GetBrush(key).Color;
            return Color.FromArgb(color.R, color.G, color.B);
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(PanelColor);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            var rectangle = new Rectangle(System.Drawing.Point.Empty, new System.Drawing.Size(e.ToolStrip.Width - 1, e.ToolStrip.Height - 1));
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawRectangle(pen, rectangle);
        }

        protected override void OnRenderImageMargin(Forms.ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(PanelColor);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            var rectangle = new Rectangle(System.Drawing.Point.Empty, e.Item.Size);
            var isActive = e.Item.Selected || e.Item.Pressed;
            var background = isActive ? Palette("SelectedHoverBrush") : PanelColor;

            using var brush = new SolidBrush(background);
            e.Graphics.FillRectangle(brush, rectangle);

            if (!isActive)
            {
                return;
            }

            using var borderPen = new Pen(AccentColor);
            e.Graphics.DrawRectangle(borderPen, 1, 1, rectangle.Width - 3, rectangle.Height - 3);

            using var edgeBrush = new SolidBrush(AccentColor);
            e.Graphics.FillRectangle(edgeBrush, 1, 4, 3, Math.Max(0, rectangle.Height - 8));
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Selected || e.Item.Pressed
                ? AccentWarmColor
                : TextColor;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            var y = e.Item.Height / 2;
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawLine(pen, 10, y, e.Item.Width - 10, y);
        }
    }
    private sealed class RepeatState
    {
        public int Count { get; set; }

        public DateTimeOffset? LastNotificationUtc { get; set; }
    }
}




