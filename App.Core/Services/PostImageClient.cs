using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using App.Core.Abstractions;
using App.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace App.Core.Services;

public class PostImageClient : IPostImageClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PostImageClient> _logger;
    private readonly IFileSystem _fileSystem;
    private readonly UploadValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly PostImageUploaderOptions _options;

    private const string UploadApiUrl = "https://postimages.org/json";
    private const string HomeUrl      = "https://postimages.org/";

    private bool _sessionReady;
    private bool _disposed;

    public PostImageClient(
        HttpClient httpClient,
        ILogger<PostImageClient> logger,
        IFileSystem fileSystem,
        UploadValidator validator,
        TimeProvider timeProvider,
        IOptions<PostImageUploaderOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _fileSystem = fileSystem;
        _validator = validator;
        _timeProvider = timeProvider;
        _options = options.Value;

        // Note: The HTTP handler configuration (Cookies, AllowAutoRedirect)
        // is now expected to be configured in DI when setting up the HttpClient.
        _httpClient.Timeout = TimeSpan.FromSeconds(120);

        // 模拟 Chrome 148 请求头
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/148.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language",
            "zh-CN,zh;q=0.9,en;q=0.8");
    }

    private async Task EnsureSessionAsync(CancellationToken ct)
    {
        if (_sessionReady) return;
        Log("  → 建立 Session（抓取首页 Cookie）...");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, HomeUrl);
            req.Headers.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9," +
                "image/avif,image/webp,image/apng,*/*;q=0.8");
            req.Headers.Add("Upgrade-Insecure-Requests", "1");
            await _httpClient.SendAsync(req, ct);
            Log("  ✓ Session 建立成功");
        }
        catch (Exception ex)
        {
            Log($"  ⚠ 主页访问失败（继续尝试上传）: {ex.Message}");
        }
        _sessionReady = true;
    }

    public async Task<UploadResult> UploadFileAsync(
        string filePath, CancellationToken ct = default)
    {
        var validationError = _validator.ValidateLocalFile(filePath);
        if (validationError is not null) return validationError;

        var ext  = Path.GetExtension(filePath).ToLowerInvariant();
        var length = _fileSystem.GetFileLength(filePath);

        byte[] bytes;
        try { bytes = await _fileSystem.ReadAllBytesAsync(filePath, ct); }
        catch (Exception ex)
        {
            return UploadValidator.Fail(FailureReason.UnknownError, $"读取文件失败: {ex.Message}");
        }

        return await UploadBytesAsync(bytes, Path.GetFileName(filePath),
            GetMime(ext), length, ct);
    }

    public async Task<UploadResult> UploadFromUrlAsync(
        string imageUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return UploadValidator.Fail(FailureReason.NetworkError, "URL 不能为空");

        if (!UploadValidator.IsHttpUrl(imageUrl))
            return UploadValidator.Fail(FailureReason.NetworkError,
                $"无效的 URL 格式（必须以 http:// 或 https:// 开头）: {imageUrl}");

        Log($"  → 正在下载远程图片: {imageUrl}");
        try
        {
            using var dlc = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            dlc.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var rsp = await dlc.GetAsync(imageUrl, ct);
            rsp.EnsureSuccessStatusCode();

            var bytes       = await rsp.Content.ReadAsByteArrayAsync(ct);
            var contentType = rsp.Content.Headers.ContentType?.MediaType ?? "image/png";
            var fileName    = Path.GetFileName(new Uri(imageUrl).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName) || fileName == "/")
                fileName = $"upload_{_timeProvider.GetUtcNow():yyyyMMddHHmmss}.png";

            Log($"  ✓ 下载完成: {bytes.Length / 1024.0:F1} KB  " +
                $"type={contentType}  file={fileName}");

            return await UploadBytesAsync(bytes, fileName, contentType, bytes.Length, ct);
        }
        catch (Exception ex)
        {
            return UploadValidator.Fail(FailureReason.NetworkError, $"下载图片失败: {ex.Message}");
        }
    }

    private async Task<UploadResult> UploadBytesAsync(
        byte[] imageBytes, string fileName, string mimeType,
        long originalSize, CancellationToken ct)
    {
        var result = new UploadResult { FileSizeBytes = originalSize };

        try { await EnsureSessionAsync(ct); }
        catch (Exception ex)
        {
            return UploadValidator.Fail(FailureReason.NetworkError, $"网络连接失败: {ex.Message}");
        }

        Log($"  → 构建 multipart 请求（文件: {fileName}, " +
            $"大小: {imageBytes.Length / 1024.0:F1} KB）...");

        var boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N")[..16];

        await using var ms = new MemoryStream();
        WriteField(ms, boundary, "gallery",        "");
        WriteField(ms, boundary, "optsize",        "0");
        WriteField(ms, boundary, "expire",         "0");
        WriteField(ms, boundary, "numfiles",       "1");
        WriteField(ms, boundary, "upload_session",
            $"{_timeProvider.GetUtcNow().ToUnixTimeMilliseconds()}.{new Random().Next(10000, 99999)}");

        WriteTextLine(ms, $"--{boundary}");
        WriteTextLine(ms, $"Content-Disposition: form-data; name=\"file\"; filename=\"{fileName}\"");
        WriteTextLine(ms, $"Content-Type: {mimeType}");
        WriteTextLine(ms);
        ms.Write(imageBytes);
        WriteTextLine(ms);
        WriteTextLine(ms, $"--{boundary}--");

        var bodyBytes   = ms.ToArray();
        var contentType = $"multipart/form-data; boundary={boundary}";

        Log($"  → 请求体大小: {bodyBytes.Length / 1024.0:F2} KB");
        Log("  → 正在上传到 postimages.org ...");

        using var request = new HttpRequestMessage(HttpMethod.Post, UploadApiUrl);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("Accept",           "application/json");
        request.Headers.Add("Origin",           "https://postimages.org");
        request.Headers.Add("Referer",          "https://postimages.org/");
        request.Headers.Add("Cache-Control",    "no-cache");
        request.Content = new ByteArrayContent(bodyBytes);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var uploadStartTime = _timeProvider.GetTimestamp();
        HttpResponseMessage httpRsp;
        try
        {
            httpRsp = await _httpClient.SendAsync(request, ct);
        }
        catch (TaskCanceledException)
        {
            return UploadValidator.Fail(FailureReason.Timeout, "请求超时（超过 120 秒）");
        }
        catch (HttpRequestException ex)
        {
            return UploadValidator.Fail(FailureReason.NetworkError,
                $"网络错误: {ex.Message}" +
                (ex.InnerException != null ? $" (内部: {ex.InnerException.Message})" : ""));
        }
        result.UploadElapsed = _timeProvider.GetElapsedTime(uploadStartTime);

        var rawJson = await httpRsp.Content.ReadAsStringAsync(ct);
        result.HttpStatusCode = (int)httpRsp.StatusCode;
        result.RawResponse    = rawJson;

        Log($"  → HTTP {result.HttpStatusCode}  耗时: {result.UploadElapsed.TotalSeconds:F2}s");
        Log($"  → 响应: {rawJson}");

        if (!httpRsp.IsSuccessStatusCode)
        {
            string hint = result.HttpStatusCode switch
            {
                400 => "请求参数错误（格式/字段问题）",
                403 => "接口权限错误（可能需要 Cookie 或被限速）",
                413 => "文件过大，超出服务器限制",
                429 => "请求过于频繁，触发限速",
                500 => "服务器内部错误",
                503 => "服务暂时不可用",
                _   => "未知 HTTP 错误"
            };
            return new UploadResult
            {
                HttpStatusCode = result.HttpStatusCode,
                RawResponse    = rawJson,
                UploadElapsed  = result.UploadElapsed,
                FileSizeBytes  = originalSize,
                Success        = false,
                ErrorMessage   = $"HTTP {result.HttpStatusCode} ({hint}): {rawJson}"
            };
        }

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            result.Success      = false;
            result.ErrorMessage = $"失败原因: {FailureReason.EmptyResponse} — 服务器返回空响应体";
            return result;
        }

        PostImageJsonResponse? apiResp;
        try
        {
            apiResp = JsonSerializer.Deserialize<PostImageJsonResponse>(rawJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            result.Success      = false;
            result.ErrorMessage = $"失败原因: {FailureReason.JsonParseError} — " +
                                  $"JSON 解析失败: {ex.Message} | 原文: {rawJson}";
            return result;
        }

        if (apiResp is null)
        {
            result.Success      = false;
            result.ErrorMessage = $"失败原因: {FailureReason.JsonParseError} — " +
                                  $"反序列化结果为 null | 原文: {rawJson}";
            return result;
        }

        if (!apiResp.IsValidUrl)
        {
            result.Success      = false;
            result.ErrorMessage = $"失败原因: {FailureReason.InvalidResponseUrl} — " +
                                  $"返回的 URL 不合法: '{apiResp.Url}' | 原文: {rawJson}";
            return result;
        }

        var pageUrl = apiResp.Url!;
        result.PageUrl = pageUrl;

        Log($"  → 正在请求页面 HTML 以解析原始图片后缀的 URL: {pageUrl}");
        var verifyStartTime = _timeProvider.GetTimestamp();

        (var directUrl, var parseError) = await FetchAndParseDirectUrlAsync(pageUrl, ct);
        if (parseError is not null || directUrl is null)
        {
            result.VerifyElapsed = _timeProvider.GetElapsedTime(verifyStartTime);
            result.Success = false;
            result.ErrorMessage = $"失败原因: {FailureReason.InvalidResponseUrl} — {parseError}";
            return result;
        }

        result.DirectImageUrl = directUrl;
        result.ImageUrl = directUrl;

        Log($"  → 正在验证直链可访问性: {result.ImageUrl}");
        (result.LinkAccessible, var accessError) =
            await VerifyLinkAsync(result.ImageUrl!, ct);
        result.VerifyElapsed = _timeProvider.GetElapsedTime(verifyStartTime);

        if (!result.LinkAccessible)
        {
            result.Success      = false;
            result.ErrorMessage = $"失败原因: {FailureReason.LinkNotAccessible} — " +
                                  $"图片直链无法访问: {accessError}";
            return result;
        }

        Log($"  ✓ 直链验证通过（耗时 {result.VerifyElapsed.TotalSeconds:F2}s）");
        result.Success = true;
        return result;
    }

    private async Task<(bool ok, string? error)> VerifyLinkAsync(
        string url, CancellationToken ct)
    {
        try
        {
            using var verifyClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            verifyClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var headResp = await verifyClient.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, url), ct);

            if (headResp.IsSuccessStatusCode)
            {
                Log($"  ✓ 链接可访问（HTTP {(int)headResp.StatusCode}）");
                return (true, null);
            }

            var getResp = await verifyClient.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead, ct);

            if (getResp.IsSuccessStatusCode)
            {
                Log($"  ✓ 链接可访问 via GET（HTTP {(int)getResp.StatusCode}）");
                return (true, null);
            }

            return (false, $"HTTP {(int)getResp.StatusCode}");
        }
        catch (TaskCanceledException)
        {
            return (false, "访问链接超时（>15s）");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private void Log(string message)
    {
        if (_options.Verbose)
        {
            _logger.LogInformation("{Message}", message);
        }
    }

    private static void WriteField(Stream s, string boundary, string name, string value)
    {
        WriteTextLine(s, $"--{boundary}");
        WriteTextLine(s, $"Content-Disposition: form-data; name=\"{name}\"");
        WriteTextLine(s);
        WriteTextLine(s, value);
    }

    private static void WriteTextLine(Stream s, string text = "")
    {
        var bytes = Encoding.UTF8.GetBytes(text + "\r\n");
        s.Write(bytes);
    }

    private static string GetMime(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif"            => "image/gif",
        ".bmp"            => "image/bmp",
        ".webp"           => "image/webp",
        ".tiff" or ".tif" => "image/tiff",
        _                 => "image/png",
    };

    private async Task<(string? directUrl, string? error)> FetchAndParseDirectUrlAsync(
        string pageUrl, CancellationToken ct)
    {
        try
        {
            using var verifyClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            verifyClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await verifyClient.GetAsync(pageUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                return (null, $"HTTP {(int)response.StatusCode} when fetching page HTML");
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var directUrl = ParseDirectImageUrl(html);
            if (string.IsNullOrWhiteSpace(directUrl))
            {
                return (null, "Failed to parse direct image URL from HTML page");
            }

            return (directUrl, null);
        }
        catch (Exception ex)
        {
            return (null, $"Error fetching page HTML: {ex.Message}");
        }
    }

    public static string? ParseDirectImageUrl(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var directMatch = System.Text.RegularExpressions.Regex.Match(html, 
            @"id=""direct""\s+value=""([^""]+)""", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (directMatch.Success) return directMatch.Groups[1].Value;

        var ogMatch = System.Text.RegularExpressions.Regex.Match(html, 
            @"<meta\s+property=""og:image""\s+content=""([^""]+)""", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (ogMatch.Success) return ogMatch.Groups[1].Value;

        var fallbackMatch = System.Text.RegularExpressions.Regex.Match(html, 
            @"https?://i\.postimg\.cc/[a-zA-Z0-9]+/[^""\s>]+", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (fallbackMatch.Success) return fallbackMatch.Value;

        return null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        // _httpClient is managed by DI
        _disposed = true;
    }
}
