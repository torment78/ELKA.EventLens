using System.IO;
using System.Diagnostics;
using System.Security;

namespace ELKA.EventLens.App.Services;

public static class AppStorageService
{
    public const string PortableFolderName = "ElkaEventLensPort";
    public const string PortableMarkerFileName = "ELKA.EventLens.portable";
    public const string CompanyFolderName = "ElkaSoft";
    public const string AppFolderName = "ELKA EventLens";

    public static string AppBaseDirectory { get; } = AppContext.BaseDirectory.TrimEnd(
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar);

    public static bool IsPortable { get; } = DetectPortableMode();

    // Previous installed layout, retained for reference and migration:
    // DataFolder: %AppData%\ELKA.EventLens
    // WebViewProfileFolder / LogFolder: %LocalAppData%\ELKA\EventLens\...
    private static readonly Lazy<StorageFolders> Folders = new(() => ResolveFolders(
        AppBaseDirectory,
        IsPortable,
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        CanMigrateLegacyFolders()));

    public static string DataFolder => Folders.Value.Data;
    public static string WebViewProfileFolder => Folders.Value.Browser;
    public static string LogFolder => Folders.Value.Logs;
    public static IReadOnlyList<string> MigrationNotes => Folders.Value.Notes;

    internal static StorageFolders ResolveFolders(string appBaseDirectory, bool isPortable,
        string roamingAppData, string localAppData, bool canMigrate = true)
    {
        var notes = new List<string>();
        if (isPortable)
        {
            return new(Path.Combine(appBaseDirectory, "Saves"),
                Path.Combine(appBaseDirectory, "WebView2Profile"),
                Path.Combine(appBaseDirectory, "Logs"), notes);
        }

        var roamingRoot = Path.Combine(roamingAppData, CompanyFolderName, AppFolderName);
        var localRoot = Path.Combine(localAppData, CompanyFolderName, AppFolderName);
        var legacyLocalRoot = Path.Combine(localAppData, "ELKA", "EventLens");
        return new(
            MigrateFolder(Path.Combine(roamingAppData, "ELKA.EventLens"), Path.Combine(roamingRoot, "Saves"), canMigrate, notes),
            MigrateFolder(Path.Combine(legacyLocalRoot, "WebView2Profile"), Path.Combine(localRoot, "WebView2Profile"), canMigrate, notes),
            MigrateFolder(Path.Combine(legacyLocalRoot, "Logs"), Path.Combine(localRoot, "Logs"), canMigrate, notes),
            notes);
    }

    private static string MigrateFolder(string previous, string destination, bool canMigrate, List<string> notes)
    {
        // Never merge over an existing destination or partially copy a browser profile.
        if (Directory.Exists(destination) || !Directory.Exists(previous))
        {
            return destination;
        }

        if (!canMigrate)
        {
            notes.Add($"Storage migration deferred: another EventLens copy may be running. Still using {previous}. Close other copies and restart to move to {destination}.");
            return previous;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(previous, destination);
            notes.Add($"Storage moved from {previous} to {destination}.");
            return destination;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            if (!Directory.Exists(previous) && Directory.Exists(destination))
            {
                return destination;
            }

            notes.Add($"Storage migration deferred: {error.Message} Still using {previous}. Close older copies and retry next launch. Target: {destination}.");
            return previous;
        }
    }

    private static bool CanMigrateLegacyFolders()
    {
        if (IsPortable) return false;
        try
        {
            var processes = Process.GetProcessesByName("ELKA.EventLens.App");
            try { return processes.All(process => process.Id == Environment.ProcessId); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    /* Previous path properties:
    public static string DataFolder => IsPortable
        ? Path.Combine(AppBaseDirectory, "Saves")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ELKA.EventLens");

    public static string WebViewProfileFolder => IsPortable
        ? Path.Combine(AppBaseDirectory, "WebView2Profile")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELKA", "EventLens", "WebView2Profile");

    public static string LogFolder => IsPortable
        ? Path.Combine(AppBaseDirectory, "Logs")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELKA", "EventLens", "Logs");
    */

    public static void EnsureStorageFolders()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(WebViewProfileFolder);
        Directory.CreateDirectory(LogFolder);
    }

    private static bool DetectPortableMode()
    {
        try
        {
            var directory = new DirectoryInfo(AppBaseDirectory);
            return directory.Name.Equals(PortableFolderName, StringComparison.OrdinalIgnoreCase)
                || File.Exists(Path.Combine(AppBaseDirectory, PortableMarkerFileName));
        }
        catch
        {
            return false;
        }
    }
}

internal sealed record StorageFolders(string Data, string Browser, string Logs, IReadOnlyList<string> Notes);
