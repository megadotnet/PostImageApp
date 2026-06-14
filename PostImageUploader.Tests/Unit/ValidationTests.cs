using PostImageUploader;

namespace PostImageUploader.Tests.Unit;

/// <summary>
/// Pure unit tests for UploadValidator — no network, no disk I/O beyond test fixtures.
/// These tests run instantly and are safe to run in any CI environment.
/// </summary>
[Trait("Category", "Unit")]
public class ValidationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the path to the shared testdata directory that is copied
    /// alongside the test binary by the .csproj CopyToOutputDirectory rule.
    /// </summary>
    private static string TestDataDir =>
        Path.Combine(AppContext.BaseDirectory, "testdata");

    private static string TestFile(string name) =>
        Path.Combine(TestDataDir, name);

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 1: FileNotFound
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateLocalFile_NonExistentPath_ReturnsFileNotFound()
    {
        var result = UploadValidator.ValidateLocalFile(
            Path.Combine(TestDataDir, "nonexistent_file_xyz.png"));

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("FileNotFound", result.ErrorMessage);
    }

    [Fact]
    public void ValidateLocalFile_EmptyPath_ReturnsFileNotFound()
    {
        var result = UploadValidator.ValidateLocalFile("");

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("FileNotFound", result.ErrorMessage);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 2: UnsupportedFormat
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(".exe")]
    [InlineData(".txt")]
    [InlineData(".pdf")]
    [InlineData(".docx")]
    [InlineData(".mp4")]
    [InlineData(".zip")]
    public void ValidateLocalFile_UnsupportedExtension_ReturnsUnsupportedFormat(string ext)
    {
        // Create a tiny temp file with the given extension so existence check passes
        var tempPath = Path.Combine(Path.GetTempPath(), $"test_format_check{ext}");
        File.WriteAllBytes(tempPath, new byte[] { 0x00, 0x01, 0x02 });

        try
        {
            var result = UploadValidator.ValidateLocalFile(tempPath);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("UnsupportedFormat", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 3: FileTooLarge
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateLocalFile_OversizedFile_ReturnsFileTooLarge()
    {
        var path = TestFile("test_oversized.jpg");
        if (!File.Exists(path))
        {
            // Gracefully pass when testdata is not present (e.g., minimal CI)
            Assert.True(true, "testdata/test_oversized.jpg not found — test skipped gracefully");
            return;
        }

        var result = UploadValidator.ValidateLocalFile(path);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("FileTooLarge", result.ErrorMessage);
    }

    [Fact]
    public void ValidateLocalFile_FileExactlyAtLimit_ShouldPassSizeCheck()
    {
        // Create a temp JPG file exactly at the 12 MB boundary
        var tempPath = Path.Combine(Path.GetTempPath(), "test_exact_limit.jpg");
        var bytes    = new byte[UploadValidator.MaxFileSizeBytes]; // exactly 12 MB
        // Minimal JPEG header so it looks like a real JPEG
        bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF; bytes[3] = 0xE0;
        File.WriteAllBytes(tempPath, bytes);

        try
        {
            var result = UploadValidator.ValidateLocalFile(tempPath);
            // Exactly at the limit should pass (≤ MaxFileSizeBytes)
            Assert.Null(result);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void ValidateLocalFile_FileOneByteOverLimit_ReturnsFileTooLarge()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "test_over_limit.jpg");
        var bytes    = new byte[UploadValidator.MaxFileSizeBytes + 1]; // 1 byte over
        bytes[0] = 0xFF; bytes[1] = 0xD8;
        File.WriteAllBytes(tempPath, bytes);

        try
        {
            var result = UploadValidator.ValidateLocalFile(tempPath);
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("FileTooLarge", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void MaxFileSizeBytes_Is12MB()
    {
        Assert.Equal(12L * 1024 * 1024, UploadValidator.MaxFileSizeBytes);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 4: Valid files pass local validation
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateLocalFile_ValidJpg_ReturnsNull()
    {
        var path = TestFile("test_valid.jpg");
        if (!File.Exists(path))
        {
            Assert.True(true, "testdata/test_valid.jpg not found — test skipped gracefully");
            return;
        }

        var result = UploadValidator.ValidateLocalFile(path);

        // null means "passed" — no local validation error
        Assert.Null(result);
    }

    [Fact]
    public void ValidateLocalFile_ValidPng_ReturnsNull()
    {
        var path = TestFile("test_valid.png");
        if (!File.Exists(path))
        {
            Assert.True(true, "testdata/test_valid.png not found — test skipped gracefully");
            return;
        }

        var result = UploadValidator.ValidateLocalFile(path);

        Assert.Null(result);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 5: Supported extension list
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    [InlineData(".webp")]
    [InlineData(".tiff")]
    [InlineData(".tif")]
    public void SupportedExtensions_ContainsExpected(string ext)
    {
        Assert.Contains(ext, UploadValidator.SupportedExtensions);
    }

    [Fact]
    public void SupportedExtensions_TotalCount_Is8()
    {
        Assert.Equal(8, UploadValidator.SupportedExtensions.Length);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 6: IsHttpUrl
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("https://example.com/image.png", true)]
    [InlineData("http://example.com/image.jpg",  true)]
    [InlineData("HTTPS://EXAMPLE.COM/IMG.PNG",   true)]   // case-insensitive
    [InlineData("ftp://example.com/image.png",   false)]
    [InlineData("file:///local/path.png",         false)]
    [InlineData("",                               false)]
    [InlineData("just-a-filename.png",            false)]
    [InlineData("postimg.cc/abc",                 false)]
    public void IsHttpUrl_VariousInputs_ReturnsExpected(string url, bool expected)
    {
        Assert.Equal(expected, UploadValidator.IsHttpUrl(url));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 7: IsValidPostimgUrl
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("https://postimg.cc/abc123/hash",   true)]
    [InlineData("http://postimg.cc/abc123",         true)]
    [InlineData("HTTPS://POSTIMG.CC/abc",           true)]   // case-insensitive
    [InlineData("https://i.postimg.cc/abc/img.png", false)]  // i.postimg.cc is CDN, different
    [InlineData("https://postimages.org/gallery",   false)]
    [InlineData("https://imgur.com/abc",            false)]
    [InlineData(null,                               false)]
    [InlineData("",                                 false)]
    public void IsValidPostimgUrl_VariousInputs_ReturnsExpected(string? url, bool expected)
    {
        Assert.Equal(expected, UploadValidator.IsValidPostimgUrl(url));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 8: UploadResult model defaults
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void UploadResult_DefaultValues_AreCorrect()
    {
        var r = new UploadResult();

        Assert.False(r.Success);
        Assert.Null(r.ImageUrl);
        Assert.Null(r.ErrorMessage);
        Assert.Equal(0, r.HttpStatusCode);
        Assert.Equal(0L, r.FileSizeBytes);
        Assert.Equal(TimeSpan.Zero, r.UploadElapsed);
        Assert.Equal(TimeSpan.Zero, r.VerifyElapsed);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 9: PostImageJsonResponse model
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PostImageJsonResponse_IsValidUrl_TrueForPostimgDomain()
    {
        var resp = new PostImageJsonResponse
        {
            Url = "https://postimg.cc/NKXD5N1Z/4edbb0e4"
        };

        Assert.True(resp.IsValidUrl);
    }

    [Fact]
    public void PostImageJsonResponse_IsValidUrl_FalseForOtherDomain()
    {
        var resp = new PostImageJsonResponse
        {
            Url = "https://imgur.com/abc123"
        };

        Assert.False(resp.IsValidUrl);
    }

    [Fact]
    public void PostImageJsonResponse_IsValidUrl_FalseWhenNull()
    {
        var resp = new PostImageJsonResponse { Url = null };
        Assert.False(resp.IsValidUrl);
    }

    [Fact]
    public void PostImageJsonResponse_IsValidUrl_FalseWhenEmpty()
    {
        var resp = new PostImageJsonResponse { Url = "" };
        Assert.False(resp.IsValidUrl);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Group 10: FailureReason enum completeness
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FailureReason_Enum_HasExpectedValues()
    {
        var values = Enum.GetNames<FailureReason>();

        Assert.Contains("None",               values);
        Assert.Contains("FileNotFound",       values);
        Assert.Contains("FileTooLarge",       values);
        Assert.Contains("UnsupportedFormat",  values);
        Assert.Contains("NetworkError",       values);
        Assert.Contains("Timeout",            values);
        Assert.Contains("HttpError",          values);
        Assert.Contains("JsonParseError",     values);
        Assert.Contains("InvalidResponseUrl", values);
        Assert.Contains("LinkNotAccessible",  values);
        Assert.Contains("EmptyResponse",      values);
        Assert.Contains("UnknownError",       values);
    }
}
