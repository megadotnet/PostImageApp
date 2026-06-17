using System.IO;
using System.Threading;
using System.Threading.Tasks;
using App.Core.Abstractions;

namespace App.Core.Infrastructure;

/// <summary>
/// A default implementation of <see cref="IFileSystem"/> that interacts with the physical disk using <see cref="System.IO.File"/>.
/// </summary>
public class PhysicalFileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path);

    /// <inheritdoc />
    public long GetFileLength(string path)
    {
        var info = new FileInfo(path);
        return info.Length;
    }

    /// <inheritdoc />
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        return File.ReadAllBytesAsync(path, cancellationToken);
    }
}
