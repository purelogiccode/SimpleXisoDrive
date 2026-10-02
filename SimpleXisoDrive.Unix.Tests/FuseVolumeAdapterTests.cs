using FuseSharp;
using SimpleXisoDrive.Core.Interfaces;

namespace SimpleXisoDrive.Unix.Tests;

/// <summary>
/// Tests the adapter that maps the POSIX-path FuseSharp contract onto the
/// backslash-separated VFS volume.
/// </summary>
public class FuseVolumeAdapterTests
{
    /// <summary>
    /// Verifies volume metadata is forwarded unchanged.
    /// </summary>
    [Fact]
    public void Metadata_ForwardsToVolume()
    {
        var volume = new FakeVolume();
        var adapter = new FuseVolumeAdapter(volume);

        Assert.Equal(volume.VolumeLabel, adapter.VolumeLabel);
        Assert.Equal(volume.VolumeCreationTime, adapter.VolumeCreationTime);
        Assert.Equal(volume.VolumeSize, adapter.VolumeSize);
    }

    /// <summary>
    /// Verifies POSIX paths are converted to backslash VFS paths before lookup.
    /// </summary>
    /// <param name="path">The POSIX path passed to the adapter.</param>
    /// <param name="expected">The expected VFS path.</param>
    [Theory]
    [InlineData("/", "\\")]
    [InlineData("", "\\")]
    [InlineData("/sub/file.bin", "\\sub\\file.bin")]
    [InlineData("/sub/", "\\sub\\")]
    public void GetEntry_ConvertsPosixPathToVfsPath(string path, string expected)
    {
        var volume = new FakeVolume();
        var adapter = new FuseVolumeAdapter(volume);

        adapter.GetEntry(path);

        Assert.Equal(expected, volume.LastPath);
    }

    /// <summary>
    /// Verifies a missing entry stays null.
    /// </summary>
    [Fact]
    public void GetEntry_WhenMissing_ReturnsNull()
    {
        var adapter = new FuseVolumeAdapter(new FakeVolume());

        Assert.Null(adapter.GetEntry("/missing"));
    }

    /// <summary>
    /// Verifies the wrapped entry exposes the underlying metadata.
    /// </summary>
    [Fact]
    public void GetEntry_WrapsEntryMetadata()
    {
        var volume = new FakeVolume();
        volume.Entries["\\Default.xbe"] = new FakeEntry { FileName = "Default.xbe", Size = 42 };
        var adapter = new FuseVolumeAdapter(volume);

        var result = adapter.GetEntry("/Default.xbe");

        Assert.NotNull(result);
        Assert.Equal("Default.xbe", result.FileName);
        Assert.False(result.IsDirectory);
        Assert.Equal(42, result.Size);
    }

    /// <summary>
    /// Verifies directory listing converts the path and wraps every child.
    /// </summary>
    [Fact]
    public void GetFolderList_ConvertsPathAndWrapsChildren()
    {
        var volume = new FakeVolume();
        volume.Children.Add(new FakeEntry { FileName = "Modules", IsDirectory = true });
        volume.Children.Add(new FakeEntry { FileName = "Default.xbe", Size = 10 });
        var adapter = new FuseVolumeAdapter(volume);

        var children = adapter.GetFolderList("/").ToList();

        Assert.Equal("\\", volume.LastPath);
        Assert.Equal(2, children.Count);
        Assert.Equal("Modules", children[0].FileName);
        Assert.True(children[0].IsDirectory);
        Assert.Equal("Default.xbe", children[1].FileName);
        Assert.Equal(10, children[1].Size);
    }

    /// <summary>
    /// Verifies reads are forwarded with the underlying entry.
    /// </summary>
    [Fact]
    public void ReadFile_ForwardsToUnderlyingEntry()
    {
        var volume = new FakeVolume { ReadResult = 7 };
        var entry = new FakeEntry { FileName = "Default.xbe", Size = 10 };
        volume.Entries["\\Default.xbe"] = entry;
        var adapter = new FuseVolumeAdapter(volume);
        var wrapped = adapter.GetEntry("/Default.xbe");
        Assert.NotNull(wrapped);

        var read = adapter.ReadFile(wrapped, new byte[10], 0);

        Assert.Equal(7, read);
        Assert.Same(entry, volume.LastReadEntry);
    }

    /// <summary>
    /// Verifies an entry that did not come from the adapter is rejected.
    /// </summary>
    [Fact]
    public void ReadFile_WithForeignEntry_Throws()
    {
        var adapter = new FuseVolumeAdapter(new FakeVolume());

        Assert.Throws<ArgumentException>(() =>
        {
            Span<byte> buffer = stackalloc byte[1];
            adapter.ReadFile(new FakeEntry(), buffer, 0);
        });
    }

    private sealed class FakeVolume : IVfsVolume
    {
        public ulong VolumeSize { get; set; } = 4096;

        public DateTime VolumeCreationTime { get; set; } = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public string VolumeLabel { get; set; } = "XBOX_TEST";

        public string FileSystemName => "XISO";

        public Dictionary<string, IVfsEntry> Entries { get; } = new(StringComparer.Ordinal);

        public List<IVfsEntry> Children { get; } = [];

        public string? LastPath { get; private set; }

        public IVfsEntry? LastReadEntry { get; private set; }

        public int ReadResult { get; set; }

        public IVfsEntry? GetEntry(string path)
        {
            LastPath = path;
            return Entries.GetValueOrDefault(path);
        }

        public IEnumerable<IVfsEntry> GetFolderList(string path)
        {
            LastPath = path;
            return Children;
        }

        public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset)
        {
            LastReadEntry = entry;
            return ReadResult;
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeEntry : IVfsEntry, IFuseEntry
    {
        public string FileName { get; set; } = string.Empty;

        public bool IsDirectory { get; set; }

        public long Size { get; set; }

        public FileAttributes GetWindowsAttributes() => FileAttributes.Normal;
    }
}
