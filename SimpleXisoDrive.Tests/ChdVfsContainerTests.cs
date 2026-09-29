using CHDSharp.Encoder;
using SimpleXisoDrive.Core;
using SimpleXisoDrive.Tests.Models;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests mounting Xbox ISO CHD files. Fixtures are encoded on the fly with the
/// CHDSharp encoder using the uncompressed codec so the tests stay fast and
/// dependency-free.
/// </summary>
public class ChdVfsContainerTests
{
    private static string CreateChd(byte[] image, uint codec = CodecTags.None)
    {
        var isoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        var chdPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.chd");
        File.WriteAllBytes(isoPath, image);

        try
        {
            ChdEncoder.EncodeRaw(isoPath, chdPath, codecTags: [codec]);
        }
        finally
        {
            File.Delete(isoPath);
        }

        return chdPath;
    }

    /// <summary>
    /// Verifies a CHD file mounts its decompressed XDVDFS image.
    /// </summary>
    [Fact]
    public void Constructor_WithChdFile_MountsXdvdfsImage()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("chd data"u8.ToArray());
        var path = CreateChd(image);

        try
        {
            using var vfs = new VfsContainer(path);

            Assert.Equal("XBOX_ISO", vfs.VolumeLabel);
            Assert.Equal("XDVDFS", vfs.FileSystemName);
            Assert.Equal((ulong)image.Length, vfs.VolumeSize);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("chd data"u8.Length, file.Size);

            var buffer = new byte[8];
            Assert.Equal(8, vfs.ReadFile(file, buffer, 0));
            Assert.Equal("chd data"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a standard-layout CHD image mounts.
    /// </summary>
    [Fact]
    public void Constructor_WithStandardLayoutChd_MountsXdvdfsImage()
    {
        var image = TestImageFactory.CreateStandardXdvdfsImage("standard"u8.ToArray());
        var path = CreateChd(image);

        try
        {
            using var vfs = new VfsContainer(path);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("standard"u8.Length, file.Size);

            var buffer = new byte[8];
            Assert.Equal(8, vfs.ReadFile(file, buffer, 0));
            Assert.Equal("standard"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies directories and files mount from a compressed CHD.
    /// </summary>
    [Fact]
    public void Constructor_WithChdTree_MountsDirectoriesAndFiles()
    {
        var image = TestImageFactory.CreateXdvdfsImage(
        [
            new TestImageEntry("default.xbe", "boot"u8.ToArray()),
            new TestImageEntry("media", null),
            new TestImageEntry("media/video.bin", "streamed blocks"u8.ToArray()),
        ]);
        var path = CreateChd(image, CodecTags.Zlib);

        try
        {
            using var vfs = new VfsContainer(path);

            var directory = vfs.GetEntry("\\media");
            Assert.NotNull(directory);
            Assert.True(directory.IsDirectory);
            Assert.Single(vfs.GetFolderList("\\media"));

            var file = vfs.GetEntry(@"\media\video.bin");
            Assert.NotNull(file);
            Assert.Equal("streamed blocks"u8.Length, file.Size);

            var buffer = new byte[6];
            Assert.Equal(6, vfs.ReadFile(file, buffer, 9));
            Assert.Equal("blocks"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies the decompressed CHD is exposed as image.iso.
    /// </summary>
    [Fact]
    public void Constructor_WithImageIsoOption_ExposesDecompressedChd()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("raw chd"u8.ToArray());
        var path = CreateChd(image);

        try
        {
            using var vfs = new VfsContainer(path, exposeImageIso: true);

            var entry = vfs.GetEntry("\\image.iso");
            Assert.NotNull(entry);
            Assert.False(entry.IsDirectory);
            Assert.Equal(image.Length, entry.Size);

            var buffer = new byte[image.Length];
            Assert.Equal(image.Length, vfs.ReadFile(entry, buffer, 0));
            Assert.Equal(image, buffer);

            // The normal tree view is still available.
            Assert.NotNull(vfs.GetEntry("\\default.xbe"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a non-Xbox CHD throws <c>InvalidImageException</c>.
    /// </summary>
    [Fact]
    public void Constructor_WithNonXboxChd_ThrowsInvalidImageException()
    {
        var path = CreateChd(new byte[64 * 1024]);

        try
        {
            var ex = Assert.Throws<InvalidImageException>(() => new VfsContainer(path));
            Assert.Contains("Xbox ISO CHD", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a renamed CHD still mounts via content detection.
    /// </summary>
    [Fact]
    public void Constructor_WithRenamedChd_FallsBackToChdMount()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("renamed"u8.ToArray());
        var chdPath = CreateChd(image);
        var renamedPath = Path.ChangeExtension(chdPath, ".iso");
        File.Move(chdPath, renamedPath);

        try
        {
            using var vfs = new VfsContainer(renamedPath);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("renamed"u8.Length, file.Size);
        }
        finally
        {
            File.Delete(renamedPath);
        }
    }

    /// <summary>
    /// Verifies a missing CHD throws <c>FileNotFoundException</c>.
    /// </summary>
    [Fact]
    public void Constructor_WithMissingChd_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.chd");

        Assert.Throws<FileNotFoundException>(() => new VfsContainer(path));
    }

    /// <summary>
    /// Verifies an uppercase .CHD extension is accepted.
    /// </summary>
    [Fact]
    public void Constructor_WithUppercaseChdExtension_Mounts()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("upper"u8.ToArray());
        var chdPath = CreateChd(image);
        var upperPath = Path.ChangeExtension(chdPath, ".CHD");
        File.Move(chdPath, upperPath);

        try
        {
            using var vfs = new VfsContainer(upperPath);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("upper"u8.Length, file.Size);
        }
        finally
        {
            File.Delete(upperPath);
        }
    }

    /// <summary>
    /// Verifies a compressed CHD exposes its decompressed image as image.iso.
    /// </summary>
    [Fact]
    public void Constructor_WithZlibChdAndImageIso_ExposesDecompressedImage()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("zlib raw"u8.ToArray());
        var path = CreateChd(image, CodecTags.Zlib);

        try
        {
            using var vfs = new VfsContainer(path, exposeImageIso: true);

            var entry = vfs.GetEntry("\\image.iso");
            Assert.NotNull(entry);
            Assert.Equal(image.Length, entry.Size);

            var buffer = new byte[image.Length];
            Assert.Equal(image.Length, vfs.ReadFile(entry, buffer, 0));
            Assert.Equal(image, buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a CD CHD is rejected with a CD-specific message.
    /// </summary>
    [Fact]
    public void Constructor_WithCdChd_ThrowsInvalidImageException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var binPath = Path.Combine(tempDir, "track01.bin");
        var cuePath = Path.Combine(tempDir, "game.cue");
        var chdPath = Path.Combine(tempDir, "game.chd");
        File.WriteAllBytes(binPath, new byte[2352 * 4]);
        File.WriteAllText(cuePath,
            "FILE \"track01.bin\" BINARY" + Environment.NewLine +
            "  TRACK 01 MODE1/2352" + Environment.NewLine +
            "    INDEX 01 00:00:00" + Environment.NewLine);

        try
        {
            ChdEncoder.EncodeCd(cuePath, chdPath, codecTags: [CodecTags.None]);

            var ex = Assert.Throws<InvalidImageException>(() => new VfsContainer(chdPath));
            Assert.Contains("CD CHD", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
