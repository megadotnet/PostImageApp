using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PostImageUploader;

// ── API 响应模型（匹配真实接口：{"url":"...","image":"..."}）────────────────
public class PostImageJsonResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    /// <summary>返回的 URL 是否为合法链接</summary>
    public bool IsValidUrl =>
        !string.IsNullOrWhiteSpace(Url) &&
        (Url.StartsWith("https://postimg.cc/", StringComparison.OrdinalIgnoreCase) ||
         Url.StartsWith("http://postimg.cc/",  StringComparison.OrdinalIgnoreCase));
}

// ── 上传结果 ──────────────────────────────────────────────────────────────────
public class UploadResult
{
    /// <summary>整体是否成功（上传 + 链接可访问）</summary>
    public bool     Success         { get; set; }

    /// <summary>postimg.cc 页面链接</summary>
    public string?  ImageUrl        { get; set; }

    /// <summary>图片直链（image 字段，与 url 相同或衍生）</summary>
    public string?  DirectImageUrl  { get; set; }

    /// <summary>链接可访问性校验是否通过</summary>
    public bool     LinkAccessible  { get; set; }

    /// <summary>失败时的原因描述</summary>
    public string?  ErrorMessage    { get; set; }

    /// <summary>HTTP 状态码（本地拦截时为 0）</summary>
    public int      HttpStatusCode  { get; set; }

    /// <summary>原始响应体（调试用）</summary>
    public string?  RawResponse     { get; set; }

    /// <summary>文件大小（字节）</summary>
    public long     FileSizeBytes   { get; set; }

    /// <summary>上传耗时</summary>
    public TimeSpan UploadElapsed  { get; set; }

    /// <summary>链接验证耗时</summary>
    public TimeSpan VerifyElapsed  { get; set; }
}

// ── 失败原因枚举 ──────────────────────────────────────────────────────────────
public enum FailureReason
{
    None,
    FileNotFound,
    FileTooLarge,
    UnsupportedFormat,
    NetworkError,
    Timeout,
    HttpError,
    JsonParseError,
    InvalidResponseUrl,
    LinkNotAccessible,
    EmptyResponse,
    UnknownError,
}

// ── 可独立测试的本地校验逻辑（static，无 I/O 依赖）────────────────────────────
public static class UploadValidator
{
    public static readonly long MaxFileSizeBytes = 12L * 1024 * 1024; // 12 MB

    public static readonly string[] SupportedExtensions =
        { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif" };

    /// <summary>
    /// 对本地文件路径执行前置校验。
    /// 返回 null 表示通过；返回非 null 表示失败的 UploadResult。
    /// </summary>
    public static UploadResult? ValidateLocalFile(string filePath)
    {
        if (!File.Exists(filePath))
            return Fail(FailureReason.FileNotFound, $"文件不存在: {filePath}");

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext))
            return Fail(FailureReason.UnsupportedFormat,
                $"不支持的格式 '{ext}'。PostImages 支持: " +
                string.Join(", ", SupportedExtensions));

        var info = new FileInfo(filePath);
        if (info.Length > MaxFileSizeBytes)
            return Fail(FailureReason.FileTooLarge,
                $"文件过大: {info.Length / 1024.0 / 1024.0:F2} MB " +
                $"（PostImages 限制约 {MaxFileSizeBytes / 1024 / 1024} MB）");

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
         url.StartsWith("http://postimg.cc/",  StringComparison.OrdinalIgnoreCase));

    internal static UploadResult Fail(FailureReason reason, string message) =>
        new() { Success = false, ErrorMessage = $"失败原因: {reason} — {message}" };
}

// ── PostImage 上传客户端 ───────────────────────────────────────────────────────
public class PostImageClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool       _verbose;

    // ── 真实接口地址（从 Chrome 抓包验证）─────────────────────────────────────
    private const string UploadApiUrl = "https://postimages.org/json";
    private const string HomeUrl      = "https://postimages.org/";

    private bool _sessionReady;
    private bool _disposed;

    /// <param name="verbose">
    /// true  = 输出详细进度日志到 Console（测试 / 调试模式）<br/>
    /// false = 静默模式（CLI 生产使用）
    /// </param>
    public PostImageClient(bool verbose = true)
    {
        _verbose = verbose;

        var handler = new HttpClientHandler
        {
            UseCookies               = true,
            CookieContainer          = new CookieContainer(),
            AllowAutoRedirect        = true,
            MaxAutomaticRedirections = 5,
        };

        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };

        // 模拟 Chrome 148 请求头
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/148.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language",
            "zh-CN,zh;q=0.9,en;q=0.8");
    }

    // ── 建立 Session（抓取首页 Cookie）────────────────────────────────────────
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

    // ── 公开方法：从本地文件上传 ─────────────────────────────────────────────
    public async Task<UploadResult> UploadFileAsync(
        string filePath, CancellationToken ct = default)
    {
        // 本地校验（委托给可单独测试的 UploadValidator）
        var validationError = UploadValidator.ValidateLocalFile(filePath);
        if (validationError is not null) return validationError;

        var ext  = Path.GetExtension(filePath).ToLowerInvariant();
        var info = new FileInfo(filePath);

        byte[] bytes;
        try { bytes = await File.ReadAllBytesAsync(filePath, ct); }
        catch (Exception ex)
        {
            return UploadValidator.Fail(FailureReason.UnknownError, $"读取文件失败: {ex.Message}");
        }

        return await UploadBytesAsync(bytes, Path.GetFileName(filePath),
            GetMime(ext), info.Length, ct);
    }

    // ── 公开方法：从远程 URL 下载后上传 ──────────────────────────────────────
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
                fileName = $"upload_{DateTime.UtcNow:yyyyMMddHHmmss}.png";

            Log($"  ✓ 下载完成: {bytes.Length / 1024.0:F1} KB  " +
                $"type={contentType}  file={fileName}");

            return await UploadBytesAsync(bytes, fileName, contentType, bytes.Length, ct);
        }
        catch (Exception ex)
        {
            return UploadValidator.Fail(FailureReason.NetworkError, $"下载图片失败: {ex.Message}");
        }
    }

    // ── 核心：构建 multipart/form-data 并上传 ────────────────────────────────
    //
    //  根据 Chrome 抓包还原真实请求：
    //    POST https://postimages.org/json
    //    字段: gallery / optsize / expire / numfiles / upload_session / file
    //    响应: {"url":"https://postimg.cc/...","image":"..."}
    //
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

        // ── 生成 boundary（与 Chrome WebKit 格式一致）────────────────────────
        var boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N")[..16];

        await using var ms = new MemoryStream();
        WriteField(ms, boundary, "gallery",        "");
        WriteField(ms, boundary, "optsize",        "0");
        WriteField(ms, boundary, "expire",         "0");
        WriteField(ms, boundary, "numfiles",       "1");
        WriteField(ms, boundary, "upload_session",
            $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.{new Random().Next(10000, 99999)}");

        // file part
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

        // ── 发送请求 ──────────────────────────────────────────────────────────
        using var request = new HttpRequestMessage(HttpMethod.Post, UploadApiUrl);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        request.Headers.Add("Accept",           "application/json");
        request.Headers.Add("Origin",           "https://postimages.org");
        request.Headers.Add("Referer",          "https://postimages.org/");
        request.Headers.Add("Cache-Control",    "no-cache");
        request.Content = new ByteArrayContent(bodyBytes);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var uploadSw = System.Diagnostics.Stopwatch.StartNew();
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
        uploadSw.Stop();
        result.UploadElapsed = uploadSw.Elapsed;

        var rawJson = await httpRsp.Content.ReadAsStringAsync(ct);
        result.HttpStatusCode = (int)httpRsp.StatusCode;
        result.RawResponse    = rawJson;

        Log($"  → HTTP {result.HttpStatusCode}  耗时: {result.UploadElapsed.TotalSeconds:F2}s");
        Log($"  → 响应: {rawJson}");

        // ── 校验 1：HTTP 状态码 ──────────────────────────────────────────────
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

        // ── 校验 2：响应体非空 ────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            result.Success      = false;
            result.ErrorMessage = $"失败原因: {FailureReason.EmptyResponse} — 服务器返回空响应体";
            return result;
        }

        // ── 校验 3：JSON 解析 ─────────────────────────────────────────────────
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

        // ── 校验 4：URL 合法性（必须是 postimg.cc 域名）──────────────────────
        if (!apiResp.IsValidUrl)
        {
            result.Success      = false;
            result.ErrorMessage = $"失败原因: {FailureReason.InvalidResponseUrl} — " +
                                  $"返回的 URL 不合法: '{apiResp.Url}' | 原文: {rawJson}";
            return result;
        }

        result.ImageUrl       = apiResp.Url;
        result.DirectImageUrl = apiResp.Image ?? apiResp.Url;

        // ── 校验 5：主动验证链接可访问性 ──────────────────────────────────────
        Log($"  → 正在验证链接可访问性: {result.ImageUrl}");
        var verifySw = System.Diagnostics.Stopwatch.StartNew();
        (result.LinkAccessible, var accessError) =
            await VerifyLinkAsync(result.ImageUrl!, ct);
        verifySw.Stop();
        result.VerifyElapsed = verifySw.Elapsed;

        if (!result.LinkAccessible)
        {
            result.Success      = false;
            result.ErrorMessage = $"失败原因: {FailureReason.LinkNotAccessible} — " +
                                  $"图片链接无法访问: {accessError}";
            return result;
        }

        // ── 全部通过 ──────────────────────────────────────────────────────────
        Log($"  ✓ 链接验证通过（耗时 {result.VerifyElapsed.TotalSeconds:F2}s）");
        result.Success = true;
        return result;
    }

    // ── 链接可访问性校验 ──────────────────────────────────────────────────────
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

            // fallback to GET
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

    // ── 工具方法 ──────────────────────────────────────────────────────────────

    private void Log(string message)
    {
        if (_verbose) Console.Error.WriteLine(message);
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

    public void Dispose()
    {
        if (_disposed) return;
        _httpClient.Dispose();
        _disposed = true;
    }
}
