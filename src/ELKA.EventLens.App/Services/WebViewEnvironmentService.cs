using System.IO;
using Microsoft.Web.WebView2.Core;

namespace ELKA.EventLens.App.Services;

public static class WebViewEnvironmentService
{
    private static readonly Lazy<Task<CoreWebView2Environment>> SharedEnvironment = new(CreateEnvironmentAsync);

    public static Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
    {
        return SharedEnvironment.Value;
    }

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var userDataFolder = AppStorageService.WebViewProfileFolder;
        Directory.CreateDirectory(userDataFolder);
        return await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
    }
}
