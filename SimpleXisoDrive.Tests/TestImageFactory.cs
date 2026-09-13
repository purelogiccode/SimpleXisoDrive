using System.Buffers.Binary;
using System.Text;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Builds minimal in-memory XDVDFS images used by the mount tests. Directory tables are
/// flat right-linked chains (left child always zero), which is a valid tree shape for the
/// XISOSharp walker.
/// </summary>
internal static class TestImageFactory
{
    private const int SectorSize = 2048;
    private const byte DirectoryAttribute = 0x10;

    /// <summary>
    /// Creates a minimal rebuilt-XISO image (volume descriptor at sector 0) whose root
    /// directory contains a single file entry.
    /// </summary>
    /// <param name="fileData">The file contents; defaults to a small ASCII payload.</param>
    /// <param name="fileName">The file name stored in the directory entry.</param>
    /// <returns>The raw image bytes.</returns>
    public static byte[] CreateMinimalXdvdfsImage(byte[]? fileData = null, string fileName = "default.xbe") =>
        CreateXdvdfsImage([new TestImageEntry(fileName, fileData ?? "hello xbox"u8.ToArray())], headerSector: 0);

    /// <summary>
    /// Creates a minimal standard Xbox ISO image (volume descriptor at sector 32) whose
    /// root directory contains a single file entry.
    /// </summary>
    /// <param name="fileData">The file contents; defaults to a small ASCII payload.</param>
    /// <param name="fileName">The file name stored in the directory entry.</param>
    /// <returns>The raw image bytes.</returns>
    public static byte[] CreateStandardXdvdfsImage(byte[]? fileData = null, string fileName = "default.xbe") =>
        CreateXdvdfsImage([new TestImageEntry(fileName, fileData ?? "hello xbox"u8.ToArray())], headerSector: 32);

    /// <summary>
    /// Creates an XDVDFS image containing the specified files and directories.
    /// </summary>
    /// <param name="entries">Files and explicit empty directories to place in the image.</param>
    /// <param name="headerSector">Sector holding the volume descriptor (0 for rebuilt, 32 for standard).</param>
    /// <param name="fileTimeUtc">
    /// Descriptor FILETIME value; defaults to the current UTC time. Pass 0 to exercise the
    /// epoch-fallback mapping.
    /// </param>
    /// <returns>The raw image bytes.</returns>
    public static byte[] CreateXdvdfsImage(
        IReadOnlyList<TestImageEntry> entries,
        int headerSector = 0,
        long? fileTimeUtc = null)
    {
        var root = BuildTree(entries);

        var directories = new List<DirectoryNode>();
        CollectDirectories(root, directories);

        var nextSector = headerSector + 1;
        foreach (var directory in directories)
        {
            directory.Sector = nextSector++;
        }

        foreach (var directory in directories)
        {
            foreach (var file in directory.Files)
            {
                if (file.Data.Length == 0)
                {
                    file.Sector = 0;
                    continue;
                }

                file.Sector = (uint)nextSector;
                nextSector += (file.Data.Length + SectorSize - 1) / SectorSize;
            }
        }

        var image = new byte[(long)nextSector * SectorSize];
        WriteDescriptor(image, headerSector, (uint)root.Sector, fileTimeUtc ?? DateTime.UtcNow.ToFileTimeUtc());

        foreach (var directory in directories)
        {
            WriteDirectoryTable(image, directory);
        }

        foreach (var directory in directories)
        {
            foreach (var file in directory.Files)
            {
                if (file.Data.Length > 0)
                {
                    file.Data.CopyTo(image.AsSpan((int)file.Sector * SectorSize));
                }
            }
        }

        return image;
    }

    private static DirectoryNode BuildTree(IReadOnlyList<TestImageEntry> entries)
    {
        var root = new DirectoryNode(string.Empty, "/");

        foreach (var entry in entries)
        {
            var segments = entry.Path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                continue;
            }

            var current = root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                current = GetOrAddDirectory(current, segments[i]);
            }

            var name = segments[^1];
            if (entry.Data is null)
            {
                GetOrAddDirectory(current, name);
            }
            else
            {
                if (current.Directories.Exists(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException($"'{entry.Path}' collides with a directory.", nameof(entries));
                }

                current.Files.Add(new FileNode(name, entry.Data, entry.Attributes));
            }
        }

        return root;
    }

    private static DirectoryNode GetOrAddDirectory(DirectoryNode parent, string name)
    {
        var existing = parent.Directories.Find(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var directory = new DirectoryNode(name, string.Equals(parent.Path, "/", StringComparison.Ordinal)
            ? "/" + name
            : parent.Path + "/" + name);
        parent.Directories.Add(directory);
        return directory;
    }

    private static void CollectDirectories(DirectoryNode node, List<DirectoryNode> directories)
    {
        directories.Add(node);
        foreach (var child in node.Directories)
        {
            CollectDirectories(child, directories);
        }
    }

    private static void WriteDescriptor(byte[] image, int headerSector, uint rootDirSector, long fileTimeUtc)
    {
        var magic = "MICROSOFT*XBOX*MEDIA"u8;
        var offset = headerSector * SectorSize;
        magic.CopyTo(image.AsSpan(offset, magic.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 0x14), rootDirSector);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + 0x18), SectorSize);
        BinaryPrimitives.WriteInt64LittleEndian(image.AsSpan(offset + 0x1C), fileTimeUtc);
        magic.CopyTo(image.AsSpan(offset + 0x7EC, magic.Length));
    }

    private static void WriteDirectoryTable(byte[] image, DirectoryNode directory)
    {
        var items = new List<(string Name, uint Sector, uint Size, byte Attributes)>();
        foreach (var child in directory.Directories)
        {
            items.Add((child.Name, (uint)child.Sector, SectorSize, DirectoryAttribute));
        }

        foreach (var file in directory.Files)
        {
            items.Add((file.Name, file.Sector, (uint)file.Data.Length, file.Attributes));
        }

        if (items.Count == 0)
        {
            return;
        }

        var table = image.AsSpan(directory.Sector * SectorSize, SectorSize);
        var offsets = new int[items.Count];
        var cursor = 0;
        for (var i = 0; i < items.Count; i++)
        {
            offsets[i] = cursor;
            cursor += AlignedEntrySize(items[i].Name);
        }

        if (cursor > SectorSize)
        {
            throw new ArgumentException($"Directory table for '{directory.Path}' exceeds one sector.", nameof(directory));
        }

        for (var i = 0; i < items.Count; i++)
        {
            var right = i + 1 < items.Count ? (ushort)(offsets[i + 1] / 4) : (ushort)0;
            WriteEntry(table, offsets[i], right, items[i]);
        }
    }

    private static void WriteEntry(
        Span<byte> table, int offset, ushort right, (string Name, uint Sector, uint Size, byte Attributes) item)
    {
        var nameBytes = Encoding.ASCII.GetBytes(item.Name);
        BinaryPrimitives.WriteUInt16LittleEndian(table[offset..], 0);
        BinaryPrimitives.WriteUInt16LittleEndian(table[(offset + 2)..], right);
        BinaryPrimitives.WriteUInt32LittleEndian(table[(offset + 4)..], item.Sector);
        BinaryPrimitives.WriteUInt32LittleEndian(table[(offset + 8)..], item.Size);
        table[offset + 12] = item.Attributes;
        table[offset + 13] = (byte)nameBytes.Length;
        nameBytes.CopyTo(table[(offset + 14)..]);
    }

    private static int AlignedEntrySize(string name)
    {
        var size = 14 + name.Length;
        return size + ((4 - (size % 4)) % 4);
    }

    private sealed class DirectoryNode(string name, string path)
    {
        public string Name { get; } = name;

        public string Path { get; } = path;

        public List<DirectoryNode> Directories { get; } = [];

        public List<FileNode> Files { get; } = [];

        public int Sector { get; set; }
    }

    private sealed class FileNode(string name, byte[] data, byte attributes)
    {
        public string Name { get; } = name;

        public byte[] Data { get; } = data;

        public byte Attributes { get; } = attributes;

        public uint Sector { get; set; }
    }
}
