using Serilog;
using SimpleXisoDrive.XDVDFs;

namespace SimpleXisoDrive.Vfs;

/// <summary>
/// Provides a read-only virtual file system view over an Xbox ISO/XISO image,
/// resolving paths to XDVDFS directory entries and serving file data to the Dokan layer.
/// </summary>
public sealed class XisoVfsVolume : IVfsVolume
{
    private readonly IsoSt _isoSt;
    private readonly Dictionary<string, FileEntry> _entryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<FileEntry>> _childrenCache = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ulong VolumeSize { get; private set; }

    /// <inheritdoc />
    public DateTime VolumeCreationTime { get; private set; }

    /// <inheritdoc />
    public string VolumeLabel => "XBOX_ISO";

    /// <inheritdoc />
    public string FileSystemName => "XDVDFS";

    /// <summary>
    /// Initializes a new instance of the <see cref="XisoVfsVolume"/> class for the specified ISO file.
    /// </summary>
    /// <param name="isoPath">The path to the Xbox ISO file to open.</param>
    /// <exception cref="InvalidImageException">Thrown when the file is not a valid Xbox ISO image.</exception>
    public XisoVfsVolume(string isoPath)
    {
        _isoSt = new IsoSt(isoPath);
        Initialize(isoPath);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XisoVfsVolume"/> class over an already-open stream.
    /// Used to mount an XISO image embedded inside a ZArchive.
    /// </summary>
    /// <param name="stream">A seekable, read-only stream positioned at the start of the image.</param>
    /// <param name="displayName">The display name used in log and error messages.</param>
    /// <exception cref="InvalidImageException">Thrown when the stream is not a valid Xbox ISO image.</exception>
    internal XisoVfsVolume(Stream stream, string displayName)
    {
        _isoSt = new IsoSt(stream);
        Initialize(displayName);
    }

    private void Initialize(string displayName)
    {
        try
        {
            var volumeDescriptor = VolumeDescriptor.ReadFrom(_isoSt);
            if (!volumeDescriptor.Validate())
            {
                throw new InvalidImageException("XDVDFS magic string not found.");
            }

            Log.Debug(volumeDescriptor.IsRebuiltXisoFormat()
                ? "Detected rebuilt XISO format (sector 0)"
                : "Detected standard Xbox ISO format (sector 32)");

            VolumeCreationTime = volumeDescriptor.CreationTime;
            VolumeSize = (ulong)_isoSt.Reader.BaseStream.Length;

            var rootEntry = FileEntry.CreateRootEntry(volumeDescriptor.RootDirTableSector);
            Log.Debug("Root entry points to sector: {Sector}", rootEntry.StartSector);
            CacheEntry("\\", rootEntry);
        }
        catch (Exception ex)
        {
            _isoSt.Dispose();

            // Exception is re-thrown and caught by Program.cs, which handles the UI feedback.
            if (ex is InvalidImageException)
            {
                Log.Debug(ex, "Invalid Xbox ISO image");
                throw;
            }

            Log.Error(ex, "Failed to read Xbox ISO '{ImagePath}'", displayName);
            throw new InvalidImageException($"Failed to read Xbox ISO: {ex.Message}", ex);
        }
    }

    private void CacheEntry(string path, FileEntry entry)
    {
        _entryCache[path] = entry;
    }

    /// <inheritdoc />
    public IVfsEntry? GetEntry(string path)
    {
        try
        {
            return GetEntryInternal(path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetEntry failed for '{Path}'", path);
            return null;
        }
    }

    private FileEntry? GetEntryInternal(string path)
    {
        var normalizedPath = path.Replace('/', '\\').TrimEnd('\\');

        if (string.IsNullOrEmpty(normalizedPath))
        {
            normalizedPath = "\\";
        }

        if (string.Equals(normalizedPath, "\\", StringComparison.OrdinalIgnoreCase))
        {
            return _entryCache.GetValueOrDefault("\\");
        }

        if (_entryCache.TryGetValue(normalizedPath, out var cachedEntry))
        {
            return cachedEntry;
        }

        var parentPath = Path.GetDirectoryName(normalizedPath) ?? "\\";
        var fileName = Path.GetFileName(normalizedPath);

        if (GetEntry(parentPath) is not { IsDirectory: true } parentEntry)
        {
            return null;
        }

        var entry = FindEntryInDirectory((FileEntry)parentEntry, fileName);
        if (entry != null)
        {
            CacheEntry(normalizedPath, entry);
        }

        return entry;
    }

    private FileEntry? FindEntryInDirectory(FileEntry parentEntry, string targetName)
    {
        try
        {
            return TraverseBinaryTree(parentEntry, entry =>
                string.Equals(entry.FileName, targetName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in FindEntryInDirectory for target '{TargetName}'", targetName);
            return null;
        }
    }

    /// <inheritdoc />
    public IEnumerable<IVfsEntry> GetFolderList(string path)
    {
        var normalizedPath = path.Replace('/', '\\').TrimEnd('\\');
        Log.Debug("[GetFolderList] Starting for path: '{NormalizedPath}'", normalizedPath);

        if (string.IsNullOrEmpty(normalizedPath))
        {
            normalizedPath = "\\";
        }

        // Check if we have the directory listing cached
        if (_childrenCache.TryGetValue(normalizedPath, out var cachedChildren))
        {
            Log.Debug("[GetFolderList] Using cached children for '{NormalizedPath}' ({Count} entries)",
                normalizedPath, cachedChildren.Count);
            foreach (var entry in cachedChildren) yield return entry;

            yield break;
        }

        // Get the directory entry itself
        var dirEntry = string.Equals(normalizedPath, "\\", StringComparison.OrdinalIgnoreCase)
            ? _entryCache.GetValueOrDefault("\\")
            : GetEntry(normalizedPath) as FileEntry;
        if (dirEntry is not { IsDirectory: true })
        {
            Log.Debug("[GetFolderList] Directory not found or invalid: '{NormalizedPath}'", normalizedPath);
            yield break;
        }

        // Traverse the binary tree to get all children
        var children = new List<FileEntry>();
        var entries = GetAllEntriesFromBinaryTree(dirEntry);

        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.FileName)) continue;

            var childPath = Path.Combine(normalizedPath, entry.FileName);
            CacheEntry(childPath, entry);
            children.Add(entry);
            yield return entry;
        }

        _childrenCache[normalizedPath] = children;
        Log.Debug("[GetFolderList] Cached {Count} children for '{NormalizedPath}'", children.Count, normalizedPath);
    }

    private List<FileEntry> GetAllEntriesFromBinaryTree(FileEntry directoryEntry)
    {
        var entries = new List<FileEntry>();
        var visited = new HashSet<(long Sector, long Offset)>(); // Track visited nodes by sector and offset

        try
        {
            var firstEntry = directoryEntry.GetFirstChild(_isoSt);
            if (firstEntry != null)
            {
                TraverseBinaryTreeForAll(firstEntry, entries, visited);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error traversing binary tree in GetAllEntriesFromBinaryTree");
        }

        return entries;
    }

    private void TraverseBinaryTreeForAll(FileEntry firstEntry, List<FileEntry> results,
        HashSet<(long Sector, long Offset)> visited)
    {
        var stack = new Stack<FileEntry>();
        const int maxIterations = 100000; // Safety limit to prevent infinite loops
        var iterations = 0;

        // Check if first entry is valid and not already visited
        if (!visited.Add((firstEntry.EntrySector, firstEntry.EntryOffset)))
        {
            return;
        }

        stack.Push(firstEntry);

        while (stack.Count > 0)
        {
            // Safety check for infinite loops
            if (++iterations > maxIterations)
            {
                Log.Error(
                    new InvalidOperationException("Max iterations reached in TraverseBinaryTreeForAll"),
                    "TraverseBinaryTreeForAll: Max iterations reached, possible corrupted tree structure - too many nodes");
                break;
            }

            var current = stack.Pop();

            // Process current node
            if (!string.IsNullOrEmpty(current.FileName))
            {
                results.Add(current);
            }

            // Push right child first (so left is processed first - LIFO)
            if (current.HasRightChild)
            {
                var rightChild = current.GetRightChild(_isoSt);
                if (rightChild != null && visited.Add((rightChild.EntrySector, rightChild.EntryOffset)))
                {
                    stack.Push(rightChild);
                }
            }

            // Push left child
            if (current.HasLeftChild)
            {
                var leftChild = current.GetLeftChild(_isoSt);
                if (leftChild != null && visited.Add((leftChild.EntrySector, leftChild.EntryOffset)))
                {
                    stack.Push(leftChild);
                }
            }
        }
    }

    private FileEntry? TraverseBinaryTree(FileEntry directoryEntry, Func<FileEntry, bool> predicate)
    {
        try
        {
            var firstEntry = directoryEntry.GetFirstChild(_isoSt);
            if (firstEntry != null)
            {
                return SearchBinaryTree(firstEntry, predicate);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in binary tree traversal (TraverseBinaryTree)");
        }

        return null;
    }

    private FileEntry? SearchBinaryTree(FileEntry? startNode, Func<FileEntry, bool> predicate)
    {
        if (startNode == null) return null;

        var stack = new Stack<FileEntry>();
        const int maxIterations = 100000; // Safety limit to prevent infinite loops
        var iterations = 0;

        // Track visited nodes to prevent infinite loops (Cycle Detection)
        // Key is (Sector, Offset)
        var visited = new HashSet<(long Sector, long Offset)>();

        // Check if start node is valid and not already visited
        if (!visited.Add((startNode.EntrySector, startNode.EntryOffset)))
        {
            return null;
        }

        stack.Push(startNode);

        while (stack.Count > 0)
        {
            // Safety check for infinite loops
            if (++iterations > maxIterations)
            {
                Log.Error(
                    new InvalidOperationException("Max iterations reached in SearchBinaryTree"),
                    "SearchBinaryTree: Max iterations reached, possible corrupted tree structure - too many nodes or circular reference");
                break;
            }

            var current = stack.Pop();

            // Check if this is the entry we are looking for
            if (!string.IsNullOrEmpty(current.FileName) && predicate(current))
            {
                return current;
            }

            // Push children to the stack.
            // To mimic the recursive order (Check -> Left -> Right),
            // we push Right first, then Left, so Left is popped next.

            if (current.HasRightChild)
            {
                var rightChild = current.GetRightChild(_isoSt);
                // Only add if not null and not already visited
                if (rightChild != null && visited.Add((rightChild.EntrySector, rightChild.EntryOffset)))
                {
                    stack.Push(rightChild);
                }
            }

            if (current.HasLeftChild)
            {
                var leftChild = current.GetLeftChild(_isoSt);
                // Only add if not null and not already visited
                if (leftChild != null && visited.Add((leftChild.EntrySector, leftChild.EntryOffset)))
                {
                    stack.Push(leftChild);
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public int ReadFile(IVfsEntry entry, Span<byte> buffer, long offset)
    {
        try
        {
            if (entry is not FileEntry fileEntry)
            {
                return 0;
            }

            return _isoSt.Read(fileEntry, buffer, offset);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "XisoVfsVolume.ReadFile failed for {FileName}", entry.FileName);
            return 0;
        }
    }

    /// <summary>
    /// Closes the underlying ISO stream.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _isoSt.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "XisoVfsVolume.Dispose failed");
        }
    }
}
