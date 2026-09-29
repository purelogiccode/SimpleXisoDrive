# What's New in 1.4.0 (since 1.3.0)

Prepared for the `release_1.4.0` tag. Framework-dependent `win-x64` and `win-arm64` builds require
the .NET 10.0 Runtime (the base runtime; the Desktop Runtime also works but is not required).

## Virtual image.iso (`--image-iso`)

- **New `-i` / `--image-iso` option** adds a synthetic read-only `image.iso` file at the mount root
  while the normal tree stays browsable, so emulators that only accept a disc image (such as xemu)
  can open `<mount>\image.iso`.
- Plain ISO/XISO inputs and CISO `.cso` images (including split `.1.cso` sets) are served on demand:
  CISO blocks are decompressed through the XISOSharp block device, nothing is extracted.
- A ZArchive with a single embedded XISO is served from the embedded image on demand.
- A ZArchive directory tree is synthesized into an XISO **in memory**: the XDVDFS layout
  (volume descriptor, directory tables and file extents) is built with XISOSharp's public layout
  primitives and file data is read from the archive on demand. Nothing is extracted, the mount
  appears immediately, and the synthesized image is byte-identical to
  `XisoWriter.PackFromDirectory` output for the same tree.
- When the mounted image already contains a real `image.iso` file, that file is shown instead of the
  synthetic one.

## Xbox ISO CHD support

- **New `.chd` mounting.** Xbox ISO images stored as CHD are mounted read-only through the
  **CHDSharp** library: hunks are decompressed on demand (CHD V1–V5, all codecs) and the source file
  is never modified.
- **Xbox ISO only.** CD and GD-ROM CHDs are rejected up front, and the decompressed image must parse
  as XDVDFS; anything else fails with
  `"<path>" is not an Xbox ISO CHD (XDVDFS filesystem not found).`
- **`--image-iso` works for CHDs.** The virtual `image.iso` serves the decompressed Xbox image
  (a second independent CHD reader, so raw-image reads never race the volume stream).
- **Renamed CHDs still mount.** A valid CHD with any extension falls back to the CHD path when the
  XISO probe fails.
- The resolver recognizes `.chd` for extensionless paths and directory lookups.

## Tests

- The suite grew from 89 to **119 tests**.
- `ImageIsoVfsVolumeTests` covers the synthetic entry (listing, lookup, reads, clamping, real-file
  precedence, disposal), `StreamRawImageSourceTests` covers seeked and parallel reads plus disposal,
  `VirtualXisoImageSourceTests` proves the synthesized ZAR image is byte-identical to the XISOSharp
  whole-image writer for nested trees (sector-crossing files, empty files, empty directories) and
  validates unaligned reads and XISOSharp readability, `ChdVfsContainerTests` encodes real CHDs with
  the CHDSharp encoder and mounts them (rebuilt and standard layouts, trees, `--image-iso`,
  non-Xbox rejection, renamed fallback), and `VfsContainerTests` mounts `.iso`, `.cso`,
  embedded-XISO `.zar` and tree `.zar` inputs with the option and validates the exposed image.

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
