using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the stream-backed raw image source.
/// </summary>
public class StreamRawImageSourceTests
{
    /// <summary>
    /// Verifies the constructor rejects a non-seekable stream.
    /// </summary>
    [Fact]
    public void Constructor_RejectsNonSeekableStream()
    {
        using var stream = new NonSeekableStream();

        Assert.Throws<ArgumentException>(() => new StreamRawImageSource(stream));
    }

    /// <summary>
    /// Verifies reads return the requested bytes at an offset across sector boundaries.
    /// </summary>
    [Fact]
    public void Read_ReturnsBytesAtOffset_AcrossSectorBoundaries()
    {
        var data = Enumerable.Range(0, 8192).Select(static i => (byte)(i % 251)).ToArray();
        using var source = new StreamRawImageSource(new MemoryStream(data));

        Assert.Equal(data.Length, source.Length);

        var buffer = new byte[5000];
        var read = source.Read(buffer, 2000);

        Assert.Equal(5000, read);
        Assert.Equal(data[2000..7000], buffer);
    }

    /// <summary>
    /// Verifies reads clamp to the image end and reject negative offsets.
    /// </summary>
    [Fact]
    public void Read_ClampsToImageEnd_AndRejectsOutOfRangeOffsets()
    {
        var data = new byte[10];
        using var source = new StreamRawImageSource(new MemoryStream(data));

        var buffer = new byte[32];
        Assert.Equal(4, source.Read(buffer, 6));
        Assert.Equal(0, source.Read(buffer, 10));
        Assert.Equal(0, source.Read(buffer, -1));
    }

    /// <summary>
    /// Verifies reads after disposal return zero bytes.
    /// </summary>
    [Fact]
    public void Read_AfterDispose_ReturnsZero()
    {
        var source = new StreamRawImageSource(new MemoryStream(new byte[16]));
        source.Dispose();

        Assert.Equal(0, source.Read(new byte[4], 0));
    }

    /// <summary>
    /// Verifies concurrent reads return correct data.
    /// </summary>
    [Fact]
    public void ConcurrentReads_ReturnCorrectData()
    {
        var data = Enumerable.Range(0, 4096).Select(static i => (byte)(i % 256)).ToArray();
        using var source = new StreamRawImageSource(new MemoryStream(data));

        var results = new byte[8][];
        Parallel.For(0, results.Length, i =>
        {
            var buffer = new byte[256];
            // ReSharper disable once AccessToDisposedClosure
            var read = source.Read(buffer, i * 256);
            Assert.Equal(256, read);
            results[i] = buffer;
        });

        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal(data[(i * 256)..((i + 1) * 256)], results[i]);
        }
    }

    /// <summary>
    /// Verifies a null stream is rejected.
    /// </summary>
    [Fact]
    public void Constructor_WithNullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StreamRawImageSource(null!));
    }

    /// <summary>
    /// Verifies an empty stream reports zero length and reads nothing.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyStream_ReportsZeroLength()
    {
        using var source = new StreamRawImageSource(new MemoryStream([]));

        Assert.Equal(0, source.Length);
        Assert.Equal(0, source.Read(new byte[8], 0));
    }

    /// <summary>
    /// Verifies an empty buffer reads nothing.
    /// </summary>
    [Fact]
    public void Read_WithEmptyBuffer_ReturnsZero()
    {
        using var source = new StreamRawImageSource(new MemoryStream(new byte[16]));

        Assert.Equal(0, source.Read([], 0));
    }

    /// <summary>
    /// Verifies stream failures degrade to a zero-byte read.
    /// </summary>
    [Fact]
    public void Read_WhenStreamThrows_ReturnsZero()
    {
        using var source = new StreamRawImageSource(new FaultyStream());

        Assert.Equal(0, source.Read(new byte[4], 0));
    }

    /// <summary>
    /// Verifies disposal disposes the underlying stream exactly once.
    /// </summary>
    [Fact]
    public void Dispose_DisposesStreamOnce()
    {
        var stream = new CountingStream(new byte[16]);
        var source = new StreamRawImageSource(stream);

        source.Dispose();
        source.Dispose();

        Assert.Equal(1, stream.DisposeCount);
    }

    /// <summary>
    /// Verifies a failing stream disposal is swallowed.
    /// </summary>
    [Fact]
    public void Dispose_DoesNotThrow_WhenStreamDisposeThrows()
    {
        var source = new StreamRawImageSource(new ThrowingDisposeStream(new byte[16]));

        source.Dispose();

        Assert.Equal(0, source.Read(new byte[4], 0));
    }

    /// <summary>
    /// A memory stream that counts disposal calls.
    /// </summary>
    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        /// <summary>
        /// Gets the number of <see cref="Dispose(bool)"/> calls.
        /// </summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A memory stream whose disposal throws.
    /// </summary>
    private sealed class ThrowingDisposeStream(byte[] data) : MemoryStream(data)
    {
        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            throw new IOException("stream dispose failure");
        }
    }

    /// <summary>
    /// A seekable stream whose seeks and reads fail.
    /// </summary>
    private sealed class FaultyStream : Stream
    {
        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => true;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => 16;

        /// <inheritdoc />
        public override long Position
        {
            get => 0;
            set => throw new IOException("seek failure");
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new IOException("read failure");
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new IOException("seek failure");
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// A read-only stream that does not support seeking, used to exercise the constructor guard.
    /// </summary>
    private sealed class NonSeekableStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return 0;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}