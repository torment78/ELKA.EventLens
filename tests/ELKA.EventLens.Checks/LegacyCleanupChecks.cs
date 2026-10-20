using System.IO;
using ELKA.EventLens.App.Services;

internal static class LegacyCleanupChecks
{
    internal static void Run(Action<bool, string> check, string output)
    {
        var root = Path.Combine(output, "cleanup-" + Guid.NewGuid().ToString("N"));
        var roaming = Path.Combine(root, "Roaming");
        var local = Path.Combine(root, "Local");
        var oldSaves = Path.Combine(roaming, "ELKA.EventLens");
        var oldLocal = Path.Combine(local, "ELKA", "EventLens");
        var folders = AppStorageService.ResolveFolders(Path.Combine(root, "App"), false, roaming, local);
        Directory.CreateDirectory(folders.Data);
        Directory.CreateDirectory(folders.Browser);
        Directory.CreateDirectory(folders.Logs);
        AppStorageService.MarkMigrationComplete(folders);
        var currentSettings = Path.Combine(folders.Data, "settings.json");
        Seed(currentSettings, "Current settings");
        Seed(Path.Combine(folders.Browser, "Default", "Cookies"), "Current login");
        Seed(Path.Combine(oldSaves, "settings.json"), "Conflicting settings");
        Seed(Path.Combine(oldSaves, "nested", "extra.dat"), "Other saved data");
        Seed(Path.Combine(oldLocal, "WebView2Profile", "Default", "Cookies"), "Old login");
        Seed(Path.Combine(oldLocal, "Logs", "activity.log"), "Old activity");
        var sibling = Path.Combine(local, "ELKA", "OtherApp", "settings.json");
        Seed(sibling, "Unrelated app");
        var stamp = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(oldSaves, "settings.json"), stamp);

        LegacyStorageCleanup.Run(folders, true, roaming, local, true);
        check(Directory.Exists(oldSaves) && Directory.Exists(oldLocal), "portable cleanup leaves installed legacy storage untouched");
        LegacyStorageCleanup.Run(folders, false, roaming, local, false);
        check(Directory.Exists(oldSaves), "another running copy defers legacy cleanup");
        LegacyStorageCleanup.Run(folders with { CompletionMarker = null }, false, roaming, local, true);
        check(Directory.Exists(oldSaves), "incomplete primary migration cannot clean up its active legacy data");
        LegacyStorageCleanup.Run(folders with { Data = oldSaves }, false, roaming, local, true);
        check(Directory.Exists(oldSaves), "fallback storage cannot be retired");

        var notes = LegacyStorageCleanup.Run(folders, false, roaming, local, true);
        check(!Directory.Exists(oldSaves) && !Directory.Exists(oldLocal), "successful launch retires both old storage roots");
        check(File.ReadAllText(currentSettings) == "Current settings" && File.ReadAllText(Path.Combine(folders.Browser, "Default", "Cookies")) == "Current login", "cleanup never overwrites active settings or merges browser profiles");
        var recoveredSettings = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(folders.Data)!, "LegacyRecovery"), "settings.json", SearchOption.AllDirectories).Single();
        check(File.ReadAllText(recoveredSettings) == "Conflicting settings" && File.GetLastWriteTimeUtc(recoveredSettings) == stamp, "conflicting saves move intact with timestamps into recovery");
        check(File.ReadAllText(Path.Combine(Path.GetDirectoryName(recoveredSettings)!, "nested", "extra.dat")) == "Other saved data", "unrecognized nested saved files are preserved");
        var localRecovery = Path.Combine(Path.GetDirectoryName(folders.Logs)!, "LegacyRecovery");
        check(File.ReadAllText(Directory.GetFiles(localRecovery, "Cookies", SearchOption.AllDirectories).Single()) == "Old login" && File.ReadAllText(Directory.GetFiles(localRecovery, "activity.log", SearchOption.AllDirectories).Single()) == "Old activity", "old browser profile and logs are preserved together");
        check(File.ReadAllText(sibling) == "Unrelated app", "shared legacy company folder retains sibling apps");
        check(notes.Count == 3 && File.Exists(Path.Combine(Path.GetDirectoryName(folders.Logs)!, "legacy-cleanup-v2.complete")), "successful cleanup records a completion marker and recovery locations");
        check(LegacyStorageCleanup.Run(folders, false, roaming, local, true).Count == 0, "cleanup is one-time and idempotent");

        var blockedRoaming = Path.Combine(root, "BlockedRoaming");
        var blockedLocal = Path.Combine(root, "BlockedLocal");
        var blocked = AppStorageService.ResolveFolders(root, false, blockedRoaming, blockedLocal);
        Directory.CreateDirectory(blocked.Data);
        Directory.CreateDirectory(blocked.Browser);
        Directory.CreateDirectory(blocked.Logs);
        AppStorageService.MarkMigrationComplete(blocked);
        var blockedOld = Path.Combine(blockedRoaming, "ELKA.EventLens");
        Seed(Path.Combine(blockedOld, "settings.json"), "Keep me");
        var obstruction = Path.Combine(Path.GetDirectoryName(blocked.Data)!, "LegacyRecovery");
        Seed(obstruction, "Blocked destination");
        var failed = LegacyStorageCleanup.Run(blocked, false, blockedRoaming, blockedLocal, true);
        check(failed.Any(note => note.Contains("deferred")) && File.ReadAllText(Path.Combine(blockedOld, "settings.json")) == "Keep me", "failed recovery transfer keeps source intact for retry");
        check(!File.Exists(Path.Combine(Path.GetDirectoryName(blocked.Logs)!, "legacy-cleanup-v2.complete")), "failed cleanup never marks itself complete");
        File.Delete(obstruction);
        Directory.CreateDirectory(Path.Combine(blockedLocal, "ELKA", "EventLens"));
        LegacyStorageCleanup.Run(blocked, false, blockedRoaming, blockedLocal, true);
        check(!Directory.Exists(blockedOld) && !Directory.Exists(Path.Combine(blockedLocal, "ELKA")), "cleanup retries and removes empty old company directories");

        var linkedRoaming = Path.Combine(root, "LinkedRoaming");
        var linkedLocal = Path.Combine(root, "LinkedLocal");
        var linked = AppStorageService.ResolveFolders(root, false, linkedRoaming, linkedLocal);
        Directory.CreateDirectory(linked.Data);
        Directory.CreateDirectory(linked.Browser);
        Directory.CreateDirectory(linked.Logs);
        AppStorageService.MarkMigrationComplete(linked);
        var linkedOld = Path.Combine(linkedRoaming, "ELKA.EventLens");
        Seed(Path.Combine(linkedOld, "settings.json"), "Preserve linked source");
        var outside = Path.Combine(root, "UnrelatedData");
        Seed(Path.Combine(outside, "sentinel.txt"), "Do not touch");
        var junction = Path.Combine(linkedOld, "LinkedFolder");
        CreateJunction(junction, outside);
        var linkedNotes = LegacyStorageCleanup.Run(linked, false, linkedRoaming, linkedLocal, true);
        check(linkedNotes.Any(note => note.Contains("deferred")) && Directory.Exists(linkedOld), "cleanup refuses a junction inside legacy data");
        Directory.Delete(junction); // Remove only the fixture link, never its target.
        var recoveryLink = Path.Combine(Path.GetDirectoryName(linked.Data)!, "LegacyRecovery");
        CreateJunction(recoveryLink, outside);
        linkedNotes = LegacyStorageCleanup.Run(linked, false, linkedRoaming, linkedLocal, true);
        check(linkedNotes.Any(note => note.Contains("deferred")) && Directory.Exists(linkedOld), "cleanup refuses a linked recovery destination");
        Directory.Delete(recoveryLink);
        check(File.ReadAllText(Path.Combine(outside, "sentinel.txt")) == "Do not touch" && Directory.GetFileSystemEntries(outside).Length == 1, "junction targets remain completely unchanged");
        LegacyStorageCleanup.Run(linked, false, linkedRoaming, linkedLocal, true);
        check(!Directory.Exists(linkedOld), "cleanup succeeds after unsafe links are removed");
    }

    private static void CreateJunction(string path, string target)
    {
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        foreach (var argument in new[] { "/d", "/c", "mklink", "/J", path, target }) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new IOException("Could not create isolated junction test fixture.");
    }

    private static void Seed(string file, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }
}
