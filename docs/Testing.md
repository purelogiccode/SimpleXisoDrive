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
| Test count | 336 (version 1.4.0): 286 core + 50 FUSE |

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

A healthy run reports `Passed: 394, Failed: 0` for `SimpleXisoDrive.Tests.dll` and
`Passed: 71, Failed: 0` for `SimpleXisoDrive.Unix.Tests.dll`. The same suites run in
CI on every push and pull request (see [Building](Building#continuous-integration)); the workflow
always uploads the `.trx` results and the Cobertura coverage report as artifacts.

---

## What is covered

| Test file | Area | Examples |
| --- | --- | --- |
| `XisoVfsVolumeTests` | XISO volume over XISOSharp | Standard sector-32 and rebuilt sector-0 images, entry lookup, directory listing, reads at offsets and past EOF, attribute mapping, descriptor creation time, image-handle release on dispose, stream-backed (embedded) images, path variants (`/`, `//`), foreign entries, post-dispose lookups, listing cache identity, missing files, null/empty path guards, I/O failures that are not masked as invalid images |
| `XisoVfsVolumeTreeTests` | Directory trees and reads | Multi-file and nested-directory images, separators and case-insensitivity, empty files/directories, reads across sector boundaries, clamped reads, attribute-flag mapping, descriptor FILETIME values, parallel reads (path and stream modes), idempotent dispose |
| `XboxIsoVfsDokanTests` | Dokan operation layer | Volume information and free space, `.`/`..` listings, wildcard filtering (`*`, `*.*` including extensionless files, `?`, literal, no-match), metadata for files/directories/root (root reports the volume label), `CreateFile` access and creation modes (failed opens leave no handle context, `OpenOrCreate` opens existing files), `ReadFile` offsets/clamping/directories/empty buffers/missing entries (`FileNotFound`)/negative offsets (`InvalidParameter`), normalized special and interior path segments (including the context-less fallback), the read-only access allow list (write-capable bits including `GenericAll`/`MaximumAllowed`/`ChangePermissions` denied, read-only bits allowed), successful no-op flush, locking, alternate-stream reporting, security descriptors for files/directories and `FileNotFound` for missing entries, and failure injection for listing/open/security/read callbacks |
| `ApiKeyProviderTests` | API key protection | Deterministic decryption of the double-encrypted key, expected key digest (without storing the key), preload behavior |
| `ResolveImagePathTests` | CLI path resolution | Existing file, extension appending (`.iso`, `.xiso`, `.cso`, `.chd`, `.zar`), split CISO sets, directory with exactly one image, multiple/zero images, current-directory lookup, null-path guard, case-insensitive extension resolution (the class runs in a non-parallel collection because it mutates the process-wide current directory) |
| `ImagePathResolverExtraTests` | CLI path resolution edge cases | Whitespace-only paths, `.iso`/`.xiso` extension preference, first `.1.cso` part resolution, continuation-only directories (`.2`, `.10`, `.99`), dot-containing names |
| `VfsContainerTests` | Volume facade and format detection | ZAR tree mount, embedded XISO mount (including nested content), renamed `.zar` fallback, content-detection fallbacks (ISO renamed to `.chd`/`.zar`, ZAR renamed to `.chd`), invalid archive errors, null/empty path guards, plain `.iso` and `.xiso` mounts, locked/missing images surface I/O errors, `--image-iso` mounts for `.iso`, `.cso`, embedded-XISO `.zar` and tree `.zar` inputs |
| `VfsContainerLifecycleTests` | Volume facade lifecycle | Null/empty path guards, idempotent disposal, image-handle release, missing-path lookups, metadata delegation |
| `ChdVfsContainerTests` | Xbox ISO CHD mounts | CHD mount of rebuilt and standard-layout images, nested directory trees, `--image-iso` decompressed image, non-Xbox CHD rejection, renamed `.chd` and renamed `.zar` fallbacks, missing file errors |
| `ImageIsoVfsVolumeTests` | Virtual `image.iso` decorator | Root listing (cached instance) and case-insensitive lookup, raw reads at offsets with clamping, inner tree entries remain readable, a real `image.iso` entry wins, metadata delegation, size policy (no double count for the mounted image, added for synthesized ZArchive images), subdirectory listings, null arguments, rewritten exception behavior, idempotent disposal |
| `ReaderOwningVfsVolumeTests` | Reader-owning decorator | Property/lookup/listing/read delegation, disposal order (inner volume then owner), owner disposal when the inner volume throws, swallowed owner failures, rethrown inner failures |
| `StreamRawImageSourceTests` | Raw stream source | Non-seekable/null rejection, offset reads across sector boundaries, end clamping, empty buffers/streams, stream failures propagate as `IOException` instead of a silent zero-byte read, disposal once, swallowed stream-disposal failures, parallel reads |
| `VirtualXisoImageSourceTests` | Synthesized ZAR XISO | Byte-identical to `XisoWriter.PackFromDirectory` for nested trees (sector-crossing files, empty files/directories), XISOSharp readability, unaligned reads across extents, unwritten-gap zeros, out-of-range reads, sector-aligned length, descriptor last-write-time fallback, caller-owned reader survival, disposal |
| `ZarVfsVolumeTests` | ZArchive volume | Tree listing, case-insensitive nested lookup, file reads at offsets, directory reads, multi-block reads, volume size, invalid/missing archives, path variants, attribute mapping, invalid ranges, foreign entries, archive last-write timestamp, entry and listing cache identity, nested directory listing, idempotent dispose, concurrent lookups/reads (the archive reader is serialized) |
| `RequestModelTests` | JSON wire format | `applicationId`/`version` and `message`/`applicationName`/... property names for the stats and bug report APIs, default values, null optional fields, API-shaped deserialization, unknown-property tolerance |
| `ConsoleKeyPressTests` | Interactive wait | Redirected input completes the shared wait immediately with a default key instead of blocking; `Reset` starts a fresh wait; concurrent callers share one task |
| `LoggingSetupTests` | Logging surface | Console level switch defaults to Information and can be raised (the global logger is deliberately not configured in tests — attaching the real sinks would forward events to the live bug report API, so idempotence is verified by inspection) |
| `SerilogDokanLoggerTests` | Dokan log adapter | Debug-enabled probing and level-by-level forwarding of Dokan messages into a collecting Serilog sink |
| `BugReportSinkTests` | Sink guard rails | Sub-Warning events are ignored and malformed events are swallowed (Warning+ intentionally not emitted: it would call the live API) |
| `BugReportHttpTests` | Bug report API | Stubbed request shaping (endpoint, method, `X-API-KEY` header, JSON body), the idle pending-reports wait, and the critical-log fallback for error statuses and network failures (log paths are redirected to a temporary directory) |
| `StatsServiceHttpTests` | Stats API | Stubbed request shaping (endpoint, bearer token, `applicationId`/version body, JSON content type), rejection/server-error/timeout/unreachable handling, the idle pending-report wait, and the reported entry-assembly version |
| `UpdateCheckerHttpTests` | Update check API | Stubbed endpoint/user-agent, malformed/empty/array/missing-field response swallowing, older or non-numeric tags never prompting, error-status and network-failure handling, and the update-available prompt callback (no live traffic, no prompts) |
| `ApiHttpClientFactoryTests` | API HTTP clients | Requested/infinite timeouts, distinct client instances over the shared handler, no default headers, and creation after disposing another client |
| `WindowsUpdatePromptTests` | Update message box | Message text includes both versions, the release URL and the download question |
| `WindowsMessageBoxTests` | Message box decision | The shared interactive/redirected decision only allows a message box for interactive runs |
| `DokanDownloadPromptTests` | Dokan download offer | Warning names the missing component, contains install/re-run guidance, the download URL and the open-page question; the default release URL is HTTPS |
| `CheckAccessTests` | Privilege probe | Administrator probe returns without throwing on any privilege level |
| `SimpleXisoDrive.Unix.Tests` | FUSE 3 interop | `FuseStructLayoutTests` pins `fuse_args` and the Linux/macFUSE `fuse_operations` field order and size, and the Cdecl callback convention; `FuseInteropTests` covers resolver idempotence, version-ordered library candidates (including equal/unversioned/patch-level/same-name ordering), deterministic failure probes with explicit candidate lists, whitespace candidates, and availability guidance; `FuseMountArgumentsTests` covers `fsname`/`volname` label exposure, debug flags, label sanitization/fallback and non-empty argv entries; `FuseHelperTests` covers native path conversion (including trailing separators), Unix time conversion, and directory fill offsets (empty/at-end/one-based); `FuseVolumeAdapterTests` covers POSIX-to-VFS path conversion, entry wrapping, read forwarding and foreign-entry rejection; `PosixErrorTests` pins the errno values; `ProgramNameTests` covers the usage-text executable name; `CommandLineOptionTests` covers case-insensitive option validation and help-flag detection |
| `TestImageFactory` / `TestImageEntry` | Shared test fixtures | Builders for minimal rebuilt (sector 0) and standard (sector 32) images with arbitrary nested file/directory trees, raw attribute bytes, and descriptor FILETIME values |
| `FakeVfsVolume` / `FakeVfsEntry` / `TrackingRawImageSource` / `RecordingDisposable` | Shared test doubles | Configurable failure injection and disposal counting for decorator tests |
| `StubHttpMessageHandler` / `ThrowingHttpMessageHandler` | Shared HTTP doubles | The stub records outbound method/URI/headers/body and returns a canned response; the throwing double surfaces a chosen network exception, so API services are tested without live traffic |
| `CommandLineParserTests` | Windows command line | Single-argument drag-and-drop mode (with and without options), case-insensitive option flags, options before the mount path, empty/invalid image paths, unknown options (with usage hint), extra positional arguments, help-flag detection, null arrays |
| `MountPathValidatorTests` | Mount path classification | Drive-letter forms (`Z:`, `Z:\`, any letter case) recognized for the administrator warning and folder-existence check; non-drive paths rejected; null guard |
| `XisoPathNotFoundTests` | Lookup-miss classification | The pinned XISOSharp "Path not found" `InvalidDataException` (including different casing and wrapped inner exceptions) and file-system miss exceptions classify as misses; other invalid-data messages and unrelated exceptions do not |
| `CommandLineExceptionTests` | Command-line error type | Message and usage-hint preservation, empty messages, exception inheritance |
| `DriveLetterSelectorTests` | Drive letter choice | Preferred `M`-`R` order and that the selected letter is free and valid |
| `DokanInstallationTests` | Dokan detection | Library and driver paths under the system directory; a detected state does not log at Warning level or higher (expected setup conditions are not reported) |
| `UsageTextTests` | Usage text | Executable name (no extension) and every documented option (including `-h`/`--help`), argument and supported format appear in the usage text |
| `BoundedCacheTests` | Cache budget | Round-trip get/set, case-insensitive keys, ordinal default comparer, stopping new entries at the limit (existing entries intact), updating existing entries at the limit, overwrite count stability, non-positive limit rejection |
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
  `Environment.CurrentDirectory`, so it runs in a non-parallel collection
  (`CurrentDirectoryCollection`). `BugReportTests`/`BugReportHttpTests` redirect the local log paths
  to a temporary directory through `BugReport.OverrideLogFilePaths` and run in the non-parallel
  `BugReportFileCollection`, so the real `error.log`/`critical_error.log` are never touched.
- **ABI pinning**: `FuseStructLayoutTests` uses `Marshal.SizeOf`/`Marshal.OffsetOf` to pin the
  native `fuse_args`/`fuse_operations` layouts and delegate calling conventions, so a wrong field
  order fails the build's tests instead of crashing a real mount. Loader-dependent assertions use
  explicit candidate lists (`FuseInterop.TryLoadLibrary`, `FuseAvailability.Check`), so the probe
  outcome is deterministic whether or not the host has FUSE 3.
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
