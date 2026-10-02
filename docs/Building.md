# Building

This page describes how to build SimpleXisoDrive from source.

---

## Prerequisites

| Requirement | Notes |
| --- | --- |
| Windows, Linux or macOS | The shared core and the Unix app target `net10.0`; the Windows app targets `net10.0-windows`. |
| .NET 10 SDK | Version 10.0.0 or later. The repository pins the SDK in `global.json`. |
| Git | To clone the repository. |
| Dokan (Windows) / FUSE 3 (Linux, macOS) | **Not** required to build. Required to run the built executable. |

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
| `CSharp_SimpleXisoDrive.sln` | Solution with all projects |
| `SimpleXisoDrive.Core/` | Shared class library (`net10.0`): VFS, image parsing, services |
| `SimpleXisoDrive/` | Windows application project (`net10.0-windows`, Dokan) |
| `SimpleXisoDrive.Unix/` | Linux/macOS application project (`net10.0`, FUSE 3) |
| `FuseSharp/` | Standalone FUSE 3 mount library (`net10.0`, packable) |
| `SimpleXisoDrive.Tests/` | xUnit test project (`net10.0-windows`) |
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

# Run the Windows application from source
dotnet run --project SimpleXisoDrive/SimpleXisoDrive.csproj -- "D:\Games\Halo.iso" Z:

# Run the Linux/macOS application from source
dotnet run --project SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj -- ~/Games/Halo.iso ~/mnt/halo
```

Build output defaults to `SimpleXisoDrive/bin/<Configuration>/net10.0-windows/`.

---

## Publishing standalone builds

The project targets Windows x64/ARM64 and Linux/macOS x64/ARM64. Publish with a runtime identifier:

```shell
# Framework-dependent (requires .NET 10 Runtime installed)
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-x64
dotnet publish SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj -c Release -r linux-x64
dotnet publish SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj -c Release -r osx-arm64

# Self-contained (bundles the runtime)
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-x64 --self-contained true
dotnet publish SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj -c Release -r linux-x64 --self-contained true

# Single-file self-contained executable
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Windows on ARM
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-arm64 --self-contained true
```

Published output lands in `SimpleXisoDrive/bin/Release/net10.0-windows/<rid>/publish/`
for the Windows app and `SimpleXisoDrive.Unix/bin/Release/net10.0/<rid>/publish/`
for the Unix app.

The standalone FUSE 3 mount library is packed on its own under the NuGet package id
`SimpleXisoDrive.FuseSharp`:

```shell
dotnet pack FuseSharp/FuseSharp.csproj -c Release
```

Release bundles use the **framework-dependent single-file** publish — one executable
(no runtime included, hence the .NET 10 Runtime prerequisite):

```shell
dotnet publish SimpleXisoDrive/SimpleXisoDrive.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
Compress-Archive -Path SimpleXisoDrive/bin/Release/net10.0-windows/win-x64/publish/SimpleXisoDrive.exe -DestinationPath release_1.5.0_win-x64.zip

dotnet publish SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj -c Release -r linux-x64 --self-contained false -p:PublishSingleFile=true
Compress-Archive -Path SimpleXisoDrive.Unix/bin/Release/net10.0/linux-x64/publish/SimpleXisoDrive -DestinationPath release_1.5.0_linux-x64.zip
```

> If you plan to upload a release, the release notes convention uses archive suffixes
> `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`
> (see [Installation](Installation)). The `release.yml` workflow does this for you on a
> `release_*` tag.

---

## Versioning

The version is defined in the project files and they must be updated together:

| File | Property |
| --- | --- |
| `SimpleXisoDrive/SimpleXisoDrive.csproj` | `<AssemblyVersion>` and `<FileVersion>` |
| `SimpleXisoDrive.Core/SimpleXisoDrive.Core.csproj` | `<AssemblyVersion>` and `<FileVersion>` |
| `SimpleXisoDrive.Unix/SimpleXisoDrive.Unix.csproj` | `<AssemblyVersion>` and `<FileVersion>` |
| `SimpleXisoDrive.Tests/SimpleXisoDrive.Tests.csproj` | `<AssemblyVersion>` and `<FileVersion>` |
| `SimpleXisoDrive.Unix.Tests/SimpleXisoDrive.Unix.Tests.csproj` | `<AssemblyVersion>` and `<FileVersion>` |

The current version is **1.5.0**. The update checker parses the three-part (`major.minor.patch`)
portion of GitHub release tags, so release tags should follow that pattern (for example,
`release_1.5.0`).

The standalone `FuseSharp` library (`FuseSharp/FuseSharp.csproj`) is versioned independently
(currently `1.0.0`) and is packed as `SimpleXisoDrive.FuseSharp`.

---

## Project configuration highlights

| Setting | Value |
| --- | --- |
| Target frameworks | `net10.0-windows` (Windows app, tests), `net10.0` (core, Unix app, FuseSharp) |
| Language version | 14 |
| Nullable | Enabled |
| Implicit usings | Enabled |
| Output type | `Exe` (console) |
| Debug symbols | Embedded |
| Application icon | `icon\xiso.ico` (Windows app) |

The application intentionally builds as a console executable: the console serves as the UI for mount
status and unmount instructions.

---

## Analyzers

The application, the shared core and `FuseSharp` reference the same analyzers:

| Analyzer | Purpose |
| --- | --- |
| `Meziantou.Analyzer` 3.0.291 | Best-practice and performance rules |
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
| `ci.yml` | Push/PR to `master`, manual | Restores, builds `Release`, runs the test suite on `windows-latest`, and uploads the `.trx` results and Cobertura coverage as artifacts. A second job builds and publishes the Unix app on `ubuntu-latest` (hosted runners have no FUSE device, so it does not mount). |
| `release.yml` | `release_*` tag, manual | Verifies the tag against `<AssemblyVersion>`, runs the suite, publishes framework-dependent single-file executables for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`, packs each as `release_<version>_<rid>.zip` containing only the executable, and creates the GitHub release with those assets. |

To cut a release:

1. Bump `<AssemblyVersion>`/`<FileVersion>` in the project files listed above, and update
   [Release History](Release-History) and `WhatsNew.md`.
2. Commit and push the version bump.
3. Tag and push:

   ```shell
   git tag release_1.5.0
   git push origin release_1.5.0
   ```

4. Watch the workflow create the GitHub release with the Windows, Linux and macOS zips attached.

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
