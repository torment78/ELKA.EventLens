using ELKA.EventLens.App.ViewModels;
using Microsoft.Web.WebView2.Wpf;
using System.Text.Json;

namespace ELKA.EventLens.App.Services;

public static class AiPromptSubmissionService
{
    public static async Task SubmitAsync(WebView2 browser, SearchTabViewModel tab, CancellationToken token)
    {
        if (browser.CoreWebView2 is null || !tab.ShouldAutoSubmitPrompt ||
            !IsProviderPage(browser.CoreWebView2.Source, tab.Source.Host) || !tab.TryBeginAutoSubmitPrompt()) return;
        var consumed = false;
        tab.PromptStatus = "Preparing prompt...";
        try
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                token.ThrowIfCancellationRequested();
                if (!IsProviderPage(browser.CoreWebView2.Source, tab.Source.Host)) break;
                var result = await browser.CoreWebView2.ExecuteScriptAsync(BuildPrepareScript(tab.AutoPrompt!));
                var status = JsonSerializer.Deserialize<string>(result);
                if (status == "ready")
                {
                    // Consume BEFORE clicking: a navigation or tab switch cannot send this prompt twice.
                    tab.MarkAutoPromptCompleted();
                    consumed = true;
                    var sent = await browser.CoreWebView2.ExecuteScriptAsync("""
                        (() => {
                            const button = window.__elkaPrompt?.button;
                            if (!button?.isConnected || button.disabled || button.getAttribute('aria-disabled') === 'true') return false;
                            window.__elkaPrompt.button = null;
                            button.click();
                            return true;
                        })();
                        """);
                    if (sent == "true")
                    {
                        tab.PromptStatus = "Prompt sent";
                        tab.IsPromptExpanded = false;
                    }
                    else tab.PromptStatus = "Use the site's Send button";
                    return;
                }
                if (status == "occupied")
                {
                    tab.PromptStatus = "Draft already present - copy prompt to continue";
                    return;
                }
                await Task.Delay(500, token);
            }
            tab.PromptStatus = "Sign in, or copy the prompt";
        }
        catch (OperationCanceledException) { }
        catch
        {
            tab.PromptStatus = consumed ? "Check the conversation before sending again" : "Copy prompt to continue";
        }
        finally
        {
            if (!consumed) tab.ResetAutoPromptAttempt();
        }
    }

    private static bool IsProviderPage(string source, string host) => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase);

    private static string BuildPrepareScript(string prompt) => $$"""
        (() => {
            const prompt = {{JsonSerializer.Serialize(prompt)}};
            const normalize = text => text.replace(/\s+/g, ' ').trim();
            const visible = el => !!el && el.getBoundingClientRect().width > 0 && el.getBoundingClientRect().height > 0
                && getComputedStyle(el).visibility !== 'hidden';
            const state = window.__elkaPrompt ??= {};
            const selectors = ['#prompt-textarea', 'textarea[data-testid="prompt-textarea"]',
                'textarea#userInput', 'textarea[placeholder*="Message"]', 'textarea[placeholder*="Ask"]',
                'div.ProseMirror[contenteditable="true"]', '[contenteditable="true"][role="textbox"]', 'textarea'];
            const editor = selectors.flatMap(selector => Array.from(document.querySelectorAll(selector))).find(visible);
            if (!editor) return 'waiting';
            const current = (editor.value ?? editor.innerText ?? '').trim();
            if (state.editor !== editor || state.prompt !== prompt) {
                if (current && normalize(current) !== normalize(prompt)) return 'occupied';
                editor.focus();
                if (editor instanceof HTMLTextAreaElement) {
                    Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set.call(editor, prompt);
                } else {
                    const range = document.createRange();
                    range.selectNodeContents(editor);
                    const selection = getSelection();
                    selection.removeAllRanges(); selection.addRange(range);
                    document.execCommand('insertText', false, prompt);
                    if (!(editor.innerText || '').trim()) editor.textContent = prompt;
                }
                editor.dispatchEvent(new InputEvent('input', { bubbles: true, inputType: 'insertText', data: prompt }));
                editor.dispatchEvent(new Event('change', { bubbles: true }));
                state.editor = editor; state.prompt = prompt;
                return 'waiting';
            }
            if (normalize(current) !== normalize(prompt)) return 'occupied';
            const sendSelectors = ['button[data-testid="send-button"]', 'button[data-testid="submit-button"]',
                'button[aria-label="Send"]', 'button[aria-label="Send prompt"]', 'button[aria-label="Send message"]',
                'button[aria-label="Send Message"]', 'button[title="Send message"]'];
            const button = sendSelectors.flatMap(selector => Array.from(document.querySelectorAll(selector)))
                .find(el => visible(el) && !el.disabled && el.getAttribute('aria-disabled') !== 'true');
            if (!button) return 'waiting';
            state.button = button;
            return 'ready';
        })();
        """;
}
