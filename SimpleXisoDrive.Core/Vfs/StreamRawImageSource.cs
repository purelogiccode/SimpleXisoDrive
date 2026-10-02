using Serilog;
using SimpleXisoDrive.Core.Interfaces;

namespace SimpleXisoDrive.Core.Vfs;

/// <summary>
/// Serves raw image bytes from a seekable, read-only stream: a plain ISO/XISO
/// file, a CISO block-device view, or an XISO embedded in a ZArchive. Seeks and
/// reads are serialized because Dokan can issue concurrent reads against the same
/// file.
/// </summary>
internal sealed class StreamRawImageSource : IRawImageSource
{
    private readonly Stream _stream;
    private readonly Lock _readLock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamRawImageSource"/> class.
    /// </summary>
    /// <param name="stream">A seekable, read-only stream positioned at the start of the image.</param>
    /// <exception cref="ArgumentException">Thrown when the stream is not seekable.</exception>
    public StreamRawImageSource(Stream stream)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!stream.CanSeek)
            {
                throw new ArgumentException("The raw image stream must be seekable.", nameof(stream));
            }

            _stream = stream;
            Length = stream.Length;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create the raw image source");
            throw;
        }
    }

    /// <inheritdoc />
    public long Length { get; }

    /// <inheritdoc />
    public int Read(Span<byte> buffer, long offset)
    {
        if (buffer.IsEmpty || offset < 0 || offset >= Length)
        {
            return 0;
        }

        var bytesToRead = (int)Math.Min(buffer.Length, Length - offset);

        lock (_readLock)
        {
            if (_disposed)
            {
                return 0;
            }

            try
            {
                _stream.Seek(offset, SeekOrigin.Begin);

                var totalRead = 0;
                while (totalRead < bytesToRead)
                {
                    var read = _stream.Read(buffer[totalRead..bytesToRead]);
                    if (read <= 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                return totalRead;
            }
            catch (Exception ex)
            {
                // A read failure must not look like EOF: propagate it so the mount layers
                // can return DokanResult.Error / -EIO instead of a silent truncation.
                Log.Debug(ex, "Raw image read failed at offset {Offset}", offset);
                throw new IOException($"Failed to read the raw image at offset {offset}.", ex);
            }
        }
    }

    /// <summary>
    /// Closes the underlying stream.
    /// </summary>
    public void Dispose()
    {
        lock (_readLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _stream.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to dispose the raw image stream");
            }
        }
    }
}