# Building

This page describes how to build SimpleXisoDrive from source.

---

## Prerequisites

| Requirement | Notes |
| --- | --- |
| Windows 10/11 | Required; the project targets `net10.0-windows`. |
| .NET 10 SDK | Version 10.0.0 or later. The repository pins the SDK in `global.json`. |
| Git | To clone the repository. |
| Dokan | **Not** required to build. Required to run the built executable. |

Verify the SDK:

```shell
dotnet --version
```

The `global.json` file specifies:

```json
{
  "sdk": {
    "version": "10.0.0",
    "rollForward": "latestMajor",
    "allowPrerelease": false
  }
}
```

`rollForward: latestMajor` allows a newer major SDK to build the project.

---

## Repository layout

| Path | Contents |
| --- | --- |
| `CSharp_SimpleXisoDrive.sln` | Solution with both projects |
| `SimpleXisoDrive/` | Application project (WinExe) |
| `SimpleXisoDrive.Tests/` | xUnit test project |
| `docs/` | This documentation |

---

## Common commands

Run all commands from the repository root.

```shell
# Restore packages
dotnet restore CSharp_SimpleXisoDrive.sln

# Debug build
dotnet build CSharp_SimpleXisoDrive.sln

# Release build
dotnet build CSharp_SimpleXisoDrive.sln -c Release

# Run the tests
dotnet test CSharp_SimpleXisoDrive.sln

# Run the application from source
dotnet run --project SimpleXisoDrive/SimpleXisoDrive.csproj -- "D:\Games\Halo.iso" Z:
```

Build output defaults to `SimpleXisoDrive/bin/<Configuration>/net10.0-windows/`.

---

## Publishing standalone builds

The project targets Windows x64 and ARM64. Publish with a runtime identifier:

```shell
# Framework-dependent (requires .NET 10 Runtime installed)
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-x64

# Self-contained (bundles the runtime)
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-x64 --self-contained true

# Single-file self-contained executable
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Windows on ARM
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-arm64 --self-contained true
```

Published output lands in `SimpleXisoDrive/bin/Release/net10.0-windows/<rid>/publish/`.

Release bundles use the **framework-dependent single-file** publish — one
`SimpleXisoDrive.exe` (no runtime included, hence the .NET 10 Runtime prerequisite):

```shell
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
Compress-Archive -Path SimpleXisoDrive/bin/Release/net10.0-windows/win-x64/publish/SimpleXisoDrive.exe -DestinationPath release_1.3.0_win-x64.zip
```

> If you plan to upload a release, the release notes convention uses archive suffixes `win-x64` and
> `win-arm64` (see [Installation](Installation)). The `release.yml` workflow does this for you on a
> `release_*` tag.

---

## Versioning

The version is defined in two places and both must be updated together:

| File | Property |
| --- | --- |
| `SimpleXisoDrive/SimpleXisoDrive.csproj` | `<AssemblyVersion>` and `<FileVersion>` |
| `SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj` | `<AssemblyVersion>` and `<FileVersion>` |

The current version is **1.3.0**. The update checker parses the three-part (`major.minor.patch`)
portion of GitHub release tags, so release tags should follow that pattern (for example,
`release_1.3.0`).

---

## Project configuration highlights

| Setting | Value |
| --- | --- |
| Target framework | `net10.0-windows` |
| Language version | 14 |
| Nullable | Enabled |
| Implicit usings | Enabled |
| Output type | `Exe` (console) |
| Debug symbols | Embedded |
| Application icon | `icon\xiso.ico` |

The application intentionally builds as a console executable: the console serves as the UI for mount
status and unmount instructions.

---

## Analyzers

Both projects reference the same analyzers:

| Analyzer | Purpose |
| --- | --- |
| `Meziantou.Analyzer` 3.0.257 | Best-practice and performance rules |
| `Roslynator.Analyzers` 5.0.0 | Code quality and style rules |

Three rules are disabled in `.editorconfig`:

| Rule | Description |
| --- | --- |
| `MA0004` | Use `ConfigureAwait` |
| `MA0051` | Method is too long |
| `MA0015` | Specify the parameter name in `ArgumentException` |

Warnings are not treated as errors, but new code should be clean. See [Contributing](Contributing).

---

## Continuous integration

Two GitHub Actions workflows automate build, test, and release (`.github/workflows/`):

| Workflow | Trigger | What it does |
| --- | --- | --- |
| `ci.yml` | Push/PR to `master`, manual | Restores, builds `Release`, runs the test suite on `windows-latest`, and uploads the `.trx` results and Cobertura coverage as artifacts. |
| `release.yml` | `release_*` tag, manual | Verifies the tag against `<AssemblyVersion>`, runs the suite, publishes framework-dependent single-file executables for `win-x64` and `win-arm64`, packs each as `release_<version>_<rid>.zip` containing only `SimpleXisoDrive.exe`, and creates the GitHub release with those assets. |

To cut a release:

1. Bump `<AssemblyVersion>`/`<FileVersion>` in both `.csproj` files, and update
   [Release History](Release-History) and `WhatsNew.md`.
2. Commit and push the version bump.
3. Tag and push:

   ```shell
   git tag release_1.3.0
   git push origin release_1.3.0
   ```

4. Watch the workflow create the GitHub release with the `win-x64` and `win-arm64` zips attached.

A manual run (`workflow_dispatch`) of `release.yml` builds the same zips as artifacts without
creating a release. The two commands that must always succeed locally are unchanged:

```shell
dotnet build CSharp_SimpleXisoDrive.sln -c Release
dotnet test CSharp_SimpleXisoDrive.sln -c Release
```

---

## Troubleshooting the build

| Symptom | Cause / fix |
| --- | --- |
| `SDK '10.0.0' not found` | Install the .NET 10 SDK. `rollForward` allows newer major versions, but the SDK must be at least 10. |
| Package restore failures | Check network/proxy access to NuGet. |
| `MSB3644` reference assemblies not found | Install the .NET 10 SDK; do not rely on an older Visual Studio. |
| Build succeeds but the app exits immediately | Dokan is missing at runtime; see [Installation](Installation). |
