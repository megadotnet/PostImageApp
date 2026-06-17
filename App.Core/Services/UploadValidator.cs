using System;
using System.IO;
using System.Linq;
using App.Core.Abstractions;
using App.Core.Models;
using Microsoft.Extensions.Options;

namespace App.Core.Services;

/// <summary>
/// Provides pure validation logic for local files and remote URLs.
/// Does not depend on network calls, making it highly testable.
/// </summary>
public class UploadValidator
{
    private readonly IFileSystem _fileSystem;
    private readonly PostImageUploaderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadValidator"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used to check file existence and size.</param>
    /// <param name="options">The configured options for the uploader (e.g., max size, supported extensions).</param>
    public UploadValidator(IFileSystem fileSystem, IOptions<PostImageUploaderOptions> options)
    {
        _fileSystem = fileSystem;
        _options = options.Value;
    }

    /// <summary>
    /// 对本地文件路径执行前置校验。
    /// 返回 null 表示通过；返回非 null 表示失败的 UploadResult。
    /// </summary>
    public UploadResult? ValidateLocalFile(string filePath)
    {
        if (!_fileSystem.Exists(filePath))
            return Fail(FailureReason.FileNotFound, $"文件不存在: {filePath}");

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (!_options.SupportedExtensions.Contains(ext))
            return Fail(FailureReason.UnsupportedFormat,
                $"不支持的格式 '{ext}'。PostImages 支持: " +
                string.Join(", ", _options.SupportedExtensions));

        var length = _fileSystem.GetFileLength(filePath);
        if (length > _options.MaxFileSizeBytes)
            return Fail(FailureReason.FileTooLarge,
                $"文件过大: {length / 1024.0 / 1024.0:F2} MB " +
                $"（PostImages 限制约 {_options.MaxFileSizeBytes / 1024 / 1024} MB）");

        return null; // all checks passed
    }

    /// <summary>检查 URL 字符串是否以 http(s):// 开头</summary>
    public static bool IsHttpUrl(string value) =>
        value.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>检查 postimg.cc URL 合法性</summary>
    public static bool IsValidPostimgUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        (url.StartsWith("https://postimg.cc/", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("http://postimg.cc/",  StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("https://i.postimg.cc/", StringComparison.OrdinalIgnoreCase) ||
         url.StartsWith("http://i.postimg.cc/",  StringComparison.OrdinalIgnoreCase));

    internal static UploadResult Fail(FailureReason reason, string message) =>
        new() { Success = false, ErrorMessage = $"失败原因: {reason} — {message}" };
}
