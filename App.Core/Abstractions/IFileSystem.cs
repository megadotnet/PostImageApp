using System.Threading;
using System.Threading.Tasks;

namespace App.Core.Abstractions;

public interface IFileSystem
{
    bool Exists(string path);
    long GetFileLength(string path);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
}
