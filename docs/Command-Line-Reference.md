# Command-Line Reference

This page documents every command-line argument, option, and behavior of the `SimpleXisoDrive`
executables. `SimpleXisoDrive.exe` is the Windows (Dokan) front end; `SimpleXisoDrive` is the
Linux/macOS (FUSE 3) front end. The arguments and options are the same on every platform; the
differences are noted inline.

---

## Synopsis

```text
# Windows
SimpleXisoDrive.exe
SimpleXisoDrive.exe -h | --help
SimpleXisoDrive.exe <image-file>
SimpleXisoDrive.exe <image-file> <mount-path> [options...]

# Linux / macOS
SimpleXisoDrive
SimpleXisoDrive -h | --help
SimpleXisoDrive <image-file>
SimpleXisoDrive <image-file> [mount-path] [options...]
```

## Arguments

| Argument | Required | Description |
| --- | --- | --- |
| `<image-file>` | Yes, when arguments are supplied | Path to the Xbox image (`.iso`, `.xiso`, `.cso`, `.chd`) or ZArchive (`.zar`). May omit the extension in some cases (see [path resolution](#image-path-resolution)). Paths containing spaces must be quoted. |
| `<mount-path>` | No | Windows: drive letter such as `Z:` or `Z:\`, or the full path to an existing empty NTFS folder such as `C:\Mounts\Halo`. When omitted, the application runs in drag-and-drop mode (auto drive letter + Explorer). Linux/macOS: an existing directory; when omitted, a temporary directory is created, printed after mounting, and removed on unmount. |
| `[options...]` | No | Zero or more option flags. Options may appear anywhere after the image path (before or after the mount path); extra positional arguments are rejected. |

## Options

| Option | Short | Description |
| --- | --- | --- |
| `--debug` | `-d` | Enables Dokan debug mode and routes Dokan's internal log output to stderr/console. Useful for diagnosing mount or file access problems. |
| `--launch` | `-l` | Opens Windows Explorer at the mount path after a successful mount. |
| `--image-iso` | `-i` | Also exposes the raw Xbox image as a virtual read-only `image.iso` file at the mount root, for emulators that only accept a disc image (such as xemu). See [Virtual image.iso](#virtual-imageiso). |
| `--help` | `-h` | Prints the usage text and exits with code `0`. Handled before the update check, so help never triggers a network call. |

Option matching is case-insensitive and options may appear before or after the mount path.
Unknown options and unexpected positional arguments are rejected with an error and the usage text,
matching the Unix front end.

> In single-argument (drag-and-drop) mode, `--launch` is implied. Options may be combined with a
> single image argument (for example `game.iso -i`), which keeps drag-and-drop mode while applying
> the option.

## Virtual image.iso

`--image-iso` adds a synthetic read-only file named `image.iso` at the root of the mounted volume
while the normal file tree remains browsable. The raw image bytes are served as follows:

| Input | `image.iso` content |
| --- | --- |
| Plain ISO/XISO (`.iso`, `.xiso`) | The image file itself. |
| CISO (`.cso`, including split `.1.cso` sets) | The decompressed Xbox image, decoded on demand. |
| Xbox ISO CHD (`.chd`) | The decompressed Xbox image, decoded hunk-by-hunk on demand. |
| ZArchive with a single embedded XISO | The embedded image, decompressed on demand. |
| ZArchive directory tree | An XISO synthesized in memory from the archived files (volume descriptor, directory tables and file extents built with XISOSharp's layout primitives). File data is read from the archive on demand — nothing is extracted and the mount appears immediately. |

If the mounted image already contains a real file named `image.iso`, that real file is shown instead
of the synthetic one.

The synthetic file counts towards the reported volume size only when it is additional content
(a ZArchive directory tree). For plain images, CISO and CHD, `image.iso` is the same image the
volume is already mounted from, so its length is not added twice.

## Behavior by argument count

| Arguments | Behavior |
| --- | --- |
| `0` | Prints usage and a drag-and-drop hint, waits for a key press, exits with code `1`. |
| `1` | Drag-and-drop mode: validates the path, automatically selects the first free drive letter from `M:` through `R:`, mounts, opens Explorer, and waits for a key press or a mount failure. |
| `1` + options | Same as `1`, with the supplied options applied (`--launch` stays implied). |
| `2+` | Standard mode: the first positional argument is the image and the next positional argument is the mount path; options are parsed wherever they appear. The process stays in the foreground until `Ctrl+C` or process termination. |

On Linux and macOS there is no drag-and-drop mode or drive-letter selection: a missing mount path
creates a temporary directory instead, and `--launch` opens the file manager (`xdg-open`/`open`).

### Single-argument (drag-and-drop) details

- The image path must not be null, empty, or contain invalid path characters.
- The preferred drive letters are tried in this order: `M:`, `N:`, `O:`, `P:`, `Q:`, `R:`.
- If no preferred letter is free, an error is printed and the process exits with code `1` after a
  key press.
- The application waits for whichever happens first: the mount task finishing (typically with an
  error) or a key press. A key press triggers a clean unmount.
- Error messages are followed by a "Press any key to exit." prompt so the information is not lost
  when launched from Explorer.

### Standard (two or more arguments) details

- The application stays attached to the console and is stopped with `Ctrl+C`.
- `Ctrl+C` is intercepted: it does not terminate the process abruptly, it requests an unmount and
  then exits cleanly with code `0`.
- Unknown options and unexpected positional arguments print an error and the usage text, then exit
  with code `1` (matching the Unix front end).
- If the mount path is a drive letter (`X:` or `X:\`) and the process is not elevated, a warning is
  printed suggesting that administrator privileges may be required for a successful mount.
- If the mount path is a folder, it must already exist; a missing folder is reported before Dokan is
  invoked.

---

## Image path resolution

Not every missing file is reported immediately. The application applies four resolution strategies
in order and stops at the first match:

| Order | Rule | Example |
| --- | --- | --- |
| 1 | If the path exists as given, use it. | `D:\Games\Halo.iso` |
| 2 | If the path is a directory that contains exactly one image file (`.iso`, `.xiso`, `.cso`, `.chd` or `.zar`), use that file. A split CISO set (`game.1.cso`, `game.2.cso`, …) counts as one image and resolves to its first part. | `D:\Games` becomes `D:\Games\Halo.iso` |
| 3 | If the path has no extension, try each supported extension in preference order (`.iso`, `.xiso`, `.cso`, `.chd`, `.zar`). | `D:\Games\Halo` becomes `D:\Games\Halo.iso` |
| 4 | If the path is a bare filename (no directory separator), look in the current working directory, first as given and then with each supported extension appended. | `Halo` becomes `<cwd>\Halo.iso` |

If none of the strategies match, the error output includes contextual hints:

- the path is a directory (but contains zero or multiple image files);
- an extension-appended variant was tried and not found;
- the path contains spaces but was not quoted.

Matching is case-insensitive: Windows file system lookups ignore case, and on case-sensitive file
systems (Linux, macOS) the resolver scans directory entries with a case-insensitive comparison, so
`game` finds `GAME.CHD` and a directory containing `GAME.ISO` still resolves.

---

## Mount point rules

| Mount point type | Notes |
| --- | --- |
| Drive letter | `Z:` or `Z:\`. A trailing backslash is stripped before passing the path to Dokan. The drive must be free. |
| NTFS folder | The folder must exist, be on an NTFS volume, and generally be empty. |

Additional behavior:

- **Administrator privileges:** when running elevated, the application enables Dokan's
  `MountManager` option, which is the most reliable way to create drive-letter mounts. Without
  elevation this option is intentionally omitted because it frequently causes the error
  *"Something's wrong with the Dokan driver"*.
- **Write protection:** every mount is created with Dokan's `WriteProtection` and `CurrentSession`
  options.
- **Debug mode:** adds `DebugMode` and `StderrOutput` to the Dokan options.

---

## Exit codes

| Exit code | Meaning |
| --- | --- |
| `0` | Clean unmount after a successful mount. |
| `1` | Any failure: usage printed, Dokan missing, image not found, invalid image, mounting error, or unhandled exception. |

Errors are written to `stderr`. Diagnostics are additionally written to the log files described in
[Services](Services).

---

## Console behavior

- The Windows console is set to a black background with green text and cleared at startup; the Unix
  console uses the terminal's own colors.
- Serilog's console sink uses no color theme (plain text) with the template
  `[HH:mm:ss LEV] message`.
- Error messages use `Console.Error`, so they can be redirected independently.
- When standard input/output is redirected (for example, when run from a script), the update prompt
  is skipped automatically on every platform: the version details and download URL are printed
  instead, and the Windows message box is not shown (so scripted runs can never block on a dialog).

---

## Examples

```shell
# Print usage
SimpleXisoDrive.exe

# Print usage explicitly (no network call)
SimpleXisoDrive.exe --help

# Drag-and-drop equivalent: auto drive letter + Explorer
SimpleXisoDrive.exe "D:\Games\Halo.iso"

# Mount to Z: without launching Explorer
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z:

# Mount to Z:, launch Explorer, enable debug output
SimpleXisoDrive.exe "D:\Games\Halo.iso" Z: -l -d

# Mount into an NTFS folder
SimpleXisoDrive.exe "D:\Games\Halo.iso" "C:\Mounts\Halo" --launch

# Omit the extension and let the resolver find the file
SimpleXisoDrive.exe "D:\Games\Halo" Z:

# Mount a ZArchive the same way
SimpleXisoDrive.exe "D:\Games\Halo.zar" Z:

# Mount a CISO image (single .cso or the first part of a split set)
SimpleXisoDrive.exe "D:\Games\Halo.cso" Z:

# Mount a CISO image and expose the decompressed image as image.iso
SimpleXisoDrive.exe "D:\Games\Halo.cso" Z: --image-iso

# Mount an Xbox ISO stored as CHD
SimpleXisoDrive.exe "D:\Games\Halo.chd" Z:

# Mount a CHD and expose the decompressed image as image.iso
SimpleXisoDrive.exe "D:\Games\Halo.chd" Z: -i

# Mount a ZArchive tree and expose a synthesized image.iso for an emulator
SimpleXisoDrive.exe "D:\Games\Halo.zar" Z: -i

# Mount the only image file in a directory
SimpleXisoDrive.exe "D:\Games\HaloCollection" Z:
```

---

## Related pages

- [Getting Started](Getting-Started)
- [Troubleshooting](Troubleshooting)
- [Architecture](Architecture) - what happens internally after the arguments are parsed.
