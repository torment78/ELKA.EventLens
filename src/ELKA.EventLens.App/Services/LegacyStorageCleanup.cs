using System.IO;
using System.Security;

namespace ELKA.EventLens.App.Services;

internal static class LegacyStorageCleanup
{
    internal static IReadOnlyList<string> Run(StorageFolders folders, bool isPortable,
        string roaming, string local, bool canMigrate)
    {
        var notes = new List<string>();
        if (isPortable || !canMigrate || folders.CompletionMarker is null || !File.Exists(folders.CompletionMarker)) return notes;
        var roamingRoot = Path.Combine(roaming, AppStorageService.CompanyFolderName, AppStorageService.AppFolderName);
        var localRoot = Path.Combine(local, AppStorageService.CompanyFolderName, AppStorageService.AppFolderName);
        if (!SamePath(folders.Data, Path.Combine(roamingRoot, "Saves")) ||
            !SamePath(folders.Browser, Path.Combine(localRoot, "WebView2Profile")) ||
            !SamePath(folders.Logs, Path.Combine(localRoot, "Logs"))) return notes;

        var marker = Path.Combine(localRoot, "legacy-cleanup-v2.complete");
        if (File.Exists(marker)) return notes;
        try
        {
            Retire(Path.Combine(roaming, "ELKA.EventLens"), roamingRoot, notes);
            Retire(Path.Combine(local, "ELKA", "EventLens"), localRoot, notes);
            var oldCompany = Path.Combine(local, "ELKA");
            RequirePlainPath(oldCompany);
            if (Directory.Exists(oldCompany) && !Directory.EnumerateFileSystemEntries(oldCompany).Any())
                Directory.Delete(oldCompany); // Empty only; sibling applications belong to their owners.
            RequirePlainPath(marker);
            using var stream = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            notes.Add("Legacy storage cleanup complete. Only the ElkaSoft layout is active.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            notes.Add($"Legacy cleanup deferred; remaining old data was preserved. Retry next launch. {error.Message}");
        }
        return notes;
    }

    private static void Retire(string source, string currentRoot, List<string> notes)
    {
        RequirePlainPath(source);
        if (File.Exists(source)) throw new IOException($"Expected a directory at {source}.");
        if (!Directory.Exists(source)) return;
        RequirePlainTree(source);
        if (!Directory.EnumerateFileSystemEntries(source).Any())
        {
            Directory.Delete(source);
            return;
        }

        var recovery = Path.Combine(currentRoot, "LegacyRecovery", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        RequirePlainPath(recovery);
        Directory.CreateDirectory(Path.GetDirectoryName(recovery)!);
        // A same-volume directory rename preserves every byte and timestamp atomically.
        // Never copy/delete piecemeal or merge an obsolete profile into the active profile.
        Directory.Move(source, recovery);
        notes.Add($"Remaining legacy data moved intact from {source} to {recovery}. Active saves were not overwritten.");
    }

    internal static void RequirePlainTree(string root)
    {
        RequirePlainPath(root);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException($"Linked path left untouched: {entry}");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
            }
        }
    }

    internal static void RequirePlainPath(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"Linked path left untouched: {current}");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static bool SamePath(string left, string right) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)).Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
}
