using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ELKA.EventLens.App.Services;

public static class BrowserThemeService
{
    private sealed class State
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public string? ScriptId;
    }
    private static readonly ConditionalWeakTable<WebView2, State> States = new();

    public static async Task ApplyAsync(WebView2 browser, bool dark)
    {
        var state = States.GetOrCreateValue(browser);
        await state.Gate.WaitAsync();
        try
        {
            var core = browser.CoreWebView2;
            if (core is null) return;
            var background = ThemeService.GetBrush("AppBackgroundBrush").Color;
            browser.DefaultBackgroundColor = dark
                ? System.Drawing.Color.FromArgb(background.R, background.G, background.B)
                : System.Drawing.Color.White;
            core.Profile.PreferredColorScheme = dark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Auto;
            await core.CallDevToolsProtocolMethodAsync("Emulation.setEmulatedMedia", dark
                ? "{\"features\":[{\"name\":\"prefers-color-scheme\",\"value\":\"dark\"}]}"
                : "{\"features\":[]}");
            try
            {
                await core.CallDevToolsProtocolMethodAsync("Emulation.setAutoDarkModeOverride", dark ? "{\"enabled\":true}" : "{\"enabled\":false}");
            }
            catch (COMException) { /* Older runtimes still receive the native dark preference. */ }

            if (state.ScriptId is not null)
            {
                core.RemoveScriptToExecuteOnDocumentCreated(state.ScriptId);
                state.ScriptId = null;
            }
            var css = dark ? $$"""
                html, body { scrollbar-color: {{ThemeService.GetBrush("ScrollThumbBrush").Color}} {{background}}; }
                ::-webkit-scrollbar { width: 12px; height: 12px; }
                ::-webkit-scrollbar-track, ::-webkit-scrollbar-corner { background: {{background}}; }
                ::-webkit-scrollbar-thumb { background: {{ThemeService.GetBrush("ScrollThumbBrush").Color}}; border: 3px solid {{background}}; border-radius: 6px; }
                """ : "";
            // WPF colors include alpha first; CSS expects six RGB digits here.
            css = System.Text.RegularExpressions.Regex.Replace(css, "#FF([0-9A-F]{6})", "#$1");
            var script = $$"""
                (() => {
                    const apply = () => {
                        document.getElementById('elka-eventlens-browser-theme')?.remove();
                        const css = {{JsonSerializer.Serialize(css)}};
                        if (!css) return;
                        const style = document.createElement('style');
                        style.id = 'elka-eventlens-browser-theme';
                        style.textContent = css;
                        (document.head || document.documentElement).appendChild(style);
                    };
                    if (document.documentElement) apply();
                    else document.addEventListener('DOMContentLoaded', apply, { once: true });
                })();
                """;
            state.ScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(script);
            await core.ExecuteScriptAsync(script);
        }
        finally { state.Gate.Release(); }
    }
}
