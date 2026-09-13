using System.Text;
using Serilog;
using SimpleXisoDrive.Models;
using SimpleXisoDrive.Vfs;

namespace SimpleXisoDrive.XDVDFs;

/// <summary>
/// Represents a single file or directory entry in the XDVDFS directory tree.
/// </summary>
public class FileEntry : IVfsEntry
{
    /// <summary>
    /// Gets the sector containing this entry's directory record.
    /// </summary>
    public long EntrySector { get; internal set; }

    /// <summary>
    /// The size of an XDVDFS sector in bytes.
    /// </summary>
    public const int SectorSize = 2048;

    /// <summary>
    /// Gets the index of the left child node in the binary tree, or 0xFFFF when there is none.
    /// </summary>
    public ushort LeftSubTree { get; internal set; }

    /// <summary>
    /// Gets the index of the right child node in the binary tree, or 0xFFFF when there is none.
    /// </summary>
    public ushort RightSubTree { get; internal set; }

    /// <summary>
    /// Gets the sector where the file or directory data begins.
    /// </summary>
    public uint StartSector { get; internal set; }

    /// <summary>
    /// Gets the size of the file in bytes.
    /// </summary>
    public uint FileSize { get; internal set; }

    /// <summary>
    /// Gets the XDVDFS attributes of the entry.
    /// </summary>
    public XisoFsFileAttributes Attributes { get; internal set; }

    /// <summary>
    /// Gets the name of the file or directory.
    /// </summary>
    public string FileName { get; internal set; }

    /// <inheritdoc />
    public long Size => FileSize;

    /// <summary>
    /// Gets or sets the byte offset of this entry within its sector.
    /// </summary>
    public long EntryOffset { get; set; }

    /// <summary>
    /// Gets the total size of this entry in bytes, including alignment padding.
    /// </summary>
    public int EntrySize { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the entry represents a directory.
    /// </summary>
    public bool IsDirectory => (Attributes & XisoFsFileAttributes.Directory) != XisoFsFileAttributes.None;

    /// <summary>
    /// Gets a value indicating whether the entry has a left child node.
    /// </summary>
    public bool HasLeftChild => LeftSubTree != 0xFFFF;

    /// <summary>
    /// Gets a value indicating whether the entry has a right child node.
    /// </summary>
    public bool HasRightChild => RightSubTree != 0xFFFF;

    internal FileEntry()
    {
        FileName = string.Empty;
    }

    /// <summary>
    /// Creates the synthetic root directory entry for the volume.
    /// </summary>
    /// <param name="rootDirTableSector">The sector containing the root directory table.</param>
    /// <returns>A <see cref="FileEntry"/> representing the root directory.</returns>
    public static FileEntry CreateRootEntry(uint rootDirTableSector)
    {
        return new FileEntry
        {
            FileName = "",
            Attributes = XisoFsFileAttributes.Directory,
            FileSize = 0,
            StartSector = rootDirTableSector,
            LeftSubTree = 0xFFFF,
            RightSubTree = 0xFFFF,
            EntrySector = 0,
            EntryOffset = 0,
            EntrySize = 0
        };
    }

    internal void ReadInternal(BinaryReader reader, long sector, long offset)
    {
        try
        {
            EntrySector = sector;
            EntryOffset = offset;

            // Read fixed-size fields (14 bytes total)
            LeftSubTree = reader.ReadUInt16(); // 2 bytes
            RightSubTree = reader.ReadUInt16(); // 2 bytes
            StartSector = reader.ReadUInt32(); // 4 bytes
            FileSize = reader.ReadUInt32(); // 4 bytes
            Attributes = (XisoFsFileAttributes)reader.ReadByte(); // 1 byte
            var nameLength = reader.ReadByte(); // 1 byte

            // Read the filename
            byte[] nameBytes;
            if (nameLength > 0)
            {
                nameBytes = reader.ReadBytes(nameLength);
                if (nameBytes.Length < nameLength)
                {
                    throw new EndOfStreamException("Could not read complete filename");
                }
            }
            else
            {
                nameBytes = [];
            }

            // Process filename - XDVDFS uses null-terminated ASCII strings
            if (nameBytes.Length > 0)
            {
                var rawString = Encoding.ASCII.GetString(nameBytes);
                var nullIndex = rawString.IndexOf('\0');
                if (nullIndex >= 0)
                {
                    rawString = rawString.Substring(0, nullIndex);
                }

                FileName = rawString.Trim();
            }
            else
            {
                FileName = string.Empty;
            }

            // Calculate actual entry size
            EntrySize = 14 + nameLength; // Fixed header (14 bytes) + variable filename length

            // Add padding to align to the 4-byte boundary (XDVDFS requirement)
            var padding = (4 - (EntrySize % 4)) % 4;
            EntrySize += padding;

            // Skip the padding bytes
            if (padding > 0)
            {
                if (reader.BaseStream.Position + padding > reader.BaseStream.Length)
                {
                    throw new EndOfStreamException("End of stream reached while skipping padding bytes");
                }

                reader.BaseStream.Seek(padding, SeekOrigin.Current);
            }

            // Validate the entry
            if (FileSize == uint.MaxValue)
            {
                Log.Warning("Suspicious FileSize detected: {FileSize} for '{FileName}'", FileSize, FileName);
            }

            // Log.Debug("Read FileEntry: '{FileName}' at sector {Sector}, offset {Offset} (Size: {FileSize}, EntrySize: {EntrySize}, L:{Left}, R:{Right})", FileName, sector, offset, FileSize, EntrySize, LeftSubTree, RightSubTree);
        }
        catch (Exception ex)
        {
            // Logged at Debug level because the exception is re-thrown and
            // reported by the caller (IsoSt.ReadFileEntry)
            Log.Debug(ex, "Error reading FileEntry at sector {Sector}, offset {Offset}", sector, offset);
            FileName = "Invalid Entry";
            FileSize = 0;
            EntrySize = 16; // Minimum aligned size
            throw;
        }
    }

    /// <summary>
    /// Reads the left child entry of this node from the specified ISO stream.
    /// </summary>
    /// <param name="isoSt">The ISO stream to read from.</param>
    /// <returns>The left child entry, or <see langword="null"/> when there is none.</returns>
    public FileEntry? GetLeftChild(IsoSt isoSt)
    {
        if (LeftSubTree == 0xFFFF) return null;

        // XDVDFS stores offsets as index * 4
        var childOffset = (long)LeftSubTree * 4;

        // Prevent self-reference (compare byte offsets)
        if (childOffset != EntryOffset)
            return isoSt.ReadFileEntry(EntrySector, childOffset);

        // Log.Debug("Invalid self-reference in LeftSubTree for entry at sector {EntrySector}, offset {EntryOffset}", EntrySector, EntryOffset);
        return null;
    }

    /// <summary>
    /// Reads the right child entry of this node from the specified ISO stream.
    /// </summary>
    /// <param name="isoSt">The ISO stream to read from.</param>
    /// <returns>The right child entry, or <see langword="null"/> when there is none.</returns>
    public FileEntry? GetRightChild(IsoSt isoSt)
    {
        if (RightSubTree == 0xFFFF) return null;

        // XDVDFS stores offsets as index * 4
        var childOffset = (long)RightSubTree * 4;

        if (childOffset != EntryOffset)
            return isoSt.ReadFileEntry(EntrySector, childOffset);

        // Log.Debug("Invalid self-reference in RightSubTree for entry at sector {EntrySector}, offset {EntryOffset}", EntrySector, EntryOffset);
        return null;
    }

    /// <summary>
    /// Reads the first entry of this directory's directory table.
    /// </summary>
    /// <param name="isoSt">The ISO stream to read from.</param>
    /// <returns>The first child entry, or <see langword="null"/> when the table cannot be read.</returns>
    /// <exception cref="InvalidOperationException">Thrown when this entry is not a directory.</exception>
    public FileEntry? GetFirstChild(IsoSt isoSt)
    {
        if (!IsDirectory)
            throw new InvalidOperationException("Not a directory");

        // For directories, StartSector points to the directory table sector
        // The first entry is always at offset 0 of that sector
        return isoSt.ReadFileEntry(StartSector, 0);
    }

    /// <summary>
    /// Maps the XDVDFS attributes to the equivalent Windows <see cref="FileAttributes"/> flags.
    /// </summary>
    /// <returns>The Windows attributes for this entry.</returns>
    public FileAttributes GetWindowsAttributes()
    {
        var winAttrs = FileAttributes.ReadOnly;
        if ((Attributes & XisoFsFileAttributes.Directory) != XisoFsFileAttributes.None)
        {
            winAttrs |= FileAttributes.Directory;
        }

        if ((Attributes & XisoFsFileAttributes.Hidden) != XisoFsFileAttributes.None)
        {
            winAttrs |= FileAttributes.Hidden;
        }

        if ((Attributes & XisoFsFileAttributes.System) != XisoFsFileAttributes.None)
        {
            winAttrs |= FileAttributes.System;
        }

        if ((Attributes & XisoFsFileAttributes.Archive) != XisoFsFileAttributes.None)
        {
            winAttrs |= FileAttributes.Archive;
        }

        const FileAttributes standardWindowsAttributes = FileAttributes.Directory | FileAttributes.Hidden |
                                                         FileAttributes.System | FileAttributes.Archive;
        if ((winAttrs & standardWindowsAttributes) == FileAttributes.None)
        {
            winAttrs |= FileAttributes.Normal;
        }

        return winAttrs;
    }
}