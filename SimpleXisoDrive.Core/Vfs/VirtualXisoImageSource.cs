using System.Buffers.Binary;
using System.Text;
using Serilog;
using SimpleXisoDrive.Core.Interfaces;
using XISOSharp;
using XISOSharp.DataStructures;
using XISOSharp.Models;
using ZArchiveSharp;

namespace SimpleXisoDrive.Core.Vfs;

/// <summary>
/// Serves a virtual XISO image for a ZArchive directory tree. A ZArchive stores a
/// file tree, not disc sectors, so the XDVDFS layout (volume descriptor, directory
/// tables and file extents) is synthesized in memory with XISOSharp's public layout
/// primitives (<see cref="DirectoryEntryTableWriter" />, <see cref="SectorAllocator" />,
/// <see cref="AvlTree" />). File data is read from the archive on demand, so nothing
/// is extracted to disk and the mount appears immediately.
/// </summary>
internal sealed class VirtualXisoImageSource : IRawImageSource
{
    /// <summary>Recursion cap for the archive walk, mirroring the extraction engine.</summary>
    private const int MaxDirectoryDepth = 1024;

    private readonly ZArchiveReader _reader;
    private readonly Extent[] _extents;
    private readonly Lock _readLock = new();
    private bool _disposed;

    /// <inheritdoc />
    public long Length { get; }

    private VirtualXisoImageSource(ZArchiveReader reader, string archivePath, ulong? fileTime)
    {
        _reader = reader;

        var root = BuildDirectory(reader, ZArchiveReader.RootNode,
            Path.GetFileNameWithoutExtension(archivePath), depth: 0);

        ComputeTableSizes(root);

        var allocator = new SectorAllocator(Constants.RootDirectorySector);
        AllocateDirectory(root, allocator);

        BuildTableBytes(root);

        var dataEnd = (long)allocator.NextFree * Constants.SectorSize;
        Length = RoundUpToModulus(dataEnd);
        var totalSectors = Length / Constants.SectorSize;
        if (totalSectors > uint.MaxValue)
        {
            throw new XisoFileTooLargeException(root.Name, Length);
        }

        var extents = new List<Extent>
        {
            new(0, BuildHeader((uint)totalSectors)),
            new(Constants.HeaderOffset, BuildDescriptor(root, fileTime ?? GetArchiveFileTime(archivePath))),
        };

        CollectTableExtents(root, extents);
        CollectFileExtents(root, extents);
        extents.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));
        _extents = extents.ToArray();
    }

    /// <summary>
    /// Builds the virtual image for the archive tree.
    /// </summary>
    /// <param name="reader">The open archive reader (owned by the caller).</param>
    /// <param name="archivePath">The archive path used in log and error messages.</param>
    /// <param name="fileTime">Optional fixed XISO volume FILETIME (tests); defaults to the archive's creation time.</param>
    /// <returns>A read-only source that serves the synthesized image.</returns>
    /// <exception cref="InvalidImageException">Thrown when the tree cannot be represented as an XISO image.</exception>
    public static VirtualXisoImageSource Create(ZArchiveReader reader, string archivePath, ulong? fileTime = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        try
        {
            return new VirtualXisoImageSource(reader, archivePath, fileTime);
        }
        catch (InvalidImageException ex)
        {
            Log.Error(ex, "Cannot synthesize image.iso for '{ArchivePath}'", archivePath);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to synthesize image.iso for '{ArchivePath}'", archivePath);
            throw new InvalidImageException(
                $"Failed to synthesize an image.iso from the ZArchive tree '{archivePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public int Read(Span<byte> buffer, long offset)
    {
        if (buffer.IsEmpty || offset < 0 || offset >= Length)
        {
            return 0;
        }

        var bytesToRead = (int)Math.Min(buffer.Length, Length - offset);

        lock (_readLock)
        {
            if (_disposed)
            {
                return 0;
            }

            try
            {
                var totalRead = 0;
                while (totalRead < bytesToRead)
                {
                    var position = offset + totalRead;
                    var extent = FindExtent(position);
                    if (extent is null)
                    {
                        // Unwritten region (alignment padding): zeros.
                        var gapEnd = NextExtentStart(position);
                        var gapLength = (int)Math.Min(bytesToRead - totalRead, gapEnd - position);
                        buffer.Slice(totalRead, gapLength).Clear();
                        totalRead += gapLength;
                        continue;
                    }

                    var delta = position - extent.Offset;
                    var length = (int)Math.Min(bytesToRead - totalRead, extent.Length - delta);

                    if (extent.IsPadding)
                    {
                        buffer.Slice(totalRead, length).Fill(Constants.PadByte);
                    }
                    else if (extent.ZarNode != ZArchiveReader.InvalidNode)
                    {
                        var read = (int)_reader.ReadFromFile(extent.ZarNode, (ulong)delta,
                            buffer.Slice(totalRead, length));
                        if (read < length)
                        {
                            // A block failure yields a short read; zero-fill the rest so the
                            // caller never sees stale buffer contents.
                            buffer.Slice(totalRead + read, length - read).Clear();
                        }
                    }
                    else
                    {
                        extent.Data.Slice((int)delta, length).CopyTo(buffer[totalRead..]);
                    }

                    totalRead += length;
                }

                return totalRead;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Virtual XISO read failed at offset {Offset}", offset);
                return 0;
            }
        }
    }

    /// <summary>
    /// Marks the source disposed. The archive reader belongs to the wrapped volume.
    /// </summary>
    public void Dispose()
    {
        lock (_readLock)
        {
            _disposed = true;
        }
    }

    private Extent? FindExtent(long offset)
    {
        var low = 0;
        var high = _extents.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var extent = _extents[middle];
            if (offset < extent.Offset)
            {
                high = middle - 1;
            }
            else if (offset >= extent.Offset + extent.Length)
            {
                low = middle + 1;
            }
            else
            {
                return extent;
            }
        }

        return null;
    }

    private long NextExtentStart(long offset)
    {
        var low = 0;
        var high = _extents.Length - 1;
        long next = Length;
        while (low <= high)
        {
            var middle = low + ((high - low) >> 1);
            var extent = _extents[middle];
            if (extent.Offset > offset)
            {
                next = extent.Offset;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return next;
    }

    private static long RoundUpToModulus(long position)
    {
        var remainder = position % Constants.FileModulus;
        return remainder == 0 ? position : position + (Constants.FileModulus - remainder);
    }

    private static ulong GetArchiveFileTime(string archivePath)
    {
        try
        {
            return File.Exists(archivePath)
                ? (ulong)File.GetCreationTimeUtc(archivePath).ToFileTimeUtc()
                : (ulong)DateTime.UtcNow.ToFileTimeUtc();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Could not read the creation time of '{ArchivePath}'; using the current time", archivePath);
            return (ulong)DateTime.UtcNow.ToFileTimeUtc();
        }
    }

    /// <summary>
    /// Walks the ZArchive tree and materializes the directory model (names, sizes and
    /// node handles only - no file data is read).
    /// </summary>
    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private static DirectoryNode BuildDirectory(ZArchiveReader reader, uint node, string name, int depth)
    {
        if (depth > MaxDirectoryDepth)
        {
            throw new InvalidImageException(
                $"The ZArchive directory nesting exceeds the supported depth ({MaxDirectoryDepth}).");
        }

        var directory = new DirectoryNode(name);
        var childCount = reader.GetDirEntryCount(node);

        for (uint index = 0; index < childCount; index++)
        {
            if (!reader.TryGetDirEntry(node, index, out var childNode, out var entry) ||
                string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            if (entry.IsDirectory)
            {
                directory.Directories.Add(BuildDirectory(reader, childNode, entry.Name, depth + 1));
            }
            else
            {
                if (entry.Size > uint.MaxValue)
                {
                    throw new XisoFileTooLargeException(entry.Name, (long)entry.Size);
                }

                directory.Files.Add(new FileNode(entry.Name, childNode, (uint)entry.Size));
            }
        }

        return directory;
    }

    /// <summary>
    /// Computes each directory's on-disk table size bottom-up, building the AVL tables
    /// (placeholder sectors) with the same shared primitive the whole-image writer uses.
    /// </summary>
    private static uint ComputeTableSizes(DirectoryNode directory)
    {
        var entries = new List<DirectoryEntryTableWriter.DirectoryTableEntry>(
            directory.Files.Count + directory.Directories.Count);

        foreach (var file in directory.Files)
        {
            entries.Add(new DirectoryEntryTableWriter.DirectoryTableEntry(file.Name, IsDirectory: false,
                StartSector: 0, FileSize: file.Size));
        }

        foreach (var subdirectory in directory.Directories)
        {
            entries.Add(new DirectoryEntryTableWriter.DirectoryTableEntry(subdirectory.Name, IsDirectory: true,
                StartSector: 0, FileSize: ComputeTableSizes(subdirectory)));
        }

        directory.Table = DirectoryEntryTableWriter.BuildTable(entries);
        directory.TableSize = DirectoryEntryTableWriter.ComputeTableSize(directory.Table);

        foreach (var file in directory.Files)
        {
            directory.FilesByName[file.Name] = file;
        }

        foreach (var subdirectory in directory.Directories)
        {
            directory.DirectoriesByName[subdirectory.Name] = subdirectory;
        }

        return directory.TableSize;
    }

    /// <summary>
    /// Assigns sectors with the same traversal order as the whole-image writer: the
    /// directory table, then its file children (AVL prefix order), then each
    /// subdirectory recursively.
    /// </summary>
    private static void AllocateDirectory(DirectoryNode directory, SectorAllocator allocator)
    {
        directory.StartSector = allocator.AllocateContiguous(SectorAllocator.RequiredSectors(directory.TableSize));

        if (directory.Table is null)
        {
            return;
        }

        AvlTree.AvlTraverseDepthFirst(directory.Table, (node, _, _) =>
        {
            if (node.Subdirectory is null && directory.FilesByName.TryGetValue(node.Filename, out var file))
            {
                file.StartSector = allocator.AllocateContiguous(SectorAllocator.RequiredSectors(file.Size));
                node.StartSector = file.StartSector;
            }

            return 0;
        }, null, AvlTraversalMethod.Prefix, 0);

        AvlTree.AvlTraverseDepthFirst(directory.Table, (node, _, _) =>
        {
            if (node.Subdirectory is not null &&
                directory.DirectoriesByName.TryGetValue(node.Filename, out var subdirectory))
            {
                AllocateDirectory(subdirectory, allocator);
                node.StartSector = subdirectory.StartSector;
            }

            return 0;
        }, null, AvlTraversalMethod.Prefix, 0);
    }

    private static void BuildTableBytes(DirectoryNode directory)
    {
        directory.TableBytes = DirectoryEntryTableWriter.SerializeTable(directory.Table);

        foreach (var subdirectory in directory.Directories)
        {
            BuildTableBytes(subdirectory);
        }
    }

    private static void CollectTableExtents(DirectoryNode directory, List<Extent> extents)
    {
        extents.Add(new Extent((long)directory.StartSector * Constants.SectorSize, directory.TableBytes));

        foreach (var subdirectory in directory.Directories)
        {
            CollectTableExtents(subdirectory, extents);
        }
    }

    private static void CollectFileExtents(DirectoryNode directory, List<Extent> extents)
    {
        foreach (var file in directory.Files)
        {
            var start = (long)file.StartSector * Constants.SectorSize;

            // Zero-byte files occupy no extent; adding one would collide with the next
            // file's extent at the same offset and break the sorted extent lookup.
            if (file.Size > 0)
            {
                extents.Add(new Extent(start, file.Node, file.Size));
            }

            var sectorEnd = start + ((long)SectorAllocator.RequiredSectors(file.Size) * Constants.SectorSize);
            var padding = sectorEnd - (start + file.Size);
            if (padding > 0)
            {
                extents.Add(new Extent(start + file.Size, padding, isPadding: true));
            }
        }

        foreach (var subdirectory in directory.Directories)
        {
            CollectFileExtents(subdirectory, extents);
        }
    }

    private static byte[] BuildHeader(uint totalSectors)
    {
        var header = new byte[Constants.HeaderOffset];

        // ECMA-119 primary volume descriptor fields, mirroring XisoWriter.WriteVolumeDescriptors.
        header[Constants.Ecma119DataAreaStart] = 0x01;
        "CD001"u8.CopyTo(header.AsSpan(Constants.Ecma119DataAreaStart + 1));
        header[Constants.Ecma119DataAreaStart + 6] = 0x01;

        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(Constants.Ecma119VolumeSpaceSize), totalSectors);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(Constants.Ecma119VolumeSpaceSize + 4), totalSectors);

        byte[] volumeSetSize = [0x01, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x08, 0x08, 0x00];
        volumeSetSize.CopyTo(header, Constants.Ecma119VolumeSetSize);

        header.AsSpan(Constants.Ecma119VolumeSetIdentifier,
            Constants.Ecma119VolumeCreationDate - Constants.Ecma119VolumeSetIdentifier).Fill(0x20);

        var dateOffset = Constants.Ecma119VolumeCreationDate;
        for (var field = 0; field < 4; field++)
        {
            header.AsSpan(dateOffset, 16).Fill((byte)'0');
            header[dateOffset + 16] = 0;
            dateOffset += 17;
        }

        header[dateOffset] = 0x01;

        const int terminator = Constants.Ecma119DataAreaStart + Constants.SectorSize;
        header[terminator] = 0xFF;
        "CD001"u8.CopyTo(header.AsSpan(terminator + 1));
        header[terminator + 6] = 0x01;

        Encoding.ASCII.GetBytes(Constants.OptimizedTag).CopyTo(header, Constants.OptimizedTagOffset);

        return header;
    }

    private static byte[] BuildDescriptor(DirectoryNode root, ulong fileTime)
    {
        var descriptor = new byte[Constants.SectorSize];
        var magic = Encoding.ASCII.GetBytes(Constants.HeaderData);
        const int magicTailOffset = Constants.HeaderDataLength + Constants.SectorOffsetSize + Constants.DirTableSize +
                                    Constants.FileTimeSize + Constants.UnusedSize;

        magic.CopyTo(descriptor, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(descriptor.AsSpan(Constants.HeaderDataLength), root.StartSector);
        BinaryPrimitives.WriteUInt32LittleEndian(
            descriptor.AsSpan(Constants.HeaderDataLength + Constants.SectorOffsetSize), root.TableSize);
        BinaryPrimitives.WriteInt64LittleEndian(
            descriptor.AsSpan(Constants.HeaderDataLength + Constants.SectorOffsetSize + Constants.DirTableSize),
            (long)fileTime);
        magic.CopyTo(descriptor.AsSpan(magicTailOffset));

        return descriptor;
    }

    /// <summary>One contiguous byte range of the virtual image.</summary>
    private sealed class Extent
    {
        private readonly byte[]? _data;

        public Extent(long offset, byte[] data)
        {
            Offset = offset;
            Length = data.Length;
            _data = data;
        }

        public Extent(long offset, uint zarNode, long length)
        {
            Offset = offset;
            Length = length;
            ZarNode = zarNode;
        }

        public Extent(long offset, long length, bool isPadding)
        {
            Offset = offset;
            Length = length;
            IsPadding = isPadding;
        }

        public long Offset { get; }

        public long Length { get; }

        public uint ZarNode { get; } = ZArchiveReader.InvalidNode;

        public bool IsPadding { get; }

        public ReadOnlySpan<byte> Data => _data ?? [];
    }

    /// <summary>
    /// A directory in the synthesized layout: its children plus the computed on-disk
    /// table, sector allocation and serialized table bytes.
    /// </summary>
    private sealed class DirectoryNode(string name)
    {
        public string Name { get; } = name;

        public List<DirectoryNode> Directories { get; } = [];

        public List<FileNode> Files { get; } = [];

        public Dictionary<string, FileNode> FilesByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, DirectoryNode> DirectoriesByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public AvlNode? Table { get; set; }

        public uint TableSize { get; set; }

        public uint StartSector { get; set; }

        public byte[] TableBytes { get; set; } = [];
    }

    /// <summary>
    /// A file in the synthesized layout: its archive node handle, size and allocated sector.
    /// </summary>
    private sealed class FileNode(string name, uint node, uint size)
    {
        public string Name { get; } = name;

        public uint Node { get; } = node;

        public uint Size { get; } = size;

        public uint StartSector { get; set; }
    }
}