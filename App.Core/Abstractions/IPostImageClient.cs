using System.Threading;
using System.Threading.Tasks;
using App.Core.Models;

namespace App.Core.Abstractions;

/// <summary>
/// Defines the contract for a client capable of uploading images to PostImages.org.
/// </summary>
public interface IPostImageClient
{
    /// <summary>
    /// Uploads an image from a local file path.
    /// </summary>
    /// <param name="filePath">The absolute or relative path to the image file.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="UploadResult"/> detailing the outcome.</returns>
    Task<UploadResult> UploadFileAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Uploads an image from a remote URL. The image is downloaded to memory first and then uploaded.
    /// </summary>
    /// <param name="imageUrl">The HTTP/HTTPS URL of the remote image.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="UploadResult"/> detailing the outcome.</returns>
    Task<UploadResult> UploadFromUrlAsync(string imageUrl, CancellationToken ct = default);
}
