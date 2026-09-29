using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the <c>image.iso</c> volume decorator.
/// </summary>
public class ImageIsoVfsVolumeTests
{
    private static string CreateImageFile(byte[]? image = null, string fileName = "default.xbe")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, image ?? TestImageFactory.CreateMinimalXdvdfsImage(fileName: fileName));
        return path;
    }

    /// <summary>
    /// Verifies the root listing adds the virtual image.iso alongside the real tree.
    /// </summary>
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

    /// <summary>
    /// Verifies image.iso resolves case-insensitively to the synthetic entry.
    /// </summary>
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

    /// <summary>
    /// Verifies reading image.iso returns the raw image bytes at an offset.
    /// </summary>
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

    /// <summary>
    /// Verifies reads past the raw image end are clamped.
    /// </summary>
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

    /// <summary>
    /// Verifies wrapped entries remain readable through the decorator.
    /// </summary>
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

    /// <summary>
    /// Verifies a real image.iso file takes precedence over the synthetic one.
    /// </summary>
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

    /// <summary>
    /// Verifies disposal releases both the raw source and the wrapped volume.
    /// </summary>
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

            Assert.Equal(1, source.DisposeCount);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    /// <summary>
    /// Verifies a null inner volume is rejected.
    /// </summary>
    [Fact]
    public void Constructor_WithNullInner_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ImageIsoVfsVolume(null!, new TrackingRawImageSource([1])));
    }

    /// <summary>
    /// Verifies a null raw image source is rejected.
    /// </summary>
    [Fact]
    public void Constructor_WithNullSource_ThrowsArgumentNullException()
    {
        var imagePath = CreateImageFile();
        try
        {
            var inner = new XisoVfsVolume(imagePath);
            try
            {
                Assert.Throws<ArgumentNullException>(() => new ImageIsoVfsVolume(inner, null!));
            }
            finally
            {
                inner.Dispose();
            }
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    /// <summary>
    /// Verifies volume metadata is delegated to the wrapped volume.
    /// </summary>
    [Fact]
    public void Properties_DelegateToInnerVolume()
    {
        var inner = new FakeVfsVolume
        {
            VolumeSize = 100,
            VolumeCreationTime = new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc),
            VolumeLabel = "LABEL",
            FileSystemName = "FSNAME"
        };

        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[8]));

        Assert.Equal("LABEL", volume.VolumeLabel);
        Assert.Equal("FSNAME", volume.FileSystemName);
        Assert.Equal(inner.VolumeCreationTime, volume.VolumeCreationTime);
    }

    /// <summary>
    /// Verifies the raw image does not double-count the wrapped volume's size when both
    /// are backed by the same image (plain ISO/XISO/CISO, CHD or embedded XISO).
    /// </summary>
    [Fact]
    public void VolumeSize_ForSameImage_DoesNotDoubleCount()
    {
        var inner = new FakeVfsVolume { VolumeSize = 100 };
        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[100]));

        Assert.Equal(100ul, volume.VolumeSize);
    }

    /// <summary>
    /// Verifies a raw image that is additional content (a synthesized ZArchive image)
    /// still counts towards the reported volume size.
    /// </summary>
    [Fact]
    public void VolumeSize_ForAdditionalImage_AddsRawImageLength()
    {
        var inner = new FakeVfsVolume { VolumeSize = 100 };
        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[8]),
            rawImageIsAdditionalContent: true);

        Assert.Equal(108ul, volume.VolumeSize);
    }

    /// <summary>
    /// Verifies the decorated root listing is cached and returned as the same instance,
    /// matching the other volumes' listing-cache policy.
    /// </summary>
    [Fact]
    public void GetFolderList_Root_IsCached()
    {
        var inner = new FakeVfsVolume();
        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[8]));

        var first = volume.GetFolderList("\\");
        var second = volume.GetFolderList("\\");

        Assert.Same(first, second);
    }

    /// <summary>
    /// Verifies missing, non-image paths return null instead of the synthetic entry.
    /// </summary>
    [Fact]
    public void GetEntry_ForMissingNonImagePath_ReturnsNull()
    {
        var inner = new FakeVfsVolume();
        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[8]));

        Assert.Null(volume.GetEntry(@"\missing.bin"));
    }

    /// <summary>
    /// Verifies the synthetic entry carries the documented metadata.
    /// </summary>
    [Fact]
    public void GetEntry_ForImageIso_ReturnsReadOnlyNormalFile()
    {
        var inner = new FakeVfsVolume();
        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[64]));

        var entry = volume.GetEntry("/image.iso");

        Assert.NotNull(entry);
        Assert.Equal("image.iso", entry.FileName);
        Assert.False(entry.IsDirectory);
        Assert.Equal(64, entry.Size);
        Assert.Equal(FileAttributes.ReadOnly | FileAttributes.Normal, entry.GetWindowsAttributes());
    }

    /// <summary>
    /// Verifies only the root listing gains the synthetic entry.
    /// </summary>
    [Fact]
    public void GetFolderList_ForSubdirectory_DoesNotAddImageIso()
    {
        var inner = new FakeVfsVolume();
        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[8]));

        var children = volume.GetFolderList(@"\sub").ToList();

        var child = Assert.Single(children);
        Assert.Equal("entry.bin", child.FileName);
    }

    /// <summary>
    /// Verifies disposal is idempotent for the raw source and the wrapped volume.
    /// </summary>
    [Fact]
    public void Dispose_IsIdempotent()
    {
        var inner = new FakeVfsVolume();
        var source = new TrackingRawImageSource(new byte[8]);
        var volume = new ImageIsoVfsVolume(inner, source);

        volume.Dispose();
        volume.Dispose();

        Assert.Equal(1, source.DisposeCount);
        Assert.Equal(1, inner.DisposeCount);
    }

    /// <summary>
    /// Verifies inner read failures are logged and rethrown.
    /// </summary>
    [Fact]
    public void ReadFile_WhenInnerThrows_Rethrows()
    {
        var inner = new FakeVfsVolume { ThrowOnReadFile = true };
        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[8]));
        var entry = volume.GetEntry("\\");

        Assert.NotNull(entry);
        Assert.Throws<InvalidOperationException>(() => volume.ReadFile(entry, new byte[4], 0));
    }

    /// <summary>
    /// Verifies lookup failures are logged and rethrown.
    /// </summary>
    [Fact]
    public void GetEntry_WhenInnerThrows_Rethrows()
    {
        var inner = new FakeVfsVolume { ThrowOnGetEntry = true };
        using var volume = new ImageIsoVfsVolume(inner, new TrackingRawImageSource(new byte[8]));

        Assert.Throws<InvalidOperationException>(() => volume.GetEntry("\\"));
    }
}