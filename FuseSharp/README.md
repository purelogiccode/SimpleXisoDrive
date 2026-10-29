# FuseSharp

[![NuGet](https://img.shields.io/nuget/v/SimpleXisoDrive.FuseSharp.svg?label=NuGet)](https://www.nuget.org/packages/SimpleXisoDrive.FuseSharp/)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://github.com/purelogiccode/SimpleXisoDrive/blob/master/LICENSE.txt)
[![.NET 8 | 9 | 10](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512bd4.svg?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet)

A FUSE 3 high-level mount library for .NET on Linux ([libfuse3](https://github.com/libfuse/libfuse))
and macOS ([macFUSE](https://macfuse.io)). Implement a read-only volume, mount it with one call,
and probe the runtime before you mount.

This library lives in the [SimpleXisoDrive](https://github.com/purelogiccode/SimpleXisoDrive)
repository (`FuseSharp/`). It is also used by
[CHDMounter](https://github.com/purelogiccode/CHDMounter).

## Requirements

- **Linux** (x64 or ARM64) with **libfuse3** (for example `sudo apt install libfuse3-3 fuse3`)
  and `/dev/fuse` available, or **macOS** (Intel or Apple silicon) with
  [macFUSE](https://macfuse.io) installed.
- **.NET 8, 9 or 10** runtime.

Set the `SIMPLEXISODRIVE_FUSE_LIBRARY` environment variable to override the FUSE library path
used for probing and loading (mainly useful for tests and custom installations).

## Quick start

Implement `IFuseVolume` for your data source:

```csharp
using FuseSharp;

public sealed class MyVolume : IFuseVolume
{
    public string VolumeLabel => "MyVolume";
    public DateTime VolumeCreationTime => DateTime.UtcNow;
    public ulong VolumeSize => 1024 * 1024;

    public IFuseEntry? GetEntry(string path) { /* look up "/file.txt", "/" ... */ }
    public IEnumerable<IFuseEntry> GetFolderList(string path) { /* list "/" ... */ }
    public int ReadFile(IFuseEntry entry, Span<byte> buffer, long offset) { /* fill buffer ... */ }
}
```

Check the runtime, then mount (blocks until unmounted):

```csharp
if (!FuseAvailability.Check(out var library))
    return; // installation guidance was already printed to stderr

var fs = new FuseFileSystem(new MyVolume());
var exitCode = fs.Run("/mnt/myvolume", debug: false, onMounted: () => Console.WriteLine("Mounted"));
```

`FuseFileSystem.Run` drives the FUSE session manually and shuts it down cleanly on `Ctrl+C`,
`SIGTERM` or `SIGHUP`. An external unmount (`umount`, `fusermount3 -u`, Finder eject) ends the
session through `ENODEV` and is also treated as a clean unmount. Call `fs.Exit()` from any thread
to request a shutdown (for example when your application closes).

To check only whether mounting is possible — without printing installation guidance — use:

```csharp
if (FuseAvailability.Check(out _, candidates: null, printInstructions: false))
    Console.WriteLine("FUSE is available.");
```

## API overview

| Type | Description |
|------|-------------|
| `IFuseVolume` | Read-only volume: label, size, entry lookup, directory listing and file reads. Paths are POSIX-style (`/` is the root). |
| `IFuseEntry` | A single file or directory: name, `IsDirectory` and `Size`. |
| `FuseFileSystem` | Serves a volume through the FUSE 3 high-level API (read-only). `Run(mountPoint, debug, onMounted)` blocks until unmount; `Exit()` requests a clean shutdown. |
| `FuseAvailability` | `Check(out libraryPath)` probes libfuse3/macFUSE and `/dev/fuse` and prints installation guidance when missing. |

The volume is exposed read-only: `open` rejects write access, macOS `setattr` returns `EROFS`,
and unimplemented operations fail with `ENOSYS`.

## License

GPLv3 — see [LICENSE.txt](https://github.com/purelogiccode/SimpleXisoDrive/blob/master/LICENSE.txt).
