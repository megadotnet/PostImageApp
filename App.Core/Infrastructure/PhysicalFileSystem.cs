using System.IO;
using System.Threading;
using System.Threading.Tasks;
using App.Core.Abstractions;

namespace App.Core.Infrastructure;

public class PhysicalFileSystem : IFileSystem
{
    public bool Exists(string path) => File.Exists(path);

    public long GetFileLength(string path)
    {
        var info = new FileInfo(path);
        return info.Length;
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        return File.ReadAllBytesAsync(path, cancellationToken);
    }
}
