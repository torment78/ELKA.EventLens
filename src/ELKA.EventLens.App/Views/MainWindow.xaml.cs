using ELKA.EventLens.App.Services;
using ELKA.EventLens.App.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;

namespace ELKA.EventLens.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private double _lastSearchPaneWidth = 560;
    private bool _allowClose;
    private readonly Dictionary<SearchTabViewModel, WebView2> _browsers = new();
    private readonly Dictionary<WebView2, CancellationTokenSource> _browserLifetimes = new();
#if false
    // Previous CSS-only dark mode, superseded by BrowserThemeService.
    private const string EmbeddedBrowserThemeScript = """
        (() => {
            const id = 'elka-eventlens-browser-theme';
            if (document.getElementById(id)) {
                return;
            }

            const style = document.createElement('style');
            style.id = id;
            style.textContent = `
                :root { color-scheme: dark !important; }
                html { background: #081316 !important; }
                body { background-color: #081316 !important; }
                input, textarea, select, button { color-scheme: dark !important; }
                html, body { scrollbar-color: #2C3E43 #081316; }
                ::-webkit-scrollbar { width: 12px; height: 12px; background: #081316; }
                ::-webkit-scrollbar-track { background: #081316; border-radius: 999px; }
                ::-webkit-scrollbar-thumb { background: #2C3E43; border: 3px solid #081316; border-radius: 999px; }
                ::-webkit-scrollbar-thumb:hover { background: #3F5B55; }
                ::-webkit-scrollbar-corner { background: #081316; }
            `;
            (document.head || document.documentElement).appendChild(style);
        })();
        """;
#endif

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (!ReferenceEquals(_viewModel, viewModel))
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= MainViewModel_PropertyChanged;
                _viewModel.SearchTabs.CollectionChanged -= SearchTabs_CollectionChanged;
            }

            _viewModel = viewModel;
            _viewModel.PropertyChanged += MainViewModel_PropertyChanged;
            _viewModel.SearchTabs.CollectionChanged += SearchTabs_CollectionChanged;
        }

        ApplySearchPaneLayout();

        if (viewModel.StartMinimized)
        {
            WindowState = WindowState.Minimized;
        }

        await viewModel.InitializeAsync();
        if (System.Windows.Application.Current is App app)
            await app.CleanupLegacyStorageAsync();
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.SearchPaneVisibility)
            or nameof(MainWindowViewModel.SearchPaneColumnWidth)
            or nameof(MainWindowViewModel.SearchPaneSplitterWidth))
        {
            ApplySearchPaneLayout();
        }
    }

    private void ApplySearchPaneLayout()
    {
        if (_viewModel is null)
        {
            return;
        }

        var isOpen = _viewModel.SearchPaneVisibility == Visibility.Visible;
        if (isOpen)
        {
            var currentWidth = SearchPaneColumn.ActualWidth > 0
                ? SearchPaneColumn.ActualWidth
                : SearchPaneColumn.Width.IsAbsolute && SearchPaneColumn.Width.Value > 0
                    ? SearchPaneColumn.Width.Value
                    : _lastSearchPaneWidth;

            _lastSearchPaneWidth = Math.Clamp(currentWidth, 420, 900);
            SearchSplitterColumn.Width = new GridLength(8);
            SearchPaneColumn.Width = new GridLength(_lastSearchPaneWidth);
            SearchGridSplitter.Visibility = Visibility.Visible;
            SearchPaneBorder.Visibility = Visibility.Visible;
            return;
        }

        if (SearchPaneColumn.ActualWidth > 0)
        {
            _lastSearchPaneWidth = Math.Clamp(SearchPaneColumn.ActualWidth, 420, 900);
        }

        SearchGridSplitter.Visibility = Visibility.Collapsed;
        SearchPaneBorder.Visibility = Visibility.Collapsed;
        SearchSplitterColumn.Width = new GridLength(0);
        SearchPaneColumn.Width = new GridLength(0);
    }

    private void SearchBrowserHost_Loaded(object sender, RoutedEventArgs e) => AttachBrowser((ContentControl)sender);

    private void SearchBrowserHost_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is ContentControl host && host.IsLoaded) AttachBrowser(host);
    }

    private async void AttachBrowser(ContentControl host)
    {
        if (host.DataContext is not SearchTabViewModel tab) return;
        if (_browsers.TryGetValue(tab, out var existing))
        {
            if (existing.Parent is ContentControl previous && !ReferenceEquals(previous, host)) previous.Content = null;
            host.Content = existing;
            return;
        }

        var browser = new WebView2 { DataContext = tab, DefaultBackgroundColor = System.Drawing.Color.FromArgb(8, 19, 22) };
        var lifetime = new CancellationTokenSource();
        var token = lifetime.Token;
        _browsers[tab] = browser;
        _browserLifetimes[browser] = lifetime;
        host.Content = browser;
        tab.PropertyChanged += SearchTab_PropertyChanged;
        try
        {
            var environment = await WebViewEnvironmentService.GetSharedEnvironmentAsync();
            if (token.IsCancellationRequested) return;
            await browser.EnsureCoreWebView2Async(environment);
            if (token.IsCancellationRequested) return;
            await BrowserThemeService.ApplyAsync(browser, tab.PreferDarkWebPages);
            if (token.IsCancellationRequested) return;
            browser.NavigationCompleted += async (_, args) =>
            {
                if (args.IsSuccess && !token.IsCancellationRequested)
                    await AiPromptSubmissionService.SubmitAsync(browser, tab, token);
            };
            browser.Source = tab.Source;
        }
        catch
        {
            tab.PromptStatus = "Browser unavailable - try Open external";
        }
    }

    private async void SearchTab_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SearchTabViewModel tab && e.PropertyName == nameof(SearchTabViewModel.PreferDarkWebPages)
            && _browsers.TryGetValue(tab, out var browser))
        {
            try { await BrowserThemeService.ApplyAsync(browser, tab.PreferDarkWebPages); }
            catch { tab.PromptStatus = "Browser theme could not be updated"; }
        }
    }

    private void SearchTabs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is null) return;
        foreach (var tab in _browsers.Keys.Where(tab => !_viewModel.SearchTabs.Contains(tab)).ToArray()) DisposeBrowser(tab);
    }

    private void DisposeBrowser(SearchTabViewModel tab)
    {
        if (!_browsers.Remove(tab, out var browser)) return;
        tab.PropertyChanged -= SearchTab_PropertyChanged;
        if (_browserLifetimes.Remove(browser, out var lifetime))
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }
        if (browser.Parent is ContentControl host) host.Content = null;
        browser.Dispose();
    }

#if false
    // Legacy browser setup and asynchronous page script, retained for reference.
    // Cached browser instances and AiPromptSubmissionService now own this flow.
    private async void SearchWebView_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is WebView2 webView)
        {
            await ConfigureSearchWebViewAsync(webView);
        }
    }

    private async void SearchWebView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is WebView2 webView)
        {
            await ConfigureSearchWebViewAsync(webView);
        }
    }

    private static async Task ConfigureSearchWebViewAsync(WebView2 webView)
    {
        if (webView.DataContext is not SearchTabViewModel tab)
        {
            return;
        }

        try
        {
            var state = GetBrowserState(webView);
            webView.DefaultBackgroundColor = System.Drawing.ColorTranslator.FromHtml("#081316");

            if (webView.CoreWebView2 is null)
            {
                var environment = await WebViewEnvironmentService.GetSharedEnvironmentAsync();
                await webView.EnsureCoreWebView2Async(environment);
            }

            if (!state.NavigationHooked)
            {
                webView.NavigationCompleted += SearchWebView_NavigationCompleted;
                state.NavigationHooked = true;
            }

            var coreWebView = webView.CoreWebView2;
            if (coreWebView is null)
            {
                return;
            }

            ApplyPreferredColorScheme(webView, tab.PreferDarkWebPages);

            if (tab.PreferDarkWebPages)
            {
                if (!state.ThemeScriptAdded)
                {
                    await coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(EmbeddedBrowserThemeScript);
                    state.ThemeScriptAdded = true;
                }

                await coreWebView.ExecuteScriptAsync(EmbeddedBrowserThemeScript);
            }

            if (webView.Source != tab.Source)
            {
                webView.Source = tab.Source;
            }
            else
            {
                await TrySubmitAiPromptAsync(webView, tab, state);
            }
        }
        catch
        {
            // Search still works if a page or runtime refuses cosmetic script injection.
        }
    }

    private static async void SearchWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (sender is WebView2 webView
            && webView.DataContext is SearchTabViewModel tab
            && webView.Tag is BrowserState state)
        {
            await TrySubmitAiPromptAsync(webView, tab, state);
        }
    }

    private static async Task TrySubmitAiPromptAsync(WebView2 webView, SearchTabViewModel tab, BrowserState state)
    {
        if (!tab.ShouldAutoSubmitPrompt
            || webView.CoreWebView2 is null
            || state.ActivePromptKeys.Contains(tab.Key)
            || !tab.TryBeginAutoSubmitPrompt())
        {
            return;
        }

        state.ActivePromptKeys.Add(tab.Key);
        var completed = false;

        try
        {
            if (!ReferenceEquals(webView.DataContext, tab))
            {
                return;
            }

            var result = await webView.CoreWebView2.ExecuteScriptAsync(BuildAiAutoSubmitScript(tab.AutoPrompt!));
            var submitResult = ReadScriptStringResult(result);

            if (string.Equals(submitResult, "submitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(submitResult, "already-submitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result, "true", StringComparison.OrdinalIgnoreCase))
            {
                completed = true;
                tab.MarkAutoPromptCompleted();
                state.SubmittedPromptKeys.Add(tab.Key);
            }
        }
        catch
        {
            // A logged-out page, site redesign, or navigation race should not break the app shell.
        }
        finally
        {
            state.ActivePromptKeys.Remove(tab.Key);

            if (!completed)
            {
                tab.ResetAutoPromptAttempt();
            }
        }
    }

    private static string? ReadScriptStringResult(string result)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(result);
        }
        catch
        {
            return result.Trim('"');
        }
    }

    private static string BuildAiAutoSubmitScript(string prompt)
    {
        var promptJson = JsonSerializer.Serialize(prompt);
        return $$"""
            (async () => {
                const prompt = {{promptJson}};
                const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
                const visible = element => {
                    if (!element) return false;
                    const style = window.getComputedStyle(element);
                    const rect = element.getBoundingClientRect();
                    return style.display !== 'none'
                        && style.visibility !== 'hidden'
                        && rect.width > 0
                        && rect.height > 0;
                };

                const editorSelectors = [
                    'textarea[data-testid="prompt-textarea"]',
                    'textarea#prompt-textarea',
                    '#prompt-textarea',
                    'div[contenteditable="true"][data-testid="prompt-textarea"]',
                    'div[contenteditable="true"][aria-label*="Message"]',
                    'div[contenteditable="true"][aria-label*="message"]',
                    'div[contenteditable="true"][aria-label*="Ask"]',
                    'div[contenteditable="true"][aria-label*="ask"]',
                    'div.ProseMirror[contenteditable="true"]',
                    'div[contenteditable="true"]',
                    'textarea'
                ];

                const sendSelectors = [
                    'button[data-testid="send-button"]',
                    'button[aria-label="Send"]',
                    'button[aria-label="Send prompt"]',
                    'button[aria-label="Send message"]',
                    'button[aria-label="Send Message"]',
                    'button[aria-label*="Send"]',
                    'button[aria-label*="send"]',
                    'button[type="submit"]'
                ];

                function findEditor() {
                    for (const selector of editorSelectors) {
                        const matches = Array.from(document.querySelectorAll(selector));
                        const editor = matches.find(visible);
                        if (editor) return editor;
                    }

                    return null;
                }

                function setNativeValue(element, value) {
                    const prototype = element instanceof HTMLTextAreaElement
                        ? HTMLTextAreaElement.prototype
                        : element instanceof HTMLInputElement
                            ? HTMLInputElement.prototype
                            : null;

                    const setter = prototype && Object.getOwnPropertyDescriptor(prototype, 'value')?.set;
                    if (setter) {
                        setter.call(element, value);
                    } else {
                        element.value = value;
                    }
                }

                function setContentEditableText(element, value) {
                    element.focus();
                    const selection = window.getSelection();
                    const range = document.createRange();
                    range.selectNodeContents(element);
                    selection.removeAllRanges();
                    selection.addRange(range);
                    document.execCommand('insertText', false, value);

                    const currentText = (element.innerText || element.textContent || '').trim();
                    if (currentText.length < Math.min(value.trim().length, 40)) {
                        element.textContent = value;
                    }
                }

                function putPromptInEditor(editor) {
                    editor.focus();

                    if (editor instanceof HTMLTextAreaElement || editor instanceof HTMLInputElement) {
                        setNativeValue(editor, prompt);
                    } else {
                        setContentEditableText(editor, prompt);
                    }

                    editor.dispatchEvent(new InputEvent('beforeinput', {
                        bubbles: true,
                        cancelable: true,
                        inputType: 'insertText',
                        data: prompt
                    }));
                    editor.dispatchEvent(new InputEvent('input', {
                        bubbles: true,
                        inputType: 'insertText',
                        data: prompt
                    }));
                    editor.dispatchEvent(new Event('change', { bubbles: true }));
                }

                function buttonLabel(button) {
                    return `${button.getAttribute('aria-label') || ''} ${button.title || ''} ${button.textContent || ''} ${button.dataset?.testid || ''}`.toLowerCase();
                }

                function findSendButton() {
                    for (const selector of sendSelectors) {
                        const matches = Array.from(document.querySelectorAll(selector));
                        const button = matches.find(item => visible(item) && !item.disabled && item.getAttribute('aria-disabled') !== 'true');
                        if (button) return button;
                    }

                    const buttons = Array.from(document.querySelectorAll('button'));
                    return buttons.find(button => {
                        const label = buttonLabel(button);
                        return visible(button)
                            && !button.disabled
                            && button.getAttribute('aria-disabled') !== 'true'
                            && label.includes('send')
                            && !label.includes('resend')
                            && !label.includes('stop');
                    });
                }

                for (let attempt = 0; attempt < 60; attempt++) {
                    const editor = findEditor();
                    if (editor) {
                        putPromptInEditor(editor);
                        await sleep(300);

                        const sendButton = findSendButton();
                        if (sendButton) {
                            sendButton.click();
                            return 'submitted';
                        }
                    }

                    await sleep(500);
                }

                return 'not-ready';
            })();
            """;
    }

    private static BrowserState GetBrowserState(WebView2 webView)
    {
        if (webView.Tag is BrowserState state)
        {
            return state;
        }

        state = new BrowserState();
        webView.Tag = state;
        return state;
    }

    private static void ApplyPreferredColorScheme(WebView2 webView, bool preferDarkWebPages)
    {
        try
        {
            var profile = webView.CoreWebView2?.Profile;
            var property = profile?.GetType().GetProperty("PreferredColorScheme");
            if (profile is null || property is null)
            {
                return;
            }

            var valueName = preferDarkWebPages ? "Dark" : "Auto";
            var value = Enum.Parse(property.PropertyType, valueName);
            property.SetValue(profile, value);
        }
        catch
        {
            // Older WebView2 runtimes do not expose the preferred color scheme API.
        }
    }

#endif

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && !_allowClose)
        {
            HideToTray();
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= MainViewModel_PropertyChanged;
            _viewModel.SearchTabs.CollectionChanged -= SearchTabs_CollectionChanged;
        }

        foreach (var tab in _browsers.Keys.ToArray()) DisposeBrowser(tab);

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveState();
        }
    }

    private void HideToTray()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SaveState();
        }

        Hide();
    }

#if false
    private sealed class BrowserState
    {
        public bool NavigationHooked { get; set; }

        public bool ThemeScriptAdded { get; set; }

        public HashSet<string> SubmittedPromptKeys { get; } = new(StringComparer.Ordinal);

        public HashSet<string> ActivePromptKeys { get; } = new(StringComparer.Ordinal);
    }
#endif
}




