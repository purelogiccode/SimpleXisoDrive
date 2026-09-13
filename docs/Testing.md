# Testing

SimpleXisoDrive has an xUnit test project covering the parsing, file system, archive, and
path-resolution logic. The tests run without Dokan installed and without any real image file.

---

## Test project

| Property | Value |
| --- | --- |
| Project | `SimpleXisoDrive.Tests` |
| Framework | `net10.0-windows` |
| Test framework | xUnit 2.9.3 |
| Runner | `xunit.runner.visualstudio` 4.0.0 |
| Coverage collector | `coverlet.collector` 10.0.1 |
| Test count | 89 (version 1.3.0) |

The application exposes internals to the test project through `InternalsVisibleTo` in
`SimpleXisoDrive/AssemblyInfo.cs`, which allows tests to use the internal
`XisoVfsVolume(Stream, string)` constructor and internal helpers.

---

## Running the tests

```shell
# Run everything
dotnet test CSharp_SimpleXisoDrive.sln

# Run only the test project
dotnet test SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj

# Release configuration
dotnet test CSharp_SimpleXisoDrive.sln -c Release

# Filter by test name
dotnet test SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj --filter "FullyQualifiedName~XisoVfsVolume"

# Verbose output
dotnet test CSharp_SimpleXisoDrive.sln --logger "console;verbosity=detailed"
```

A healthy run reports `Passed: 89, Failed: 0` for `SimpleXisoDrive.Tests.dll`. The same suite runs in
CI on every push and pull request (see [Building](Building#continuous-integration)); the workflow
always uploads the `.trx` results and the Cobertura coverage report as artifacts.

---

## What is covered

| Test file | Area | Examples |
| --- | --- | --- |
| `XisoVfsVolumeTests` | XISO volume over XISOSharp | Standard sector-32 and rebuilt sector-0 images, entry lookup, directory listing, reads at offsets and past EOF, attribute mapping, descriptor creation time, image-handle release on dispose, stream-backed (embedded) images |
| `XisoVfsVolumeTreeTests` | Directory trees and reads | Multi-file and nested-directory images, separators and case-insensitivity, empty files/directories, reads across sector boundaries, clamped reads, attribute-flag mapping, descriptor FILETIME values, parallel reads (path and stream modes), idempotent dispose |
| `XboxIsoVfsDokanTests` | Dokan operation layer | Volume information and free space, `.`/`..` listings, wildcard filtering, metadata for files and directories, `CreateFile` access modes, `ReadFile` offsets/clamping/directories, normalized special segments, read-only denials, locking, alternate-stream reporting, security descriptors |
| `ApiKeyProviderTests` | API key protection | Deterministic decryption of the double-encrypted key, expected key digest (without storing the key), preload behavior |
| `ResolveImagePathTests` | CLI path resolution | Existing file, extension appending (`.iso`, `.xiso`, `.cso`, `.zar`), split CISO sets, directory with exactly one image, multiple/zero images, current-directory lookup |
| `VfsContainerTests` | Volume facade and format detection | ZAR tree mount, embedded XISO mount (including nested content), renamed `.zar` fallback, invalid archive errors, plain `.iso` and `.xiso` mounts, locked/missing images surface I/O errors |
| `ZarVfsVolumeTests` | ZArchive volume | Tree listing, case-insensitive nested lookup, file reads at offsets, directory reads, multi-block reads, volume size, invalid archives |
| `TestImageFactory` / `TestImageEntry` | Shared test fixtures | Builders for minimal rebuilt (sector 0) and standard (sector 32) images with arbitrary nested file/directory trees, raw attribute bytes, and descriptor FILETIME values |
| `InvalidImageExceptionTests` | Exception contract | Message and inner exception constructors, inheritance, catchability |

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
- **Temporary files**: `ResolveImagePathTests`, `XisoVfsVolumeTests`, and the Dokan tests create and
  clean up temp files/directories; `ResolveImagePathTests` also temporarily changes
  `Environment.CurrentDirectory`.
- **No driver dependency**: nothing in the test suite requires a mounted volume or admin rights.

---

## Coverage

Collect coverage with coverlet:

```shell
dotnet test SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj --collect:"XPlat Code Coverage"
```

Results are written under `SimpleXisoDrive.Tests/TestResults/` as Cobertura XML.

Note that Dokan callbacks (`XboxIsoVfsDokan`), the application entry point, and the live HTTP
services are not covered by unit tests because they require a driver, a running process, or network
access.

---

## Adding tests

1. Add a new test class to `SimpleXisoDrive.Tests`, or extend an existing one by topic.
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
