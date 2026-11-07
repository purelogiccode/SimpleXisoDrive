using SimpleXisoDrive.Core;
using SimpleXisoDrive.Tests.Fixtures;
using SimpleXisoDrive.Tests.Models;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Mounts deterministic GOD/SVOD containers (a plaintext XContent header beside
/// a <c>.data</c> fragment directory) through <see cref="VfsContainer"/>.
/// </summary>
public sealed class GodVfsContainerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "simplexisodrive_god_" + Guid.NewGuid().ToString("N"));

    public GodVfsContainerTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup for tests.
        }
    }

    private byte[] CreateImage()
    {
        return TestImageFactory.CreateXdvdfsImage(
        [
            new TestImageEntry("default.xbe", "god payload"u8.ToArray()),
            new TestImageEntry("media/data.bin", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]),
            new TestImageEntry("empty.bin", []),
        ]);
    }

    private string CreateGod(out byte[] image, string titleName = "Synthetic GOD")
    {
        image = CreateImage();
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var isoPath = Path.Combine(sourceDirectory, "source.iso");
        File.WriteAllBytes(isoPath, image);

        var godDirectory = Directory.CreateDirectory(Path.Combine(_root, "god")).FullName;
        var headerPath = Path.Combine(godDirectory, "4D5307E6ABCDEF0123456789");
        SyntheticGodContainer.CreateFromIso(isoPath, headerPath, titleName);
        return headerPath;
    }

    [Fact]
    public void Constructor_WithGodHeader_MountsXdvdfsContents()
    {
        var header = CreateGod(out var image);

        using var vfs = new VfsContainer(header);

        Assert.Equal("XBOX_ISO", vfs.VolumeLabel);
        Assert.Equal("XDVDFS", vfs.FileSystemName);
        Assert.True(vfs.VolumeSize >= (ulong)image.Length);
        Assert.InRange(vfs.VolumeSize - (ulong)image.Length, 0UL, 4096UL);

        var root = vfs.GetEntry("\\");
        Assert.NotNull(root);
        Assert.True(root.IsDirectory);

        var file = vfs.GetEntry("\\default.xbe");
        Assert.NotNull(file);
        Assert.Equal("god payload"u8.Length, file.Size);

        var buffer = new byte[file.Size];
        Assert.Equal(buffer.Length, vfs.ReadFile(file, buffer, 0));
        Assert.Equal("god payload"u8.ToArray(), buffer);

        var nested = vfs.GetEntry("\\media\\data.bin");
        Assert.NotNull(nested);
        var nestedBuffer = new byte[4];
        Assert.Equal(4, vfs.ReadFile(nested, nestedBuffer, 3));
        Assert.Equal(new byte[] { 4, 5, 6, 7 }, nestedBuffer);
    }

    [Fact]
    public void Constructor_WithGodDataDirectory_MountsSameContents()
    {
        var header = CreateGod(out _);

        using var vfs = new VfsContainer(header + ".data");

        var file = vfs.GetEntry("\\default.xbe");
        Assert.NotNull(file);
        Assert.Equal("god payload"u8.Length, file.Size);
    }

    [Fact]
    public void Constructor_WithGodHeaderAndImageIso_ExposesDecompressedImage()
    {
        var header = CreateGod(out var image);

        using var vfs = new VfsContainer(header, exposeImageIso: true);

        var imageEntry = vfs.GetEntry("\\image.iso");
        Assert.NotNull(imageEntry);
        Assert.False(imageEntry.IsDirectory);
        // GOD stores an even number of sectors, so the logical image can carry
        // up to one 2048-byte sector of trailing zero padding.
        Assert.True(imageEntry.Size >= image.Length);
        Assert.True(imageEntry.Size <= image.Length + 4096);

        var buffer = new byte[image.Length];
        Assert.Equal(buffer.Length, vfs.ReadFile(imageEntry, buffer, 0));
        Assert.Equal(image, buffer);
    }

    [Fact]
    public void Constructor_WithIncompleteGod_ThrowsInvalidImageException()
    {
        var header = Path.Combine(_root, "incomplete_header");
        File.WriteAllBytes(header, new byte[0x4000]);

        Assert.Throws<InvalidImageException>(() => new VfsContainer(header));
    }

    [Fact]
    public void Resolve_WithGodDataDirectory_ReturnsHeader()
    {
        var header = CreateGod(out _);

        Assert.Equal(header, ImagePathResolver.Resolve(header + ".data"));
    }

    [Fact]
    public void Resolve_WithGodParentDirectory_ReturnsHeader()
    {
        var header = CreateGod(out _);

        Assert.Equal(header, ImagePathResolver.Resolve(Path.GetDirectoryName(header)!));
    }

    [Fact]
    public void Constructor_WithIsoRenamedToCso_StillMountsAsXdvdfs()
    {
        var image = CreateImage();
        var path = Path.Combine(_root, "renamed.cso");
        File.WriteAllBytes(path, image);

        using var vfs = new VfsContainer(path);

        Assert.Equal("XDVDFS", vfs.FileSystemName);
        Assert.NotNull(vfs.GetEntry("\\default.xbe"));
    }
}
