using ELKA.EventLens.App.Services;
using Microsoft.Web.WebView2.Wpf;
using System.Windows;

namespace ELKA.EventLens.App.Views;

public partial class AiLoginWindow : Window
{
    private readonly Uri _loginUri;
    private readonly bool _preferDarkWebPages;
#if false
    // Previous CSS-only dark mode, superseded by the shared BrowserThemeService.
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

    public AiLoginWindow(string provider, string loginUrl, bool preferDarkWebPages)
    {
        InitializeComponent();
        Title = $"{provider} login";
        ProviderTitleText.Text = $"{provider} login";
        _loginUri = new Uri(loginUrl);
        _preferDarkWebPages = preferDarkWebPages;
        Loaded += AiLoginWindow_Loaded;
    }

    private async void AiLoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LoginWebView.DefaultBackgroundColor = System.Drawing.ColorTranslator.FromHtml("#081316");
            var environment = await WebViewEnvironmentService.GetSharedEnvironmentAsync();
            await LoginWebView.EnsureCoreWebView2Async(environment);
            await BrowserThemeService.ApplyAsync(LoginWebView, _preferDarkWebPages);

            LoginWebView.Source = _loginUri;
        }
        catch
        {
            // The user can still close the dialog if WebView2 cannot initialize.
        }
    }

#if false
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

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        LoginWebView.Dispose();
        base.OnClosed(e);
    }
}
