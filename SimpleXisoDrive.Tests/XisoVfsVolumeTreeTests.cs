using SimpleXisoDrive.Vfs;

namespace SimpleXisoDrive.Tests;

public class XisoVfsVolumeTreeTests
{
    private static readonly TestImageEntry[] SampleEntries =
    [
        new("default.xbe", "root file"u8.ToArray()),
        new("readme.txt", "hello"u8.ToArray()),
        new("sub", null),
        new("sub/data.bin", "nested data"u8.ToArray()),
        new("sub/deep", null),
        new("sub/deep/note.txt", "deep note"u8.ToArray()),
        new("empty", null),
        new("empty.bin", []),
        new("big.bin", CreatePattern(5000)),
    ];

    private static byte[] CreatePattern(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }

        return data;
    }

    private static string WriteImage(IReadOnlyList<TestImageEntry> entries, long? fileTimeUtc = null,
        int headerSector = 0)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".iso");
        File.WriteAllBytes(path, TestImageFactory.CreateXdvdfsImage(entries, headerSector, fileTimeUtc));
        return path;
    }

    private static bool HasName(IVfsEntry entry, string name) =>
        string.Equals(entry.FileName, name, StringComparison.Ordinal);

    [Fact]
    public void PathVolume_ListsRootEntries()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            using var volume = new XisoVfsVolume(path);
            var children = volume.GetFolderList("\\").ToList();

            Assert.Equal(6, children.Count);
            Assert.Contains(children, entry => HasName(entry, "default.xbe") && !entry.IsDirectory);
            Assert.Contains(children, entry => HasName(entry, "readme.txt") && !entry.IsDirectory);
            Assert.Contains(children, entry => HasName(entry, "sub") && entry.IsDirectory);
            Assert.Contains(children, entry => HasName(entry, "empty") && entry.IsDirectory);
            Assert.Contains(children, entry => HasName(entry, "empty.bin") && !entry.IsDirectory);
            Assert.Contains(children, entry => HasName(entry, "big.bin") && !entry.IsDirectory);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_ListsNestedDirectories()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            using var volume = new XisoVfsVolume(path);

            var subChildren = volume.GetFolderList("\\sub").ToList();
            Assert.Equal(2, subChildren.Count);
            Assert.Contains(subChildren, entry => HasName(entry, "data.bin") && !entry.IsDirectory);
            Assert.Contains(subChildren, entry => HasName(entry, "deep") && entry.IsDirectory);

            var deepChildren = volume.GetFolderList(@"\sub\deep").ToList();
            var note = Assert.Single(deepChildren);
            Assert.Equal("note.txt", note.FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_ResolvesNestedPaths_CaseAndSeparatorInsensitively()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            using var volume = new XisoVfsVolume(path);

            foreach (var candidate in new[]
                     {
                         @"\sub\deep\note.txt", "/SUB/DEEP/NOTE.TXT", "sub/deep/note.txt", @"\Sub\Deep\Note.TXT"
                     })
            {
                var entry = volume.GetEntry(candidate);
                Assert.NotNull(entry);
                Assert.Equal("note.txt", entry.FileName);
                Assert.Equal("deep note"u8.Length, entry.Size);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_EmptyDirectory_ListsNothing()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            using var volume = new XisoVfsVolume(path);

            var entry = volume.GetEntry("\\empty");
            Assert.NotNull(entry);
            Assert.True(entry.IsDirectory);
            Assert.Empty(volume.GetFolderList("\\empty"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_EmptyFile_HasZeroSizeAndReadsNothing()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            using var volume = new XisoVfsVolume(path);

            var entry = volume.GetEntry("\\empty.bin");
            Assert.NotNull(entry);
            Assert.False(entry.IsDirectory);
            Assert.Equal(0, entry.Size);

            var buffer = new byte[16];
            Assert.Equal(0, volume.ReadFile(entry, buffer, 0));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_ReadFile_CrossesSectorBoundary()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            using var volume = new XisoVfsVolume(path);
            var entry = volume.GetEntry("\\big.bin");
            Assert.NotNull(entry);

            var expected = CreatePattern(5000);
            const int offset = 2040;
            var buffer = new byte[100];
            var read = volume.ReadFile(entry, buffer, offset);

            Assert.Equal(100, read);
            Assert.Equal(expected.AsSpan(offset, 100).ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_ReadFile_ClampsToFileSize()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            using var volume = new XisoVfsVolume(path);
            var entry = volume.GetEntry("\\big.bin");
            Assert.NotNull(entry);

            var buffer = new byte[100];
            var read = volume.ReadFile(entry, buffer, entry.Size - 10);

            Assert.Equal(10, read);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_StandardLayout_ResolvesNestedEntries()
    {
        var path = WriteImage(SampleEntries, headerSector: 32);
        try
        {
            using var volume = new XisoVfsVolume(path);

            var root = volume.GetEntry("\\");
            Assert.NotNull(root);
            Assert.True(root.IsDirectory);
            Assert.Equal(6, volume.GetFolderList("\\").Count());

            var entry = volume.GetEntry(@"\sub\data.bin");
            Assert.NotNull(entry);

            var buffer = new byte[11];
            Assert.Equal(11, volume.ReadFile(entry, buffer, 0));
            Assert.Equal("nested data"u8.ToArray(), buffer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_Attributes_MapXdvdfsFlags()
    {
        TestImageEntry[] entries =
        [
            new("archive.bin", [1], 0x20),
            new("hidden.bin", [1], 0x02),
            new("system.bin", [1], 0x04),
            new("hidden-system.bin", [1], 0x06),
            new("plain.bin", [1], 0x00),
        ];

        var path = WriteImage(entries);
        try
        {
            using var volume = new XisoVfsVolume(path);

            Assert.Equal(FileAttributes.ReadOnly | FileAttributes.Archive,
                volume.GetEntry("\\archive.bin")!.GetWindowsAttributes());
            Assert.Equal(FileAttributes.ReadOnly | FileAttributes.Hidden,
                volume.GetEntry("\\hidden.bin")!.GetWindowsAttributes());
            Assert.Equal(FileAttributes.ReadOnly | FileAttributes.System,
                volume.GetEntry("\\system.bin")!.GetWindowsAttributes());
            Assert.Equal(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System,
                volume.GetEntry("\\hidden-system.bin")!.GetWindowsAttributes());
            Assert.Equal(FileAttributes.ReadOnly | FileAttributes.Normal,
                volume.GetEntry("\\plain.bin")!.GetWindowsAttributes());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_VolumeCreationTime_UsesDescriptorValue()
    {
        var fileTimeUtc = DateTime.UtcNow.ToFileTimeUtc();
        var path = WriteImage(SampleEntries, fileTimeUtc);
        try
        {
            using var volume = new XisoVfsVolume(path);
            Assert.Equal(DateTime.FromFileTime(fileTimeUtc), volume.VolumeCreationTime);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PathVolume_VolumeCreationTime_ZeroFileTimeMapsToEpoch()
    {
        var path = WriteImage(SampleEntries, fileTimeUtc: 0);
        try
        {
            using var volume = new XisoVfsVolume(path);
            Assert.Equal(DateTime.FromFileTime(0), volume.VolumeCreationTime);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StreamVolume_NestedEntries_ResolveAndRead()
    {
        var image = TestImageFactory.CreateXdvdfsImage(SampleEntries);

        using var stream = new MemoryStream(image);
        using var volume = new XisoVfsVolume(stream, "embedded.iso");

        var entry = volume.GetEntry(@"\SUB\data.bin");
        Assert.NotNull(entry);
        Assert.Equal("nested data"u8.Length, entry.Size);

        var buffer = new byte[4];
        Assert.Equal(4, volume.ReadFile(entry, buffer, 7));
        Assert.Equal("data"u8.ToArray(), buffer);

        Assert.Equal(2, volume.GetFolderList("\\sub").Count());
        Assert.True(volume.GetEntry(@"\sub\deep")!.IsDirectory);
        Assert.Empty(volume.GetFolderList("\\empty"));
    }

    [Fact]
    public async Task PathVolume_ParallelReads_ReturnCorrectData()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            using var volume = new XisoVfsVolume(path);
            var entry = volume.GetEntry("\\big.bin");
            Assert.NotNull(entry);

            var expected = CreatePattern(5000);
            var tasks = Enumerable.Range(0, 16).Select(i => Task.Run(() =>
            {
                var offset = i * 300;
                var buffer = new byte[64];
                // ReSharper disable once AccessToDisposedClosure
                var read = volume.ReadFile(entry, buffer, offset);
                Assert.Equal(64, read);
                Assert.Equal(expected.AsSpan(offset, 64).ToArray(), buffer);
            }));

            await Task.WhenAll(tasks);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task StreamVolume_ParallelReads_ReturnCorrectData()
    {
        var image = TestImageFactory.CreateXdvdfsImage(SampleEntries);
        using var stream = new MemoryStream(image);
        using var volume = new XisoVfsVolume(stream, "embedded.iso");

        var entry = volume.GetEntry("\\big.bin");
        Assert.NotNull(entry);

        var expected = CreatePattern(5000);
        var tasks = Enumerable.Range(0, 16).Select(i => Task.Run(() =>
        {
            var offset = i * 300;
            var buffer = new byte[64];
            // ReSharper disable once AccessToDisposedClosure
            var read = volume.ReadFile(entry, buffer, offset);
            Assert.Equal(64, read);
            Assert.Equal(expected.AsSpan(offset, 64).ToArray(), buffer);
        }));

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var path = WriteImage(SampleEntries);
        try
        {
            var volume = new XisoVfsVolume(path);
            volume.Dispose();
            volume.Dispose();

            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Assert.True(exclusive.CanRead);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
