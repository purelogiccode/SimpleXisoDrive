# Installation

This page describes everything required to run SimpleXisoDrive on Windows.

---

## System requirements

| Requirement | Details |
| --- | --- |
| Operating system | Windows 10 or Windows 11 (x64 or ARM64) |
| Architecture | `x64` or `ARM64` |
| Runtime | .NET 10.0 Runtime (base runtime; self-contained builds bundle it) |
| Driver | Dokan user-mode file system library 2.x (`dokan2.dll`) |
| Privileges | Administrator recommended (required by most systems for drive letter mounts) |
| Disk usage | A few MB for the application; images are streamed, not copied |

SimpleXisoDrive does not require a GPU, does not install a service of its own, and does not modify
the images it opens.

---

## Step 1 - Install the Dokan library

SimpleXisoDrive cannot run without Dokan. The application checks for
`%SystemRoot%\System32\dokan2.dll` at startup and exits with instructions if the file is missing.

1. Open the Dokan releases page:
   <https://github.com/dokan-dev/dokany/releases>
2. Download the Dokan installer that matches your Windows architecture.
3. Run the installer and accept the default options. The default installation includes
   `dokan2.dll` and the `dokan2.sys` driver.
4. **Restart your computer** when prompted. The driver is not active until the system restarts.

To verify the installation manually, open a PowerShell window and run:

```powershell
Test-Path "$env:SystemRoot\System32\dokan2.dll"
Test-Path "$env:SystemRoot\System32\drivers\dokan2.sys"
```

Both commands should print `True`. If only `dokan2.dll` is present, the application still runs but
prints a warning that the driver (`dokan2.sys`) was not found; mounting may fail in that case.

For more detail about Dokan, see the [official documentation](https://github.com/dokan-dev/dokany).

---

## Step 2 - Install the .NET runtime

Framework-dependent builds require the **.NET 10.0 Runtime** (the base runtime; the Desktop
Runtime also works but is not required).

1. Download the runtime from <https://dotnet.microsoft.com/download/dotnet/10.0>.
2. Choose the **.NET Runtime** for your architecture (`x64` or `ARM64`).
3. Install it.

Self-contained release builds bundle the runtime and do not require this step. If a release archive
contains the runtime files alongside the executable, you can skip this step for that build.

To verify an installed runtime:

```powershell
dotnet --list-runtimes
```

Look for an entry such as `Microsoft.NETCore.App 10.x.x`.

---

## Step 3 - Install SimpleXisoDrive

SimpleXisoDrive is a portable application; there is no installer and no registry footprint.

1. Download the release archive for your architecture from
   <https://github.com/purelogiccode/SimpleXisoDrive/releases>:
   - `win-x64` for standard Intel/AMD PCs
   - `win-arm64` for Windows on ARM devices
2. Extract the archive to a folder of your choice, for example `C:\Tools\SimpleXisoDrive`.
3. Optionally create a desktop shortcut to `SimpleXisoDrive.exe`.

> **Tip:** Do not extract the application directly into `C:\Program Files` if you intend to run it
> without administrator rights; the application writes log files next to the executable.

### Architecture notes

The release names use the following conventions:

| Archive suffix | Architecture | Typical devices |
| --- | --- | --- |
| `win-x64` | x86-64 | Most desktops and laptops |
| `win-arm64` | ARM64 | Surface Pro X, Snapdragon-based laptops |

The Dokan and .NET runtimes must match the application architecture.

---

## Step 4 - First run

Open a terminal and run:

```shell
SimpleXisoDrive.exe
```

With no arguments the application prints its usage information and a reminder that you can drag and
drop an ISO, XISO or ZAR file onto the executable. The console uses a green-on-black theme and
remains open until you press a key.

For a first real mount:

```shell
SimpleXisoDrive.exe "D:\Games\MyGame.iso" Z:
```

See [Getting Started](Getting-Started) for a guided walkthrough.

---

## Upgrading

1. Close any running instance of SimpleXisoDrive.
2. Replace the application files with the new release. Your `logs`, `error.log`, and
   `critical_error.log` files can be deleted or kept; they are not configuration.
3. Optionally delete `logs\` to start a clean log history.

The application checks GitHub for newer releases on every start and offers to open the release page
in your default browser. See [Services](Services) for details.

---

## Uninstalling

1. Unmount any active volumes (press a key in the console window, or press `Ctrl+C`).
2. Delete the application folder.
3. Optional: uninstall Dokan through **Windows Settings > Apps** if no other software uses it.

No files outside the application folder are created, aside from logs written next to the
executable and temporary files used by the .NET runtime.

---

## Verifying a healthy installation

| Check | Expected result |
| --- | --- |
| `SimpleXisoDrive.exe` with no arguments | Usage text is printed, exit code 1 |
| `%SystemRoot%\System32\dokan2.dll` exists | Dokan runtime is present |
| `%SystemRoot%\System32\drivers\dokan2.sys` exists | Dokan driver is present (warning otherwise) |
| A test mount | The volume appears in Explorer as `XBOX_ISO` |

If a check fails, see [Troubleshooting](Troubleshooting).
