# Getting Started

This page walks through the two supported ways to mount an Xbox image on Windows: drag-and-drop and
the command line. Xbox ISO/XISO images (`.iso`, `.xiso`), CISO-compressed images (`.cso`), Xbox ISO
CHD images (`.chd`) and ZArchive (`.zar`) files are supported.

Before you begin, make sure [the .NET runtime and your platform's mount driver are installed](Installation)
(Dokan on Windows, FUSE 3 on Linux, macFUSE on macOS). On Linux and macOS the workflow is
command-line only with a directory mount point; see [Linux and macOS](Linux-and-macOS) for the
platform-specific steps.

---

## What happens when the application starts

Every run follows the same sequence:

1. The console is switched to the green-on-black theme and cleared (Windows).
2. Global exception handlers are installed so that crashes are logged and reported.
3. `-h`/`--help` prints the usage text and exits with code `0`. This happens before the update
   check and the mount-backend probe, so help never makes a network call and works even when
   Dokan or FUSE is not installed.
4. Launch statistics are reported and the application checks GitHub for a newer release
   immediately at startup, before arguments are handled or anything is mounted. A newer release
   is offered through the native Windows message box or the Unix console prompt; non-interactive
   runs (Windows: no interactive session or redirected input/output; Unix: redirected input) print
   the version and URL instead of showing a dialog.
5. A run without arguments prints the usage text and the drag-and-drop hint, waits for a key
   press, and exits with code `1`.
6. The mount backend is verified: `dokan2.dll` on Windows, or the FUSE 3 library and `/dev/fuse`
   on Linux. On Windows, a missing Dokan runtime prints guidance, offers the Dokan download page
   (<https://github.com/dokan-dev/dokany/releases>), and exits with code `1`; a missing driver
   warns and mounting continues.
7. Arguments are parsed:
   - **no arguments** - handled above;
   - **one argument** - treated as a drag-and-drop mount (automatic drive letter, Explorer opens);
   - **two or more arguments** - image path followed by mount point and optional flags.
8. The image path is resolved (see [path resolution](Command-Line-Reference#image-path-resolution)).
9. The image is opened, validated, and the Dokan file system is mounted. For `.chd` files the
   decompressed Xbox image is validated as XDVDFS and served hunk-by-hunk; for `.zar` files the
   archive tree (or a single embedded XISO image) is exposed.
10. The console remains open until the volume is unmounted.

---

## Drag-and-drop mounting

This is the fastest way to mount an image and requires no typing.

1. Locate an Xbox ISO/XISO (`.iso`, `.xiso`), CISO (`.cso`), Xbox ISO CHD (`.chd`) or ZArchive (`.zar`) file in File Explorer.
2. Drag the file and drop it onto `SimpleXisoDrive.exe`.
3. A console window opens. The application:
   - validates the path,
   - chooses the first free drive letter from `M:` through `R:`,
   - mounts the image read-only,
   - opens Windows Explorer at the new drive.
4. Use the files as you would from any read-only drive. Copying files out is allowed; writing to
   the mounted volume is not. ZArchive contents are decompressed on demand.
5. **To unmount:** click the console window and press any key. Alternatively close the console
   window, or press `Ctrl+C`.

If the image path is invalid, the console prints the error, a set of hints, and waits for a key
press before closing so the message is not lost.

> Drag-and-drop always launches Explorer. If you do not want Explorer to open, use the
> [command line](Command-Line-Reference) instead.

---

## Command-line mounting

Open **Windows Terminal**, **PowerShell**, or **Command Prompt** and run:

```shell
SimpleXisoDrive.exe <PathToImageFile> <MountPoint> [options]
```

Run `SimpleXisoDrive.exe -h` (or `--help`) to print the usage text and exit with code `0`. The help
check runs before the update check and the Dokan probe, so it makes no network call.

### Mount to a drive letter

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z:
```

The image becomes available as drive `Z:`. A trailing backslash (`Z:\`) is accepted as well; the
application removes it because the Dokan driver expects the form without it.

### Mount a ZArchive

A `.zar` archive is mounted exactly like an ISO. The stored game tree appears at the root of the
drive; if the archive wraps a single XISO image, the image's contents are shown instead:

```shell
SimpleXisoDrive.exe "D:\Games\Halo.zar" Z:
```

### Mount an Xbox ISO CHD

An Xbox ISO stored as CHD is mounted exactly like an ISO. Hunks are decompressed on demand, so the
mount appears immediately and the source `.chd` is never modified:

```shell
SimpleXisoDrive.exe "D:\Games\Halo.chd" Z:
```

Only CHDs whose decompressed image is a valid Xbox ISO are accepted. CD/GD-ROM CHDs and
differential child CHDs (which need their parent merged first) are rejected with a clear error.

### Mount into an NTFS folder

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso" "C:\Mounts\Halo"
```

The target folder must already exist and be empty; a missing folder is reported before Dokan is
invoked. NTFS folder mounts usually work without administrator privileges.

### Open Explorer after mounting

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z: --launch
```

The short form is `-l`.

### Show Dokan debug output

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z: --debug
```

The short form is `-d`. Debug mode also routes Dokan's internal logging to the console, which is
useful when diagnosing mount problems. Debug and launch can be combined:

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z: -d -l
```

### Let the application choose a drive letter

If you pass only the image path, the application behaves like drag-and-drop:

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso"
```

It picks the first free letter between `M:` and `R:`, mounts the image, and opens Explorer.

---

## Unmounting

| Scenario | How to unmount |
| --- | --- |
| Drag-and-drop / single argument | Press any key in the console window |
| Interactive command line | Press `Ctrl+C` in the console window |
| Console window closed | The mount is torn down when the process exits |

The application waits for the unmount signal, disposes the file stream, and logs `Unmounted.` before
exiting with code `0`.

> **Note:** Closing Explorer or ejecting the drive from Explorer does not unmount the virtual
> volume; the console process owns the mount.

---

## Walking through a complete example

Assume the ISO is `D:\Xbox\ProjectGotham.iso` and you want it on drive `P:` and Explorer opened.

```shell
SimpleXisoDrive.exe "D:\Xbox\ProjectGotham.iso" P: -l
```

Expected console output (simplified):

```text
[HH:mm:ss INF] === SimpleXisoDrive Started ===
[HH:mm:ss INF] Arguments: D:\Xbox\ProjectGotham.iso | P: | -l
[HH:mm:ss INF] Attempting to mount 'D:\Xbox\ProjectGotham.iso' to 'P:'...
[HH:mm:ss INF] Mount successful: 'D:\Xbox\ProjectGotham.iso' -> 'P:'
[HH:mm:ss INF] Press Ctrl+C to unmount (if run from command line).
```

Windows Explorer opens at `P:\`, which shows the contents of the disc under the volume label
`XBOX_ISO` (ISO/XISO/CHD) or `XBOX_ZAR` (ZArchive).

---

## Example automation script

You can wrap the executable in a small batch file to mount a favorite image on a fixed letter:

```bat
@echo off
"C:\Tools\SimpleXisoDrive\SimpleXisoDrive.exe" "D:\Xbox\Halo.iso" "H:" -l
```

Double-clicking the batch file mounts the image; pressing `Ctrl+C` in its console unmounts it.

---

## Where the application writes data

All files are created next to the executable unless noted otherwise:

| Path | Contents |
| --- | --- |
| `logs\simplexisodrive-YYYYMMDD.log` | Rolling Serilog file log (7 days retained) |
| `error.log` | Local bug report log (Warning level and above) |
| `critical_error.log` | Fallback log for failures in the logging pipeline itself |

See [Services](Services) for the exact logging configuration and
[Privacy and Networking](Privacy-and-Networking) for what is transmitted off the machine.

---

## Next steps

- [Command-Line Reference](Command-Line-Reference) - every argument and behavior in detail.
- [Troubleshooting](Troubleshooting) - if the mount fails.
- [FAQ](FAQ) - common questions about formats and limitations.
