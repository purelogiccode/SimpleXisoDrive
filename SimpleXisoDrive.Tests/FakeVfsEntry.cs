using SimpleXisoDrive.Core.Interfaces;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// A minimal in-memory <see cref="IVfsEntry"/>.
/// </summary>
/// <param name="fileName">The entry name.</param>
/// <param name="isDirectory">Whether the entry represents a directory.</param>
/// <param name="size">The entry size in bytes.</param>
internal sealed class FakeVfsEntry(string fileName, bool isDirectory, long size) : IVfsEntry
{
    /// <inheritdoc />
    public string FileName { get; } = fileName;

    /// <inheritdoc />
    public bool IsDirectory { get; } = isDirectory;

    /// <inheritdoc />
    public long Size { get; } = size;

    /// <inheritdoc />
    public FileAttributes GetWindowsAttributes()
    {
        return IsDirectory ? FileAttributes.Directory : FileAttributes.Archive;
    }
}
