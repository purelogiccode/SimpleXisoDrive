# Release History

This page summarizes tagged releases and notable changes. Source: repository tags and commit
history.

| Version | Tag | Date |
| --- | --- | --- |
| Unreleased | `master` | 2026-09-12 |
| 1.2.0 | `release_1.2.0` | 2026-06-21 |
| 1.1.0 | `release_1.1.0` | 2026-04-12 |
| 1.0.3 | `release_1.0.3` | 2026-02-13 |
| 1.0.2 | `release_1.0.2` | 2026-01-24 |
| 1.0.1 | `release_1.0.1` | 2026-01-24 |
| 1.0.0 | `release_1.0.0` | 2026-01-24 |

---

## Unreleased (after 1.2.0)

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
- Path resolution now recognizes `.iso`, `.xiso`, and `.zar`, and a renamed ZArchive still mounts.
- Added tests for ZAR volumes, embedded XISO images, format detection, and the extended resolver.
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
`release_1.2.0`), and the update checker extracts the numeric portion from GitHub tag names when
comparing against the installed version.

See [Building](Building#versioning) for where to bump the version.
