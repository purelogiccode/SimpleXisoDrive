using Serilog;
using SimpleXisoDrive.XDVDFs;

namespace SimpleXisoDrive;

/// <summary>
/// Provides a read-only virtual file system view over an Xbox ISO image,
/// resolving paths to directory entries and serving file data to the Dokan layer.
/// </summary>
public class VfsContainer : IDisposable
{
    private readonly IsoSt _isoSt;
    private readonly Dictionary<string, FileEntry> _entryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<FileEntry>> _childrenCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the total size of the ISO image in bytes.
    /// </summary>
    public ulong VolumeSize { get; }

    /// <summary>
    /// Gets the volume creation time recorded in the volume descriptor.
    /// </summary>
    public DateTime VolumeCreationTime { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VfsContainer"/> class for the specified ISO file.
    /// </summary>
    /// <param name="isoPath">The path to the Xbox ISO file to open.</param>
    /// <exception cref="InvalidImageException">Thrown when the file is not a valid Xbox ISO image.</exception>
    public VfsContainer(string isoPath)
    {
        _isoSt = new IsoSt(isoPath);

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

            Log.Error(ex, "Failed to read Xbox ISO '{IsoPath}'", isoPath);
            throw new InvalidImageException($"Failed to read Xbox ISO: {ex.Message}", ex);
        }
    }

    private void CacheEntry(string path, FileEntry entry)
    {
        _entryCache[path] = entry;
    }

    /// <summary>
    /// Gets the file entry for the specified virtual path.
    /// </summary>
    /// <param name="path">The virtual path to look up.</param>
    /// <returns>The matching <see cref="FileEntry"/>, or <see langword="null"/> if no entry exists at the path.</returns>
    public FileEntry? GetEntry(string path)
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

        var entry = FindEntryInDirectory(parentEntry, fileName);
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

    /// <summary>
    /// Enumerates the child entries of the directory at the specified virtual path.
    /// </summary>
    /// <param name="path">The virtual directory path to list.</param>
    /// <returns>The entries contained in the directory; empty if the path is not a valid directory.</returns>
    public IEnumerable<FileEntry> GetFolderList(string path)
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
        var dirEntry = string.Equals(normalizedPath, "\\", StringComparison.OrdinalIgnoreCase) ? _entryCache.GetValueOrDefault("\\") : GetEntry(normalizedPath);
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

    /// <summary>
    /// Reads file data for the specified entry into the buffer.
    /// </summary>
    /// <param name="entry">The file entry to read from.</param>
    /// <param name="buffer">The buffer that receives the data.</param>
    /// <param name="offset">The byte offset within the file at which to start reading.</param>
    /// <returns>The number of bytes read, or zero if the read fails.</returns>
    public int ReadFile(FileEntry entry, Span<byte> buffer, long offset)
    {
        try
        {
            return _isoSt.Read(entry, buffer, offset);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "VfsContainer.ReadFile failed for {FileName}", entry.FileName);
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
            Log.Error(ex, "VfsContainer.Dispose failed");
        }

        GC.SuppressFinalize(this);
    }
}