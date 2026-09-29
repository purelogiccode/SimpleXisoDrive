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
| Test count | 317 (version 1.4.0): 282 core + 35 FUSE |

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

A healthy run reports `Passed: 282, Failed: 0` for `SimpleXisoDrive.Tests.dll` and
`Passed: 35, Failed: 0` for `SimpleXisoDrive.Unix.Tests.dll`. The same suites run in
CI on every push and pull request (see [Building](Building#continuous-integration)); the workflow
always uploads the `.trx` results and the Cobertura coverage report as artifacts.

---

## What is covered

| Test file | Area | Examples |
| --- | --- | --- |
| `XisoVfsVolumeTests` | XISO volume over XISOSharp | Standard sector-32 and rebuilt sector-0 images, entry lookup, directory listing, reads at offsets and past EOF, attribute mapping, descriptor creation time, image-handle release on dispose, stream-backed (embedded) images, path variants (`/`, `//`), foreign entries, post-dispose lookups, listing cache identity, missing files, null/empty path guards, I/O failures that are not masked as invalid images |
| `XisoVfsVolumeTreeTests` | Directory trees and reads | Multi-file and nested-directory images, separators and case-insensitivity, empty files/directories, reads across sector boundaries, clamped reads, attribute-flag mapping, descriptor FILETIME values, parallel reads (path and stream modes), idempotent dispose |
| `XboxIsoVfsDokanTests` | Dokan operation layer | Volume information and free space, `.`/`..` listings, wildcard filtering (`*`, `*.*` including extensionless files, `?`, literal, no-match), metadata for files/directories/root (root reports the volume label), `CreateFile` access and creation modes (failed opens leave no handle context), `ReadFile` offsets/clamping/directories/empty buffers/missing entries/negative offsets (`InvalidParameter`), normalized special and interior path segments (including the context-less fallback), read-only denials, successful no-op flush, locking, alternate-stream reporting, security descriptors for files/directories and `FileNotFound` for missing entries |
| `ApiKeyProviderTests` | API key protection | Deterministic decryption of the double-encrypted key, expected key digest (without storing the key), preload behavior |
| `ResolveImagePathTests` | CLI path resolution | Existing file, extension appending (`.iso`, `.xiso`, `.cso`, `.chd`, `.zar`), split CISO sets, directory with exactly one image, multiple/zero images, current-directory lookup, null-path guard, case-insensitive extension resolution |
| `VfsContainerTests` | Volume facade and format detection | ZAR tree mount, embedded XISO mount (including nested content), renamed `.zar` fallback, content-detection fallbacks (ISO renamed to `.chd`/`.zar`, ZAR renamed to `.chd`), invalid archive errors, null/empty path guards, plain `.iso` and `.xiso` mounts, locked/missing images surface I/O errors, `--image-iso` mounts for `.iso`, `.cso`, embedded-XISO `.zar` and tree `.zar` inputs |
| `ChdVfsContainerTests` | Xbox ISO CHD mounts | CHD mount of rebuilt and standard-layout images, nested directory trees, `--image-iso` decompressed image, non-Xbox CHD rejection, renamed `.chd` and renamed `.zar` fallbacks, missing file errors |
| `ImageIsoVfsVolumeTests` | Virtual `image.iso` decorator | Root listing (cached instance) and case-insensitive lookup, raw reads at offsets with clamping, inner tree entries remain readable, a real `image.iso` entry wins, metadata delegation, size policy (no double count for the mounted image, added for synthesized ZArchive images), subdirectory listings, null arguments, rewritten exception behavior, idempotent disposal |
| `ReaderOwningVfsVolumeTests` | Reader-owning decorator | Property/lookup/listing/read delegation, disposal order (inner volume then owner), owner disposal when the inner volume throws, swallowed owner failures, rethrown inner failures |
| `StreamRawImageSourceTests` | Raw stream source | Non-seekable/null rejection, offset reads across sector boundaries, end clamping, empty buffers/streams, failure degradation to zero, disposal once, swallowed stream-disposal failures, parallel reads |
| `VirtualXisoImageSourceTests` | Synthesized ZAR XISO | Byte-identical to `XisoWriter.PackFromDirectory` for nested trees (sector-crossing files, empty files/directories), XISOSharp readability, unaligned reads across extents, unwritten-gap zeros, out-of-range reads, sector-aligned length, descriptor last-write-time fallback, caller-owned reader survival, disposal |
| `ZarVfsVolumeTests` | ZArchive volume | Tree listing, case-insensitive nested lookup, file reads at offsets, directory reads, multi-block reads, volume size, invalid/missing archives, path variants, attribute mapping, invalid ranges, foreign entries, archive last-write timestamp, idempotent dispose |
| `RequestModelTests` | JSON wire format | `applicationId`/`version` and `message`/`applicationName`/... property names for the stats and bug report APIs, default values |
| `ConsoleKeyPressTests` | Interactive wait | Redirected input completes the shared wait immediately with a default key instead of blocking; `Reset` starts a fresh wait |
| `LoggingSetupTests` | Logging surface | Console level switch defaults to Information and can be raised (the global logger is deliberately not configured in tests — attaching the real sinks would forward events to the live bug report API, so idempotence is verified by inspection) |
| `SerilogDokanLoggerTests` | Dokan log adapter | Debug-enabled probing and level-by-level forwarding of Dokan messages into a collecting Serilog sink |
| `BugReportSinkTests` | Sink guard rails | Sub-Warning events are ignored and malformed events are swallowed (Warning+ intentionally not emitted: it would call the live API) |
| `BugReportHttpTests` | Bug report API | Stubbed request shaping (endpoint, method, `X-API-KEY` header, JSON body) and the idle pending-reports wait |
| `StatsServiceHttpTests` | Stats API | Stubbed request shaping (endpoint, bearer token, `applicationId`/`version` body) and rejection handling |
| `UpdateCheckerHttpTests` | Update check API | Stubbed endpoint/user-agent, malformed response swallowing, error-status handling and the update-available prompt callback (no live traffic, no prompts) |
| `WindowsUpdatePromptTests` | Update message box | Message text includes both versions, the release URL and the download question |
| `CheckAccessTests` | Privilege probe | Administrator probe returns without throwing on any privilege level |
| `SimpleXisoDrive.Unix.Tests` | FUSE 3 interop | `FuseStructLayoutTests` pins `fuse_args` and the Linux/macFUSE `fuse_operations` field order and size, and the Cdecl callback convention; `FuseInteropTests` covers resolver idempotence, version-ordered library candidates, missing-library probing, availability guidance and loader failures; `FuseMountArgumentsTests` covers `fsname`/`volname` label exposure, debug flags and label sanitization; `FuseHelperTests` covers native path conversion, Unix time conversion and directory fill offsets; `PosixErrorTests` pins the errno values; `ProgramNameTests` covers the usage-text executable name |
| `TestImageFactory` / `TestImageEntry` | Shared test fixtures | Builders for minimal rebuilt (sector 0) and standard (sector 32) images with arbitrary nested file/directory trees, raw attribute bytes, and descriptor FILETIME values |
| `FakeVfsVolume` / `FakeVfsEntry` / `TrackingRawImageSource` / `RecordingDisposable` | Shared test doubles | Configurable failure injection and disposal counting for decorator tests |
| `StubHttpMessageHandler` | Shared HTTP double | Records outbound method/URI/headers/body and returns a canned response, so API services are tested without live traffic |
| `CommandLineParserTests` | Windows command line | Single-argument drag-and-drop mode, case-insensitive option flags, empty/invalid image paths, unknown options (with usage hint), extra positional arguments, null arrays |
| `DriveLetterSelectorTests` | Drive letter choice | Preferred `M`-`R` order and that the selected letter is free and valid |
| `DokanInstallationTests` | Dokan paths | Library and driver paths under the system directory |
| `UsageTextTests` | Usage text | Executable name and every documented option appear in the usage text |
| `BoundedCacheTests` | Cache budget | Round-trip get/set, case-insensitive keys, stopping new entries at the limit, updating existing entries at the limit, non-positive limit rejection |
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
- **Stubbed HTTP, no live traffic**: the API services expose internal overloads that take an
  `HttpClient`. `StubHttpMessageHandler` records the outbound method, URI, headers and body and
  returns a canned response, so request shaping and failure handling are verified without network
  access (`BugReportHttpTests`, `StatsServiceHttpTests`, `UpdateCheckerHttpTests`).
- **No driver dependency**: nothing in the test suite requires a mounted volume, FUSE, or admin rights.

---

## Coverage

Collect coverage with coverlet:

```shell
dotnet test SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj --collect:"XPlat Code Coverage"
```

Results are written under `SimpleXisoDrive.Tests/TestResults/` as Cobertura XML.

Note that the Dokan callbacks (`XboxIsoVfsDokan` runs against in-memory volumes, but real driver
behavior needs a driver) and the FUSE callbacks themselves (they need a real mount) are not covered
by unit tests, nor are the `Program.Main` entry points themselves. The logic extracted from the
entry points (command line, drive letters, usage text, Dokan paths) is covered, as are the front-end
Unix helpers (path conversion, timestamps, directory fill offsets) and the HTTP services through
stubbed clients rather than live traffic. The FUSE struct layouts, resolver and error constants are
covered by `SimpleXisoDrive.Unix.Tests`.

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
