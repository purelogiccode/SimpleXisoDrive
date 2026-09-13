# SimpleXisoDrive Wiki

SimpleXisoDrive is a lightweight, read-only virtual file system driver for Windows. It mounts
original Xbox ISO images (`.iso`, `.xiso`) and ZArchive (`.zar`) files as virtual drive letters or
NTFS folder mount points and exposes their contents directly in Windows Explorer.

The application is built on the [Dokan](https://github.com/dokan-dev/dokany) user-mode file system
driver through [DokanNet](https://github.com/dokan-dev/dokany/tree/master/dokan-dotnet) and uses the
XISOSharp library to parse the Xbox Disc Video File System (**XDVDFS**). ZArchive files are read
directly with on-demand zstd decompression. The source image is never modified: every write
operation is rejected at the file system layer.

Developed by [PureLogic Code](https://purelogiccode.com/) and released under the **GPL-3.0** license.

---

## Documentation map

### User guide

| Page | What it covers |
| --- | --- |
| [Installation](Installation) | Requirements, Dokan and .NET runtime setup, installing and upgrading |
| [Getting Started](Getting-Started) | Your first mount, drag-and-drop, unmounting, example workflows |
| [Command-Line Reference](Command-Line-Reference) | Complete argument/option reference, path resolution, exit codes |
| [Troubleshooting](Troubleshooting) | Every known error message with causes and fixes |
| [FAQ](FAQ) | Short answers to common questions |

### Technical reference

| Page | What it covers |
| --- | --- |
| [Architecture](Architecture) | Component overview, startup and mount lifecycle, threading, error handling |
| [XDVDFS Format](XDVDFS-Format) | On-disk format: volume descriptor, directory entries, partition offsets |
| [Virtual File System](Virtual-File-System) | Path resolution, caching, Dokan operation behaviour, read-only enforcement |
| [Services](Services) | Logging, bug reporting, statistics, update checker, access checks |
| [Privacy and Networking](Privacy-and-Networking) | Every network request, its payload, and how to run fully offline |
| [Glossary](Glossary) | Definitions of terms used throughout the documentation |

### Development

| Page | What it covers |
| --- | --- |
| [Building](Building) | Building and publishing for `win-x64` and `win-arm64` |
| [Testing](Testing) | Test project layout, running tests, coverage |
| [Contributing](Contributing) | Code style, analyzers, workflow, pull request checklist |
| [Release History](Release-History) | Tagged releases and notable changes |

---

## Feature overview

- **Read-only by design** - the ISO or ZAR is never modified, and write, delete, rename, attribute,
  and timestamp operations are denied.
- **Broad Xbox format support** - standard Xbox ISO dumps (volume descriptor at sector 32),
  rebuilt XISO images (sector 0), dual-layer/hybrid dumps using the XGD1, XGD3, and GLOBAL
  partition offsets, and CISO-compressed images (`.cso`, including split `.1.cso` part sets).
- **ZArchive support** - `.zar` archives mount directly: either the stored game tree or a single
  embedded XISO image, streamed through the pure-C# zstd block decoder.
- **Zero-config drag-and-drop** - drop an image onto the executable and it automatically picks a
  free drive letter from `M:` through `R:`.
- **Flexible mount targets** - mount to a drive letter such as `Z:` or into an empty NTFS folder.
- **Memory efficient** - file data is streamed from the image (and decompressed block-by-block for
  ZARs) on demand; the entire image is never loaded into memory.
- **Corruption resistant** - the directory tree walker uses cycle detection and iteration limits to
  survive malformed images.
- **Built-in diagnostics** - rolling logs, a local error log, crash handling, and an optional bug
  report/telemetry pipeline.
- **Update awareness** - checks GitHub releases at startup and offers to open the release page.
- **Multi-architecture** - native builds for Windows x64 and Windows ARM64.

## At a glance

| Property | Value |
| --- | --- |
| Application name | SimpleXisoDrive |
| Current version | 1.3.0 |
| Platform | Windows (x64, ARM64) |
| Target framework | .NET 10 (`net10.0-windows`) |
| File systems | XDVDFS (original Xbox disc format), ZArchive (`ZARCHIVE`) |
| Access mode | Read-only |
| Volume label | `XBOX_ISO` (ISO) / `XBOX_ZAR` (ZArchive) |
| File system name | `XDVDFS` (ISO) / `ZARCHIVE` (ZArchive) |
| Prerequisites | Dokan 2.x, .NET 10 Runtime |
| License | GPL-3.0 |
| Repository | <https://github.com/purelogiccode/SimpleXisoDrive> |

## Quick start

```shell
# Mount an ISO to the first free drive letter (M: through R:) and open Explorer
SimpleXisoDrive.exe "D:\Games\Halo.iso"

# Mount an ISO to drive Z:
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z:

# Mount a ZArchive the same way
SimpleXisoDrive.exe "D:\Games\Halo.zar" Z:

# Mount a CISO image (single or split .1.cso part set)
SimpleXisoDrive.exe "D:\Games\Halo.cso" Z:

# Mount an ISO into an empty NTFS folder
SimpleXisoDrive.exe "D:\Games\Halo.iso" "C:\Mounts\Halo"

# Enable verbose Dokan debug output
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z: --debug
```

The simplest path of all is to drag and drop an `.iso`, `.xiso`, `.cso` or `.zar` file onto
`SimpleXisoDrive.exe`.

## Support the project

- Star the repository: <https://github.com/purelogiccode/SimpleXisoDrive>
- Donate: <https://purelogiccode.com/Donate>
- Report issues: include the contents of `logs\`, `error.log`, and `critical_error.log` when relevant.

## License

SimpleXisoDrive is licensed under **GPL-3.0**. Third-party components:

| Component | License |
| --- | --- |
| DokanNet | MIT |
| Dokan Library | LGPL/MIT |
