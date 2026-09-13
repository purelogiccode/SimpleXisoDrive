# Testing

SimpleXisoDrive has an xUnit test project covering the parsing, file system, and path-resolution
logic. The tests run without Dokan installed and without any real ISO file.

---

## Test project

| Property | Value |
| --- | --- |
| Project | `SimpleXisoDrive.Tests` |
| Framework | `net10.0-windows` |
| Test framework | xUnit 2.9.3 |
| Runner | `xunit.runner.visualstudio` 4.0.0 |
| Coverage collector | `coverlet.collector` 10.0.1 |
| Test count | 72 (version 1.2.0) |

The application exposes internals to the test project through `InternalsVisibleTo` in
`SimpleXisoDrive/AssemblyInfo.cs`, which allows tests to use the internal `IsoSt(Stream)` constructor
and internal helpers.

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
dotnet test SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj --filter "FullyQualifiedName~FileEntry"

# Verbose output
dotnet test CSharp_SimpleXisoDrive.sln --logger "console;verbosity=detailed"
```

A healthy run reports `Passed: 72, Failed: 0` for `SimpleXisoDrive.Tests.dll`.

---

## What is covered

| Test file | Area | Examples |
| --- | --- | --- |
| `FileEntryTests` | Directory entry parsing and attribute mapping | Root entry defaults, `IsDirectory`, child-pointer sentinels, `ReadInternal` for files/directories/empty names, entry-size padding, Windows attribute mapping |
| `IsoStTests` | Stream access layer | Constructor behavior, `VolumeOffset`, sector size, `ExecuteLocked`, offset-based reads, reads past EOF, entry reads, disposal |
| `VolumeDescriptorTests` | Descriptor discovery and validation | Magic validation, sector 0 vs sector 32 detection, all `ReadFrom` strategies, invalid FILETIME handling, diagnostic messages |
| `ResolveIsoPathTests` | CLI path resolution | Existing file, extension appending, directory with exactly one ISO, multiple/zero ISOs, current-directory lookup |
| `XisoFsFileAttributesTests` | Attribute flag values | Individual values, flag combinations, independence checks |
| `InvalidImageExceptionTests` | Exception contract | Message and inner exception constructors, inheritance, catchability |

### Testing techniques

- **In-memory images**: tests build byte arrays for volume descriptors and directory entries and wrap
  them in `MemoryStream`, using the internal `IsoSt(Stream)` constructor.
- **Temporary files**: `ResolveIsoPathTests` creates and cleans up temp files/directories and
  temporarily changes `Environment.CurrentDirectory`.
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
