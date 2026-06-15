using System.Net.Http.Headers;

namespace PostImageUploader.Tests.Integration;

/// <summary>
/// Downloads test data files from known URLs if they don't already exist.
/// Used by integration tests to ensure required images are available.
/// </summary>
public static class TestDataDownloader
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static TestDataDownloader()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("PostImageUploader.Tests", "1.0"));
    }

    /// <summary>
    /// Downloads a file from the specified URL to the target path.
    /// Skips download if the file already exists.
    /// </summary>
    public static async Task EnsureFileAsync(string url, string targetPath)
    {
        if (File.Exists(targetPath))
            return;

        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var bytes = await HttpClient.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(targetPath, bytes);
    }
}
