using ELKA.EventLens.App.Models;
using System.IO;
using System.Text.Json;

namespace ELKA.EventLens.App.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _appFolder;
    private readonly string _settingsPath;
    private readonly string _historyPath;

    public SettingsService()
    {
        _appFolder = AppStorageService.DataFolder;
        _settingsPath = Path.Combine(_appFolder, "settings.json");
        _historyPath = Path.Combine(_appFolder, "events.json");
    }

    public AppSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_appFolder);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // Settings persistence should never stop event monitoring.
        }
    }

    public IReadOnlyList<EventLogEntry> LoadHistory()
    {
        try
        {
            if (!File.Exists(_historyPath))
            {
                return Array.Empty<EventLogEntry>();
            }

            var json = File.ReadAllText(_historyPath);
            return JsonSerializer.Deserialize<List<EventLogEntry>>(json, JsonOptions) ?? new List<EventLogEntry>();
        }
        catch
        {
            return Array.Empty<EventLogEntry>();
        }
    }

    public void SaveHistory(IEnumerable<EventLogEntry> entries, int maxEvents)
    {
        try
        {
            Directory.CreateDirectory(_appFolder);
            var items = entries.Take(Math.Max(1, maxEvents)).ToList();
            File.WriteAllText(_historyPath, JsonSerializer.Serialize(items, JsonOptions));
        }
        catch
        {
            // History is useful, but optional.
        }
    }
}
