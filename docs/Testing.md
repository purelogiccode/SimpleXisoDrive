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
| Test count | 43 (version 1.3.0) |

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

A healthy run reports `Passed: 43, Failed: 0` for `SimpleXisoDrive.Tests.dll`.

---

## What is covered

| Test file | Area | Examples |
| --- | --- | --- |
| `XisoVfsVolumeTests` | XISO volume over XISOSharp | Standard sector-32 and rebuilt sector-0 images, entry lookup, directory listing, reads at offsets and past EOF, attribute mapping, descriptor creation time, image-handle release on dispose, stream-backed (embedded) images |
| `ResolveImagePathTests` | CLI path resolution | Existing file, extension appending (`.iso`, `.xiso`, `.zar`), directory with exactly one image, multiple/zero images, current-directory lookup |
| `VfsContainerTests` | Volume facade and format detection | ZAR tree mount, embedded XISO mount, renamed `.zar` fallback, invalid archive errors, plain ISO mount |
| `ZarVfsVolumeTests` | ZArchive volume | Tree listing, case-insensitive nested lookup, file reads at offsets, directory reads, volume size, invalid archives |
| `TestImageFactory` | Shared test fixtures | Minimal rebuilt-XISO (sector 0) and standard Xbox ISO (sector 32) image builders with a root file entry |
| `InvalidImageExceptionTests` | Exception contract | Message and inner exception constructors, inheritance, catchability |

### Testing techniques

- **In-memory images**: tests build byte arrays for volume descriptors and directory entries and wrap
  them in `MemoryStream`, using the internal `XisoVfsVolume(Stream, string)` constructor.
- **Real archives**: `ZarVfsVolumeTests`/`VfsContainerTests` pack small trees and embedded XISO
  images with `ZArchiveWriter` into temporary `.zar` files, so the real reader (including zstd
  decompression) is exercised end to end.
- **Temporary files**: `ResolveImagePathTests` and `XisoVfsVolumeTests` create and clean up temp
  files/directories; `ResolveImagePathTests` also temporarily changes `Environment.CurrentDirectory`.
- **No driver dependency**: nothing in the test suite requires Dokan, a mounted volume, or admin
  rights.

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
