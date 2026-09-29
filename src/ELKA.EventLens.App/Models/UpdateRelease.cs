using System.Globalization;
using System.Text.RegularExpressions;

namespace ELKA.EventLens.App.Models;

public sealed record UpdateRelease(string Version, Uri PackageUrl, string FileName, long Size, Uri ChecksumUrl, bool IsPortable);
public sealed record DownloadedUpdate(UpdateRelease Release, string Path, string Sha256);

internal readonly record struct ReleaseVersion(int Major, int Minor, int Patch, int? Development) : IComparable<ReleaseVersion>
{
    // The project's release channel is deliberately restricted to stable and -dev.N tags.
    public static bool TryParse(string text, out ReleaseVersion version)
    {
        version = default;
        var match = Regex.Match(text, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-dev\.(0|[1-9]\d*))?$", RegexOptions.CultureInvariant);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var major) ||
            !int.TryParse(match.Groups[2].Value, out var minor) || !int.TryParse(match.Groups[3].Value, out var patch)) return false;
        int? development = null;
        if (match.Groups[4].Success)
        {
            if (!int.TryParse(match.Groups[4].Value, out var number)) return false;
            development = number;
        }
        version = new(major, minor, patch, development);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result == 0) result = Minor.CompareTo(other.Minor);
        if (result == 0) result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;
        if (Development is null) return other.Development is null ? 0 : 1;
        return other.Development is null ? -1 : Development.Value.CompareTo(other.Development.Value);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{Major}.{Minor}.{Patch}") + (Development is { } number ? "-dev." + number.ToString(CultureInfo.InvariantCulture) : "");
}
