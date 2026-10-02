# FuseSharp

`FuseSharp` is the standalone FUSE 3 high-level mount library used by the Linux and macOS front
end of SimpleXisoDrive. It was extracted from the application so the mount layer can be tested,
reused and versioned on its own: the library knows nothing about Xbox images, XDVDFS or
`SimpleXisoDrive.Core` - it mounts any read-only volume that implements its interface.

- **Project:** `FuseSharp/FuseSharp.csproj` (`net10.0`, packable)
- **Assembly and namespace:** `FuseSharp`
- **NuGet package id:** `SimpleXisoDrive.FuseSharp`
- **Native dependency:** `libfuse3` on Linux, macFUSE's `libfuse3` on macOS

---

## The volume contract

A volume is exposed through two small interfaces with POSIX-style paths (`/` is the root and
components are separated by `/`):

```csharp
public interface IFuseVolume
{
    string VolumeLabel { get; }
    DateTime VolumeCreationTime { get; }
    ulong VolumeSize { get; }

    IFuseEntry? GetEntry(string path);
    IEnumerable<IFuseEntry> GetFolderList(string path);
    int ReadFile(IFuseEntry entry, Span<byte> buffer, long offset);
}

public interface IFuseEntry
{
    string FileName { get; }
    bool IsDirectory { get; }
    long Size { get; }
}
```

The contract is deliberately read-only and minimal:

| Member | Behavior expected by `FuseFileSystem` |
| --- | --- |
| `VolumeLabel` | Exposed as `fsname=` on Linux and `volname=` on macOS (sanitized by the library). |
| `VolumeCreationTime` | Reported as the `st_atim`/`st_mtim`/`st_ctim` timestamps of every entry. |
| `VolumeSize` | Reported through `statfs` as the total number of 4096-byte blocks. |
| `GetEntry` | Returns `null` for a missing path (mapped to `ENOENT`); the root path is `/`. |
| `GetFolderList` | Returns the children of a directory; empty when the path is not a directory. |
| `ReadFile` | Returns the number of bytes read; throws `IOException` when the data cannot be read (mapped to `EIO`). |

`FuseFileSystem` never calls a mutating member: there is none. `open` rejects anything but
`O_RDONLY` with `EACCES`, macOS `setattr` returns `EROFS`, and every callback the library does not
implement fails with `ENOSYS` by default.

## Mounting a volume

```csharp
using FuseSharp;

var volume = new MyReadOnlyVolume();
var fileSystem = new FuseFileSystem(volume);

var exitCode = fileSystem.Run(mountPoint: "/mnt/myvolume", debug: false, onMounted: () =>
{
    Console.WriteLine("Mounted.");
});
```

`Run` blocks until the file system is unmounted (`Ctrl+C`, `SIGTERM`, `SIGHUP` or
`fusermount3 -u`/`umount`), then returns `0` on a clean unmount. The constructor registers the
native library resolver, so no separate initialization call is required. `FuseAvailability.Check`
can be used before mounting to print installation guidance when FUSE is missing.

## Components

| Component | Purpose |
| --- | --- |
| `FuseFileSystem` | High-level mount: builds the `fuse_operations` table, runs the FUSE session loop, maps callback results to POSIX errors and installs signal handlers that request a clean exit through `fuse_exit`. |
| `FuseInterop` | Platform-aware P/Invoke layer: loads `libfuse3`, declares the Linux and macOS `fuse_operations` layouts and `struct stat`/`struct statvfs` writes, chooses the exported `fuse_new`/`fuse_new_31` entry point, and pokes the mount with `statfs` to wake a blocked loop. |
| `FuseAvailability` | Probes the FUSE library, `/dev/fuse` and `fusermount3`, and prints per-platform installation guidance when a prerequisite is missing. |
| `IFuseVolume` / `IFuseEntry` | The POSIX-path read-only volume contract described above. |

The library targets Linux x64/ARM64 and macOS x64/ARM64. The FUSE library can be overridden with
the `SIMPLEXISODRIVE_FUSE_LIBRARY` environment variable; otherwise the standard distribution paths
are probed in version order.

## How SimpleXisoDrive uses it

The application keeps the Xbox-specific code in `SimpleXisoDrive.Core` and adapts it to the
library contract in `SimpleXisoDrive.Unix/FuseVolumeAdapter.cs`:

- `FuseVolumeAdapter` implements `IFuseVolume` over an `IVfsVolume` (whose paths use backslashes)
  and converts `/sub/file` to `\sub\file` in both directions.
- Each `IVfsEntry` is wrapped in an internal `EntryAdapter` so `ReadFile` can unwrap it again and
  forward the read to the original entry. An entry that did not come from the adapter is rejected.
- The Unix front end wires the two together:
  `new FuseFileSystem(new FuseVolumeAdapter(vfsContainer))`.

Because the adapter lives in the application, `FuseSharp` has no project reference to
`SimpleXisoDrive.Core` and can be packed and consumed independently.

## See also

- [Architecture](Architecture) - where `FuseSharp` sits in the component and mount lifecycle.
- [Virtual File System](Virtual-File-System) - the volume behavior on the other side of the adapter.
- [Linux and macOS](Linux-and-macOS) - end-user usage and prerequisites.
- [Building](Building) - building and testing the solution, including `FuseSharp`.
