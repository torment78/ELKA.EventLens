using System.Text.Json.Serialization;

namespace ELKA.EventLens.App.Models;

public sealed class EventLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime? TimeCreated { get; set; }

    public string LogName { get; set; } = string.Empty;

    public string ProviderName { get; set; } = string.Empty;

    public int EventId { get; set; }

    public long? RecordId { get; set; }

    public EventSeverity Severity { get; set; } = EventSeverity.Unknown;

    public string LevelDisplayName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public string RawXml { get; set; } = string.Empty;

    public string TaskDisplayName { get; set; } = string.Empty;

    public string OpcodeDisplayName { get; set; } = string.Empty;

    public string KeywordsDisplayNames { get; set; } = string.Empty;

    [JsonIgnore]
    public string IdentityKey => $"{LogName}|{RecordId}|{ProviderName}|{EventId}|{TimeCreated:O}";

    [JsonIgnore]
    public string RepeatKey => $"{LogName}|{ProviderName}|{EventId}|{Normalize(Message)}";

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 300 ? trimmed[..300] : trimmed;
    }
}
