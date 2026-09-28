using System.IO;

namespace ELKA.EventLens.App.Services;

public sealed class ActivityLogService
{
    private readonly string _logPath;

    public ActivityLogService()
    {
        Directory.CreateDirectory(AppStorageService.LogFolder);
        _logPath = ResolveLogPath();
    }

    public void Write(string message)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}{Environment.NewLine}";
            File.AppendAllText(_logPath, line);
        }
        catch
        {
            // Activity logs are useful, but must never block the monitor.
        }
    }

    private static string ResolveLogPath()
    {
        var now = DateTimeOffset.Now;
        var latestLog = Directory
            .EnumerateFiles(AppStorageService.LogFolder, "ELKA.EventLens-*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.CreationTimeUtc)
            .FirstOrDefault();

        if (latestLog is not null && now - latestLog.CreationTimeUtc < TimeSpan.FromHours(24))
        {
            return latestLog.FullName;
        }

        return Path.Combine(AppStorageService.LogFolder, $"ELKA.EventLens-{now:yyyyMMdd-HHmmss}.log");
    }
}
