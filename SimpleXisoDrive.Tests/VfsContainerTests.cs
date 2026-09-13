using ZArchiveSharp;

namespace SimpleXisoDrive.Tests;

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
}
