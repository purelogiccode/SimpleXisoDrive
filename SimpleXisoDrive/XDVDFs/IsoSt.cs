using Serilog;

namespace SimpleXisoDrive.XDVDFs;

/// <inheritdoc />
/// <summary>
/// Manages thread-safe access to the ISO file stream.
/// </summary>
public class IsoSt : IDisposable
{
    /// <summary>
    /// The XDVDFS sector size in bytes; always 2048.
    /// </summary>
    public const int SectorSize = 2048;

    private readonly Stream _fileStream;

    /// <summary>
    /// Gets or sets the global byte offset of the volume within the stream
    /// (for example, for dual-layer or partitioned disc images).
    /// </summary>
    public long VolumeOffset { get; set; }

    /// <summary>
    /// Gets the object used to synchronize access to the underlying stream.
    /// </summary>
    public object LockObject { get; } = new();

    // Keep Reader private or internal, access should go through locked methods
    internal BinaryReader Reader { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IsoSt"/> class and opens the specified ISO file.
    /// </summary>
    /// <param name="isoPath">The path to the ISO file to open.</param>
    public IsoSt(string isoPath)
    {
        // Use FileShare.ReadWrite to allow other processes (like antivirus) to open the file
        // while SimpleXisoDrive has it open.
        _fileStream = new FileStream(isoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Reader = new BinaryReader(_fileStream);
    }

    /// <summary>
    /// Internal constructor for testing with a MemoryStream.
    /// </summary>
    internal IsoSt(Stream stream)
    {
        _fileStream = stream;
        Reader = new BinaryReader(stream);
    }

    /// <summary>
    /// Reads file data for a given entry and offset, thread-safely.
    /// </summary>
    public int Read(FileEntry entry, Span<byte> buffer, long offset)
    {
        lock (LockObject)
        {
            try
            {
                // Apply VolumeOffset to the calculation
                var fileOffset = VolumeOffset + ((long)entry.StartSector * FileEntry.SectorSize) + offset;

                // Ensure we don't seek past the end of the stream
                if (fileOffset >= _fileStream.Length)
                {
                    return 0;
                }

                _fileStream.Seek(fileOffset, SeekOrigin.Begin);

                var totalRead = 0;
                while (totalRead < buffer.Length)
                {
                    var n = _fileStream.Read(buffer[totalRead..]);
                    if (n == 0) break; // End of stream reached

                    totalRead += n;
                }

                return totalRead;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Physical read failure: Sector {Sector}, Offset {Offset}, File: {FileName}",
                    entry.StartSector, offset, entry.FileName);
                return 0;
            }
        }
    }

    /// <summary>
    /// Reads a FileEntry structure from a specific sector and offset, thread-safely.
    /// </summary>
    public FileEntry? ReadFileEntry(long sector, long offset)
    {
        lock (LockObject)
        {
            try
            {
                // Apply VolumeOffset to the calculation
                var position = VolumeOffset + (sector * SectorSize) + offset;

                if (position >= _fileStream.Length)
                {
                    throw new EndOfStreamException($"Position {position} is beyond file length");
                }

                _fileStream.Seek(position, SeekOrigin.Begin);
                var entry = new FileEntry();
                entry.ReadInternal(Reader, sector, offset); // This reads one entry

                // Now, for traversal, we need to return the entry but ensure the next read knows the size
                return entry;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to read FileEntry at sector {Sector}, offset {Offset}", sector, offset);
                return null;
            }
        }
    }

    /// <summary>
    /// Executes an action with the BinaryReader under the stream lock.
    /// Useful for reading structures that require multiple sequential reads after a single seek.
    /// </summary>
    public void ExecuteLocked(Action<BinaryReader> action)
    {
        lock (LockObject)
        {
            action(Reader);
        }
    }

    /// <summary>
    /// Releases the underlying file stream.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Reader.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IsoSt.Dispose failed");
        }
    }
}