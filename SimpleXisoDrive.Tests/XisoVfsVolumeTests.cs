using SimpleXisoDrive.Core;
using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests constructing and reading <c>XisoVfsVolume</c> from files and embedded streams.
/// </summary>
public class XisoVfsVolumeTests
{
    private static string WriteTempImage(byte[] image, string extension = ".iso")
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        File.WriteAllBytes(path, image);
        return path;
    }

    /// <summary>
    /// Verifies a standard-layout image lists and reads entries.
    /// </summary>
    [Fact]
    public void PathVolume_WithStandardImage_ListsAndReadsEntries()
    {
        var image = TestImageFactory.CreateStandardXdvdfsImage("standard data"u8.ToArray());
        var path = WriteTempImage(image);

        try
        {
            using var volume = new XisoVfsVolume(path);

            Assert.Equal((ulong)image.Length, volume.VolumeSize);
            Assert.Equal("XBOX_ISO", volume.VolumeLabel);
            Assert.Equal("XDVDFS", volume.FileSystemName);

            var root = volume.GetEntry("\\");
            Assert.NotNull(root);
            Assert.True(root.IsDirectory);
            Assert.Equal(FileAttributes.ReadOnly | FileAttributes.Directory, root.GetWindowsAttributes());

            var children = volume.GetFolderList("\\").ToList();
            var child = Assert.Single(children);
            Assert.Equal("default.xbe", child.FileName);

            var file = volume.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("standard data"u8.Length, file.Size);
            Assert.NotEqual(FileAttributes.None, file.GetWindowsAttributes() & FileAttributes.Archive);

            var buffer = new byte[8];
            Assert.Equal(4, volume.ReadFile(file, buffer, offset: 9));
            Assert.Equal("data"u8.ToArray(), buffer[..4]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a rebuilt-layout image lists and reads entries.
    /// </summary>
    [Fact]
    public void PathVolume_WithRebuiltImage_ListsAndReadsEntries()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("rebuilt data"u8.ToArray());
        var path = WriteTempImage(image);

        try
        {
            using var volume = new XisoVfsVolume(path);

            var file = volume.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("rebuilt data"u8.Length, file.Size);

            var buffer = new byte[7];
            Assert.Equal(7, volume.ReadFile(file, buffer, offset: 0));
            Assert.Equal("rebuilt"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies the creation time comes from the volume descriptor.
    /// </summary>
    [Fact]
    public void PathVolume_VolumeCreationTime_ComesFromDescriptor()
    {
        var path = WriteTempImage(TestImageFactory.CreateStandardXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);
            Assert.True((DateTime.Now - volume.VolumeCreationTime).Duration() < TimeSpan.FromMinutes(5));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies non-ISO content throws <c>InvalidImageException</c>.
    /// </summary>
    [Fact]
    public void PathVolume_WithNonIsoImage_ThrowsInvalidImageException()
    {
        var path = WriteTempImage(new byte[4096]);

        try
        {
            Assert.Throws<InvalidImageException>(() => new XisoVfsVolume(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies an embedded image stream lists and reads entries.
    /// </summary>
    [Fact]
    public void StreamVolume_WithEmbeddedImage_ListsAndReadsEntries()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("stream data"u8.ToArray());

        using var stream = new MemoryStream(image);
        using var volume = new XisoVfsVolume(stream, "embedded.iso");

        Assert.Equal((ulong)image.Length, volume.VolumeSize);

        var children = volume.GetFolderList("\\").ToList();
        var child = Assert.Single(children);
        Assert.Equal("default.xbe", child.FileName);

        var file = volume.GetEntry("\\default.xbe");
        Assert.NotNull(file);
        Assert.Equal("stream data"u8.Length, file.Size);

        var buffer = new byte[6];
        Assert.Equal(4, volume.ReadFile(file, buffer, offset: 7));
        Assert.Equal("data"u8.ToArray(), buffer[..4]);

        Assert.Equal(0, volume.ReadFile(file, buffer, offset: file.Size));
    }

    /// <summary>
    /// Verifies an invalid embedded image throws and disposes the stream.
    /// </summary>
    [Fact]
    public void StreamVolume_WithInvalidImage_ThrowsAndDisposesStream()
    {
        var stream = new MemoryStream(new byte[4096]);

        Assert.Throws<InvalidImageException>(() => new XisoVfsVolume(stream, "embedded.iso"));
        Assert.False(stream.CanRead);
    }

    /// <summary>
    /// Verifies disposal releases the image file handle.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesImageHandle()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            var volume = new XisoVfsVolume(path);
            volume.Dispose();

            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(exclusive.CanRead);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies missing paths return null entries and empty listings.
    /// </summary>
    [Fact]
    public void GetEntry_And_GetFolderList_ForMissingPaths_AreEmptyOrNull()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);

            Assert.Null(volume.GetEntry("\\missing.xbe"));
            Assert.Empty(volume.GetFolderList("\\missing"));
            Assert.Empty(volume.GetFolderList("\\default.xbe"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies empty and separator-only paths resolve to the root entry.
    /// </summary>
    [Fact]
    public void GetEntry_PathVariants_ReturnRoot()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);

            foreach (var candidate in new[] { string.Empty, "/", "\\", "///" })
            {
                var root = volume.GetEntry(candidate);
                Assert.NotNull(root);
                Assert.True(root.IsDirectory);
                Assert.Equal(string.Empty, root.FileName);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies reading a directory entry returns zero bytes.
    /// </summary>
    [Fact]
    public void ReadFile_DirectoryEntry_ReturnsZero()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);
            var root = volume.GetEntry("\\");
            Assert.NotNull(root);

            Assert.Equal(0, volume.ReadFile(root, new byte[16], 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies negative offsets and empty buffers read nothing.
    /// </summary>
    [Fact]
    public void ReadFile_NegativeOffsetAndEmptyBuffer_ReturnZero()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);
            var file = volume.GetEntry("\\default.xbe");
            Assert.NotNull(file);

            Assert.Equal(0, volume.ReadFile(file, new byte[16], -1));
            Assert.Equal(0, volume.ReadFile(file, [], 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies entries created by other implementations are ignored.
    /// </summary>
    [Fact]
    public void ReadFile_ForeignEntry_ReturnsZero()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);

            Assert.Equal(0, volume.ReadFile(new FakeVfsEntry("foreign.bin", false, 4), new byte[4], 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies lookups after disposal degrade to null and empty results.
    /// </summary>
    [Fact]
    public void Lookups_AfterDispose_ReturnNullAndEmpty()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());
        var volume = new XisoVfsVolume(path);
        volume.Dispose();

        try
        {
            Assert.Null(volume.GetEntry("\\default.xbe"));
            Assert.Empty(volume.GetFolderList("\\"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies directory listings are cached and returned as the same instance.
    /// </summary>
    [Fact]
    public void GetFolderList_IsCached()
    {
        var path = WriteTempImage(TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var volume = new XisoVfsVolume(path);

            var first = volume.GetFolderList("\\");
            var second = volume.GetFolderList("\\");

            Assert.Same(first, second);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a null stream is rejected by the embedded-image constructor.
    /// </summary>
    [Fact]
    public void StreamVolume_WithNullStream_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new XisoVfsVolume(null!, "embedded.iso"));
    }

    /// <summary>
    /// Verifies a missing image file surfaces a <c>FileNotFoundException</c>.
    /// </summary>
    [Fact]
    public void PathVolume_WithMissingFile_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");

        Assert.Throws<FileNotFoundException>(() => new XisoVfsVolume(path));
    }

    /// <summary>
    /// Verifies stream-backed volumes report the same metadata as path-based ones.
    /// </summary>
    [Fact]
    public void StreamVolume_ReportsStandardMetadata()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage();
        using var stream = new MemoryStream(image);
        using var volume = new XisoVfsVolume(stream, "embedded.iso");

        Assert.Equal((ulong)image.Length, volume.VolumeSize);
        Assert.Equal("XBOX_ISO", volume.VolumeLabel);
        Assert.Equal("XDVDFS", volume.FileSystemName);
        Assert.True((DateTime.Now - volume.VolumeCreationTime).Duration() < TimeSpan.FromMinutes(5));
    }
}