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
    public static byte[] CreateMinimalXdvdfsImage(byte[]? fileData = null, string fileName = "default.xbe") =>
        CreateXdvdfsImage(headerSector: 0, fileData, fileName);

    /// <summary>
    /// Creates a minimal standard Xbox ISO image (volume descriptor at sector 32) whose
    /// root directory contains a single file entry.
    /// </summary>
    /// <param name="fileData">The file contents; defaults to a small ASCII payload.</param>
    /// <param name="fileName">The file name stored in the directory entry.</param>
    /// <returns>The raw image bytes.</returns>
    public static byte[] CreateStandardXdvdfsImage(byte[]? fileData = null, string fileName = "default.xbe") =>
        CreateXdvdfsImage(headerSector: 32, fileData, fileName);

    private static byte[] CreateXdvdfsImage(int headerSector, byte[]? fileData, string fileName)
    {
        fileData ??= "hello xbox"u8.ToArray();
        var nameBytes = Encoding.ASCII.GetBytes(fileName);
        var descriptorOffset = headerSector * SectorSize;
        var rootDirSector = headerSector + 1;
        var dataSector = headerSector + 2;
        var dataSectorCount = (fileData.Length + SectorSize - 1) / SectorSize;
        var image = new byte[(dataSector + dataSectorCount) * SectorSize];

        // Volume descriptor (sector 0 for rebuilt XISOs, sector 32 otherwise)
        var magic = "MICROSOFT*XBOX*MEDIA"u8;
        magic.CopyTo(image.AsSpan(descriptorOffset, magic.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(descriptorOffset + 0x14), (uint)rootDirSector);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(descriptorOffset + 0x18), SectorSize);
        BinaryPrimitives.WriteInt64LittleEndian(image.AsSpan(descriptorOffset + 0x1C), DateTime.UtcNow.ToFileTimeUtc());
        magic.CopyTo(image.AsSpan(descriptorOffset + 0x7EC, magic.Length));

        // Root directory table: a single entry with no children
        // (0 = no left/right child, per XDVDFS; 0xFFFF marks an empty table)
        var entry = image.AsSpan(rootDirSector * SectorSize);
        BinaryPrimitives.WriteUInt16LittleEndian(entry, 0); // left subtree
        BinaryPrimitives.WriteUInt16LittleEndian(entry[2..], 0); // right subtree
        BinaryPrimitives.WriteUInt32LittleEndian(entry[4..], (uint)dataSector); // data sector
        BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)fileData.Length);
        entry[12] = 0x20; // raw XDVDFS archive attribute
        entry[13] = (byte)nameBytes.Length;
        nameBytes.CopyTo(entry[14..]);

        // File data
        fileData.CopyTo(image.AsSpan(dataSector * SectorSize));

        return image;
    }
}
