using System.Buffers.Binary;
using System.Text;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Builds minimal in-memory images used by the mount tests.
/// </summary>
internal static class TestImageFactory
{
    private const int SectorSize = 2048;

    /// <summary>
    /// Creates a minimal rebuilt-XISO image (volume descriptor at sector 0) whose root
    /// directory contains a single file entry.
    /// </summary>
    /// <param name="fileData">The file contents; defaults to a small ASCII payload.</param>
    /// <param name="fileName">The file name stored in the directory entry.</param>
    /// <returns>The raw image bytes.</returns>
    public static byte[] CreateMinimalXdvdfsImage(byte[]? fileData = null, string fileName = "default.xbe")
    {
        fileData ??= "hello xbox"u8.ToArray();
        var nameBytes = Encoding.ASCII.GetBytes(fileName);
        var sectorCount = 3 + ((fileData.Length + SectorSize - 1) / SectorSize);
        var image = new byte[sectorCount * SectorSize];

        // Volume descriptor at sector 0 (rebuilt XISO format)
        var magic = "MICROSOFT*XBOX*MEDIA"u8;
        magic.CopyTo(image.AsSpan(0x00, 0x14));
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x14), 1); // root dir table sector
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x18), SectorSize); // root dir table size
        BinaryPrimitives.WriteInt64LittleEndian(image.AsSpan(0x1C), DateTime.UtcNow.ToFileTimeUtc());
        magic.CopyTo(image.AsSpan(0x7EC, 0x14));

        // Root directory table at sector 1: a single entry with no children
        var entry = image.AsSpan(SectorSize);
        BinaryPrimitives.WriteUInt16LittleEndian(entry, 0xFFFF); // left subtree
        BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], 0xFFFF); // right subtree
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], 2); // data sector
        BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)fileData.Length);
        entry[12] = 0x20; // XisoFsFileAttributes.Archive
        entry[13] = (byte)nameBytes.Length;
        nameBytes.CopyTo(entry[14..]);

        // File data at sector 2
        fileData.CopyTo(image.AsSpan(2 * SectorSize));

        return image;
    }
}
