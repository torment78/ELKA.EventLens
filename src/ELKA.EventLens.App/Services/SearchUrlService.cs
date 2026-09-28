using EventLogEntryModel = ELKA.EventLens.App.Models.EventLogEntry;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Security.Cryptography;

namespace ELKA.EventLens.App.Services;

public enum SearchTarget
{
    Web,
    Reddit,
    StackOverflow,
    Microsoft
}

public sealed class SearchUrlService
{
    public const string DefaultAiPromptTemplate = """
        Help me troubleshoot this Windows Event Viewer entry.
        Find likely causes, prioritize Microsoft/vendor documentation, and give practical next steps. Cite or name the sources you would rely on.

        Log: {LogName}
        Source/provider: {Provider}
        Event ID: {EventId}
        Level: {Level} / {Severity}
        Machine: {Machine}
        Time: {Time}

        Message:
        {Message}
        """;

    public static IReadOnlyList<string> SearchEngines { get; } = ["Brave", "Google", "DuckDuckGo", "Bing"];
    public static IReadOnlyList<string> AiProviders { get; } = ["ChatGPT", "Claude", "Microsoft Copilot"];
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "that", "this", "was", "were", "has", "have", "not",
        "error", "event", "windows", "service", "application", "system", "level", "user", "computer"
    };

    public string BuildUrl(SearchTarget target, EventLogEntryModel entry, string engine = "Bing")
    {
        var query = BuildQuery(entry);

        return target switch
        {
            SearchTarget.Reddit => EngineSearch(engine, $"{query} site:reddit.com"),
            SearchTarget.StackOverflow => $"https://stackoverflow.com/search?q={Uri.EscapeDataString(query)}",
            SearchTarget.Microsoft => EngineSearch(engine, $"{query} (site:learn.microsoft.com OR site:support.microsoft.com)"),
            _ => EngineSearch(engine, query)
        };
    }

    public string BuildMicrosoftHelpUrl(EventLogEntryModel entry)
    {
        return BuildUrl(SearchTarget.Microsoft, entry);
    }

    public string BuildCacheKey(SearchTarget target, EventLogEntryModel entry, string engine = "Bing")
    {
        return $"{NormalizeSearchEngine(engine)}|{target}|{BuildQuery(entry)}";
    }

    public string BuildAiCacheKey(string provider, EventLogEntryModel entry, string? prompt = null)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prompt ?? BuildAiPrompt(entry))));
        return $"AI|{NormalizeAiProvider(provider)}|{digest}";
    }

    public string BuildTabTitle(SearchTarget target, EventLogEntryModel entry)
    {
        var source = string.IsNullOrWhiteSpace(entry.ProviderName) ? "Windows" : entry.ProviderName;
        return $"{GetTargetLabel(target)}: {entry.EventId} {source}";
    }

    public string BuildAiTabTitle(string provider, EventLogEntryModel entry)
    {
        return $"{GetAiProviderLabel(provider)}: {entry.EventId} {entry.ProviderName}";
    }

    public string GetTargetLabel(SearchTarget target)
    {
        return target switch
        {
            SearchTarget.Reddit => "Reddit",
            SearchTarget.StackOverflow => "Stack",
            SearchTarget.Microsoft => "Microsoft",
            _ => "Web"
        };
    }

    public string GetAiProviderLabel(string provider)
    {
        return NormalizeAiProvider(provider);
    }

    public string BuildAiProviderUrl(string provider)
    {
        return NormalizeAiProvider(provider) switch
        {
            "Claude" => "https://claude.ai/new",
            "Microsoft Copilot" => "https://copilot.microsoft.com/",
            _ => "https://chatgpt.com/"
        };
    }

    public string BuildAiLoginUrl(string provider)
    {
        return NormalizeAiProvider(provider) == "Claude" ? "https://claude.ai/login" : BuildAiProviderUrl(provider);
    }

    public string BuildAiPrompt(EventLogEntryModel entry, string? template = null)
    {
        var message = string.IsNullOrWhiteSpace(entry.Message)
            ? "No formatted event message was available."
            : entry.Message.Trim();

        if (message.Length > 2500)
        {
            message = message[..2500] + "...";
        }

        // The default wording is preserved in DefaultAiPromptTemplate above.
        var values = new Dictionary<string, string>
        {
            ["LogName"] = entry.LogName, ["Provider"] = entry.ProviderName,
            ["EventId"] = entry.EventId.ToString(CultureInfo.InvariantCulture),
            ["Level"] = entry.LevelDisplayName, ["Severity"] = entry.Severity.ToString(),
            ["Machine"] = entry.MachineName, ["Time"] = entry.TimeCreated?.ToString("O") ?? "Unknown",
            ["Message"] = message
        };
        // One pass prevents placeholders inside an event's message being expanded again.
        return Regex.Replace(string.IsNullOrWhiteSpace(template) ? DefaultAiPromptTemplate : template,
            @"\{(LogName|Provider|EventId|Level|Severity|Machine|Time|Message)\}",
            match => values[match.Groups[1].Value]);
    }

    public string BuildQuery(EventLogEntryModel entry)
    {
        var builder = new StringBuilder();
        builder.Append("Windows Event ID ");
        builder.Append(entry.EventId);

        if (!string.IsNullOrWhiteSpace(entry.ProviderName))
        {
            builder.Append(' ');
            builder.Append(entry.ProviderName);
        }

        var keywords = ExtractKeywords(entry.Message, 8);
        if (keywords.Count > 0)
        {
            builder.Append(' ');
            builder.AppendJoin(' ', keywords);
        }

        return builder.ToString();
    }

    public void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // The UI remains usable if the default browser cannot be launched.
        }
    }

    private static string EngineSearch(string engine, string query)
    {
        var endpoint = NormalizeSearchEngine(engine) switch
        {
            "Brave" => "https://search.brave.com/search?q=",
            "Google" => "https://www.google.com/search?q=",
            "DuckDuckGo" => "https://duckduckgo.com/?q=",
            _ => "https://www.bing.com/search?q="
        };
        return endpoint + Uri.EscapeDataString(query);
    }

    public static string NormalizeSearchEngine(string? engine) =>
        SearchEngines.FirstOrDefault(item => string.Equals(item, engine, StringComparison.OrdinalIgnoreCase)) ?? "Bing";

    public static string NormalizeAiProvider(string? provider)
    {
        if (provider?.Contains("claude", StringComparison.OrdinalIgnoreCase) == true) return "Claude";
        if (provider?.Contains("copilot", StringComparison.OrdinalIgnoreCase) == true) return "Microsoft Copilot";
        return "ChatGPT";
    }

    private static IReadOnlyList<string> ExtractKeywords(string message, int maxKeywords)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Array.Empty<string>();
        }

        return Regex.Matches(message, "[A-Za-z0-9][A-Za-z0-9_.-]{2,}")
            .Select(match => match.Value.Trim('.', ',', ';', ':', ')', '('))
            .Where(word => word.Length > 2 && !StopWords.Contains(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxKeywords)
            .ToList();
    }
}
