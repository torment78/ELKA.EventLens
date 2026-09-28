using ELKA.EventLens.App.Models;
using System.Diagnostics.Eventing.Reader;
using System.Security;

namespace ELKA.EventLens.App.Services;

public sealed class WindowsEventLogService : IDisposable
{
    private readonly List<EventLogWatcher> _watchers = [];
    private bool _isPaused;

    public event EventHandler<EventLogEntry>? EventReceived;

    public event EventHandler<string>? StatusChanged;

    public bool IsPaused
    {
        get => _isPaused;
        set => _isPaused = value;
    }

    public Task<IReadOnlyList<EventLogEntry>> LoadRecentEventsAsync(
        IEnumerable<string> logNames,
        IEnumerable<EventSeverity> severities,
        int maxCount,
        CancellationToken cancellationToken)
    {
        var logs = logNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var queryText = BuildLevelQuery(severities);

        if (logs.Count == 0 || string.IsNullOrWhiteSpace(queryText))
        {
            return Task.FromResult<IReadOnlyList<EventLogEntry>>(Array.Empty<EventLogEntry>());
        }

        return Task.Run<IReadOnlyList<EventLogEntry>>(() =>
        {
            var results = new List<EventLogEntry>();
            var perLogLimit = Math.Max(25, maxCount);

            foreach (var logName in logs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var query = new EventLogQuery(logName, PathType.LogName, queryText)
                    {
                        ReverseDirection = true,
                        TolerateQueryErrors = true
                    };

                    using var reader = new EventLogReader(query);
                    var read = 0;

                    while (read < perLogLimit)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        EventRecord? record = null;

                        try
                        {
                            record = reader.ReadEvent();
                            if (record is null)
                            {
                                break;
                            }

                            var entry = CreateEntry(record, logName);
                            if (entry is not null)
                            {
                                results.Add(entry);
                            }
                        }
                        finally
                        {
                            record?.Dispose();
                        }

                        read++;
                    }
                }
                catch (Exception ex) when (IsRecoverableEventLogException(ex))
                {
                    RaiseStatus($"{logName} log could not be read: {ex.Message}");
                }
            }

            return results
                .OrderByDescending(entry => entry.TimeCreated ?? DateTime.MinValue)
                .Take(Math.Max(1, maxCount))
                .ToList();
        }, cancellationToken);
    }

    public void StartWatching(IEnumerable<string> logNames, IEnumerable<EventSeverity> severities)
    {
        StopWatching();

        var queryText = BuildLevelQuery(severities);
        if (string.IsNullOrWhiteSpace(queryText))
        {
            RaiseStatus("Live monitoring is idle because no severities are selected.");
            return;
        }

        foreach (var logName in logNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var query = new EventLogQuery(logName, PathType.LogName, queryText)
                {
                    TolerateQueryErrors = true
                };

                var watcher = new EventLogWatcher(query);
                watcher.EventRecordWritten += OnEventRecordWritten;
                watcher.Enabled = true;
                _watchers.Add(watcher);
                RaiseStatus($"Monitoring {logName} log.");
            }
            catch (Exception ex) when (IsRecoverableEventLogException(ex))
            {
                RaiseStatus($"{logName} log could not be monitored: {ex.Message}");
            }
        }
    }

    public void StopWatching()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Enabled = false;
            watcher.EventRecordWritten -= OnEventRecordWritten;
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    public void Dispose()
    {
        StopWatching();
    }

    private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (_isPaused)
        {
            return;
        }

        if (e.EventException is not null)
        {
            RaiseStatus($"Live event read failed: {e.EventException.Message}");
            return;
        }

        EventRecord? record = e.EventRecord;
        if (record is null)
        {
            return;
        }

        try
        {
            var entry = CreateEntry(record, record.LogName ?? string.Empty);
            if (entry is not null)
            {
                EventReceived?.Invoke(this, entry);
            }
        }
        catch (Exception ex) when (IsRecoverableEventLogException(ex))
        {
            RaiseStatus($"Live event could not be read: {ex.Message}");
        }
        finally
        {
            record.Dispose();
        }
    }

    private static EventLogEntry? CreateEntry(EventRecord record, string fallbackLogName)
    {
        var severity = MapSeverity(record.Level);

        return new EventLogEntry
        {
            TimeCreated = record.TimeCreated,
            LogName = SafeGet(() => record.LogName) ?? fallbackLogName,
            ProviderName = SafeGet(() => record.ProviderName) ?? "Unknown source",
            EventId = record.Id,
            RecordId = record.RecordId,
            Severity = severity,
            LevelDisplayName = SafeGet(() => record.LevelDisplayName) ?? severity.ToString(),
            Message = SafeGet(record.FormatDescription) ?? "No formatted event message was available on this machine.",
            MachineName = SafeGet(() => record.MachineName) ?? Environment.MachineName,
            RawXml = SafeGet(record.ToXml) ?? string.Empty,
            TaskDisplayName = SafeGet(() => record.TaskDisplayName) ?? string.Empty,
            OpcodeDisplayName = SafeGet(() => record.OpcodeDisplayName) ?? string.Empty,
            KeywordsDisplayNames = string.Join(", ", SafeGet(() => record.KeywordsDisplayNames)?.ToArray() ?? [])
        };
    }

    private static EventSeverity MapSeverity(byte? level)
    {
        return level switch
        {
            1 => EventSeverity.Critical,
            2 => EventSeverity.Error,
            3 => EventSeverity.Warning,
            4 => EventSeverity.Information,
            5 => EventSeverity.Verbose,
            _ => EventSeverity.Unknown
        };
    }

    private static string? BuildLevelQuery(IEnumerable<EventSeverity> severities)
    {
        var levels = severities
            .Select(ToEventLevel)
            .Where(level => level > 0)
            .Distinct()
            .Order()
            .ToList();

        if (levels.Count == 0)
        {
            return null;
        }

        var filter = string.Join(" or ", levels.Select(level => $"Level={level}"));
        return $"*[System[({filter})]]";
    }

    private static int ToEventLevel(EventSeverity severity)
    {
        return severity switch
        {
            EventSeverity.Critical => 1,
            EventSeverity.Error => 2,
            EventSeverity.Warning => 3,
            EventSeverity.Information => 4,
            EventSeverity.Verbose => 5,
            _ => 0
        };
    }

    private static T? SafeGet<T>(Func<T> factory)
    {
        try
        {
            return factory();
        }
        catch
        {
            return default;
        }
    }

    private static bool IsRecoverableEventLogException(Exception ex)
    {
        return ex is EventLogException
            or UnauthorizedAccessException
            or SecurityException
            or InvalidOperationException;
    }

    private void RaiseStatus(string message)
    {
        StatusChanged?.Invoke(this, message);
    }
}
