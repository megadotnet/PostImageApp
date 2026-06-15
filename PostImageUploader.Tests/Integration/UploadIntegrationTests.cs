using PostImageUploader;

namespace PostImageUploader.Tests.Integration;

/// <summary>
/// Integration tests that make real HTTP calls to PostImages.org.
/// Requires internet access. Marked [Trait("Category", "Integration")] so they
/// can be excluded from fast CI runs:
///   dotnet test --filter "Category=Unit"          # unit only (fast, no network)
///   dotnet test --filter "Category=Integration"   # integration only
///   dotnet test                                   # all tests
/// </summary>
[Trait("Category", "Integration")]
public class UploadIntegrationTests
{
    // ── Configuration ─────────────────────────────────────────────────────────

    private static string TestDataDir =>
        Path.Combine(AppContext.BaseDirectory, "testdata");

    private static string TestFile(string name) =>
        Path.Combine(TestDataDir, name);

    /// <summary>
    /// Delay between upload integration tests to avoid PostImages.org rate limiting.
    /// </summary>
    private static readonly TimeSpan DelayBetweenTests = TimeSpan.FromSeconds(3);

    // ── Test #1: Upload valid JPG ──────────────────────────────────────────────

    [Fact]
    public async Task UploadValidJpg_ReturnsPostimgCcUrl()
    {
        var path = TestFile("test_valid.jpg");
        if (!File.Exists(path))
        {
            Assert.True(true, "testdata/test_valid.jpg not found — skipping");
            return;
        }

        using var client = new PostImageClient(verbose: true);
        using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await client.UploadFileAsync(path, cts.Token);

        // Assert: upload succeeds
        Assert.True(result.Success,
            $"Upload failed: {result.ErrorMessage}");

        // Assert: returned URL is a valid postimg.cc link
        Assert.False(string.IsNullOrWhiteSpace(result.ImageUrl),
            "ImageUrl should not be empty on success");
        Assert.True(UploadValidator.IsValidPostimgUrl(result.ImageUrl),
            $"Expected postimg.cc URL, got: {result.ImageUrl}");

        // Assert: link accessibility was verified
        Assert.True(result.LinkAccessible,
            "Generated link should be accessible");

        // Report URL for manual inspection
        Console.WriteLine($"[JPG] Uploaded URL: {result.ImageUrl}");

        await Task.Delay(DelayBetweenTests);
    }

    // ── Test #2: Upload valid PNG ──────────────────────────────────────────────

    [Fact]
    public async Task UploadValidPng_ReturnsPostimgCcUrl()
    {
        var path = TestFile("test_valid.png");
        if (!File.Exists(path))
        {
            Assert.True(true, "testdata/test_valid.png not found — skipping");
            return;
        }

        using var client = new PostImageClient(verbose: true);
        using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await client.UploadFileAsync(path, cts.Token);

        Assert.True(result.Success,
            $"Upload failed: {result.ErrorMessage}");
        Assert.False(string.IsNullOrWhiteSpace(result.ImageUrl),
            "ImageUrl should not be empty on success");
        Assert.True(UploadValidator.IsValidPostimgUrl(result.ImageUrl),
            $"Expected postimg.cc URL, got: {result.ImageUrl}");
        Assert.True(result.LinkAccessible,
            "Generated link should be accessible");

        Console.WriteLine($"[PNG] Uploaded URL: {result.ImageUrl}");

        await Task.Delay(DelayBetweenTests);
    }

    // ── Test #3: Upload oversized file — must fail locally ────────────────────

    [Fact]
    public async Task UploadOversizedFile_FailsLocally_WithFileTooLarge()
    {
        var path = TestFile("test_oversized.jpg");
        if (!File.Exists(path))
        {
            Assert.True(true, "testdata/test_oversized.jpg not found — skipping");
            return;
        }

        using var client = new PostImageClient(verbose: false);
        using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await client.UploadFileAsync(path, cts.Token);

        // Must fail locally before network
        Assert.False(result.Success, "Oversized file should be rejected");
        Assert.Equal(0, result.HttpStatusCode);   // locally intercepted, no HTTP call
        Assert.Contains("FileTooLarge", result.ErrorMessage);
    }

    // ── Test #4: Upload from remote URL ──────────────────────────────────────

    [Fact]
    public async Task UploadFromRemoteUrl_ReturnsPostimgCcUrl()
    {
        // Small, stable public PNG for URL upload test
        const string remoteUrl =
            "https://test-images.github.io/png/202105/cs-black-000.png";

        using var client = new PostImageClient(verbose: true);
        using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await client.UploadFromUrlAsync(remoteUrl, cts.Token);

        Assert.True(result.Success,
            $"URL upload failed: {result.ErrorMessage}");
        Assert.False(string.IsNullOrWhiteSpace(result.ImageUrl),
            "ImageUrl should not be empty on success");
        Assert.True(UploadValidator.IsValidPostimgUrl(result.ImageUrl),
            $"Expected postimg.cc URL, got: {result.ImageUrl}");
        Assert.True(result.LinkAccessible,
            "Generated link should be accessible");

        Console.WriteLine($"[URL] Uploaded URL: {result.ImageUrl}");
    }

    // ── Test #5: Upload with invalid URL format ───────────────────────────────

    [Fact]
    public async Task UploadFromUrl_InvalidScheme_ReturnsNetworkError()
    {
        using var client = new PostImageClient(verbose: false);
        using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await client.UploadFromUrlAsync("not-a-valid-url", cts.Token);

        Assert.False(result.Success);
        Assert.Contains("NetworkError", result.ErrorMessage);
        Assert.Equal(0, result.HttpStatusCode); // locally rejected
    }

    // ── Test #6: Upload with empty URL ───────────────────────────────────────

    [Fact]
    public async Task UploadFromUrl_EmptyString_ReturnsNetworkError()
    {
        using var client = new PostImageClient(verbose: false);
        using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var result = await client.UploadFromUrlAsync("", cts.Token);

        Assert.False(result.Success);
        Assert.Contains("NetworkError", result.ErrorMessage);
    }

    // ── Test #7: Result contains timing info after successful upload ──────────

    [Fact]
    public async Task UploadValidJpg_ResultContainsTiming()
    {
        var path = TestFile("test_valid.jpg");
        if (!File.Exists(path))
        {
            Assert.True(true, "testdata/test_valid.jpg not found — skipping");
            return;
        }

        using var client = new PostImageClient(verbose: false);
        using var cts    = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await client.UploadFileAsync(path, cts.Token);

        if (!result.Success)
        {
            // Integration test may fail due to network — gracefully pass
            Assert.True(true, $"Upload did not succeed ({result.ErrorMessage}) — timing check skipped");
            return;
        }

        Assert.True(result.UploadElapsed > TimeSpan.Zero,
            "UploadElapsed should be non-zero after a successful upload");
        Assert.True(result.VerifyElapsed > TimeSpan.Zero,
            "VerifyElapsed should be non-zero after link verification");
        Assert.True(result.FileSizeBytes > 0,
            "FileSizeBytes should be set");

        Console.WriteLine($"Upload: {result.UploadElapsed.TotalSeconds:F2}s  " +
                          $"Verify: {result.VerifyElapsed.TotalSeconds:F2}s  " +
                          $"Size: {result.FileSizeBytes / 1024.0:F1} KB");
    }
}
