using App.Core.Models;
using App.Core.Services;
using App.Core.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace PostImageUploader.Tests.Unit;

[Trait("Category", "Unit")]
public class ValidationTests
{
    private readonly Mock<IFileSystem> _fileSystemMock;
    private readonly PostImageUploaderOptions _options;
    private readonly UploadValidator _validator;

    public ValidationTests()
    {
        _fileSystemMock = new Mock<IFileSystem>();
        _options = new PostImageUploaderOptions(); // Use default options
        _validator = new UploadValidator(_fileSystemMock.Object, Options.Create(_options));
    }

    private static string TestDataDir => Path.Combine(AppContext.BaseDirectory, "testdata");
    private static string TestFile(string name) => Path.Combine(TestDataDir, name);

    [Fact]
    public void ValidateLocalFile_NonExistentPath_ReturnsFileNotFound()
    {
        var path = "C:\\fake\\path\\to\\nothing.jpg";
        _fileSystemMock.Setup(fs => fs.Exists(path)).Returns(false);

        var result = _validator.ValidateLocalFile(path);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("FileNotFound", result.ErrorMessage);
    }

    [Fact]
    public void ValidateLocalFile_UnsupportedExtension_ReturnsUnsupportedFormat()
    {
        var path = TestFile("test_unsupported.txt");
        _fileSystemMock.Setup(fs => fs.Exists(path)).Returns(true);

        var result = _validator.ValidateLocalFile(path);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("UnsupportedFormat", result.ErrorMessage);
    }

    [Fact]
    public void ValidateLocalFile_FileTooLarge_ReturnsFileTooLarge()
    {
        var path = "C:\\fake\\path\\too_large.jpg";
        _fileSystemMock.Setup(fs => fs.Exists(path)).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetFileLength(path)).Returns(_options.MaxFileSizeBytes + 1);

        var result = _validator.ValidateLocalFile(path);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("FileTooLarge", result.ErrorMessage);
    }

    [Fact]
    public void ValidateLocalFile_ValidFile_ReturnsNull()
    {
        var path = "C:\\fake\\path\\valid.jpg";
        _fileSystemMock.Setup(fs => fs.Exists(path)).Returns(true);
        _fileSystemMock.Setup(fs => fs.GetFileLength(path)).Returns(_options.MaxFileSizeBytes - 1);

        var result = _validator.ValidateLocalFile(path);

        Assert.Null(result); // Null means validation passed
    }

    [Fact]
    public void MaxFileSizeBytes_Is12MB()
    {
        Assert.Equal(12L * 1024 * 1024, _options.MaxFileSizeBytes);
    }

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
        Assert.Contains(ext, _options.SupportedExtensions);
    }

    [Theory]
    [InlineData("https://example.com/image.png", true)]
    [InlineData("http://example.com/image.jpg",  true)]
    [InlineData("HTTPS://EXAMPLE.COM/IMG.PNG",   true)]
    [InlineData("ftp://example.com/image.png",   false)]
    [InlineData("file:///local/path.png",         false)]
    [InlineData("",                               false)]
    [InlineData("just-a-filename.png",            false)]
    public void IsHttpUrl_VariousInputs_ReturnsExpected(string url, bool expected)
    {
        Assert.Equal(expected, UploadValidator.IsHttpUrl(url));
    }

    [Theory]
    [InlineData("https://postimg.cc/abc123/hash",   true)]
    [InlineData("http://postimg.cc/abc123",         true)]
    [InlineData("HTTPS://POSTIMG.CC/abc",           true)]
    [InlineData("https://i.postimg.cc/abc/img.png", true)]
    [InlineData("https://postimages.org/gallery",   false)]
    [InlineData("https://imgur.com/abc",            false)]
    [InlineData(null,                               false)]
    [InlineData("",                                 false)]
    public void IsValidPostimgUrl_VariousInputs_ReturnsExpected(string? url, bool expected)
    {
        Assert.Equal(expected, UploadValidator.IsValidPostimgUrl(url));
    }

    [Fact]
    public void UploadResult_DefaultValues_AreCorrect()
    {
        var r = new UploadResult();

        Assert.False(r.Success);
        Assert.Null(r.ImageUrl);
        Assert.Null(r.DirectImageUrl);
        Assert.Null(r.PageUrl);
        Assert.Null(r.ErrorMessage);
        Assert.Equal(0, r.HttpStatusCode);
        Assert.Equal(0L, r.FileSizeBytes);
        Assert.Equal(TimeSpan.Zero, r.UploadElapsed);
        Assert.Equal(TimeSpan.Zero, r.VerifyElapsed);
    }

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
    public void ParseDirectImageUrl_InputIdDirect_ExtractsCorrectUrl()
    {
        var html = @"<html><body>
            <input type=""text"" class=""form-control"" id=""direct"" value=""https://i.postimg.cc/V6Z83K1d/shang-hai.png"">
            </body></html>";

        var result = PostImageClient.ParseDirectImageUrl(html);

        Assert.Equal("https://i.postimg.cc/V6Z83K1d/shang-hai.png", result);
    }
}
