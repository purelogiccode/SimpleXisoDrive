# Testing

SimpleXisoDrive has two xUnit test projects covering the parsing, file system, archive,
path-resolution and FUSE-interop logic. The tests run without Dokan or FUSE installed, and without
any real image file.

---

## Test projects

| Property | Value |
| --- | --- |
| Projects | `SimpleXisoDrive.Tests` (application/core), `SimpleXisoDrive.Unix.Tests` (FUSE interop) |
| Frameworks | `net10.0-windows` (core suite), `net10.0` (FUSE suite) |
| Test framework | xUnit 2.9.3 |
| Runner | `xunit.runner.visualstudio` 4.0.0 |
| Coverage collector | `coverlet.collector` 10.0.1 |
| Test count | 250 (version 1.4.0): 234 core + 16 FUSE |

The application exposes internals to the test projects through `InternalsVisibleTo` in
`SimpleXisoDrive/AssemblyInfo.cs`, `SimpleXisoDrive.Core/AssemblyInfo.cs` and
`SimpleXisoDrive.Unix/AssemblyInfo.cs`, which allows tests to use internal constructors
(such as `XisoVfsVolume(Stream, string)`), decorators, test doubles and FUSE helpers.

---

## Running the tests

```shell
# Run everything
dotnet test CSharp_SimpleXisoDrive.sln

# Run only one test project
dotnet test SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj
dotnet test SimpleXisoDrive.Unix.Tests/SimpleXisoDrive.Unix.Tests.csproj

# Release configuration
dotnet test CSharp_SimpleXisoDrive.sln -c Release

# Filter by test name
dotnet test SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj --filter "FullyQualifiedName~XisoVfsVolume"

# Verbose output
dotnet test CSharp_SimpleXisoDrive.sln --logger "console;verbosity=detailed"
```

A healthy run reports `Passed: 234, Failed: 0` for `SimpleXisoDrive.Tests.dll` and
`Passed: 16, Failed: 0` for `SimpleXisoDrive.Unix.Tests.dll`. The same suites run in
CI on every push and pull request (see [Building](Building#continuous-integration)); the workflow
always uploads the `.trx` results and the Cobertura coverage report as artifacts.

---

## What is covered

| Test file | Area | Examples |
| --- | --- | --- |
| `XisoVfsVolumeTests` | XISO volume over XISOSharp | Standard sector-32 and rebuilt sector-0 images, entry lookup, directory listing, reads at offsets and past EOF, attribute mapping, descriptor creation time, image-handle release on dispose, stream-backed (embedded) images, path variants (`/`, `//`), foreign entries, post-dispose lookups, listing cache identity, missing files |
| `XisoVfsVolumeTreeTests` | Directory trees and reads | Multi-file and nested-directory images, separators and case-insensitivity, empty files/directories, reads across sector boundaries, clamped reads, attribute-flag mapping, descriptor FILETIME values, parallel reads (path and stream modes), idempotent dispose |
| `XboxIsoVfsDokanTests` | Dokan operation layer | Volume information and free space, `.`/`..` listings, wildcard filtering (`*`, `?`, literal, no-match), metadata for files/directories/root, `CreateFile` access and creation modes, `ReadFile` offsets/clamping/directories/empty buffers/missing entries, normalized special segments, read-only denials, locking, alternate-stream reporting, security descriptors for files, directories and missing entries |
| `ApiKeyProviderTests` | API key protection | Deterministic decryption of the double-encrypted key, expected key digest (without storing the key), preload behavior |
| `ResolveImagePathTests` | CLI path resolution | Existing file, extension appending (`.iso`, `.xiso`, `.cso`, `.chd`, `.zar`), split CISO sets, directory with exactly one image, multiple/zero images, current-directory lookup |
| `VfsContainerTests` | Volume facade and format detection | ZAR tree mount, embedded XISO mount (including nested content), renamed `.zar` fallback, invalid archive errors, plain `.iso` and `.xiso` mounts, locked/missing images surface I/O errors, `--image-iso` mounts for `.iso`, `.cso`, embedded-XISO `.zar` and tree `.zar` inputs |
| `ChdVfsContainerTests` | Xbox ISO CHD mounts | CHD mount of rebuilt and standard-layout images, nested directory trees, `--image-iso` decompressed image, non-Xbox CHD rejection, renamed `.chd` fallback, missing file errors |
| `ImageIsoVfsVolumeTests` | Virtual `image.iso` decorator | Root listing and case-insensitive lookup, raw reads at offsets with clamping, inner tree entries remain readable, a real `image.iso` entry wins, metadata delegation, size accumulation, subdirectory listings, null arguments, rewritten exception behavior, idempotent disposal |
| `ReaderOwningVfsVolumeTests` | Reader-owning decorator | Property/lookup/listing/read delegation, disposal order (inner volume then owner), owner disposal when the inner volume throws, swallowed owner failures, rethrown inner failures |
| `StreamRawImageSourceTests` | Raw stream source | Non-seekable/null rejection, offset reads across sector boundaries, end clamping, empty buffers/streams, failure degradation to zero, disposal once, swallowed stream-disposal failures, parallel reads |
| `VirtualXisoImageSourceTests` | Synthesized ZAR XISO | Byte-identical to `XisoWriter.PackFromDirectory` for nested trees (sector-crossing files, empty files/directories), XISOSharp readability, unaligned reads across extents, unwritten-gap zeros, out-of-range reads, sector-aligned length, descriptor FILETIME fallback, caller-owned reader survival, disposal |
| `ZarVfsVolumeTests` | ZArchive volume | Tree listing, case-insensitive nested lookup, file reads at offsets, directory reads, multi-block reads, volume size, invalid/missing archives, path variants, attribute mapping, invalid ranges, foreign entries, idempotent dispose |
| `RequestModelTests` | JSON wire format | `applicationId`/`version` and `message`/`applicationName`/... property names for the stats and bug report APIs, default values |
| `ConsoleKeyPressTests` | Interactive wait | Redirected input completes the shared wait immediately with a default key instead of blocking |
| `LoggingSetupTests` | Logging surface | Console level switch defaults to Information and can be raised (global sinks intentionally not attached in tests) |
| `SerilogDokanLoggerTests` | Dokan log adapter | Debug-enabled probing and level-by-level forwarding of Dokan messages into a collecting Serilog sink |
| `BugReportSinkTests` | Sink guard rails | Sub-Warning events are ignored and malformed events are swallowed (Warning+ intentionally not emitted: it would call the live API) |
| `CheckAccessTests` | Privilege probe | Administrator probe returns without throwing on any privilege level |
| `SimpleXisoDrive.Unix.Tests` | FUSE 3 interop | `FuseStructLayoutTests` pins `fuse_args` and the Linux/macFUSE `fuse_operations` field order and size, and the Cdecl callback convention; `FuseInteropTests` covers resolver idempotence, missing-library probing, availability guidance and loader failures; `PosixErrorTests` pins the errno values |
| `TestImageFactory` / `TestImageEntry` | Shared test fixtures | Builders for minimal rebuilt (sector 0) and standard (sector 32) images with arbitrary nested file/directory trees, raw attribute bytes, and descriptor FILETIME values |
| `FakeVfsVolume` / `FakeVfsEntry` / `TrackingRawImageSource` / `RecordingDisposable` | Shared test doubles | Configurable failure injection and disposal counting for decorator tests |
| `InvalidImageExceptionTests` | Exception contract | Default, message, null-message and inner-exception constructors, inheritance, catchability |

### Testing techniques

- **In-memory images**: `TestImageFactory` builds real XDVDFS byte layouts (descriptor, directory
  tables, sector-aligned file data) for arbitrary trees, wrapped in `MemoryStream` via the internal
  `XisoVfsVolume(Stream, string)` constructor or written to temporary `.iso`/`.xiso` files.
- **Dokan layer without a driver**: `XboxIsoVfsDokanTests` calls the `IDokanOperations`
  implementation directly with DokanNet's `MockDokanFileInfo`, so path normalization, handle
  context, clamping, and read-only denials are verified without installing Dokan.
- **Real archives**: `ZarVfsVolumeTests`/`VfsContainerTests` pack small trees and embedded XISO
  images with `ZArchiveWriter` into temporary `.zar` files, so the real reader (including zstd
  decompression) is exercised end to end.
- **Real CHDs**: `ChdVfsContainerTests` encodes `TestImageFactory` images with `ChdEncoder` (the
  uncompressed codec, for speed) into temporary `.chd` files, so the real CHDSharp reader,
  decompression path and XDVDFS validation are exercised end to end.
- **Temporary files**: `ResolveImagePathTests`, `XisoVfsVolumeTests`, and the Dokan tests create and
  clean up temp files/directories; `ResolveImagePathTests` also temporarily changes
  `Environment.CurrentDirectory`.
- **ABI pinning**: `FuseStructLayoutTests` uses `Marshal.SizeOf`/`Marshal.OffsetOf` to pin the
  native `fuse_args`/`fuse_operations` layouts and delegate calling conventions, so a wrong field
  order fails the build's tests instead of crashing a real mount. Loader-dependent assertions
  (library probing, availability guidance) run only on hosts without FUSE 3.
- **No driver dependency**: nothing in the test suite requires a mounted volume, FUSE, or admin rights.

---

## Coverage

Collect coverage with coverlet:

```shell
dotnet test SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj --collect:"XPlat Code Coverage"
```

Results are written under `SimpleXisoDrive.Tests/TestResults/` as Cobertura XML.

Note that Dokan callbacks (`XboxIsoVfsDokan`), the application entry points, the FUSE callbacks
themselves (they need a real mount), and the live HTTP services are not covered by unit tests
because they require a driver, a running process, a kernel mount, or network access. The FUSE
struct layouts, resolver and error constants are covered by `SimpleXisoDrive.Unix.Tests`.

---

## Adding tests

1. Add a new test class to the matching project (`SimpleXisoDrive.Tests` for application and core
   code, `SimpleXisoDrive.Unix.Tests` for FUSE interop), or extend an existing one by topic.
2. Follow the existing naming convention: `MethodOrFeature_Scenario_ExpectedResult`, for example
   `ReadInternal_CalculatesEntrySize_NoPaddingNeeded`.
3. Prefer hand-built byte layouts over external fixture files so tests stay self-contained.
4. Clean up any temporary files and restore global state (such as `Environment.CurrentDirectory`) in
   `finally` blocks.
5. Keep tests deterministic: no network calls, no real drives, no timing assumptions.

Before submitting changes, run:

```shell
dotnet build CSharp_SimpleXisoDrive.sln -c Release
dotnet test CSharp_SimpleXisoDrive.sln -c Release
```

See [Contributing](Contributing) for the full checklist.
