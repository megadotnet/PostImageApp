using System;
using System.Text.Json.Serialization;

namespace App.Core.Models;

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

    /// <summary>postimg.cc 页面链接或解析后的最终图片链接</summary>
    public string?  ImageUrl        { get; set; }

    /// <summary>图片直链（从 HTML 解析出的原始后缀 URL）</summary>
    public string?  DirectImageUrl  { get; set; }

    /// <summary>原始上传返回的 HTML 页面链接</summary>
    public string?  PageUrl         { get; set; }

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
