using SimpleXisoDrive.Vfs;

namespace SimpleXisoDrive.Tests;

public class StreamRawImageSourceTests
{
    [Fact]
    public void Constructor_RejectsNonSeekableStream()
    {
        using var stream = new NonSeekableStream();

        Assert.Throws<ArgumentException>(() => new StreamRawImageSource(stream));
    }

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

    [Fact]
    public void Read_AfterDispose_ReturnsZero()
    {
        var source = new StreamRawImageSource(new MemoryStream(new byte[16]));
        source.Dispose();

        Assert.Equal(0, source.Read(new byte[4], 0));
    }

    [Fact]
    public void ConcurrentReads_ReturnCorrectData()
    {
        var data = Enumerable.Range(0, 4096).Select(static i => (byte)(i % 256)).ToArray();
        using var source = new StreamRawImageSource(new MemoryStream(data));

        var results = new byte[8][];
        Parallel.For(0, results.Length, i =>
        {
            var buffer = new byte[256];
            var read = source.Read(buffer, i * 256);
            Assert.Equal(256, read);
            results[i] = buffer;
        });

        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal(data[(i * 256)..((i + 1) * 256)], results[i]);
        }
    }

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

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
