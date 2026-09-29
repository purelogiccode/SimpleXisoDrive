using SimpleXisoDrive.Core.Interfaces;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// An in-memory <see cref="IVfsVolume"/> whose members can be configured to fail, used to test
/// the volume decorators (<c>ImageIsoVfsVolume</c>, <c>ReaderOwningVfsVolume</c>).
/// </summary>
internal sealed class FakeVfsVolume : IVfsVolume
{
    /// <summary>
    /// Gets or sets the reported volume size.
    /// </summary>
    public ulong VolumeSize { get; set; } = 42;

    /// <summary>
    /// Gets or sets the reported volume creation time.
    /// </summary>
    public DateTime VolumeCreationTime { get; set; } = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    /// <summary>
    /// Gets or sets the reported volume label.
    /// </summary>
    public string VolumeLabel { get; set; } = "FAKE_VOLUME";

    /// <summary>
    /// Gets or sets the reported file system name.
    /// </summary>
    public string FileSystemName { get; set; } = "FAKEFS";

    /// <summary>
    /// Gets the single entry served by <see cref="GetEntry"/> and <see cref="GetFolderList"/>.
    /// </summary>
    public IVfsEntry Entry { get; } = new FakeVfsEntry("entry.bin", isDirectory: false, size: 7);

    /// <summary>
    /// Gets a value indicating whether the volume has been disposed.
    /// </summary>
    public bool Disposed { get; private set; }

    /// <summary>
    /// Gets the number of <see cref="Dispose"/> calls.
    /// </summary>
    public int DisposeCount { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Dispose"/> throws after recording the call.
    /// </summary>
    public bool ThrowOnDispose { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="GetEntry"/> throws.
    /// </summary>
    public bool ThrowOnGetEntry { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="GetFolderList"/> throws.
    /// </summary>
    public bool ThrowOnGetFolderList { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="ReadFile"/> throws.
    /// </summary>
    public bool ThrowOnReadFile { get; set; }

    /// <inheritdoc />
    public IVfsEntry? GetEntry(string path)
    {
        if (ThrowOnGetEntry)
        {
            throw new InvalidOperationException("get-entry failure");
        }

        return string.Equals(path, @"\", StringComparison.Ordinal) ? Entry : null;
    }

    /// <inheritdoc />
    public IEnumerable<IVfsEntry> GetFolderList(string path)
    {
        if (ThrowOnGetFolderList)
        {
            throw new InvalidOperationException("get-folder-list failure");
        }

        return [Entry];
    }

    /// <inheritdoc />
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset)
    {
        if (ThrowOnReadFile)
        {
            throw new InvalidOperationException("read-file failure");
        }

        if (!buffer.IsEmpty)
        {
            buffer[0] = 0xAB;
        }

        return Math.Min(buffer.Length, 1);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeCount++;
        Disposed = true;

        if (ThrowOnDispose)
        {
            throw new InvalidOperationException("dispose failure");
        }
    }
}