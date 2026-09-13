# Virtual File System

This page explains how SimpleXisoDrive turns an Xbox image into a Windows-visible, read-only volume.
It covers format selection, path resolution, caching, every Dokan operation, and the read-only
guarantees. Both XDVDFS images (`.iso`, `.xiso`) and ZArchive (`.zar`) trees are supported.

---

## VfsContainer and IVfsVolume

`VfsContainer` is the facade between the Dokan operations layer and the image data. It is created once
per mount and disposed on unmount. The actual storage is an `IVfsVolume` implementation selected by
`VfsVolumeFactory`:

| Input | Detection | Volume |
| --- | --- | --- |
| `.iso`, `.xiso`, extensionless | XDVDFS volume descriptor validates | `XisoVfsVolume` |
| `.zar` | ZArchive footer validates | `ZarVfsVolume`, unless the archive root holds exactly one file that validates as an XDVDFS image — then that embedded image mounts through `XisoVfsVolume` over `ZArchiveReader.OpenRead` |
| Any other extension | XISO probe first; a renamed ZArchive falls back to the archive path | as above |

### Construction

`XisoVfsVolume`:

1. A keep-open `XisoExplorer` opens the ISO file with `FileShare.ReadWrite` (shared access lets
   antivirus and indexing tools open the file too) and eagerly probes the volume descriptor.
2. XISOSharp probes the rebuilt sector-0 layout and the standard sector-32 layouts (plain, GLOBAL,
   XGD3, XGD2 Hybrid, XGD1) and records the winning disc offset.
3. Invalid images surface as `XisoFormatException`, which the volume wraps in
   `InvalidImageException`; missing files still throw `FileNotFoundException`.
4. The volume size and creation time come from the probed `VolumeInfo`; the synthetic root entry
   (`\`) is cached.

For an embedded XISO, the same class uses the `XisoReader` stream APIs instead: `GetVolumeInfo`
probes the stream, and lookups/listings go through `GetEntryInfo`/`ListDirectory` under a stream
lock.

`ZarVfsVolume`:

1. `ZArchiveReader.TryOpen` validates the archive footer and loads the offset records, name table,
   and file tree. Failure throws `InvalidImageException`.
2. The uncompressed volume size is computed by summing every file in the tree; the creation time
   comes from the `.zar` file's timestamp.
3. The root ZArchive node is cached.

For an embedded XISO, the factory opens a seekable stream over the single archive file with
`ZArchiveReader.OpenRead` and lets `XisoVfsVolume` validate and mount it exactly like a standalone
ISO; `ReaderOwningVfsVolume` keeps the archive reader alive and closes it with the mount.

### Public surface

| Member | Behavior |
| --- | --- |
| `VolumeSize` | ISO length in bytes, or the summed uncompressed size of a ZArchive tree. |
| `VolumeCreationTime` | Timestamp from the volume descriptor (ISO) or the archive file (ZAR); `DateTime.MinValue` when the descriptor timestamp is invalid. |
| `GetEntry(path)` | Resolves a virtual path to an `IVfsEntry`, or `null`. Never throws. |
| `GetFolderList(path)` | Lazily enumerates the children of a directory. Returns nothing when the path is not a valid directory. |
| `ReadFile(entry, buffer, offset)` | Reads file data (decompressing ZAR blocks as needed); returns the number of bytes read, or `0` on failure. |
| `Dispose()` | Closes the underlying stream or archive. |

---

## Path resolution

### Normalization

Virtual paths use backslashes. Normalization (identical in both volumes):

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
3. Otherwise ask XISOSharp for the entry: `XisoExplorer.GetNode` for path-based images or
   `XisoReader.GetEntryInfo` for stream-based images (both case-insensitive, `/`-separated library
   paths).
4. Cache and return the result.

Every step is wrapped so that a failure logs an error and returns `null` instead of propagating to
Dokan. ZArchive lookups preserve the archive's original name casing in the returned entry even when
the requested path uses different case.

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

### ISO volumes

`GetFolderList` enumerates the entries of a directory table:

1. If the listing is cached, it is replayed from the cache.
2. Otherwise the directory entry is resolved (it must be a directory) and XISOSharp's
   `XisoExplorer.ListChildren` / `XisoReader.ListDirectory` walks the TOC.
3. Every named entry is cached by full path and added to the returned list.
4. The complete list is stored in the children cache.

The library's TOC walk is hardened:

- visited offsets are tracked, preventing cycles;
- each directory table is capped at a fixed number of entries;
- separator-bearing file names abort the walk instead of escaping the mount root.

### ZArchive volumes

`GetFolderList` reads children directly from the archive's flat file tree:

1. If the listing is cached, it is replayed from the cache.
2. Otherwise each child is read with `TryGetDirEntry`, which returns the name, type, size, and node
   handle in one call; entries are cached by full path and yielded.
3. The complete list is stored in the children cache.

Safety: directory counts are capped at 100,000 entries and the underlying reader validates node
counts against the file-tree bounds, so a crafted archive cannot make the enumeration run away.

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
| `CreateFile` | Opens a handle; resolves the path and stores the `IVfsEntry` in `info.Context`. Denies write access, creation, and truncation. Directory/file type mismatches are reported precisely. | `Success`, `FileNotFound`, `AccessDenied`, `AlreadyExists`, `PathNotFound`, `NotADirectory`, `Error` |
| `ReadFile` | Reads up to the buffer size, clamped to the remaining file size. Returns `InvalidHandle` for directories. A read at or beyond the file size returns `Success` with 0 bytes. | `Success`, `InvalidHandle`, `Error` |
| `GetFileInformation` | Returns name, attributes, size, and timestamps. Directories report length 0. An empty name is reported as `Unknown`. | `Success`, `FileNotFound`, `Error` |
| `FindFiles` | Lists `.`, `..`, and all real children. Requires a directory. | `Success`, `NotADirectory`, `Error` |
| `FindFilesWithPattern` | Lists children matching a wildcard translated to a case-insensitive regex with a 1-second match timeout. `.` and `..` are always included. | `Success`, `NotADirectory`, `Error` |
| `GetFileSecurity` | Builds a `FileSecurity` or `DirectorySecurity` granting `Everyone` read and execute access. | `Success`, `Error` |
| `GetVolumeInformation` | Returns the volume label and file system name for the active volume: `XBOX_ISO`/`XDVDFS` for images, `XBOX_ZAR`/`ZARCHIVE` for ZArchive trees (an embedded XISO keeps the image values). Maximum component length 255, features `ReadOnlyVolume | CasePreservedNames | UnicodeOnDisk`. | `Success`, `Error` |
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
| File length | `Size` from the entry |
| Attributes | Always include `ReadOnly`; ISO entries also map XDVDFS flags, ZArchive entries are `ReadOnly` plus `Normal`/`Directory` (the format stores no attributes) |
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

- A keep-open `XisoExplorer` serializes its metadata operations on an internal lock; the
  stream-backed `XisoVfsVolume` locks its held image stream around every seek/read. `ZArchiveReader`
  is likewise internally locked and swaps whole decompressed 64 KiB blocks in and out of a bounded
  LRU cache.
- Both volume implementations use concurrent dictionaries for their entry and children caches.
- `GetFolderList` returns the cached list, built on first request while the underlying stream or
  archive lock is taken per read.
