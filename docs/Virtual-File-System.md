# Virtual File System

This page explains how SimpleXisoDrive turns an XDVDFS directory tree into a Windows-visible,
read-only volume. It covers path resolution, caching, every Dokan operation, and the read-only
guarantees.

---

## VfsContainer

`VfsContainer` is the bridge between the Dokan operations layer and the raw ISO stream. It is created
once per mount and disposed on unmount.

### Construction

1. A new `IsoSt` opens the ISO file with `FileMode.Open`, `FileAccess.Read`, and
   `FileShare.ReadWrite` (shared access lets antivirus and indexing tools open the file too).
2. `VolumeDescriptor.ReadFrom` probes the five known descriptor locations and sets
   `IsoSt.VolumeOffset` to the winning partition offset.
3. If validation fails, `InvalidImageException` is thrown:
   - the original `InvalidImageException` is rethrown unchanged when the descriptor was readable but
     invalid;
   - any other failure is wrapped with `Failed to read Xbox ISO: <message>`.
4. The volume size is taken from the stream length, the creation time from the descriptor, and the
   synthetic root `FileEntry` (`\`) is cached.

### Public surface

| Member | Behavior |
| --- | --- |
| `VolumeSize` | Length of the ISO in bytes. |
| `VolumeCreationTime` | Timestamp from the volume descriptor; `DateTime.MinValue` when invalid. |
| `GetEntry(path)` | Resolves a virtual path to a `FileEntry`, or `null`. Never throws. |
| `GetFolderList(path)` | Lazily enumerates the children of a directory. Returns nothing when the path is not a valid directory. |
| `ReadFile(entry, buffer, offset)` | Reads file data; returns the number of bytes read, or `0` on failure. |
| `Dispose()` | Closes the underlying stream. |

---

## Path resolution

### Normalization in VfsContainer

Virtual paths use backslashes. Normalization:

| Input | Normalized |
| --- | --- |
| `/Games/Halo` | `\Games\Halo` |
| `\Games\Halo\` | `\Games\Halo` |
| `""` | `\` |
| `\` | `\` |

Entry lookup is case-insensitive. The root path always resolves to the synthetic root entry created
during construction.

### Recursive lookup

`GetEntry` resolves a path as follows:

1. If the path is `\`, return the cached root entry.
2. If the path is already in the entry cache, return it.
3. Split the path into parent directory and file name.
4. Resolve the parent recursively; it must be a directory.
5. Search the parent's directory table for the name (case-insensitive).
6. Cache and return the result.

Every step is wrapped so that a failure logs an error and returns `null` instead of propagating to
Dokan.

### Path normalization in Dokan callbacks

Windows can send special relative segments. `XboxIsoVfsDokan.NormalizePath` handles them:

| Input path | Normalized |
| --- | --- |
| `\` | `\` |
| `\.` or `\..` | `\` |
| `\Games\.` | `\Games` |
| `\Games\Halo\..` | `\Games` |
| `/` separators | Converted to `\` |

---

## Directory listing

`GetFolderList` enumerates the entries of a directory table:

1. If the listing is cached, it is replayed from the cache.
2. Otherwise the directory entry is resolved and its first child is read from offset `0` of its start
   sector.
3. The binary tree is traversed iteratively with an explicit stack (left children processed first).
4. Entries with empty names are skipped; every named entry is cached by full path and yielded.
5. The complete list is stored in the children cache.

Traversal safety:

- visited nodes are tracked by `(EntrySector, EntryOffset)`, preventing cycles;
- each traversal is limited to 100,000 nodes and aborts with an error log when exceeded;
- self-referencing child pointers are rejected.

`XboxIsoVfsDokan.FindFiles` augments directory listings with Windows-style virtual entries:

| Entry | When added |
| --- | --- |
| `.` | Always |
| `..` | Every directory except the root |

Both virtual entries are reported as read-only directories and carry the volume creation time.

---

## Dokan operation matrix

The table lists every operation implemented by `XboxIsoVfsDokan`, its behavior, and the status codes
it can return.

### Query and read operations

| Operation | Behavior | Status codes |
| --- | --- | --- |
| `CreateFile` | Opens a handle; resolves the path and stores the `FileEntry` in `info.Context`. Denies write access, creation, and truncation. Directory/file type mismatches are reported precisely. | `Success`, `FileNotFound`, `AccessDenied`, `AlreadyExists`, `PathNotFound`, `NotADirectory`, `Error` |
| `ReadFile` | Reads up to the buffer size, clamped to the remaining file size. Returns `InvalidHandle` for directories. A read at or beyond the file size returns `Success` with 0 bytes. | `Success`, `InvalidHandle`, `Error` |
| `GetFileInformation` | Returns name, attributes, size, and timestamps. Directories report length 0. An empty name is reported as `Unknown`. | `Success`, `FileNotFound`, `Error` |
| `FindFiles` | Lists `.`, `..`, and all real children. Requires a directory. | `Success`, `NotADirectory`, `Error` |
| `FindFilesWithPattern` | Lists children matching a wildcard translated to a case-insensitive regex with a 1-second match timeout. `.` and `..` are always included. | `Success`, `NotADirectory`, `Error` |
| `GetFileSecurity` | Builds a `FileSecurity` or `DirectorySecurity` granting `Everyone` read and execute access. | `Success`, `Error` |
| `GetVolumeInformation` | Returns the volume label `XBOX_ISO`, file system `XDVDFS`, maximum component length 255, and features `ReadOnlyVolume | CasePreservedNames | UnicodeOnDisk`. | `Success`, `Error` |
| `GetDiskFreeSpace` | Reports the ISO size as total capacity and `0` free bytes. | `Success`, `Error` |
| `FindStreams` | Alternate data streams are not supported. | `NotImplemented` |
| `LockFile` / `UnlockFile` | No-op, reported as successful. | `Success` |

### Lifecycle operations

| Operation | Behavior |
| --- | --- |
| `Cleanup`, `CloseFile` | Empty: no per-handle resources exist. |
| `Mounted`, `Unmounted` | Return `Success`; exceptions are logged. |

### Mutating operations (always denied)

| Operation | Status |
| --- | --- |
| `WriteFile` | `AccessDenied` (bytes written = 0) |
| `FlushFileBuffers` | `AccessDenied` |
| `SetFileAttributes` | `AccessDenied` |
| `SetFileTime` | `AccessDenied` |
| `DeleteFile` | `AccessDenied` |
| `DeleteDirectory` | `AccessDenied` |
| `MoveFile` | `AccessDenied` |
| `SetEndOfFile` | `AccessDenied` |
| `SetAllocationSize` | `AccessDenied` |
| `SetFileSecurity` | `AccessDenied` |

> The read-only volume option is already requested from Dokan, so most write attempts are rejected by
> the driver; the explicit denials are a second line of defense.

---

## File metadata

| Property | Value |
| --- | --- |
| Creation time | Volume creation time |
| Last access time | Volume creation time |
| Last write time | Volume creation time |
| Directory length | `0` |
| File length | `FileSize` from the directory entry |
| Attributes | Always include `ReadOnly`; mapped from XDVDFS flags |
| Security | `Everyone`: `ReadAndExecute` allowed |

Windows may cache metadata, so all files appear to have the disc's creation timestamp.

---

## Error handling in Dokan operations

All public operations except the empty lifecycle methods run through
`ExecuteWithReporting(operation, fileName, action)`:

1. Execute the action.
2. On success, return its `NtStatus`.
3. On exception, log `Dokan operation {Operation} failed for '{FileName}'` at Error level, then
   return `DokanResult.Error`.

Because the Serilog pipeline includes `BugReportSink` at Warning level, such failures are also
written to `error.log` and can be forwarded to the bug report API. See
[Services](Services) and [Privacy and Networking](Privacy-and-Networking).

---

## Thread safety notes

- `IsoSt` serializes every stream seek/read pair on a single lock, so concurrent reads cannot corrupt
  the stream position.
- The `VfsContainer` caches are populated on demand while servicing requests and are not explicitly
  synchronized; they are designed for the common case where the first directory listing populates
  them for later lookups.
- `GetFolderList` is an iterator; enumeration happens on the calling Dokan thread while the
  underlying stream lock is taken per read.
