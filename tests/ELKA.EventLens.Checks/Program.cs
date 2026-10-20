using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using System.Windows.Markup;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ELKA.EventLens.App.Controls;
using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using ELKA.EventLens.App.ViewModels;
using ELKA.EventLens.App.Views;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

internal static class Program
{
    [STAThread]
    public static int Main()
    {
        var app = new CheckApplication();
        var source = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures/Theme.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary = new XElement(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XAttribute(XNamespace.Xmlns + "controls", "clr-namespace:ELKA.EventLens.App.Controls;assembly=ELKA.EventLens.App"),
            new XAttribute(XNamespace.Xmlns + "sys", "clr-namespace:System;assembly=System.Runtime"),
            source.Root!.Element(presentation + "Application.Resources")!.Elements());
        app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
        return app.Run();
    }
}

internal sealed class CheckApplication : System.Windows.Application
{
    private readonly string _output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/checks"));
    private int _checks;

    protected override async void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Directory.CreateDirectory(_output);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, AppStorageService.PortableMarkerFileName), "Isolated test storage");
        try
        {
            ThemeService.Apply(new());
            WindowThemeService.Initialize();
            CheckLogic();
            CheckStorage();
            LegacyCleanupChecks.Run(Check, _output);
            await UpdateChecks.RunAsync(Check, _output);
            if (e.Args.Contains("--live-update-check"))
            {
                using var client = new System.Net.Http.HttpClient();
                var service = new AppUpdateService(client, "0.1.0-dev.1", false, Path.Combine(_output, "LiveUpdate"));
                var release = await service.CheckAsync(CancellationToken.None);
                Check(release is not null, "live GitHub feed discovers published development update");
                if (e.Args.Contains("--live-update-download"))
                {
                    var package = await service.DownloadAsync(release!, new Progress<double>(), CancellationToken.None);
                    Check(File.Exists(package.Path), "live release installer downloaded and checksum verified without execution");
                }
            }
            await CheckLayoutAsync();
            await CheckBrowserAsync();
            await CheckTabsAsync();
            Console.WriteLine($"PASS: {_checks} checks. Screenshots: {_output}");
            Shutdown(0);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            Shutdown(1);
        }
    }

    private void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++;
        Console.WriteLine("PASS " + label);
    }

    private static EventLogEntry Sample() => new()
    {
        EventId = 41, ProviderName = "Kernel-Power", LogName = "System", Severity = EventSeverity.Critical,
        LevelDisplayName = "Critical", MachineName = "TEST-MACHINE", TimeCreated = new DateTime(2026, 9, 28, 12, 0, 0),
        Message = "Unexpected shutdown. " + new string('X', 250) + " {Provider} " + string.Join(" ", Enumerable.Repeat("Long diagnostic message for wrapping.", 35)),
        RawXml = "<Event><Message>" + new string('X', 1500) + "</Message></Event>"
    };

    private void CheckLogic()
    {
        var service = new SearchUrlService();
        var entry = Sample();
        var hosts = new[] { "search.brave.com", "www.google.com", "duckduckgo.com", "www.bing.com" };
        for (var i = 0; i < hosts.Length; i++)
        {
            var engine = SearchUrlService.SearchEngines[i];
            var url = service.BuildUrl(SearchTarget.Web, entry, engine);
            Check(new Uri(url).Host == hosts[i], engine + " search endpoint");
            Check(Uri.UnescapeDataString(service.BuildUrl(SearchTarget.Reddit, entry, engine)).Contains("site:reddit.com"), engine + " Reddit restriction");
            Check(Uri.UnescapeDataString(service.BuildUrl(SearchTarget.Microsoft, entry, engine)).Contains("site:learn.microsoft.com OR site:support.microsoft.com"), engine + " Microsoft restriction");
        }
        Check(service.BuildCacheKey(SearchTarget.Web, entry, "Bing") != service.BuildCacheKey(SearchTarget.Web, entry, "Brave"), "engine change uses a distinct tab");
        Check(new Uri(service.BuildAiLoginUrl("Microsoft Copilot")).Host == "copilot.microsoft.com", "Copilot login URL");
        Check(service.BuildAiPrompt(entry).StartsWith("Help me troubleshoot this Windows Event Viewer entry."), "default prompt preserved");
        var custom = service.BuildAiPrompt(entry, "ID={EventId} {Message} {Unknown}");
        Check(custom.StartsWith("ID=41 ") && custom.Contains("{Provider}") && custom.EndsWith("{Unknown}"), "template substitution is single-pass");
        Check(service.BuildAiCacheKey("Claude", entry, "one") != service.BuildAiCacheKey("Claude", entry, "two"), "edited prompt gets its own tab");
        var settings = new AppSettings { SearchEngine = "Brave", SelectedAiProvider = "Microsoft Copilot", UseAdvancedAiPrompt = true, AdvancedAiPrompt = "Test {Message}", Appearance = new() { BackgroundHueShift = 120 } };
        var roundTrip = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(new SettingsViewModel(settings).ToSettings()))!;
        Check(roundTrip.SearchEngine == "Brave" && roundTrip.AdvancedAiPrompt == settings.AdvancedAiPrompt && roundTrip.Appearance.BackgroundHueShift == 120, "new settings survive save/load");
        var copy = settings.Copy(); copy.Appearance.BackgroundHueShift = 0;
        Check(settings.Appearance.BackgroundHueShift == 120, "appearance copy is independent");
        Check(new SettingsViewModel(JsonSerializer.Deserialize<AppSettings>("{}")!).AdvancedAiPrompt == SearchUrlService.DefaultAiPromptTemplate, "old settings get default prompt");
        foreach (var hex in new[] { "#0E1B1F", "#55C27A", "#FB7185", "#FBBF24" })
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            Check(HueColor.Rotate(color, 0) == color, hex + " unchanged at default hue");
            Check(Enumerable.Range(0, 24).All(i => Math.Abs(HueColor.GetLightness(color) - HueColor.GetLightness(HueColor.Rotate(color, i * 15))) < 0.004), hex + " lightness preserved around wheel");
        }
        var accent = ThemeService.SeverityBrush(EventSeverity.Error);
        ThemeService.Apply(new() { ErrorHueShift = 100 });
        Check(ReferenceEquals(accent, ThemeService.SeverityBrush(EventSeverity.Error)) && accent.Color != (Color)ColorConverter.ConvertFromString("#FB7185"), "existing severity brush updates live");
        ThemeService.Apply(new());
    }

    private void CheckStorage()
    {
        var root = Path.Combine(_output, "storage-" + Guid.NewGuid().ToString("N"));
        var roaming = Path.Combine(root, "Roaming");
        var local = Path.Combine(root, "Local");
        var executable = Path.Combine(root, "App");
        var dataTarget = Path.Combine(roaming, "ElkaSoft", "ELKA EventLens", "Saves");
        var localTarget = Path.Combine(local, "ElkaSoft", "ELKA EventLens");

        static void Seed(string file, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, contents);
        }

        var fresh = AppStorageService.ResolveFolders(executable, false, roaming, local);
        Check(fresh.Data == dataTarget, "installed saves use ElkaSoft/app/Saves");
        Check(fresh.Browser == Path.Combine(localTarget, "WebView2Profile") && fresh.Logs == Path.Combine(localTarget, "Logs"), "installed local data uses ElkaSoft/app");
        Check(fresh.Notes.Count == 0 && !Directory.Exists(roaming), "fresh paths do not create legacy data");

        var legacyData = Path.Combine(roaming, "ELKA.EventLens");
        var legacyLocal = Path.Combine(local, "ELKA", "EventLens");
        Seed(Path.Combine(legacyData, "settings.json"), "{\"SearchEngine\":\"Brave\"}");
        Seed(Path.Combine(legacyData, "events.json"), "[]");
        Seed(Path.Combine(legacyLocal, "WebView2Profile", "Default", "Network", "Cookies"), "Synthetic session fixture");
        Seed(Path.Combine(legacyLocal, "Logs", "ELKA.EventLens-test.log"), "Existing activity");
        var savedTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(legacyLocal, "Logs", "ELKA.EventLens-test.log"), savedTime);

        var portable = AppStorageService.ResolveFolders(executable, true, roaming, local);
        Check(portable.Data == Path.Combine(executable, "Saves") && portable.Browser == Path.Combine(executable, "WebView2Profile") && portable.Logs == Path.Combine(executable, "Logs"), "portable data stays beside EXE");
        Check(Directory.Exists(legacyData) && !Directory.Exists(dataTarget) && portable.Notes.Count == 0, "portable mode never migrates installed data");

        var deferred = AppStorageService.ResolveFolders(executable, false, roaming, local, canMigrate: false);
        Check(deferred.Data == legacyData && deferred.Browser == Path.Combine(legacyLocal, "WebView2Profile") && deferred.Notes.Count == 3, "another running copy defers migration without losing old paths");
        Check(!Directory.Exists(dataTarget), "deferred migration does not leave empty destination");

        var moved = AppStorageService.ResolveFolders(executable, false, roaming, local);
        Check(moved.Data == dataTarget && JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(moved.Data, "settings.json")))!.SearchEngine == "Brave", "existing settings migrate intact");
        Check(File.ReadAllText(Path.Combine(moved.Data, "events.json")) == "[]", "existing history migrates intact");
        Check(File.ReadAllText(Path.Combine(moved.Browser, "Default", "Network", "Cookies")) == "Synthetic session fixture", "nested browser profile migrates intact");
        Check(File.ReadAllText(Path.Combine(moved.Logs, "ELKA.EventLens-test.log")) == "Existing activity" && File.GetLastWriteTimeUtc(Path.Combine(moved.Logs, "ELKA.EventLens-test.log")) == savedTime, "activity logs and timestamps survive migration");
        Check(!Directory.Exists(legacyData) && !Directory.Exists(Path.Combine(legacyLocal, "WebView2Profile")) && moved.Notes.Count == 3, "migration moves whole folders and records changes");
        Check(AppStorageService.ResolveFolders(executable, false, roaming, local).Notes.Count == 0, "migration is idempotent");

        Seed(Path.Combine(legacyData, "settings.json"), "Older conflicting settings");
        var existing = AppStorageService.ResolveFolders(executable, false, roaming, local);
        Check(existing.Data == dataTarget && File.ReadAllText(Path.Combine(dataTarget, "settings.json")).Contains("Brave"), "existing destination settings are never overwritten");
        Check(File.ReadAllText(Path.Combine(legacyData, "settings.json")) == "Older conflicting settings", "conflicting legacy folder is left intact");
        AppStorageService.MarkMigrationComplete(existing);
        Check(File.Exists(existing.CompletionMarker), "successful layout adoption records one-time migration marker");
        File.Delete(Path.Combine(dataTarget, "settings.json"));
        var completed = AppStorageService.ResolveFolders(executable, false, roaming, local);
        Check(completed.Data == dataTarget && !File.Exists(Path.Combine(dataTarget, "settings.json")), "completed migration never reimports obsolete legacy settings");
        Check(completed.Notes.Count == 0, "completed migration skips legacy layout entirely");

        var blockedRoaming = Path.Combine(root, "BlockedRoaming");
        var blockedLocal = Path.Combine(root, "BlockedLocal");
        var blockedLegacy = Path.Combine(blockedRoaming, "ELKA.EventLens");
        var blockedTarget = Path.Combine(blockedRoaming, "ElkaSoft", "ELKA EventLens", "Saves");
        Seed(Path.Combine(blockedLegacy, "settings.json"), "Recoverable settings");
        Seed(blockedTarget, "A conflicting file prevents the directory move");
        var blocked = AppStorageService.ResolveFolders(executable, false, blockedRoaming, blockedLocal);
        Check(blocked.Data == blockedLegacy && blocked.Notes.Any(note => note.Contains("deferred")), "failed migration keeps using original folder");
        AppStorageService.MarkMigrationComplete(blocked);
        Check(!File.Exists(Path.Combine(blockedLocal, "ElkaSoft", "ELKA EventLens", "storage-layout-v1.complete")) && blocked.CompletionMarker is null, "blocked migration cannot mark itself complete");
        Check(File.ReadAllText(blockedTarget).StartsWith("A conflicting file") && File.ReadAllText(Path.Combine(blockedLegacy, "settings.json")) == "Recoverable settings", "failed migration preserves both source and conflicting file");
        File.Delete(blockedTarget);
        var retry = AppStorageService.ResolveFolders(executable, false, blockedRoaming, blockedLocal);
        Check(retry.Data == blockedTarget && File.ReadAllText(Path.Combine(retry.Data, "settings.json")) == "Recoverable settings", "migration retries successfully after obstruction is removed");
    }

    private async Task CheckLayoutAsync()
    {
        var cards = Enumerable.Range(0, 30).Select(_ => new EventCardViewModel(Sample(), new EventExplanationService().Explain(Sample()), (_, _) => { }, _ => { }) { IsExpanded = true }).ToArray();
        var list = new ListBox { ItemsSource = cards, BorderThickness = new Thickness(0), Background = ThemeService.GetBrush("AppBackgroundBrush") };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        ScrollViewer.SetCanContentScroll(list, true);
        VirtualizingPanel.SetScrollUnit(list, ScrollUnit.Pixel);
        var factory = new FrameworkElementFactory(typeof(EventCard));
        list.ItemTemplate = new DataTemplate { VisualTree = factory };
        list.ItemContainerStyle = new Style(typeof(ListBoxItem));
        list.ItemContainerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        var window = new Window { Title = "ELKA layout checks", Width = 1000, Height = 700, Content = list };
        window.Show();
        await Task.Delay(150);
        var outer = Descendants<ScrollViewer>(list).First();
        var card = Descendants<EventCard>(list).First();
        var message = Descendants<TextBlock>(card).First(x => x.Text == cards[0].Entry.Message);
        Check(message.ActualWidth <= 1000 && message.ActualHeight > 50, "long message wraps in expanded card");
        Check(outer.ScrollableWidth == 0, "event list has no horizontal overflow");
        var raw = Descendants<TextBox>(card).First(x => x.Text == cards[0].Entry.RawXml);
        Check(Math.Abs(raw.ActualWidth - 500) < 1 && Math.Abs(raw.ActualHeight - 165) < 1, "raw XML remains 500 by 165");
        Check(Descendants<TextBlock>(card).Any(x => x.Text == cards[0].Title && x.ActualWidth > 0), "expander header is visible");
        Snapshot(window, "events-wide.png");
        message.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent });
        await Task.Delay(250);
        Check(Math.Abs(outer.VerticalOffset - 36) < 2, $"wheel advances 36 pixels, actual {outer.VerticalOffset:F1}");
        window.Width = 600;
        outer.ScrollToTop();
        await Task.Delay(150);
        Check(outer.ScrollableWidth == 0 && raw.ActualWidth <= 500 && raw.ActualWidth > 400, "split-pane layout constrains long text and XML");
        Snapshot(window, "events-narrow.png");
        window.Width = 1000;
        await Task.Delay(100);
        Check(Math.Abs(raw.ActualWidth - 500) < 1, "XML width recovers after resizing");
        window.Close();

        var settingsModel = new SettingsViewModel(new AppSettings(), updateService: UpdateChecks.UiService());
        var settings = new SettingsWindow { DataContext = settingsModel };
        settings.Show();
        await Task.Delay(100);
        Snapshot(settings, "settings.png");
        Check(Descendants<ScrollViewer>(settings).Any(x => x.ScrollableHeight > 0), "settings content scrolls");
        Check(Descendants<Button>(settings).Any(button => Equals(button.Content, "Check for updates") && button.IsVisible), "Settings exposes update check button");
        await settingsModel.Updates.CheckAsync();
        settings.Width = 460;
        await Task.Delay(100);
        var downloadButton = Descendants<Button>(settings).Single(button => Equals(button.Content, "Download and install"));
        Check(downloadButton.IsVisible && downloadButton.IsEnabled, "Settings exposes install action only for available update");
        var downloadPosition = downloadButton.TransformToAncestor(settings).Transform(new Point(0, 0));
        Check(downloadPosition.X + downloadButton.ActualWidth < settings.ActualWidth, "update controls fit minimum Settings width");
        Snapshot(settings, "settings-update-ready.png");
        var appearance = new AppearanceWindow { DataContext = new AppearanceViewModel(new()), Owner = settings };
        appearance.Show();
        await Task.Delay(100);
        ((AppearanceViewModel)appearance.DataContext).Colors[0].Hue = 300;
        Snapshot(appearance, "appearance.png");
        appearance.Close();
        Check(ThemeService.Current.BackgroundHueShift == 0, "closing appearance cancels live preview");
        settings.Close();
    }

    private async Task CheckBrowserAsync()
    {
        var browser = new WebView2();
        var window = new Window { Title = "ELKA local browser checks", Width = 800, Height = 600, Content = browser };
        window.Show();
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(_output, "BrowserProfile"));
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.SetVirtualHostNameToFolderMapping("elka.test", Path.Combine(AppContext.BaseDirectory, "Fixtures"), CoreWebView2HostResourceAccessKind.DenyCors);
            await BrowserThemeService.ApplyAsync(browser, true);
            var loaded = new TaskCompletionSource();
            browser.NavigationCompleted += (_, e) => { if (e.IsSuccess) loaded.TrySetResult(); else loaded.TrySetException(new Exception("Fixture failed to load")); };
            browser.Source = new Uri("https://elka.test/composer.html");
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Check(await browser.CoreWebView2.ExecuteScriptAsync("matchMedia('(prefers-color-scheme: dark)').matches") == "true", "browser dark preference is active");
            var tab = new SearchTabViewModel("test", "Test", "Test", "Synthetic diagnostic", "https://elka.test/composer.html", isAiTab: true, autoPrompt: "Synthetic diagnostic");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await AiPromptSubmissionService.SubmitAsync(browser, tab, timeout.Token);
            Check(await browser.CoreWebView2.ExecuteScriptAsync("window.sends") == "1", "prompt sends once");
            Check(!tab.IsPromptExpanded && tab.PromptStatus == "Prompt sent", "successful send collapses prompt panel");
            await AiPromptSubmissionService.SubmitAsync(browser, tab, timeout.Token);
            Check(await browser.CoreWebView2.ExecuteScriptAsync("window.sends") == "1", "repeat navigation cannot resend prompt");
            await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('textarea').outerHTML = '<div id=\"prompt-textarea\" contenteditable=\"true\" style=\"min-height:160px\"></div>'; window.sends = 0;");
            var multiline = new SearchTabViewModel("multiline", "Test", "Test", "", "https://elka.test/composer.html", isAiTab: true, autoPrompt: "First line\r\n\r\nSecond line");
            await AiPromptSubmissionService.SubmitAsync(browser, multiline, timeout.Token);
            Check(await browser.CoreWebView2.ExecuteScriptAsync("window.sends") == "1", "multiline prompt sends through contenteditable composer");
            var capture = await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", "{\"format\":\"png\"}");
            var png = Convert.FromBase64String(JsonDocument.Parse(capture).RootElement.GetProperty("data").GetString()!);
            File.WriteAllBytes(Path.Combine(_output, "browser-dark.png"), png);
            using var stream = new MemoryStream(png);
            var bitmap = BitmapFrame.Create(stream);
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4];
            converted.CopyPixels(new Int32Rect(5, 5, 1, 1), pixel, 4, 0);
            Check(pixel[0] < 100 && pixel[1] < 100 && pixel[2] < 100, "forced dark mode darkens a white page");
            await BrowserThemeService.ApplyAsync(browser, false);
            Check(await browser.CoreWebView2.ExecuteScriptAsync("!!document.getElementById('elka-eventlens-browser-theme')") == "false", "disabling dark mode removes injected style");
        }
        finally { browser.Dispose(); window.Close(); }
    }

    private void Snapshot(Window window, string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(_output, name));
        encoder.Save(stream);
    }

    private async Task CheckTabsAsync()
    {
        var storage = new SettingsService();
        storage.SaveSettings(new AppSettings { LoadRecentOnStartup = false, MonitorApplication = false, MonitorSystem = false, MonitorSecurity = false });
        storage.SaveHistory([], 500);
        using var model = new MainWindowViewModel(storage, new WindowsEventLogService(), new EventExplanationService(), new SearchUrlService(), new NotificationService());
        var first = new SearchTabViewModel("a", "A", "ChatGPT", Sample().Message, "about:blank", isAiTab: true);
        var second = new SearchTabViewModel("b", "B", "Web", "Test", "about:blank");
        model.SearchTabs.Add(first);
        model.SearchTabs.Add(second);
        model.SelectedSearchTab = first;
        typeof(MainWindowViewModel).GetProperty(nameof(MainWindowViewModel.IsSearchPaneOpen))!.SetValue(model, true);
        var window = new MainWindow { DataContext = model };
        window.Show();
        try
        {
            async Task<WebView2> ReadyBrowser()
            {
                for (var i = 0; i < 80; i++)
                {
                    var item = Descendants<WebView2>(window).FirstOrDefault();
                    if (item?.CoreWebView2 is not null && item.Source is not null) return item;
                    await Task.Delay(100);
                }
                throw new InvalidOperationException("Main window browser did not initialize");
            }
            var browserA = await ReadyBrowser();
            Check(Descendants<TextBox>(window).Any(x => x.Text == first.Query && x.ActualHeight <= 145 && x.IsVisible), "AI prompt panel has a bounded height");
            await browserA.CoreWebView2.ExecuteScriptAsync("window.elkaRetentionTest = 42;");
            model.SelectedSearchTab = second;
            await Task.Delay(100);
            var browserB = await ReadyBrowser();
            Check(!ReferenceEquals(browserA, browserB), "tabs have distinct browser instances");
            model.SelectedSearchTab = first;
            await Task.Delay(150);
            var restored = await ReadyBrowser();
            Check(ReferenceEquals(browserA, restored) && await restored.CoreWebView2.ExecuteScriptAsync("window.elkaRetentionTest") == "42", "switching tabs retains page state");
            first.IsPromptExpanded = false;
            await Task.Delay(100);
            Check(!Descendants<TextBox>(window).Any(x => x.Text == first.Query && x.IsVisible), "prompt hide releases browser space");
            Snapshot(window, "main-prompt-collapsed.png");
            model.CloseSearchTabCommand.Execute(first);
            model.CloseSearchTabCommand.Execute(second);
            await Task.Delay(100);
            Check(((ColumnDefinition)window.FindName("SearchPaneColumn")).ActualWidth == 0 && ((ColumnDefinition)window.FindName("SearchSplitterColumn")).ActualWidth == 0, "closing final tab removes pane and divider");
        }
        finally { window.AllowClose(); window.Close(); }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
