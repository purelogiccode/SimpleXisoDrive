using SimpleXisoDrive.Core.Interfaces;
using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Tests;

public class ImageIsoVfsVolumeTests
{
    private sealed class TrackingRawImageSource(byte[] data) : IRawImageSource
    {
        private readonly byte[] _data = data;
        public bool Disposed { get; private set; }

        public long Length => _data.Length;

        public int Read(Span<byte> buffer, long offset)
        {
            if (offset < 0 || offset >= _data.Length)
            {
                return 0;
            }

            var count = (int)Math.Min(buffer.Length, _data.Length - offset);
            _data.AsSpan((int)offset, count).CopyTo(buffer);
            return count;
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private static string CreateImageFile(byte[]? image = null, string fileName = "default.xbe")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, image ?? TestImageFactory.CreateMinimalXdvdfsImage(fileName: fileName));
        return path;
    }

    [Fact]
    public void GetFolderList_Root_IncludesVirtualImageIso()
    {
        var imagePath = CreateImageFile();
        try
        {
            var inner = new XisoVfsVolume(imagePath);
            var source = new TrackingRawImageSource([1, 2, 3, 4, 5, 6]);
            using var volume = new ImageIsoVfsVolume(inner, source);

            var children = volume.GetFolderList("\\").ToList();

            Assert.Contains(children, entry => string.Equals(entry.FileName, "image.iso", StringComparison.Ordinal));
            Assert.Contains(children, entry => string.Equals(entry.FileName, "default.xbe", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void GetEntry_ResolvesVirtualImageIso_CaseInsensitively()
    {
        var imagePath = CreateImageFile();
        try
        {
            var inner = new XisoVfsVolume(imagePath);
            var source = new TrackingRawImageSource(new byte[64]);
            using var volume = new ImageIsoVfsVolume(inner, source);

            var entry = volume.GetEntry(@"\IMAGE.ISO");

            Assert.NotNull(entry);
            Assert.False(entry.IsDirectory);
            Assert.Equal(64, entry.Size);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void ReadFile_ReturnsRawImageBytes_AtOffset()
    {
        var imagePath = CreateImageFile();
        try
        {
            var data = Enumerable.Range(0, 100).Select(static i => (byte)i).ToArray();
            var inner = new XisoVfsVolume(imagePath);
            var source = new TrackingRawImageSource(data);
            using var volume = new ImageIsoVfsVolume(inner, source);
            var entry = volume.GetEntry("\\image.iso");
            Assert.NotNull(entry);

            var buffer = new byte[10];
            var read = volume.ReadFile(entry, buffer, 25);

            Assert.Equal(10, read);
            Assert.Equal(data[25..35], buffer);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void ReadFile_ClampsToImageLength()
    {
        var imagePath = CreateImageFile();
        try
        {
            var inner = new XisoVfsVolume(imagePath);
            var source = new TrackingRawImageSource([1, 2, 3, 4]);
            using var volume = new ImageIsoVfsVolume(inner, source);
            var entry = volume.GetEntry("\\image.iso");
            Assert.NotNull(entry);

            var buffer = new byte[16];
            var read = volume.ReadFile(entry, buffer, 2);

            Assert.Equal(2, read);
            Assert.Equal([3, 4], buffer[..read]);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void InnerEntries_RemainReadable()
    {
        var imagePath = CreateImageFile(TestImageFactory.CreateMinimalXdvdfsImage("boot"u8.ToArray()));
        try
        {
            var inner = new XisoVfsVolume(imagePath);
            using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[8]));
            var entry = volume.GetEntry("\\default.xbe");
            Assert.NotNull(entry);

            var buffer = new byte[4];
            var read = volume.ReadFile(entry, buffer, 0);

            Assert.Equal(4, read);
            Assert.Equal("boot"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void RealImageIsoFile_WinsOverVirtualEntry()
    {
        var imagePath = CreateImageFile(TestImageFactory.CreateMinimalXdvdfsImage("real"u8.ToArray(), "image.iso"));
        try
        {
            var inner = new XisoVfsVolume(imagePath);
            using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[512]));

            var entry = volume.GetEntry("\\image.iso");
            Assert.NotNull(entry);
            Assert.Equal(4, entry.Size);

            var children = volume.GetFolderList("\\").ToList();
            Assert.Single(children,
                child => string.Equals(child.FileName, "image.iso", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public void Dispose_DisposesSourceAndInnerVolume()
    {
        var imagePath = CreateImageFile();

        try
        {
            var inner = new XisoVfsVolume(imagePath);
            var source = new TrackingRawImageSource([9, 8, 7]);
            var volume = new ImageIsoVfsVolume(inner, source);

            volume.Dispose();

            Assert.True(source.Disposed);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }
}