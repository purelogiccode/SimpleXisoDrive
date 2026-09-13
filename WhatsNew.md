# What's New in 1.3.0 (since 1.2.0)

Prepared for the `release_1.3.0` tag. Framework-dependent `win-x64` and `win-arm64` builds require
the .NET 10.0 Runtime (the base runtime; the Desktop Runtime also works but is not required).

This release adds ZArchive (`.zar`) mounting, CISO (`.cso`) image support, moves all XDVDFS parsing
to the **XISOSharp 1.2.0** library, modernizes ZArchive reads on **ZArchiveSharp 1.3.0**, and adds
CI plus a tag-driven release pipeline.

## Mounting

- **ZArchive support.** `.zar` files mount directly: directory-tree archives expose the stored game
  files, and an archive holding a single XISO image mounts that image. Content is read on demand
  through a zstd block cache; nothing is extracted to disk.
- **CISO support.** Compressed `.cso` images mount like plain ISOs, including split `.1.cso` part
  sets; the resolver recognizes `.cso` and treats a numbered part set as a single image.
- **Renamed archives still mount.** A valid ZArchive with any extension (including `.iso`) falls
  back to the archive path when the XISO probe fails.
- **ZArchiveSharp 1.3.0 reader API.** Node-handle enumeration (`TryGetDirEntry`), canonical names
  (`TryGetNodeName`), computed volume size (`TotalUncompressedSize`), seekable entry streams
  (`OpenRead`), shared-read opens, and typed `ZArchiveOpenFailure` reasons in error messages.

## XISO parsing

- **Parsing delegated to XISOSharp 1.2.0.** The in-repo parser (`IsoSt`, `VolumeDescriptor`,
  `FileEntry`, `XisoFsFileAttributes`) was removed. Path-based images use a keep-open `XisoExplorer`
  with a shared read/write handle; images embedded in archives use the `XisoReader` stream APIs.
- **Rebuilt sector-0 images** are detected alongside the standard sector-32 layouts (plain, GLOBAL,
  XGD3, XGD2 hybrid, XGD1), and the app reports the library's parsed attributes, size, and creation
  time.

## Reliability

- **Truthful I/O errors.** Locked or unreadable images now surface the real `IOException`; only
  format failures are reported as "not a valid image". ZArchive open failures keep their type
  (missing file, access denied/read error, invalid path, or bad format).
- **No handle leaks.** The embedded-ISO probe disposes the archive reader when the probe itself
  errors, and the probe stream on a failed validation.
- Directory walks use XISOSharp's hardened TOC traversal: cycle detection, per-table entry limits,
  and rejection of separator-bearing names.
- Reads are clamped to each entry's size, never the image length.
- Every Dokan operation still contains failures, logs them, and returns an error status instead of
  propagating.
- The shared console key-press watcher no longer swallows the key meant for an error prompt.
- The bug report / stats API key is no longer stored in plain text: it is double-encrypted in the
  assembly (AES-256-CBC over a SHA-256 XOR layer) and decrypted once at startup.

## Tests

- The suite grew from 43 to **89 tests**.
- `XisoVfsVolumeTreeTests` covers multi-file and nested images, empty files/directories, reads
  across sector boundaries, clamped reads, attribute mapping, descriptor FILETIMEs, and parallel
  reads in path and stream modes.
- `XboxIsoVfsDokanTests` exercises the Dokan operation layer directly with `MockDokanFileInfo` —
  volume information, `.`/`..` listings, wildcard filtering, `CreateFile`/`ReadFile` semantics, path
  normalization, and read-only denials — with no Dokan driver installed.
- `ResolveImagePathTests` and `VfsContainerTests` cover `.cso` resolution (including split sets),
  locked/missing images, and `ApiKeyProviderTests` verifies the decrypted key without storing it.
- `TestImageFactory` now builds arbitrary nested trees (with `TestImageEntry`) for standard
  sector-32 and rebuilt sector-0 layouts.

## CI and releases

- `.github/workflows/ci.yml` builds and tests every push/pull request to `master` on
  `windows-latest` and uploads TRX results plus Cobertura coverage.
- `.github/workflows/release.yml` runs on `release_*` tags: it verifies the tag against
  `<AssemblyVersion>`, runs the suite, publishes framework-dependent single-file executables for
  `win-x64` and `win-arm64`, packs each as `release_<version>_<rid>.zip` containing only
  `SimpleXisoDrive.exe`, and creates the GitHub release.
- The workflow can also be dispatched manually to produce the same zips as artifacts.

## Docs

- Updated `ReadMe.md` with a CI badge and the building guide; refreshed Architecture,
  Virtual File System, XDVDFS Format, Glossary, Testing, Troubleshooting, Contributing, and Release
  History for the library-based architecture and the new pipeline.
