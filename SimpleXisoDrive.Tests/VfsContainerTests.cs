using SimpleXisoDrive.Core;
using SimpleXisoDrive.Tests.Models;
using XISOSharp;
using ZArchiveSharp;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the <c>VfsContainer</c> facade over ISO, CHD and ZArchive images.
/// </summary>
public class VfsContainerTests
{
    private static string CreateZar(string extension, Action<ZArchiveWriter> build)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        using var stream = File.Create(path);
        using var writer = new ZArchiveWriter(stream);
        build(writer);
        writer.Finalize();

        return path;
    }

    /// <summary>
    /// Verifies a ZArchive tree mounts its archived contents.
    /// </summary>
    [Fact]
    public void Constructor_WithZarTree_MountsArchiveContents()
    {
        var path = CreateZar(".zar", writer =>
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("hello xbox"u8);
        });

        try
        {
            using var vfs = new VfsContainer(path);

            var root = vfs.GetEntry("\\");
            Assert.NotNull(root);
            Assert.True(root.IsDirectory);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("hello xbox"u8.Length, file.Size);

            var buffer = new byte[10];
            Assert.Equal(10, vfs.ReadFile(file, buffer, 0));
            Assert.Equal("hello xbox"u8.ToArray(), buffer);
            Assert.Equal("XBOX_ZAR", vfs.VolumeLabel);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies an embedded XISO mounts its XDVDFS contents.
    /// </summary>
    [Fact]
    public void Constructor_WithEmbeddedIso_MountsXdvdfsContents()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("mount me"u8.ToArray());
        var path = CreateZar(".zar", writer =>
        {
            Assert.True(writer.StartNewFile("game.iso"));
            writer.AppendData(image);
        });

        try
        {
            using var vfs = new VfsContainer(path);

            Assert.Equal("XBOX_ISO", vfs.VolumeLabel);
            Assert.Equal("XDVDFS", vfs.FileSystemName);
            Assert.Equal((ulong)image.Length, vfs.VolumeSize);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("mount me"u8.Length, file.Size);

            var buffer = new byte[8];
            Assert.Equal(8, vfs.ReadFile(file, buffer, 0));
            Assert.Equal("mount me"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a renamed ZArchive still mounts as an archive.
    /// </summary>
    [Fact]
    public void Constructor_WithRenamedZar_FallsBackToArchiveMount()
    {
        var path = CreateZar(".iso", writer =>
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("renamed"u8);
        });

        try
        {
            using var vfs = new VfsContainer(path);
            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal(7, file.Size);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies an invalid ZArchive throws with the failure reason.
    /// </summary>
    [Fact]
    public void Constructor_WithInvalidZar_ThrowsInvalidImageExceptionWithReason()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zar");
        File.WriteAllBytes(path, new byte[4096]);
        try
        {
            var ex = Assert.Throws<InvalidImageException>(() => new VfsContainer(path));
            // The ZArchiveSharp 1.3.0 failure code is surfaced in the message.
            Assert.Contains("BadMagic", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies an ISO file mounts as an XDVDFS image.
    /// </summary>
    [Fact]
    public void Constructor_WithIsoFile_MountsXdvdfsImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage("iso data"u8.ToArray()));
        try
        {
            using var vfs = new VfsContainer(path);
            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal(8, file.Size);
            Assert.Equal("XBOX_ISO", vfs.VolumeLabel);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a .xiso file mounts as an XDVDFS image.
    /// </summary>
    [Fact]
    public void Constructor_WithXisoExtension_MountsXdvdfsImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xiso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage("xiso data"u8.ToArray()));
        try
        {
            using var vfs = new VfsContainer(path);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("xiso data"u8.Length, file.Size);
            Assert.Equal("XDVDFS", vfs.FileSystemName);

            var buffer = new byte[4];
            Assert.Equal(4, vfs.ReadFile(file, buffer, 5));
            Assert.Equal("data"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies an embedded XISO with directories mounts stream-backed contents.
    /// </summary>
    [Fact]
    public void Constructor_WithEmbeddedNestedIso_MountsStreamBackedContents()
    {
        var image = TestImageFactory.CreateXdvdfsImage(
        [
            new TestImageEntry("default.xbe", "boot"u8.ToArray()),
            new TestImageEntry("media", null),
            new TestImageEntry("media/video.bin", "streamed blocks"u8.ToArray()),
        ]);

        var path = CreateZar(".zar", writer =>
        {
            Assert.True(writer.StartNewFile("game.iso"));
            writer.AppendData(image);
        });

        try
        {
            using var vfs = new VfsContainer(path);

            Assert.Equal((ulong)image.Length, vfs.VolumeSize);

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
    /// Verifies a locked ISO surfaces an I/O error.
    /// </summary>
    [Fact]
    public void Constructor_WithLockedIso_ThrowsIOException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            // Sharing violations must surface as I/O errors, not as invalid-image errors.
            Assert.Throws<IOException>(() => new VfsContainer(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a locked ZArchive surfaces an I/O error.
    /// </summary>
    [Fact]
    public void Constructor_WithLockedZar_ThrowsIOException()
    {
        var path = CreateZar(".zar", writer =>
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("hello xbox"u8);
        });

        try
        {
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            Assert.Throws<IOException>(() => new VfsContainer(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a missing ZArchive throws <c>FileNotFoundException</c>.
    /// </summary>
    [Fact]
    public void Constructor_WithMissingZar_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zar");

        Assert.Throws<FileNotFoundException>(() => new VfsContainer(path));
    }

    /// <summary>
    /// Verifies image.iso is absent unless requested.
    /// </summary>
    [Fact]
    public void Constructor_WithoutImageIsoOption_DoesNotExposeVirtualImageIso()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage());
        try
        {
            using var vfs = new VfsContainer(path);

            Assert.Null(vfs.GetEntry("\\image.iso"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies the raw image is exposed for a plain ISO.
    /// </summary>
    [Fact]
    public void Constructor_WithImageIsoOption_ExposesRawImageForPlainIso()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("raw data"u8.ToArray());
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, image);

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
    /// Verifies the decompressed CISO is exposed as image.iso.
    /// </summary>
    [Fact]
    public void Constructor_WithImageIsoOption_ExposesDecompressedCso()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("cso data"u8.ToArray());
        var isoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        var csoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cso");
        File.WriteAllBytes(isoPath, image);

        try
        {
            Assert.Equal(0, CisoWriter.CompressToCso(isoPath, csoPath));

            using var vfs = new VfsContainer(csoPath, exposeImageIso: true);

            var entry = vfs.GetEntry("\\image.iso");
            Assert.NotNull(entry);
            Assert.Equal(image.Length, entry.Size);

            var buffer = new byte[image.Length];
            Assert.Equal(image.Length, vfs.ReadFile(entry, buffer, 0));
            Assert.Equal(image, buffer);

            Assert.NotNull(vfs.GetEntry("\\default.xbe"));
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    /// <summary>
    /// Verifies the embedded XISO of a ZArchive is exposed as image.iso.
    /// </summary>
    [Fact]
    public void Constructor_WithImageIsoOption_ExposesEmbeddedIsoFromZar()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("embedded"u8.ToArray());
        var path = CreateZar(".zar", writer =>
        {
            Assert.True(writer.StartNewFile("game.iso"));
            writer.AppendData(image);
        });

        try
        {
            using var vfs = new VfsContainer(path, exposeImageIso: true);

            var entry = vfs.GetEntry("\\image.iso");
            Assert.NotNull(entry);
            Assert.Equal(image.Length, entry.Size);

            var buffer = new byte[image.Length];
            Assert.Equal(image.Length, vfs.ReadFile(entry, buffer, 0));
            Assert.Equal(image, buffer);

            Assert.NotNull(vfs.GetEntry("\\default.xbe"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a ZArchive tree is synthesized into a valid XISO exposed as image.iso.
    /// </summary>
    [Fact]
    public void Constructor_WithImageIsoOption_ServesSynthesizedXisoForZarTree()
    {
        var path = CreateZar(".zar", writer =>
        {
            Assert.True(writer.MakeDir("sub", recursive: true));
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("hello xbox"u8);
            Assert.True(writer.StartNewFile("sub/data.bin"));
            writer.AppendData("archived data"u8);
        });

        try
        {
            using var vfs = new VfsContainer(path, exposeImageIso: true);

            var entry = vfs.GetEntry("\\image.iso");
            Assert.NotNull(entry);
            Assert.True(entry.Size > 0);

            var buffer = new byte[(int)entry.Size];
            Assert.Equal(buffer.Length, vfs.ReadFile(entry, buffer, 0));

            using var imageStream = new MemoryStream(buffer);
            Assert.True(XisoReader.GetVolumeInfo(imageStream, "image.iso").IsValid);

            imageStream.Position = 0;
            var boot = XisoReader.GetEntryInfo(imageStream, "image.iso", "default.xbe");
            Assert.NotNull(boot);
            Assert.Equal((ulong)"hello xbox"u8.Length, boot.FileSize);

            imageStream.Position = 0;
            Assert.NotNull(XisoReader.GetEntryInfo(imageStream, "image.iso", "sub/data.bin"));

            // The normal tree view is still available.
            Assert.NotNull(vfs.GetEntry(@"\sub\data.bin"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a CISO file mounts its decompressed image.
    /// </summary>
    [Fact]
    public void Constructor_WithCsoFile_MountsDecompressedImage()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage("cso data"u8.ToArray());
        var isoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        var csoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cso");
        File.WriteAllBytes(isoPath, image);

        try
        {
            Assert.Equal(0, CisoWriter.CompressToCso(isoPath, csoPath));
            using var vfs = new VfsContainer(csoPath);

            Assert.Equal("XBOX_ISO", vfs.VolumeLabel);
            Assert.Equal((ulong)image.Length, vfs.VolumeSize);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);

            var buffer = new byte[8];
            Assert.Equal(8, vfs.ReadFile(file, buffer, 0));
            Assert.Equal("cso data"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(csoPath);
        }
    }

    /// <summary>
    /// Verifies uppercase image extensions are accepted.
    /// </summary>
    [Fact]
    public void Constructor_WithUppercaseExtensions_Mounts()
    {
        var isoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ISO");
        var zarPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ZAR");
        File.WriteAllBytes(isoPath, TestImageFactory.CreateMinimalXdvdfsImage("upper"u8.ToArray()));

        using (var stream = File.Create(zarPath))
        using (var writer = new ZArchiveWriter(stream))
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("upper zar"u8);
            writer.Finalize();
        }

        try
        {
            using var isoVfs = new VfsContainer(isoPath);
            Assert.Equal("upper"u8.Length, isoVfs.GetEntry("\\default.xbe")!.Size);

            using var zarVfs = new VfsContainer(zarPath);
            Assert.Equal("upper zar"u8.Length, zarVfs.GetEntry("\\default.xbe")!.Size);
        }
        finally
        {
            File.Delete(isoPath);
            File.Delete(zarPath);
        }
    }

    /// <summary>
    /// Verifies a missing ISO surfaces <c>FileNotFoundException</c>.
    /// </summary>
    [Fact]
    public void Constructor_WithMissingIso_ThrowsFileNotFoundException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");

        Assert.Throws<FileNotFoundException>(() => new VfsContainer(path));
    }

    /// <summary>
    /// Verifies an empty ISO file is rejected as an invalid image.
    /// </summary>
    [Fact]
    public void Constructor_WithEmptyIso_ThrowsInvalidImageException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, []);

        try
        {
            Assert.Throws<InvalidImageException>(() => new VfsContainer(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies the facade exposes the underlying volume metadata.
    /// </summary>
    [Fact]
    public void Properties_ExposeImageMetadata()
    {
        var image = TestImageFactory.CreateMinimalXdvdfsImage();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, image);

        try
        {
            using var vfs = new VfsContainer(path);

            Assert.Equal((ulong)image.Length, vfs.VolumeSize);
            Assert.Equal("XBOX_ISO", vfs.VolumeLabel);
            Assert.Equal("XDVDFS", vfs.FileSystemName);
            Assert.True((DateTime.Now - vfs.VolumeCreationTime).Duration() < TimeSpan.FromMinutes(5));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies entry lookup is case-insensitive.
    /// </summary>
    [Fact]
    public void GetEntry_IsCaseInsensitive()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var vfs = new VfsContainer(path);

            var entry = vfs.GetEntry("\\DEFAULT.XBE");
            Assert.NotNull(entry);
            Assert.Equal("default.xbe", entry.FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies the root listing is exposed through the facade.
    /// </summary>
    [Fact]
    public void GetFolderList_Root_ListsChildren()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var vfs = new VfsContainer(path);

            var child = Assert.Single(vfs.GetFolderList("\\"));
            Assert.Equal("default.xbe", child.FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies directory reads and negative offsets return zero through the facade.
    /// </summary>
    [Fact]
    public void ReadFile_DirectoryAndNegativeOffset_ReturnZero()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            using var vfs = new VfsContainer(path);

            var root = vfs.GetEntry("\\");
            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(root);
            Assert.NotNull(file);

            Assert.Equal(0, vfs.ReadFile(root, new byte[8], 0));
            Assert.Equal(0, vfs.ReadFile(file, new byte[8], -1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies disposing the facade twice is safe.
    /// </summary>
    [Fact]
    public void Dispose_IsIdempotent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllBytes(path, TestImageFactory.CreateMinimalXdvdfsImage());

        try
        {
            var vfs = new VfsContainer(path);

            vfs.Dispose();
            vfs.Dispose();

            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(exclusive.CanRead);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies a ZArchive with a single non-XISO file mounts its tree view.
    /// </summary>
    [Fact]
    public void Constructor_WithSingleNonIsoZarFile_MountsTreeView()
    {
        var path = CreateZar(".zar", writer =>
        {
            Assert.True(writer.StartNewFile("readme.txt"));
            writer.AppendData("just text"u8);
        });

        try
        {
            using var vfs = new VfsContainer(path);

            var file = vfs.GetEntry("\\readme.txt");
            Assert.NotNull(file);
            Assert.Equal("just text"u8.Length, file.Size);
            Assert.Equal("XBOX_ZAR", vfs.VolumeLabel);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies the single-file tree is synthesized into image.iso when requested.
    /// </summary>
    [Fact]
    public void Constructor_WithSingleNonIsoZarFile_AndImageIso_SynthesizesImage()
    {
        var path = CreateZar(".zar", writer =>
        {
            Assert.True(writer.StartNewFile("readme.txt"));
            writer.AppendData("just text"u8);
        });

        try
        {
            using var vfs = new VfsContainer(path, exposeImageIso: true);

            var entry = vfs.GetEntry("\\image.iso");
            Assert.NotNull(entry);
            Assert.True(entry.Size > 0);

            var buffer = new byte[(int)entry.Size];
            Assert.Equal(buffer.Length, vfs.ReadFile(entry, buffer, 0));

            using var imageStream = new MemoryStream(buffer);
            Assert.True(XisoReader.GetVolumeInfo(imageStream, "image.iso").IsValid);

            imageStream.Position = 0;
            var readme = XisoReader.GetEntryInfo(imageStream, "image.iso", "readme.txt");
            Assert.NotNull(readme);
            Assert.Equal((ulong)"just text"u8.Length, readme.FileSize);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Verifies null and empty paths are rejected up front.
    /// </summary>
    [Fact]
    public void Constructor_WithNullOrEmptyPath_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => new VfsContainer(null!));
        Assert.Throws<ArgumentException>(() => new VfsContainer(string.Empty));
    }

    /// <summary>
    /// Verifies an Xbox ISO renamed to .chd still mounts by content detection.
    /// </summary>
    [Fact]
    public void Constructor_WithIsoRenamedToChd_StillMountsAsXdvdfs()
    {
        var isoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".iso");
        File.WriteAllBytes(isoPath, TestImageFactory.CreateMinimalXdvdfsImage("renamed chd"u8.ToArray()));
        var renamedPath = Path.ChangeExtension(isoPath, ".chd");
        File.Move(isoPath, renamedPath);

        try
        {
            using var vfs = new VfsContainer(renamedPath);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("renamed chd"u8.Length, file.Size);
        }
        finally
        {
            File.Delete(renamedPath);
        }
    }

    /// <summary>
    /// Verifies an Xbox ISO renamed to .zar still mounts by content detection.
    /// </summary>
    [Fact]
    public void Constructor_WithIsoRenamedToZar_StillMountsAsXdvdfs()
    {
        var isoPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".iso");
        File.WriteAllBytes(isoPath, TestImageFactory.CreateMinimalXdvdfsImage("renamed zar"u8.ToArray()));
        var renamedPath = Path.ChangeExtension(isoPath, ".zar");
        File.Move(isoPath, renamedPath);

        try
        {
            using var vfs = new VfsContainer(renamedPath);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("renamed zar"u8.Length, file.Size);
        }
        finally
        {
            File.Delete(renamedPath);
        }
    }

    /// <summary>
    /// Verifies a ZArchive renamed to .chd still mounts as an archive by content detection.
    /// </summary>
    [Fact]
    public void Constructor_WithZarRenamedToChd_StillMountsAsArchive()
    {
        var zarPath = CreateZar(".zar", writer =>
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("zar as chd"u8);
        });
        var renamedPath = Path.ChangeExtension(zarPath, ".chd");
        File.Move(zarPath, renamedPath);

        try
        {
            using var vfs = new VfsContainer(renamedPath);

            var file = vfs.GetEntry("\\default.xbe");
            Assert.NotNull(file);
            Assert.Equal("zar as chd"u8.Length, file.Size);
        }
        finally
        {
            File.Delete(renamedPath);
        }
    }
}