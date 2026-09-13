using SimpleXisoDrive.Vfs;
using ZArchiveSharp;

namespace SimpleXisoDrive.Tests;

public class ZarVfsVolumeTests
{
    private static string CreateArchive(Action<ZArchiveWriter> build)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zar");
        using var stream = File.Create(path);
        using var writer = new ZArchiveWriter(stream);
        build(writer);
        writer.Finalize();

        return path;
    }

    private static string CreateSampleArchive()
    {
        return CreateArchive(writer =>
        {
            Assert.True(writer.MakeDir("sub", recursive: true));
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("hello xbox"u8);
            Assert.True(writer.StartNewFile("sub/data.bin"));
            writer.AppendData("archived data"u8);
        });
    }

    [Fact]
    public void Constructor_ThrowsInvalidImageException_ForNonZarFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zar");
        File.WriteAllText(path, "not an archive");
        try
        {
            Assert.Throws<InvalidImageException>(() => new ZarVfsVolume(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetFolderList_RootListsFilesAndDirectories()
    {
        var path = CreateSampleArchive();
        try
        {
            using var volume = new ZarVfsVolume(path);
            var children = volume.GetFolderList("\\").ToList();

            Assert.Equal(2, children.Count);
            Assert.Contains(children,
                entry => string.Equals(entry.FileName, "default.xbe", StringComparison.Ordinal) && !entry.IsDirectory);
            Assert.Contains(children,
                entry => string.Equals(entry.FileName, "sub", StringComparison.Ordinal) && entry.IsDirectory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetEntry_ResolvesNestedPath_CaseInsensitively()
    {
        var path = CreateSampleArchive();
        try
        {
            using var volume = new ZarVfsVolume(path);
            var entry = volume.GetEntry("/SUB/DATA.BIN");

            Assert.NotNull(entry);
            Assert.Equal("data.bin", entry.FileName);
            Assert.False(entry.IsDirectory);
            Assert.Equal("archived data"u8.Length, entry.Size);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetEntry_ReturnsNull_ForMissingPath()
    {
        var path = CreateSampleArchive();
        try
        {
            using var volume = new ZarVfsVolume(path);
            Assert.Null(volume.GetEntry(@"\sub\missing.bin"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFile_ReturnsFileContents_AtOffset()
    {
        var path = CreateSampleArchive();
        try
        {
            using var volume = new ZarVfsVolume(path);
            var entry = volume.GetEntry("\\default.xbe");
            Assert.NotNull(entry);

            var buffer = new byte[5];
            var read = volume.ReadFile(entry, buffer, 6);

            Assert.Equal(4, read);
            Assert.Equal("xbox"u8.ToArray(), buffer[..read]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFile_ReturnsZero_ForDirectories()
    {
        var path = CreateSampleArchive();
        try
        {
            using var volume = new ZarVfsVolume(path);
            var entry = volume.GetEntry("\\sub");
            Assert.NotNull(entry);

            var buffer = new byte[16];
            Assert.Equal(0, volume.ReadFile(entry, buffer, 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VolumeSize_SumsUncompressedFileSizes()
    {
        var path = CreateSampleArchive();
        try
        {
            using var volume = new ZarVfsVolume(path);
            Assert.Equal((ulong)("hello xbox"u8.Length + "archived data"u8.Length), volume.VolumeSize);
            Assert.Equal("XBOX_ZAR", volume.VolumeLabel);
            Assert.Equal("ZARCHIVE", volume.FileSystemName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFile_SpansMultipleBlocks()
    {
        var data = new byte[150_000];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        var path = CreateArchive(writer =>
        {
            Assert.True(writer.StartNewFile("big.bin"));
            writer.AppendData(data);
        });

        try
        {
            using var volume = new ZarVfsVolume(path);
            var entry = volume.GetEntry("\\big.bin");
            Assert.NotNull(entry);
            Assert.Equal(data.Length, entry.Size);

            // Start just before the first 64 KiB boundary and read across it.
            const int offset = 65_500;
            const int count = 2000;
            var buffer = new byte[count];
            var read = volume.ReadFile(entry, buffer, offset);

            Assert.Equal(count, read);
            Assert.Equal(data.Skip(offset).Take(count).ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetFolderList_ReturnsEmpty_ForFilesAndMissingPaths()
    {
        var path = CreateSampleArchive();
        try
        {
            using var volume = new ZarVfsVolume(path);
            Assert.Empty(volume.GetFolderList("\\default.xbe"));
            Assert.Empty(volume.GetFolderList("\\does-not-exist"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
