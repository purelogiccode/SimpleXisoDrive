# Installation

This page is the one-stop install guide for every platform: the .NET runtime, the mount driver
(Dokan on Windows, FUSE 3 on Linux, macFUSE on macOS), the application itself, and the first run.
For Linux/macOS usage details see [Linux and macOS](Linux-and-macOS).

---

## System requirements

| Requirement | Details |
| --- | --- |
| Operating system | Windows 10 or 11 (x64/ARM64), Linux (x64/ARM64), macOS (Intel/Apple silicon) |
| Runtime | .NET 10.0 Runtime (base runtime; self-contained builds bundle it) |
| Mount driver | Windows: Dokan 2.x; Linux: FUSE 3 (`libfuse3` plus the `fuse3` tools); macOS: macFUSE |
| Privileges | Windows drive-letter mounts are most reliable elevated; Linux FUSE does not need `sudo`; macOS requires allowing the macFUSE system extension |
| Disk usage | A few MB for the application; images are streamed, not copied |

SimpleXisoDrive does not require a GPU, does not install a service of its own, and does not modify
the images it opens.

---

## Step 1 - Install the .NET 10.0 Runtime (all platforms)

Framework-dependent builds require the **.NET 10.0 Runtime** (the base runtime; the Desktop
Runtime also works but is not required).

1. Open <https://dotnet.microsoft.com/download/dotnet/10.0>.
2. Choose the **.NET Runtime** for your architecture (`x64` or `ARM64`).
3. Install it.
4. Verify the installation:

   ```shell
   dotnet --list-runtimes
   ```

   Look for an entry such as `Microsoft.NETCore.App 10.x.x`.

Self-contained release builds bundle the runtime and do not require this step.

---

## Step 2 - Install the mount driver

### Windows - Dokan

SimpleXisoDrive cannot run on Windows without Dokan. The application checks for
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

### Linux - FUSE 3

Linux mounting uses FUSE 3: the `libfuse3` library plus the `fuse3` tools that provide
`fusermount3`.

```shell
# Debian / Ubuntu
sudo apt install libfuse3-3 fuse3

# Fedora
sudo dnf install fuse3 fuse3-libs

# Arch
sudo pacman -S fuse3
```

Verify that the kernel device exists:

```shell
ls -l /dev/fuse
```

If it is missing, load the module with `sudo modprobe fuse`. The application searches for the FUSE
library in the standard locations; set `SIMPLEXISODRIVE_FUSE_LIBRARY` to an explicit path if it
lives somewhere else.

### macOS - macFUSE

1. Download and install macFUSE from <https://macfuse.io>.
2. Allow the system extension when macOS prompts for it (the installer restarts the system).
3. On macOS 15.4 or later, choose the FSKit backend when prompted; it needs no kernel extension.

macOS may quarantine a downloaded binary. If it refuses to start, remove the attribute:

```shell
xattr -d com.apple.quarantine SimpleXisoDrive
```

---

## Step 3 - Install SimpleXisoDrive

SimpleXisoDrive is a portable application; there is no installer and no registry footprint.

1. Download the release archive for your platform and architecture from
   <https://github.com/purelogiccode/SimpleXisoDrive/releases>:

   | Archive suffix | Platform | Typical devices |
   | --- | --- | --- |
   | `win-x64` | Windows x86-64 | Most desktops and laptops |
   | `win-arm64` | Windows on ARM | Surface Pro X, Snapdragon-based laptops |
   | `linux-x64` | Linux x86-64 | Most distributions |
   | `linux-arm64` | Linux on ARM | Raspberry Pi 4/5 (64-bit), ARM servers |
   | `osx-x64` | macOS Intel | Intel Macs |
   | `osx-arm64` | macOS Apple silicon | M-series Macs |

2. Extract the archive to a folder of your choice, for example `C:\Tools\SimpleXisoDrive` or
   `~/opt/simplexisodrive`. Each archive contains the single-file executable plus `ReadMe.md`,
   `LICENSE.txt` and `WhatsNew.md`.
3. On Linux and macOS the executable bit is already set. On Windows, optionally create a desktop
   shortcut to `SimpleXisoDrive.exe`.

> **Tip:** Do not extract the Windows application directly into `C:\Program Files` if you intend
> to run it without administrator rights; the application writes log files next to the executable.

---

## Step 4 - First run

### Windows

Open a terminal and run:

```shell
SimpleXisoDrive.exe
```

With no arguments the application prints its usage information and a reminder that you can drag and
drop an ISO/XISO/CISO, CHD or ZAR file onto the executable. The console uses a green-on-black theme
and remains open until you press a key.

For a first real mount:

```shell
SimpleXisoDrive.exe "D:\Games\MyGame.iso" Z:
```

See [Getting Started](Getting-Started) for a guided walkthrough.

### Linux and macOS

```shell
SimpleXisoDrive ~/Games/MyGame.iso ~/mnt/mygame
```

When the mount path is omitted, a temporary directory is created and printed after mounting. The
mount stays attached to the terminal; `Ctrl+C` (or `fusermount3 -u` / `umount`) unmounts cleanly.
See [Linux and macOS](Linux-and-macOS) for the full platform guide.

---

## Upgrading

1. Close any running instance of SimpleXisoDrive (press a key or `Ctrl+C` to unmount first).
2. Replace the application files with the new release. Your `logs`, `error.log`, and
   `critical_error.log` files can be deleted or kept; they are not configuration.
3. Optionally delete `logs/` to start a clean log history.

The application checks GitHub for newer releases on every start and offers to open the release
page in your default browser. See [Services](Services) for details.

---

## Uninstalling

1. Unmount any active volumes (press a key in the console window, or press `Ctrl+C`).
2. Delete the application folder.
3. Windows only: uninstall Dokan through **Windows Settings > Apps** if no other software uses it.
   The Linux FUSE packages and macFUSE are usually left installed because other software uses them.

No files outside the application folder are created, aside from logs written next to the
executable and temporary files used by the .NET runtime.

---

## Verifying a healthy installation

| Check | Expected result |
| --- | --- |
| `SimpleXisoDrive.exe` with no arguments (Windows) | Usage text is printed, exit code 1 |
| `SimpleXisoDrive --help` (Linux/macOS) | Usage text is printed, exit code 0 |
| `%SystemRoot%\System32\dokan2.dll` exists (Windows) | Dokan runtime is present |
| `%SystemRoot%\System32\drivers\dokan2.sys` exists (Windows) | Dokan driver is present (warning otherwise) |
| `ls -l /dev/fuse` (Linux) | The FUSE kernel device exists |
| `command -v fusermount3` (Linux) | The FUSE tools are on `PATH` |
| A test mount | The volume appears in the file manager as `XBOX_ISO` (ISO/CHD) or `XBOX_ZAR` (ZArchive) |

If a check fails, see [Troubleshooting](Troubleshooting).
