using System.Security.AccessControl;
using DokanNet;
using Serilog;
using SimpleXisoDrive.Vfs;
using FileAccess = DokanNet.FileAccess;

namespace SimpleXisoDrive;

/// <summary>
/// Dokan file system implementation that exposes an Xbox ISO image as a read-only
/// virtual drive backed by a <see cref="VfsContainer"/>.
/// </summary>
public class XboxIsoVfsDokan(VfsContainer vfs) : IDokanOperations
{
    private readonly VfsContainer _vfs = vfs;
    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Helper to ensure every single operation is tracked.
    /// If any logic fails, the bug is sent to the API endpoint.
    /// </summary>
    private static NtStatus ExecuteWithReporting(string operation, string fileName, Func<NtStatus> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Dokan operation {Operation} failed for '{FileName}'", operation, fileName);

            // The Serilog BugReportSink forwards this to the API fire-and-forget
            return DokanResult.Error;
        }
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('/', '\\');
        if (normalized is @"\." or @"\..") return @"\";

        if (normalized.EndsWith(@"\.", StringComparison.Ordinal))
            return Path.GetDirectoryName(normalized) ?? @"\";

        if (normalized.EndsWith(@"\..", StringComparison.Ordinal))
        {
            var parent = Path.GetDirectoryName(normalized);
            return parent == null ? @"\" : Path.GetDirectoryName(parent) ?? @"\";
        }

        return normalized;
    }

    /// <summary>
    /// Opens a file or directory handle, enforcing the read-only semantics of the volume.
    /// </summary>
    /// <param name="fileName">The path of the file or directory to open.</param>
    /// <param name="access">The requested access mode.</param>
    /// <param name="share">The requested sharing mode.</param>
    /// <param name="mode">The action to take when opening the file.</param>
    /// <param name="options">Additional file options.</param>
    /// <param name="attributes">The requested file attributes.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the handle is opened; otherwise, an error status.</returns>
    public NtStatus CreateFile(string fileName, FileAccess access, FileShare share,
        FileMode mode, FileOptions options, FileAttributes attributes, IDokanFileInfo info)
    {
        return ExecuteWithReporting(nameof(CreateFile), fileName, () =>
        {
            var path = NormalizePath(fileName);

            if (string.Equals(path, @"\", StringComparison.OrdinalIgnoreCase))
            {
                var rootEntry = _vfs.GetEntry(@"\");
                if (rootEntry is not { IsDirectory: true }) return DokanResult.Error;

                info.IsDirectory = true;
                info.Context = rootEntry;
                return DokanResult.Success;
            }

            var entry = _vfs.GetEntry(path);
            if (entry == null)
            {
                return mode == FileMode.Open ? DokanResult.FileNotFound : DokanResult.AccessDenied;
            }

            info.IsDirectory = entry.IsDirectory;
            info.Context = entry;

            // Deny write access (Read-Only FS)
            if ((access & (FileAccess.GenericWrite | FileAccess.WriteData | FileAccess.AppendData |
                           FileAccess.Delete)) != FileAccess.None)
            {
                return DokanResult.AccessDenied;
            }

            return mode switch
            {
                FileMode.CreateNew => DokanResult.AlreadyExists,
                FileMode.Create or FileMode.Truncate => DokanResult.AccessDenied,
                _ => DokanResult.Success
            };
        });
    }

    /// <summary>
    /// Reads data from an open file into the supplied buffer.
    /// </summary>
    /// <param name="fileName">The path of the file to read.</param>
    /// <param name="buffer">The buffer that receives the data.</param>
    /// <param name="bytesRead">When this method returns, the number of bytes read.</param>
    /// <param name="offset">The byte offset within the file at which to start reading.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the read completes; otherwise, an error status.</returns>
    public NtStatus ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, IDokanFileInfo info)
    {
        var internalBytesRead = 0;
        var result = ExecuteWithReporting(nameof(ReadFile), fileName, () =>
        {
            if (info.Context is not IVfsEntry entry)
            {
                entry = _vfs.GetEntry(fileName) ?? throw new InvalidOperationException("File entry missing");
                info.Context = entry;
            }

            if (entry.IsDirectory) return DokanResult.InvalidHandle;
            if (offset >= entry.Size) return DokanResult.Success;

            var remainingBytes = entry.Size - offset;
            var bytesToRead = (int)Math.Min(buffer.Length, remainingBytes);

            if (bytesToRead > 0)
            {
                internalBytesRead = _vfs.ReadFile(entry, buffer.AsSpan(0, bytesToRead), offset);
            }

            return DokanResult.Success;
        });

        bytesRead = internalBytesRead;
        return result;
    }

    /// <summary>
    /// Retrieves metadata such as size, attributes and timestamps for a file or directory.
    /// </summary>
    /// <param name="fileName">The path of the file or directory.</param>
    /// <param name="fileInfo">When this method returns, the information for the entry.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the entry is found; otherwise, an error status.</returns>
    public NtStatus GetFileInformation(string fileName, out FileInformation fileInfo, IDokanFileInfo info)
    {
        FileInformation internalInfo = default;
        var result = ExecuteWithReporting(nameof(GetFileInformation), fileName, () =>
        {
            var path = NormalizePath(fileName);

            // Safely get entry from context or lookup in VFS
            IVfsEntry? entry = null;
            try
            {
                entry = info.Context as IVfsEntry;
            }
            catch
            {
                // Context is not a file entry, will try lookup
            }

            entry ??= _vfs.GetEntry(path);

            if (entry == null) return DokanResult.FileNotFound;

            // Ensure FileName is never null
            var safeFileName = entry.FileName;
            if (string.IsNullOrEmpty(safeFileName))
            {
                safeFileName = "Unknown";
            }

            internalInfo = new FileInformation
            {
                FileName = safeFileName,
                Attributes = entry.GetWindowsAttributes(),
                CreationTime = _vfs.VolumeCreationTime,
                LastAccessTime = _vfs.VolumeCreationTime,
                LastWriteTime = _vfs.VolumeCreationTime,
                Length = entry.IsDirectory ? 0 : entry.Size
            };

            return DokanResult.Success;
        });

        fileInfo = internalInfo;
        return result;
    }

    /// <summary>
    /// Lists the entries of a directory, including the virtual "." and ".." entries.
    /// </summary>
    /// <param name="fileName">The path of the directory to list.</param>
    /// <param name="files">When this method returns, the entries found in the directory.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the directory is listed; otherwise, an error status.</returns>
    public NtStatus FindFiles(string fileName, out IList<FileInformation> files, IDokanFileInfo info)
    {
        var internalFiles = new List<FileInformation>();
        var result = ExecuteWithReporting(nameof(FindFiles), fileName, () =>
        {
            var path = NormalizePath(fileName);
            var dirEntry = _vfs.GetEntry(path);

            if (dirEntry is not { IsDirectory: true }) return DokanResult.NotADirectory;

            // Add virtual entries
            var template = new FileInformation
            {
                Attributes = FileAttributes.Directory | FileAttributes.ReadOnly,
                CreationTime = _vfs.VolumeCreationTime, LastAccessTime = _vfs.VolumeCreationTime,
                LastWriteTime = _vfs.VolumeCreationTime
            };

            internalFiles.Add(new FileInformation
                { FileName = ".", Attributes = template.Attributes, CreationTime = template.CreationTime });
            if (!string.Equals(path, @"\", StringComparison.OrdinalIgnoreCase))
                internalFiles.Add(new FileInformation
                    { FileName = "..", Attributes = template.Attributes, CreationTime = template.CreationTime });

            foreach (var entry in _vfs.GetFolderList(path))
            {
                if (string.Equals(entry.FileName, @"\", StringComparison.OrdinalIgnoreCase)) continue;

                internalFiles.Add(new FileInformation
                {
                    FileName = entry.FileName,
                    Attributes = entry.GetWindowsAttributes(),
                    CreationTime = _vfs.VolumeCreationTime,
                    LastAccessTime = _vfs.VolumeCreationTime,
                    LastWriteTime = _vfs.VolumeCreationTime,
                    Length = entry.Size
                });
            }

            return DokanResult.Success;
        });

        files = internalFiles;
        return result;
    }

    /// <summary>
    /// Lists the entries of a directory that match the specified wildcard search pattern.
    /// </summary>
    /// <param name="fileName">The path of the directory to list.</param>
    /// <param name="searchPattern">The wildcard pattern (for example, "*.txt") used to filter entries.</param>
    /// <param name="files">When this method returns, the matching entries.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the directory is listed; otherwise, an error status.</returns>
    public NtStatus FindFilesWithPattern(string fileName, string searchPattern, out IList<FileInformation> files,
        IDokanFileInfo info)
    {
        var filteredFiles = new List<FileInformation>();
        var result = ExecuteWithReporting(nameof(FindFilesWithPattern), fileName, () =>
        {
            var status = FindFiles(fileName, out var allFiles, info);
            if (status != DokanResult.Success) return status;

            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(searchPattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            var regex = new System.Text.RegularExpressions.Regex(regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase, RegexMatchTimeout);

            foreach (var f in allFiles)
            {
                if (f.FileName is "." or ".." || regex.IsMatch(f.FileName))
                    filteredFiles.Add(f);
            }

            return DokanResult.Success;
        });

        files = filteredFiles;
        return result;
    }

    /// <summary>
    /// Returns a security descriptor that grants everyone read and execute access.
    /// </summary>
    /// <param name="fileName">The path of the file or directory.</param>
    /// <param name="security">When this method returns, the security descriptor for the entry.</param>
    /// <param name="sections">The sections of the security descriptor requested.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the descriptor is built.</returns>
    public NtStatus GetFileSecurity(string fileName, out FileSystemSecurity? security, AccessControlSections sections,
        IDokanFileInfo info)
    {
        FileSystemSecurity? internalSecurity = null;
        var result = ExecuteWithReporting(nameof(GetFileSecurity), fileName, () =>
        {
            var entry = _vfs.GetEntry(NormalizePath(fileName));
            internalSecurity = entry is { IsDirectory: true } ? new DirectorySecurity() : new FileSecurity();

            var everyone =
                new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid,
                    null);
            internalSecurity.AddAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.ReadAndExecute,
                AccessControlType.Allow));

            return DokanResult.Success;
        });

        security = internalSecurity;
        return result;
    }

    /// <summary>
    /// Returns information about the virtual volume, such as its label, file system name and features.
    /// </summary>
    /// <param name="volumeLabel">When this method returns, the volume label.</param>
    /// <param name="features">When this method returns, the features supported by the volume.</param>
    /// <param name="fileSystemName">When this method returns, the name of the file system.</param>
    /// <param name="maximumComponentLength">When this method returns, the maximum length of a file name component.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the information is returned.</returns>
    public NtStatus GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
        out string fileSystemName, out uint maximumComponentLength, IDokanFileInfo info)
    {
        volumeLabel = _vfs.VolumeLabel;
        fileSystemName = _vfs.FileSystemName;
        maximumComponentLength = 255;
        features = FileSystemFeatures.ReadOnlyVolume | FileSystemFeatures.CasePreservedNames |
                   FileSystemFeatures.UnicodeOnDisk;

        try
        {
            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetVolumeInformation failed");
            return DokanResult.Error;
        }
    }

    /// <summary>
    /// Returns the volume capacity. Because the volume is read-only, no free space is reported.
    /// </summary>
    /// <param name="freeBytesAvailable">When this method returns, the free space available to the user.</param>
    /// <param name="totalNumberOfBytes">When this method returns, the total size of the volume.</param>
    /// <param name="totalNumberOfFreeBytes">When this method returns, the total free space on the volume.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the information is returned.</returns>
    public NtStatus GetDiskFreeSpace(out long freeBytesAvailable, out long totalNumberOfBytes,
        out long totalNumberOfFreeBytes, IDokanFileInfo info)
    {
        try
        {
            totalNumberOfBytes = (long)_vfs.VolumeSize;
            freeBytesAvailable = 0;
            totalNumberOfFreeBytes = 0;
            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            freeBytesAvailable = 0;
            totalNumberOfBytes = 0;
            totalNumberOfFreeBytes = 0;
            Log.Error(ex, "GetDiskFreeSpace failed");
            return DokanResult.Error;
        }
    }

    // Boilerplate / Read-Only Enforcement
    /// <summary>
    /// Invoked when a file handle is cleaned up. No per-handle resources are held.
    /// </summary>
    /// <param name="fileName">The path of the file or directory.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    public void Cleanup(string fileName, IDokanFileInfo info)
    {
    }

    /// <summary>
    /// Invoked when a file handle is closed. No per-handle resources are held.
    /// </summary>
    /// <param name="fileName">The path of the file or directory.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    public void CloseFile(string fileName, IDokanFileInfo info)
    {
    }

    /// <summary>
    /// Invoked after the file system has been mounted.
    /// </summary>
    /// <param name="mountPoint">The mount point that was mounted.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the callback completes.</returns>
    public NtStatus Mounted(string mountPoint, IDokanFileInfo info)
    {
        try
        {
            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Mounted callback failed for '{MountPoint}'", mountPoint);
            return DokanResult.Error;
        }
    }

    /// <summary>
    /// Invoked after the file system has been unmounted.
    /// </summary>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/> when the callback completes.</returns>
    public NtStatus Unmounted(IDokanFileInfo info)
    {
        try
        {
            return DokanResult.Success;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unmounted callback failed");
            return DokanResult.Error;
        }
    }

    /// <summary>
    /// Denies all writes because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the file to write to.</param>
    /// <param name="buffer">The data that would be written.</param>
    /// <param name="bytesWritten">Always zero, since no data is written.</param>
    /// <param name="offset">The byte offset at which the write would start.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset, IDokanFileInfo info)
    {
        bytesWritten = 0;
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies flushing because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the file.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus FlushFileBuffers(string fileName, IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies attribute changes because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the file or directory.</param>
    /// <param name="attributes">The attributes that would be applied.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus SetFileAttributes(string fileName, FileAttributes attributes, IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies timestamp changes because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the file or directory.</param>
    /// <param name="creationTime">The creation time that would be applied.</param>
    /// <param name="lastAccessTime">The last access time that would be applied.</param>
    /// <param name="lastWriteTime">The last write time that would be applied.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
        DateTime? lastWriteTime, IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies file deletion because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the file to delete.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus DeleteFile(string fileName, IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies directory deletion because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the directory to delete.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus DeleteDirectory(string fileName, IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies rename and move operations because the volume is read-only.
    /// </summary>
    /// <param name="oldName">The current path of the file or directory.</param>
    /// <param name="newName">The destination path.</param>
    /// <param name="replace">Whether an existing destination file would be replaced.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus MoveFile(string oldName, string newName, bool replace, IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies end-of-file changes because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the file.</param>
    /// <param name="length">The requested end-of-file position.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus SetEndOfFile(string fileName, long length, IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies allocation size changes because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the file.</param>
    /// <param name="length">The requested allocation size.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus SetAllocationSize(string fileName, long length, IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Denies security descriptor changes because the volume is read-only.
    /// </summary>
    /// <param name="fileName">The path of the file or directory.</param>
    /// <param name="security">The security descriptor that would be applied.</param>
    /// <param name="sections">The sections of the security descriptor to change.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.AccessDenied"/>.</returns>
    public NtStatus SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
        IDokanFileInfo info)
    {
        return DokanResult.AccessDenied;
    }

    /// <summary>
    /// Reports success; locking is a no-op on this read-only volume.
    /// </summary>
    /// <param name="fileName">The path of the file.</param>
    /// <param name="offset">The byte offset of the range to lock.</param>
    /// <param name="length">The length of the range to lock.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/>.</returns>
    public NtStatus LockFile(string fileName, long offset, long length, IDokanFileInfo info)
    {
        return DokanResult.Success;
    }

    /// <summary>
    /// Reports success; unlocking is a no-op on this read-only volume.
    /// </summary>
    /// <param name="fileName">The path of the file.</param>
    /// <param name="offset">The byte offset of the range to unlock.</param>
    /// <param name="length">The length of the range to unlock.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.Success"/>.</returns>
    public NtStatus UnlockFile(string fileName, long offset, long length, IDokanFileInfo info)
    {
        return DokanResult.Success;
    }

    /// <summary>
    /// Reports that alternate data streams are not supported.
    /// </summary>
    /// <param name="fileName">The path of the file.</param>
    /// <param name="streams">When this method returns, an empty list of streams.</param>
    /// <param name="info">Dokan file information for the operation.</param>
    /// <returns><see cref="DokanResult.NotImplemented"/>.</returns>
    public NtStatus FindStreams(string fileName, out IList<FileInformation> streams, IDokanFileInfo info)
    {
        streams = new List<FileInformation>();
        return DokanResult.NotImplemented;
    }
}