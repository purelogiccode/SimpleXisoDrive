# Getting Started

This page walks through the two supported ways to mount an Xbox ISO image: drag-and-drop and the
command line.

Before you begin, make sure [Dokan and the .NET runtime are installed](Installation).

---

## What happens when the application starts

Every run follows the same sequence:

1. The console is switched to the green-on-black theme and cleared.
2. Global exception handlers are installed so that crashes are logged and reported.
3. The application verifies that `dokan2.dll` exists. If it does not, actionable instructions are
   printed and the process exits with code `1`.
4. The application checks GitHub for a newer release and may prompt you to open the release page.
5. Arguments are parsed:
   - **no arguments** - usage is printed and the process waits for a key;
   - **one argument** - treated as a drag-and-drop mount (automatic drive letter, Explorer opens);
   - **two or more arguments** - ISO path followed by mount point and optional flags.
6. The ISO path is resolved (see [path resolution](Command-Line-Reference#iso-path-resolution)).
7. The ISO is opened, the volume descriptor is validated, and the Dokan file system is mounted.
8. The console remains open until the volume is unmounted.

---

## Drag-and-drop mounting

This is the fastest way to mount an image and requires no typing.

1. Locate an Xbox ISO file in File Explorer.
2. Drag the `.iso` file and drop it onto `SimpleXisoDrive.exe`.
3. A console window opens. The application:
   - validates the path,
   - chooses the first free drive letter from `M:` through `R:`,
   - mounts the image read-only,
   - opens Windows Explorer at the new drive.
4. Use the files as you would from any read-only drive. Copying files out is allowed; writing to
   the mounted volume is not.
5. **To unmount:** click the console window and press any key. Alternatively close the console
   window, or press `Ctrl+C`.

If the ISO path is invalid, the console prints the error, a set of hints, and waits for a key press
before closing so the message is not lost.

> Drag-and-drop always launches Explorer. If you do not want Explorer to open, use the
> [command line](Command-Line-Reference) instead.

---

## Command-line mounting

Open **Windows Terminal**, **PowerShell**, or **Command Prompt** and run:

```shell
SimpleXisoDrive.exe <PathToIsoFile> <MountPoint> [options]
```

### Mount to a drive letter

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z:
```

The image becomes available as drive `Z:`. A trailing backslash (`Z:\`) is accepted as well; the
application removes it because the Dokan driver expects the form without it.

### Mount into an NTFS folder

```shell
SimpleXisoDrive.exe "D:\Games\Halo.iso" "C:\Mounts\Halo"
```

The target folder must exist and be empty. NTFS folder mounts usually work without administrator
privileges.

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

If you pass only the ISO path, the application behaves like drag-and-drop:

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
`XBOX_ISO`.

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
