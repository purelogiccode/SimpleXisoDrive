using SimpleXisoDrive.Core;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the <c>VfsContainer</c> facade's argument validation, disposal and missing-path
/// behavior against a real minimal image.
/// </summary>
public class VfsContainerLifecycleTests
{
    private static string WriteMinimalImage()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".iso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage());
        return path;
    }

    /// <summary>
    /// Verifies a null image path is rejected.
    /// </summary>
    [Fact]
    public void Constructor_WithNullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new VfsContainer(null!));
    }

    /// <summary>
    /// Verifies an empty image path is rejected.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new VfsContainer(string.Empty));
    }

    /// <summary>
    /// Verifies disposing the container twice is safe.
    /// </summary>
    [Fact]
    public void Dispose_IsIdempotent()
    {
        var path = WriteMinimalImage();
        try
        {
            var container = new VfsContainer(path);

            container.Dispose();
            container.Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies disposal releases the image file handle for exclusive access.
    /// </summary>
    [Fact]
    public void Dispose_ReleasesImageHandle()
    {
        var path = WriteMinimalImage();
        try
        {
            var container = new VfsContainer(path);
            container.Dispose();

            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(exclusive.CanRead);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies missing paths resolve to null entries and empty listings.
    /// </summary>
    [Fact]
    public void GetEntry_And_GetFolderList_ForMissingPaths()
    {
        var path = WriteMinimalImage();
        try
        {
            using var container = new VfsContainer(path);

            Assert.Null(container.GetEntry("\\missing.xbe"));
            Assert.Empty(container.GetFolderList("\\missing"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies the container delegates the volume metadata to the underlying volume.
    /// </summary>
    [Fact]
    public void Metadata_DelegatesToUnderlyingVolume()
    {
        var path = WriteMinimalImage();
        try
        {
            using var container = new VfsContainer(path);

            Assert.Equal("XBOX_ISO", container.VolumeLabel);
            Assert.Equal("XDVDFS", container.FileSystemName);
            Assert.True(container.VolumeSize > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
