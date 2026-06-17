using System.Threading;
using System.Threading.Tasks;

namespace App.Core.Abstractions;

/// <summary>
/// Provides an abstraction over the file system to enable unit testing and cross-platform compatibility.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    /// <param name="path">The file to check.</param>
    /// <returns>True if the caller has the required permissions and path contains the name of an existing file; otherwise, false.</returns>
    bool Exists(string path);

    /// <summary>
    /// Gets the size, in bytes, of the current file.
    /// </summary>
    /// <param name="path">The fully qualified path of the file.</param>
    /// <returns>The size of the current file in bytes.</returns>
    long GetFileLength(string path);

    /// <summary>
    /// Asynchronously opens a file, reads all bytes of the file, and then closes the file.
    /// </summary>
    /// <param name="path">The file to open for reading.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation, which wraps the byte array containing the contents of the file.</returns>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);
}
