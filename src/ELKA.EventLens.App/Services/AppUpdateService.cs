using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ELKA.EventLens.App.Models;

namespace ELKA.EventLens.App.Services;

public interface IAppUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateRelease?> CheckAsync(CancellationToken cancellationToken);
    Task<DownloadedUpdate> DownloadAsync(UpdateRelease release, IProgress<double> progress, CancellationToken cancellationToken);
}

public sealed class AppUpdateService : IAppUpdateService
{
    private const string Repository = "https://github.com/torment78/ELKA.EventLens";
    private const long MaxPackageBytes = 250_000_000;
    private static readonly HttpClient SharedClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly HttpClient _client;
    private readonly bool _portable;
    private readonly string _downloadFolder;

    public AppUpdateService() : this(SharedClient, GetCurrentVersion(), AppStorageService.IsPortable, AppStorageService.UpdateFolder) { }

    internal AppUpdateService(HttpClient client, string currentVersion, bool portable, string downloadFolder)
    {
        _client = client;
        CurrentVersion = currentVersion;
        _portable = portable;
        _downloadFolder = downloadFolder;
    }

    public string CurrentVersion { get; }

    private static string GetCurrentVersion() => typeof(AppUpdateService).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    public async Task<UpdateRelease?> CheckAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        // /latest excludes prereleases; this app must also see the development channel.
        var json = await ReadTextAsync(new Uri("https://api.github.com/repos/torment78/ELKA.EventLens/releases?per_page=100"), 2_000_000, timeout.Token);
        return SelectRelease(json, CurrentVersion, _portable);
    }

    internal static UpdateRelease? SelectRelease(string json, string currentVersion, bool portable)
    {
        if (!ReleaseVersion.TryParse(currentVersion, out var current)) throw new InvalidDataException("Current app version is not recognized.");
        using var document = JsonDocument.Parse(json);
        var newest = current;
        UpdateRelease? selected = null;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("draft", out var draft) || draft.GetBoolean() ||
                !item.TryGetProperty("tag_name", out var tagProperty) ||
                !ReleaseVersion.TryParse(tagProperty.GetString() ?? "", out var version) || version.CompareTo(newest) <= 0) continue;
            if (!item.TryGetProperty("prerelease", out var preview)) continue;
            if (current.Development is null && (preview.GetBoolean() || version.Development is not null)) continue;
            var tag = tagProperty.GetString()!;
            if (tag != "v" + version) continue;
            var packageName = $"ELKA_EventLens_{(portable ? "Portable" : "Setup")}_{version}.{(portable ? "zip" : "exe")}";
            var checksumName = $"SHA256SUMS-{version}.txt";
            Uri? packageUrl = null;
            Uri? checksumUrl = null;
            long size = 0;
            if (!item.TryGetProperty("assets", out var assets)) continue;
            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameProperty) || !asset.TryGetProperty("state", out var state) || state.GetString() != "uploaded") continue;
                var name = nameProperty.GetString();
                if (name != packageName && name != checksumName) continue;
                if (!asset.TryGetProperty("browser_download_url", out var urlProperty) ||
                    !Uri.TryCreate(urlProperty.GetString(), UriKind.Absolute, out var url) ||
                    url.AbsoluteUri != $"{Repository}/releases/download/{tag}/{name}") continue;
                if (name == checksumName) checksumUrl = url;
                else if (asset.TryGetProperty("size", out var sizeProperty) && sizeProperty.TryGetInt64(out size)) packageUrl = url;
            }
            if (packageUrl is null || checksumUrl is null || size <= 0 || size > MaxPackageBytes) continue;
            newest = version;
            selected = new(version.ToString(), packageUrl, packageName, size, checksumUrl, portable);
        }
        return selected;
    }

    public async Task<DownloadedUpdate> DownloadAsync(UpdateRelease release, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ValidateRelease(release);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var token = timeout.Token;
        var expected = ReadChecksum(await ReadTextAsync(release.ChecksumUrl, 32768, token), release.FileName);
        var folder = Path.Combine(_downloadFolder, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, release.FileName);
        var partial = file + ".partial";
        try
        {
            using var request = NewRequest(release.PackageUrl);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            ValidateResponseLocation(response);
            if (response.Content.Headers.ContentLength is { } length && length != release.Size) throw new InvalidDataException("Update download size does not match the release.");
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, token)) > 0)
                {
                    total += count;
                    if (total > release.Size || total > MaxPackageBytes) throw new InvalidDataException("Update download exceeded the expected size.");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                    progress.Report(100d * total / release.Size);
                }
                if (total != release.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Update verification failed. Nothing was installed; please retry.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(partial, file);
            return new(release, file, expected);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    private static void ValidateRelease(UpdateRelease release)
    {
        if (!ReleaseVersion.TryParse(release.Version, out var version) || version.ToString() != release.Version ||
            release.Size <= 0 || release.Size > MaxPackageBytes) throw new InvalidDataException("Invalid update metadata.");
        var name = $"ELKA_EventLens_{(release.IsPortable ? "Portable" : "Setup")}_{version}.{(release.IsPortable ? "zip" : "exe")}";
        if (release.FileName != name || release.PackageUrl.AbsoluteUri != $"{Repository}/releases/download/v{version}/{name}" ||
            release.ChecksumUrl.AbsoluteUri != $"{Repository}/releases/download/v{version}/SHA256SUMS-{version}.txt")
            throw new InvalidDataException("The update is not an official EventLens release asset.");
    }

    internal static string ReadChecksum(string text, string fileName)
    {
        var hashes = text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 66 && line[64..66] == "  " && line[66..] == fileName).Select(line => line[..64]).ToArray();
        if (hashes.Length != 1 || hashes[0].Any(character => !Uri.IsHexDigit(character))) throw new InvalidDataException("A valid SHA-256 checksum is missing for this update.");
        return hashes[0];
    }

    private static HttpRequestMessage NewRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("ELKA-EventLens-Updater/1.0");
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static void ValidateResponseLocation(HttpResponseMessage response)
    {
        var uri = response.RequestMessage?.RequestUri;
        if (uri is null || uri.Scheme != "https" ||
            !(uri.Host == "api.github.com" || uri.Host == "github.com" || uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Unexpected update download location.");
    }

    private async Task<string> ReadTextAsync(Uri uri, int limit, CancellationToken token)
    {
        using var request = NewRequest(uri);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        ValidateResponseLocation(response);
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Update metadata is too large.");
            output.Write(buffer, 0, count);
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }

    internal static ProcessStartInfo InstallerStartInfo(string file, string appFolder, int processId) => new(file)
    {
        UseShellExecute = true,
        Arguments = $"/SP- /SILENT /NORESTART /CLOSEAPPLICATIONS /NOFORCECLOSEAPPLICATIONS /AUTOUPDATE=1 /ELKAPID={processId} /DIR=\"{UpgradeInstallFolder(appFolder, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))}\""
    };

    internal static string UpgradeInstallFolder(string appFolder, string programFiles)
    {
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appFolder));
        var legacy = Path.Combine(programFiles, "Elka Software", AppStorageService.AppFolderName);
        return current.Equals(legacy, StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(programFiles, AppStorageService.CompanyFolderName, AppStorageService.AppFolderName)
            : current;
    }

    public static async Task StartInstallerAsync(DownloadedUpdate download, CancellationToken cancellationToken = default)
    {
        ValidateRelease(download.Release);
        if (download.Release.IsPortable) throw new InvalidOperationException("Portable updates must not run the installer.");
        // Keep the verified file read-locked until ShellExecute has accepted the launch.
        await using var file = new FileStream(download.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
        if (file.Length != download.Release.Size || !hash.Equals(download.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The downloaded installer changed. Check for updates and download it again.");
        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(InstallerStartInfo(download.Path, AppStorageService.AppBaseDirectory, Environment.ProcessId))
            ?? throw new InvalidOperationException("Windows did not start the installer. EventLens will remain open.");
    }
}
