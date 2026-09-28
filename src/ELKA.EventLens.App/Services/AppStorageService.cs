using System.IO;

namespace ELKA.EventLens.App.Services;

public static class AppStorageService
{
    public const string PortableFolderName = "ElkaEventLensPort";
    public const string PortableMarkerFileName = "ELKA.EventLens.portable";

    public static string AppBaseDirectory { get; } = AppContext.BaseDirectory.TrimEnd(
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar);

    public static bool IsPortable { get; } = DetectPortableMode();

    public static string DataFolder => IsPortable
        ? Path.Combine(AppBaseDirectory, "Saves")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ELKA.EventLens");

    public static string WebViewProfileFolder => IsPortable
        ? Path.Combine(AppBaseDirectory, "WebView2Profile")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELKA", "EventLens", "WebView2Profile");

    public static string LogFolder => IsPortable
        ? Path.Combine(AppBaseDirectory, "Logs")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELKA", "EventLens", "Logs");

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
