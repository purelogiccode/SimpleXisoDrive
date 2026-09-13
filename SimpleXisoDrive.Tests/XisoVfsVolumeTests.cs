using SimpleXisoDrive.Vfs;

namespace SimpleXisoDrive.Tests;

public class XisoVfsVolumeTests
{
    private static string WriteTempImage(byte[] image, string extension = ".iso")
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        File.WriteAllBytes(path, image);
        return path;
    }

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
            Assert.True((file.GetWindowsAttributes() & FileAttributes.Archive) != FileAttributes.None);

            var buffer = new byte[8];
            Assert.Equal(4, volume.ReadFile(file, buffer, offset: 9));
            Assert.Equal("data"u8.ToArray(), buffer[..4]);
        }
        finally
        {
            File.Delete(path);
        }
    }

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

    [Fact]
    public void StreamVolume_WithInvalidImage_ThrowsAndDisposesStream()
    {
        var stream = new MemoryStream(new byte[4096]);

        Assert.Throws<InvalidImageException>(() => new XisoVfsVolume(stream, "embedded.iso"));
        Assert.False(stream.CanRead);
    }

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
}
