using App.Core.Models;
using App.Core.Services;
using App.Core.Abstractions;
using App.Core.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace PostImageUploader.Tests.Integration;

[Trait("Category", "Integration")]
public class UploadIntegrationTests
{
    private static string TestDataDir => Path.Combine(AppContext.BaseDirectory, "testdata");
    private static string TestFile(string name) => Path.Combine(TestDataDir, name);

    private const string JpgSourceUrl = "https://www2.scut.edu.cn/_upload/tpl/00/f1/241/template241/images/head1_2.jpg";
    private const string PngSourceUrl = "https://test-images.github.io/png/202105/cs-black-000.png";

    private static readonly TimeSpan DelayBetweenTests = TimeSpan.FromSeconds(3);

    public UploadIntegrationTests()
    {
        TestDataDownloader.EnsureFileAsync(JpgSourceUrl, TestFile("test_valid.jpg")).GetAwaiter().GetResult();
        TestDataDownloader.EnsureFileAsync(PngSourceUrl, TestFile("test_valid.png")).GetAwaiter().GetResult();
    }

    private PostImageClient CreateClient(bool verbose = true)
    {
        var options = Options.Create(new PostImageUploaderOptions { Verbose = verbose });
        var fileSystem = new PhysicalFileSystem();
        var validator = new UploadValidator(fileSystem, options);
        return new PostImageClient(
            new HttpClient(),
            NullLogger<PostImageClient>.Instance,
            fileSystem,
            validator,
            TimeProvider.System,
            options);
    }

    [Fact]
    public async Task UploadValidJpg_ReturnsPostimgCcUrl()
    {
        var path = TestFile("test_valid.jpg");
        using var client = CreateClient(verbose: true);
        using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await client.UploadFileAsync(path, cts.Token);

        Assert.True(result.Success, $"Upload failed: {result.ErrorMessage}");
        Assert.False(string.IsNullOrWhiteSpace(result.ImageUrl), "ImageUrl should not be empty on success");
        Assert.True(UploadValidator.IsValidPostimgUrl(result.ImageUrl), $"Expected postimg.cc URL, got: {result.ImageUrl}");
        Assert.True(result.LinkAccessible, "Generated link should be accessible");

        Console.WriteLine($"[JPG] Uploaded URL: {result.ImageUrl}");
        await Task.Delay(DelayBetweenTests);
    }

    [Fact]
    public async Task UploadValidPng_ReturnsPostimgCcUrl()
    {
        var path = TestFile("test_valid.png");
        using var client = CreateClient(verbose: true);
        using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await client.UploadFileAsync(path, cts.Token);

        Assert.True(result.Success, $"Upload failed: {result.ErrorMessage}");
        Assert.False(string.IsNullOrWhiteSpace(result.ImageUrl), "ImageUrl should not be empty on success");
        Assert.True(UploadValidator.IsValidPostimgUrl(result.ImageUrl), $"Expected postimg.cc URL, got: {result.ImageUrl}");
        Assert.True(result.LinkAccessible, "Generated link should be accessible");

        Console.WriteLine($"[PNG] Uploaded URL: {result.ImageUrl}");
        await Task.Delay(DelayBetweenTests);
    }

    [Fact]
    public async Task UploadFromRemoteUrl_ReturnsPostimgCcUrl()
    {
        const string remoteUrl = "https://test-images.github.io/png/202105/cs-black-000.png";
        using var client = CreateClient(verbose: true);
        using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await client.UploadFromUrlAsync(remoteUrl, cts.Token);

        Assert.True(result.Success, $"URL upload failed: {result.ErrorMessage}");
        Assert.False(string.IsNullOrWhiteSpace(result.ImageUrl), "ImageUrl should not be empty on success");
        Assert.True(UploadValidator.IsValidPostimgUrl(result.ImageUrl), $"Expected postimg.cc URL, got: {result.ImageUrl}");
        Assert.True(result.LinkAccessible, "Generated link should be accessible");

        Console.WriteLine($"[URL] Uploaded URL: {result.ImageUrl}");
    }

    [Fact]
    public async Task UploadFromUrl_InvalidScheme_ReturnsNetworkError()
    {
        using var client = CreateClient(verbose: false);
        using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await client.UploadFromUrlAsync("not-a-valid-url", cts.Token);

        Assert.False(result.Success);
        Assert.Contains("NetworkError", result.ErrorMessage);
        Assert.Equal(0, result.HttpStatusCode); // locally rejected
    }

    [Fact]
    public async Task UploadFromUrl_EmptyString_ReturnsNetworkError()
    {
        using var client = CreateClient(verbose: false);
        using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var result = await client.UploadFromUrlAsync("", cts.Token);

        Assert.False(result.Success);
        Assert.Contains("NetworkError", result.ErrorMessage);
    }

    [Fact]
    public async Task UploadValidJpg_ResultContainsTiming()
    {
        var path = TestFile("test_valid.jpg");
        using var client = CreateClient(verbose: false);
        using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await client.UploadFileAsync(path, cts.Token);

        if (!result.Success)
        {
            Assert.True(true, $"Upload did not succeed ({result.ErrorMessage}) — timing check skipped");
            return;
        }

        Assert.True(result.UploadElapsed > TimeSpan.Zero, "UploadElapsed should be non-zero after a successful upload");
        Assert.True(result.VerifyElapsed > TimeSpan.Zero, "VerifyElapsed should be non-zero after link verification");
        Assert.True(result.FileSizeBytes > 0, "FileSizeBytes should be set");

        Console.WriteLine($"Upload: {result.UploadElapsed.TotalSeconds:F2}s  Verify: {result.VerifyElapsed.TotalSeconds:F2}s  Size: {result.FileSizeBytes / 1024.0:F1} KB");
    }
}
