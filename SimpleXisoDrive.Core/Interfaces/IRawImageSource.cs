namespace SimpleXisoDrive.Core.Interfaces;

/// <summary>
/// Provides read access to the raw bytes of the mounted Xbox image, served to
/// consumers as the virtual <c>image.iso</c> file. Implementations back the file
/// with a plain ISO stream, a CISO block device view, an Xbox ISO CHD stream, an
/// XISO embedded in a ZArchive, or an XISO synthesized from a ZArchive tree.
/// </summary>
internal interface IRawImageSource : IDisposable
{
    /// <summary>
    /// Gets the total size of the raw image in bytes.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Reads raw image bytes at the specified offset into the buffer.
    /// </summary>
    /// <param name="buffer">The buffer that receives the data.</param>
    /// <param name="offset">The byte offset within the image at which to start reading.</param>
    /// <returns>The number of bytes read.</returns>
    /// <exception cref="IOException">Thrown when the underlying data cannot be read.</exception>
    int Read(Span<byte> buffer, long offset);
}