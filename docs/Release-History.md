# Release History

This page summarizes tagged releases and notable changes. Source: repository tags and commit
history.

| Version | Tag | Date |
| --- | --- | --- |
| Unreleased | `master` | 2026-10-02 |
| 1.4.0 | `release_1.4.0` | 2026-09-29 |
| 1.3.0 | `release_1.3.0` | 2026-09-13 |
| 1.2.0 | `release_1.2.0` | 2026-06-21 |
| 1.1.0 | `release_1.1.0` | 2026-04-12 |
| 1.0.3 | `release_1.0.3` | 2026-02-13 |
| 1.0.2 | `release_1.0.2` | 2026-01-24 |
| 1.0.1 | `release_1.0.1` | 2026-01-24 |
| 1.0.0 | `release_1.0.0` | 2026-01-24 |

---

## Unreleased (1.5.0)

- **Startup experience.** The GitHub update check runs immediately at startup on both front ends;
  non-interactive runs print the version and URL instead of showing a dialog. A missing Dokan
  runtime or driver now offers the Dokan download page. `-h`/`--help` prints the usage text and
  exits before any network call or backend probe, and a run without arguments shows the usage text
  before Dokan/FUSE is checked.
- **Windows command line.** Options may appear before or after the mount path; a single image
  argument with options stays in drag-and-drop mode; folder mount paths must already exist; the
  administrator warning now also covers `Z:`.
- **Reliability.** Read failures surface as `DokanResult.Error`/`-EIO` instead of a silent EOF;
  ZArchive reader access is serialized; entry caches are case-sensitive; the XISO explorer handle
  is released on descriptor failures; `ReaderOwningVfsVolume` disposal is idempotent; and image
  lookup misses are classified below the bug-report threshold.
- **FuseSharp.** The FUSE 3 mount layer is a standalone packable library
  (`SimpleXisoDrive.FuseSharp`) with a new `IFuseVolume`/`IFuseEntry` POSIX-path contract. The Unix
  app adapts the Xbox volume through `FuseVolumeAdapter`, so `FuseSharp` no longer depends on
  `SimpleXisoDrive.Core`.
- **Logging.** Expected user-setup and input conditions (missing Dokan/FUSE, missing image files,
  invalid images, failed file-manager launches) no longer auto-report; launch statistics are
  tracked and awaited at shutdown, and browser `Process` handles are disposed.
- **Tests and docs.** 476 tests (395 Windows + 81 Unix) and refreshed documentation with a shared
  wiki/Pages side menu that now includes the [FuseSharp](FuseSharp) page.

## 1.4.0 - 2026-09-29

- **Cross-platform.** The application was split into a shared `SimpleXisoDrive.Core` library and two
  front ends: `SimpleXisoDrive` (Windows, Dokan) and `SimpleXisoDrive.Unix` (Linux/macOS, FUSE 3 via
  `libfuse3`/macFUSE). Native builds are produced for `win-x64`, `win-arm64`, `linux-x64`,
  `linux-arm64`, `osx-x64` and `osx-arm64`.
- **Xbox ISO CHD support.** `.chd` images mount directly through CHDSharp with on-demand hunk
  decompression (CHD V1–V5, all codecs). CD/GD-ROM CHDs are rejected up front and the decompressed
  image must parse as XDVDFS. A renamed CHD still mounts by content detection.
- **Virtual `image.iso`.** The `-i`/`--image-iso` option exposes the raw Xbox image as a virtual
  read-only `image.iso` at the mount root. Plain ISO/XISO and CISO inputs are served on demand; a
  ZArchive directory tree is synthesized into an XISO entirely in memory with XISOSharp's layout
  primitives (byte-identical to `XisoWriter.PackFromDirectory` output for the same tree), and a
  single-embedded-XISO archive serves the embedded image. Nothing is extracted to disk.
- **Review fixes and hardening.** The volume size no longer double-counts `image.iso` for images
  that are not additional content; extension-specific openers fall back to content detection so
  renamed `.iso`/`.chd`/`.zar` files always mount; entry and listing caches are bounded (4096
  entries / 512 listings) so a long-lived mount cannot grow without limit; disposal is idempotent
  across volumes and decorators; and stream/reader ownership is closed on every error path.
- **FUSE-specific fixes.** macOS uses the exported `fuse_new_31` entry point (macFUSE's libfuse3
  disables ELF symbol versioning), the `statfs` wake-up poke allocates a buffer large enough for the
  macOS structure, temporary mount directories are cleaned up, and option matching is
  case-insensitive like the Windows front end.
- **Services.** Logging is routed through Serilog with a console level switch (`--debug`) and a
  rolling file sink; Warning-and-higher events are archived to `error.log` and forwarded to the bug
  report API with an 8-per-minute rate limit. In-flight reports are tracked and given a 5-second
  grace period at shutdown, all API clients share one connection pool, and the update prompt never
  blocks scripted runs (the Windows message box is skipped when the console is redirected).
- **Tests.** The suite grew from 89 to **336 tests**: 286 in `SimpleXisoDrive.Tests` and 50 in
  `SimpleXisoDrive.Unix.Tests` (FUSE struct layouts, resolver, mount arguments, path/time/directory
  helpers, option validation).
- **Documentation.** Added a side menu for both renderings of this documentation: `docs/_Sidebar.md`
  for the GitHub wiki and `docs/_data/navigation.yml` + `docs/_layouts/default.html` for the published
  site (GitHub Pages). All pages were refreshed for the cross-platform, CHD and `--image-iso`
  releases.

## 1.3.0 - 2026-09-13

- Added the `-i`/`--image-iso` option: the mount also exposes a virtual read-only `image.iso` file
  at the volume root for emulators that only accept a disc image (such as xemu). Plain ISO/XISO and
  CISO inputs are served on demand (CISO blocks are decompressed through the XISOSharp block
  device), and a ZArchive directory tree is synthesized into an XISO entirely in memory (XISOSharp
  layout primitives; file data read from the archive on demand) with no extraction. The synthesized
  image is byte-identical to `XisoWriter.PackFromDirectory` output for the same tree.
- Added ZArchive (`.zar`) mounting: directory-tree archives expose their game files directly, and
  archives containing a single embedded XISO image mount that image — all with on-demand zstd block
  decompression and no extraction to disk.
- Introduced the `IVfsVolume`/`IVfsEntry` abstraction with `XisoVfsVolume`, `ZarVfsVolume`,
  `ReaderOwningVfsVolume`, and `VfsVolumeFactory`; `VfsContainer` is now a facade over the selected
  volume.
- ZAR mounting uses the ZArchiveSharp 1.3.0 mount-host reader API: node-handle enumeration
  (`TryGetDirEntry`), canonical names (`TryGetNodeName`), computed volume size
  (`TotalUncompressedSize`), seekable entry streams (`OpenRead`), shared-read opens, and typed
  `ZArchiveOpenFailure` reasons surfaced in errors.
- Migrated all XDVDFS parsing to XISOSharp 1.2.0: path images use a keep-open `XisoExplorer` and
  embedded images use the `XisoReader` stream APIs (including rebuilt sector-0 images). The in-repo
  parser (`IsoSt`, `VolumeDescriptor`, `FileEntry`, `XisoFsFileAttributes`) was removed.
- Path resolution now recognizes `.iso`, `.xiso`, `.cso` (single or split `.1.cso` sets), and
  `.zar`, and a renamed ZArchive still mounts.
- Added GitHub Actions CI: every push/pull request builds and tests on Windows and uploads TRX and
  coverage artifacts; the `release_*` workflow verifies the tag against `AssemblyVersion`, packages
  framework-dependent single-file `win-x64`/`win-arm64` zips, and creates the GitHub release.
- Expanded the test suite from 43 to 110 tests, covering XISO directory trees and reads, the Dokan
  operation layer, stream-backed embedded images, parallel reads, resolver CISO cases, the virtual
  `image.iso` sources (including byte-identity with the whole-image writer), and API key
  decryption.
- Added tests for ZAR volumes, embedded XISO images, format detection, and the extended resolver.
- Hardened error handling: locked or unreadable images surface real I/O errors instead of "invalid
  image", ZArchive open failures keep their type (missing/denied/read error vs bad format), and the
  embedded-ISO probe no longer leaks the archive reader on an unexpected error.
- The API key for the bug report and stats endpoints is no longer stored in plain text: it is
  double-encrypted in the assembly (AES-256-CBC over a SHA-256 XOR layer) and decrypted once at
  startup.
- Fixed a console race where the drag-and-drop key watcher could swallow the key press meant for an
  error prompt.
- Integrated Serilog logging with console and rolling file sinks.
- Enriched bug reports with environment details (OS, architecture, bitness, paths).
- Added regex match timeouts for wildcard searches and version parsing.
- Added analyzer packages (Meziantou, Roslynator) and analyzer-driven cleanups.
- Added XML documentation to the public API.

## 1.2.0 - 2026-06-21

- Added XGD3 (`0x02080000`) and GLOBAL (`0x0FD90000`) partition offset support.
- Added a unit test project covering `FileEntry`, `IsoSt`, `VolumeDescriptor`, path resolution, and
  attribute flags.
- Refactored `IsoSt` for testability with an internal stream constructor.
- Removed the `Launch.bat` helper.
- Bumped version and added company metadata to both projects.
- Cleaned up project references.

## 1.1.0 - 2026-04-12

- Renamed `ErrorLogger` to `BugReport` and added `StatsService` for anonymous launch statistics.
- Improved error handling and logging across mount and file operations.
- Added the green CRT console theme.
- Strengthened binary tree safety (cycle detection and iteration limits).
- Pointed the statistics endpoint at the production service.

## 1.0.3 - 2026-02-13

- Refactored `InvalidImageException` handling for clearer error reporting.
- Added command-line hints when a path cannot be resolved (directory, missing extension, quoting).
- Improved recovery when the specified ISO path is invalid.

## 1.0.2 - 2026-01-24

- Reduced the update checker HTTP timeout to 5 seconds.
- Made `CheckForUpdateAsync` awaitable.

## 1.0.1 - 2026-01-24

- Added Windows ARM64 support.
- Updated documentation for multi-architecture releases.

## 1.0.0 - 2026-01-24

Initial public release lineage:

- .NET 10 port with `LangVersion` 14 and `global.json` SDK pinning.
- Dokan-based mounting with write protection and administrator-aware options.
- Drive letter normalization (`Z:\` -> `Z:`).
- Drag-and-drop mounting with automatic drive letter selection (`M:` through `R:`) and key-press
  unmounting.
- Update checker, error logger, and read-only XDVDFS parsing with binary tree traversal.
- Support for standard Xbox ISOs and rebuilt XISO images.

---

## Version scheme

Versions follow `major.minor.patch`. Release tags use the `release_<version>` prefix (for example
`release_1.4.0`), and the update checker extracts the numeric portion from GitHub tag names when
comparing against the installed version.

See [Building](Building#versioning) for where to bump the version.
