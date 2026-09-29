# SimpleXisoDrive Wiki

SimpleXisoDrive is a lightweight, read-only virtual file system driver for Windows, Linux and macOS.
It mounts original Xbox ISO images (`.iso`, `.xiso`, `.cso`), Xbox ISO CHD images (`.chd`) and
ZArchive (`.zar`) files as virtual drive letters or folder mount points and exposes their contents
directly in the file manager.

On Windows the application is built on the [Dokan](https://github.com/dokan-dev/dokany) user-mode
file system driver through [DokanNet](https://github.com/dokan-dev/dokany/tree/master/dokan-dotnet);
on Linux and macOS it uses the FUSE 3 high-level API (`libfuse3` / macFUSE). All platforms share the
XISOSharp library that parses the Xbox Disc Video File System (**XDVDFS**). CHD files are
decompressed hunk-by-hunk with the CHDSharp library, and ZArchive files are read directly with
on-demand zstd decompression. The source image is never modified: every write operation is rejected
at the file system layer.

Developed by [PureLogic Code](https://purelogiccode.com/) and released under the **GPL-3.0** license.

> **Side menu:** on the GitHub wiki, use the menu on the right (driven by `docs/_Sidebar.md`); on the
> published documentation site, use the menu on the left (driven by `docs/_data/navigation.yml`).
> Both list every page below.

---

## Documentation map

### User guide

| Page | What it covers |
| --- | --- |
| [Installation](Installation) | Requirements, Dokan and .NET runtime setup, installing and upgrading |
| [Linux and macOS](Linux-and-macOS) | FUSE prerequisites, usage, unmounting and platform notes |
| [Getting Started](Getting-Started) | Your first mount, drag-and-drop, unmounting, example workflows |
| [Command-Line Reference](Command-Line-Reference) | Complete argument/option reference, path resolution, exit codes |
| [Troubleshooting](Troubleshooting) | Every known error message with causes and fixes |
| [FAQ](FAQ) | Short answers to common questions |

### Technical reference

| Page | What it covers |
| --- | --- |
| [Architecture](Architecture) | Component overview, startup and mount lifecycle, threading, error handling |
| [XDVDFS Format](XDVDFS-Format) | On-disk format: volume descriptor, directory entries, partition offsets |
| [Virtual File System](Virtual-File-System) | Path resolution, caching, mount operation behaviour, read-only enforcement |
| [Services](Services) | Logging, bug reporting, statistics, update checker, access checks |
| [Privacy and Networking](Privacy-and-Networking) | Every network request, its payload, and how to run fully offline |
| [Glossary](Glossary) | Definitions of terms used throughout the documentation |

### Development

| Page | What it covers |
| --- | --- |
| [Building](Building) | Building and publishing for every supported platform |
| [Testing](Testing) | Test project layout, running tests, coverage |
| [Contributing](Contributing) | Code style, analyzers, workflow, pull request checklist |
| [Release History](Release-History) | Tagged releases and notable changes |

---

## Feature overview

- **Read-only by design** - the ISO, CHD or ZAR is never modified, and write, delete, rename,
  attribute, and timestamp operations are denied.
- **Cross-platform** - native builds for Windows x64/ARM64 (Dokan), Linux x64/ARM64 and macOS
  x64/ARM64 (FUSE 3 / macFUSE), sharing one core library.
- **Broad Xbox format support** - standard Xbox ISO dumps (volume descriptor at sector 32),
  rebuilt XISO images (sector 0), dual-layer/hybrid dumps using the XGD1, XGD3, and GLOBAL
  partition offsets, and CISO-compressed images (`.cso`, including split `.1.cso` part sets).
- **Xbox ISO CHD support** - `.chd` images mount directly with on-demand hunk decompression through
  the pure-C# CHDSharp decoder (CHD V1–V5, all codecs). Only CHDs whose decompressed image is a
  valid Xbox ISO are accepted; CD/GD-ROM CHDs are rejected.
- **ZArchive support** - `.zar` archives mount directly: either the stored game tree or a single
  embedded XISO image, streamed through the pure-C# zstd block decoder.
- **Zero-config drag-and-drop** - drop an image onto the executable and it automatically picks a
  free drive letter from `M:` through `R:`.
- **Flexible mount targets** - mount to a drive letter such as `Z:` or into an empty NTFS folder on
  Windows, and to any existing directory on Linux and macOS.
- **Virtual `image.iso`** - the `--image-iso` option also exposes the raw Xbox image as `image.iso`
  at the mount root, for emulators that only accept a disc image (such as xemu).
- **Memory efficient** - file data is streamed from the image (and decompressed block-by-block for
  ZARs, hunk-by-hunk for CHDs) on demand; the entire image is never loaded into memory.
- **Corruption resistant** - the directory tree walker uses cycle detection and iteration limits to
  survive malformed images.
- **Built-in diagnostics** - rolling logs, a local error log, crash handling, and an optional bug
  report/telemetry pipeline.
- **Update awareness** - checks GitHub releases at startup and offers to open the release page
  (a message box on Windows, a console prompt on Linux/macOS).
- **Multi-architecture** - native builds for x64 and ARM64 on every platform.

## At a glance

| Property | Value |
| --- | --- |
| Application name | SimpleXisoDrive |
| Current version | 1.4.0 |
| Platform | Windows (x64, ARM64), Linux (x64, ARM64), macOS (x64, ARM64) |
| Target framework | .NET 10 (`net10.0-windows` / `net10.0`) |
| File systems | XDVDFS (original Xbox disc format), ZArchive (`ZARCHIVE`) |
| Access mode | Read-only |
| Volume label | `XBOX_ISO` (ISO/CHD) / `XBOX_ZAR` (ZArchive) |
| File system name | `XDVDFS` (ISO/CHD) / `ZARCHIVE` (ZArchive) |
| Prerequisites | Windows: Dokan 2.x; Linux: FUSE 3; macOS: macFUSE; .NET 10 Runtime |
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

# Mount an Xbox ISO stored as CHD
SimpleXisoDrive.exe "D:\Games\Halo.chd" Z:

# Mount an ISO into an empty NTFS folder
SimpleXisoDrive.exe "D:\Games\Halo.iso" "C:\Mounts\Halo"

# Enable verbose Dokan debug output
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z: --debug

# Expose the raw image as image.iso for an emulator that only opens disc images
SimpleXisoDrive.exe "D:\Games\Halo.chd" Z: --image-iso
```

On Linux and macOS the executable takes the same options with a directory mount point:

```shell
mkdir -p ~/mnt/halo
SimpleXisoDrive ~/Games/Halo.iso ~/mnt/halo --launch
```

The simplest path of all is to drag and drop an `.iso`, `.xiso`, `.cso`, `.chd` or `.zar` file onto
`SimpleXisoDrive.exe` (Windows only).

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
| CHDSharp | MIT |
