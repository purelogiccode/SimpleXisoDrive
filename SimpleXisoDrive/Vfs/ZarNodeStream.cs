using ZArchiveSharp;

namespace SimpleXisoDrive.Vfs;

/// <summary>
/// A seekable, read-only <see cref="Stream"/> over a single file stored inside a
/// <see cref="ZArchiveReader"/>. Used to mount an XISO image embedded in a ZArchive
/// with the regular XDVDFS reader.
/// </summary>
internal sealed class ZarNodeStream : Stream
{
    private readonly ZArchiveReader _reader;
    private readonly uint _node;
    private bool _leaveOpen;
    private long _position;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZarNodeStream"/> class.
    /// </summary>
    /// <param name="reader">The open archive that owns the file data.</param>
    /// <param name="node">The ZArchive file node handle.</param>
    /// <param name="length">The uncompressed file size in bytes.</param>
    /// <param name="leaveOpen">
    /// When <see langword="false"/> (the default), disposing this stream also disposes the archive.
    /// </param>
    public ZarNodeStream(ZArchiveReader reader, uint node, long length, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _reader = reader;
        _node = node;
        Length = length;
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Transfers ownership of the archive to this stream. Call after the stream has been
    /// accepted by its final owner; a probing caller that created the stream with
    /// <c>leaveOpen: true</c> keeps the archive usable when the probe fails.
    /// </summary>
    public void TakeOwnership()
    {
        _leaveOpen = false;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length { get; }

    /// <inheritdoc />
    public override long Position
    {
        get => _position;
        set => _position = Math.Clamp(value, 0, Length);
    }

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc />
    public override int Read(Span<byte> buffer)
    {
        var remaining = Length - _position;
        if (remaining <= 0)
        {
            return 0;
        }

        var toRead = (int)Math.Min(buffer.Length, remaining);
        var read = (int)_reader.ReadFromFile(_node, (ulong)_position, buffer[..toRead]);
        _position += read;
        return read;
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };

        return _position;
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen)
        {
            _reader.Dispose();
        }

        base.Dispose(disposing);
    }
}
