using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ELKA.EventLens.App.Models;
using ELKA.EventLens.App.Services;
using ELKA.EventLens.App.ViewModels;

internal static class UpdateChecks
{
    internal static IAppUpdateService UiService() => new FakeUpdateService { CurrentVersion = "0.1.0-dev.3", Candidate = Candidate("0.1.0-dev.4", 54_000_000) };

    internal static async Task RunAsync(Action<bool, string> check, string output)
    {
        var package = Encoding.UTF8.GetBytes("Synthetic installer fixture - never executed");
        var sha = Convert.ToHexString(SHA256.HashData(package));
        var version = "0.1.0-dev.10";
        var release = Candidate(version, package.Length);
        var feed = JsonSerializer.Serialize(new[] { FeedItem("0.1.0-dev.3", package.Length), FeedItem(version, package.Length), FeedItem("0.1.0-dev.1", package.Length) });
        check(AppUpdateService.SelectRelease(feed, "0.1.0-dev.2", false)?.Version == version, "update version comparison handles dev.10 numerically");
        check(AppUpdateService.SelectRelease(feed, version, false) is null, "equal and older updates are ignored");
        check(AppUpdateService.SelectRelease(feed, "0.1.0", false) is null, "stable builds ignore development previews");
        check(AppUpdateService.SelectRelease(JsonSerializer.Serialize(new[] { FeedItem("0.1.0", package.Length) }), version, false)?.Version == "0.1.0", "stable release supersedes its development preview");
        check(AppUpdateService.SelectRelease(feed, "0.1.0-dev.2", true)?.FileName.EndsWith(".zip") == true, "portable updates select ZIP instead of installer");
        check(AppUpdateService.SelectRelease(JsonSerializer.Serialize(new[] { FeedItem(version, package.Length, draft: true) }), "0.1.0-dev.2", false) is null, "draft releases are ignored");
        check(AppUpdateService.SelectRelease(feed.Replace("https://github.com/torment78/ELKA.EventLens/releases/download/", "https://untrusted.example/"), "0.1.0-dev.2", false) is null, "untrusted asset URLs cannot become updates");
        check(AppUpdateService.SelectRelease(feed.Replace("SHA256SUMS-", "missing-checksum-"), "0.1.0-dev.2", false) is null, "updates require a checksum asset");
        check(!ReleaseVersion.TryParse("0.1.0-dev.3/evil.exe", out _) && !ReleaseVersion.TryParse("0.1.0-dev.99999999999", out _), "invalid version tags are rejected");
        check(AppUpdateService.ReadChecksum($"{sha.ToLowerInvariant()}  {release.FileName}\r\n", release.FileName) == sha.ToLowerInvariant(), "checksum selection supports release text format");
        await RejectAsync(() => Task.FromResult(AppUpdateService.ReadChecksum($"{sha}  {release.FileName}\n{sha}  {release.FileName}", release.FileName)), "ambiguous checksums are rejected", check);

        var root = Path.Combine(output, "updates-" + Guid.NewGuid().ToString("N"));
        var badChecksum = false;
        var failNetwork = false;
        var slow = false;
        using var client = new HttpClient(new TestHandler(async (request, token) =>
        {
            if (failNetwork) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request };
            if (slow) await Task.Delay(Timeout.Infinite, token);
            var uri = request.RequestUri!.AbsoluteUri;
            var bytes = uri.Contains("api.github.com") ? Encoding.UTF8.GetBytes(feed)
                : uri.EndsWith(".txt") ? Encoding.UTF8.GetBytes($"{(badChecksum ? new string('0', 64) : sha)}  {release.FileName}\n") : package;
            return new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(bytes) };
        }));
        var service = new AppUpdateService(client, "0.1.0-dev.2", false, root);
        check((await service.CheckAsync(CancellationToken.None))?.Version == version, "release feed parsed through HTTP service");
        var downloaded = await service.DownloadAsync(release, new Progress<double>(), CancellationToken.None);
        check(File.ReadAllBytes(downloaded.Path).SequenceEqual(package) && downloaded.Sha256 == sha, "downloaded bytes verified before ready state");
        check(!Directory.EnumerateFiles(root, "*.partial", SearchOption.AllDirectories).Any(), "successful download leaves no partial file");
        badChecksum = true;
        await RejectAsync(() => service.DownloadAsync(release, new Progress<double>(), CancellationToken.None), "checksum mismatch blocks update", check);
        check(Directory.EnumerateFiles(root, "*.exe", SearchOption.AllDirectories).Count() == 1 && !Directory.EnumerateFiles(root, "*.partial", SearchOption.AllDirectories).Any(), "failed checksum removes partial download and preserves verified copy");
        badChecksum = false;
        await RejectAsync(() => service.DownloadAsync(release with { FileName = "../evil.exe" }, new Progress<double>(), CancellationToken.None), "download rejects unsafe filenames", check);
        failNetwork = true;
        await RejectAsync(() => service.CheckAsync(CancellationToken.None), "HTTP errors are surfaced", check);
        failNetwork = false;
        slow = true;
        using (var cancellation = new CancellationTokenSource())
        {
            var pending = service.CheckAsync(cancellation.Token);
            cancellation.Cancel();
            try { await pending; check(false, "cancelled check must stop"); }
            catch (OperationCanceledException) { check(true, "network check is cancellable"); }
        }

        var launch = AppUpdateService.InstallerStartInfo("C:\\Update Cache\\setup.exe", "C:\\Program Files\\ElkaSoft\\ELKA EventLens", 1234);
        check(launch.UseShellExecute && launch.Arguments.Contains("/ELKAPID=1234") && launch.Arguments.Contains("/AUTOUPDATE=1"), "installer handoff includes process wait and restart flags");
        check(launch.Arguments.Contains("/DIR=\"C:\\Program Files\\ElkaSoft\\ELKA EventLens\"") && launch.Arguments.Contains("/NORESTART"), "installer preserves target path and cannot reboot Windows automatically");
        check(AppUpdateService.UpgradeInstallFolder(@"C:\Program Files\Elka Software\ELKA EventLens\", @"C:\Program Files") == @"C:\Program Files\ElkaSoft\ELKA EventLens", "updater remaps the old standard company folder");
        check(AppUpdateService.UpgradeInstallFolder(@"C:\PROGRAM FILES\ELKA SOFTWARE\ELKA EVENTLENS", @"C:\Program Files") == @"C:\Program Files\ElkaSoft\ELKA EventLens", "legacy target matching ignores case");
        check(AppUpdateService.UpgradeInstallFolder(@"D:\My Tools\EventLens", @"C:\Program Files") == @"D:\My Tools\EventLens", "updater preserves genuine custom install folders");
        var tampered = downloaded with { Sha256 = new string('0', 64) };
        await RejectAsync(() => AppUpdateService.StartInstallerAsync(tampered), "installer is reverified before execution", check);
        await RejectAsync(() => AppUpdateService.StartInstallerAsync(downloaded with { Release = release with { IsPortable = true } }), "portable download cannot launch installer", check);

        var fake = new FakeUpdateService { Candidate = release, Download = downloaded };
        var model = new UpdateViewModel(fake);
        await model.CheckAsync();
        check(model.HasUpdate && model.DownloadCommand.CanExecute(null), "update UI enables download after a newer release");
        var handoffs = 0;
        model.CompleteDownloadAsync = (_, _) => { handoffs++; return Task.CompletedTask; };
        await model.DownloadAsync();
        check(handoffs == 1 && model.Progress == 100 && !model.IsBusy, "verified download triggers a single UI handoff");
        fake.Error = new IOException("Offline fixture");
        await model.CheckAsync();
        check(model.Status.Contains("Offline fixture") && !model.IsBusy && !model.DownloadCommand.CanExecute(null), "failed update check leaves app open and commands recoverable");
        fake.Error = null;
        fake.Wait = true;
        var checking = model.CheckAsync();
        check(model.IsBusy && !model.CheckCommand.CanExecute(null) && !model.DownloadCommand.CanExecute(null), "busy updater prevents duplicate actions");
        model.Cancel();
        await checking;
        check(!model.IsBusy && !model.HasUpdate && model.Status.Contains("cancelled"), "cancelled update resets UI without a handoff");
    }

    private static UpdateRelease Candidate(string version, int size) => new(version,
        new Uri($"https://github.com/torment78/ELKA.EventLens/releases/download/v{version}/ELKA_EventLens_Setup_{version}.exe"),
        $"ELKA_EventLens_Setup_{version}.exe", size,
        new Uri($"https://github.com/torment78/ELKA.EventLens/releases/download/v{version}/SHA256SUMS-{version}.txt"), false);

    private static object FeedItem(string version, int size, bool draft = false) => new
    {
        tag_name = "v" + version, draft, prerelease = version.Contains("-dev."),
        assets = new[] { $"ELKA_EventLens_Setup_{version}.exe", $"ELKA_EventLens_Portable_{version}.zip", $"SHA256SUMS-{version}.txt" }
            .Select(name => new { name, state = "uploaded", size, browser_download_url = $"https://github.com/torment78/ELKA.EventLens/releases/download/v{version}/{name}" }).ToArray()
    };

    private static async Task RejectAsync(Func<Task> action, string label, Action<bool, string> check)
    {
        try { await action(); }
        catch (Exception error) when (error is IOException or InvalidDataException or HttpRequestException or InvalidOperationException) { check(true, label); return; }
        check(false, label);
    }

    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => response(request, cancellationToken);
    }

    private sealed class FakeUpdateService : IAppUpdateService
    {
        public string CurrentVersion { get; init; } = "0.1.0-dev.2";
        public UpdateRelease? Candidate { get; init; }
        public DownloadedUpdate Download { get; init; } = null!;
        public Exception? Error { get; set; }
        public bool Wait { get; set; }
        public async Task<UpdateRelease?> CheckAsync(CancellationToken cancellationToken)
        {
            if (Error is not null) throw Error;
            if (Wait) await Task.Delay(Timeout.Infinite, cancellationToken);
            return Candidate;
        }
        public Task<DownloadedUpdate> DownloadAsync(UpdateRelease release, IProgress<double> progress, CancellationToken cancellationToken) => Task.FromResult(Download);
    }
}
