using System.Threading;
using System.Threading.Tasks;
using App.Core.Models;

namespace App.Core.Abstractions;

public interface IPostImageClient
{
    Task<UploadResult> UploadFileAsync(string filePath, CancellationToken ct = default);
    Task<UploadResult> UploadFromUrlAsync(string imageUrl, CancellationToken ct = default);
}
