# Command-Line Reference

This page documents every command-line argument, option, and behavior of `SimpleXisoDrive.exe`.

---

## Synopsis

```text
SimpleXisoDrive.exe
SimpleXisoDrive.exe <iso-file>
SimpleXisoDrive.exe <iso-file> <mount-path> [options...]
```

## Arguments

| Argument | Required | Description |
| --- | --- | --- |
| `<iso-file>` | Yes, when arguments are supplied | Path to the Xbox ISO image. May omit the `.iso` extension in some cases (see [path resolution](#iso-path-resolution)). Paths containing spaces must be quoted. |
| `<mount-path>` | No | Drive letter such as `Z:` or `Z:\`, or the full path to an existing empty NTFS folder such as `C:\Mounts\Halo`. Required when options are supplied. |
| `[options...]` | No | Zero or more option flags. All arguments after the mount path are scanned for options. |

## Options

| Option | Short | Description |
| --- | --- | --- |
| `--debug` | `-d` | Enables Dokan debug mode and routes Dokan's internal log output to stderr/console. Useful for diagnosing mount or file access problems. |
| `--launch` | `-l` | Opens Windows Explorer at the mount path after a successful mount. |

Option matching is case-insensitive. Any token after the mount path that is not a recognized option
is ignored rather than treated as an error.

> In single-argument (drag-and-drop) mode, `--launch` is implied and `--debug` is not available.
> Use the two-argument form if you need debug output.

## Behavior by argument count

| Arguments | Behavior |
| --- | --- |
| `0` | Prints usage and a drag-and-drop hint, waits for a key press, exits with code `1`. |
| `1` | Drag-and-drop mode: validates the path, automatically selects the first free drive letter from `M:` through `R:`, mounts, opens Explorer, and waits for a key press or a mount failure. |
| `2+` | Standard mode: `<iso-file>` and `<mount-path>` are used as-is, options are parsed from the remaining arguments. The process stays in the foreground until `Ctrl+C` or process termination. |

### Single-argument (drag-and-drop) details

- The ISO path must not be null, empty, or contain invalid path characters.
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
- If the mount path is a drive letter (`X:\`) and the process is not elevated, a warning is printed
  suggesting that administrator privileges may be required for a successful mount.

---

## ISO path resolution

Not every missing file is reported immediately. The application applies four resolution strategies
in order and stops at the first match:

| Order | Rule | Example |
| --- | --- | --- |
| 1 | If the path exists as given, use it. | `D:\Games\Halo.iso` |
| 2 | If the path is a directory that contains exactly one `.iso` file, use that file. | `D:\Games` becomes `D:\Games\Halo.iso` |
| 3 | If the path has no extension and `<path>.iso` exists, use it. | `D:\Games\Halo` becomes `D:\Games\Halo.iso` |
| 4 | If the path is a bare filename (no directory separator), look in the current working directory, first as given and then with `.iso` appended. | `Halo` becomes `<cwd>\Halo.iso` |

If none of the strategies match, the error output includes contextual hints:

- the path is a directory (but contains zero or multiple `.iso` files);
- an `.iso`-appended variant was tried and not found;
- the path contains spaces but was not quoted.

Resolution is case-insensitive in practice because Windows file system lookups are case-insensitive;
the matching logic itself only performs literal `File.Exists`/`Directory.Exists` checks.

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
| `1` | Any failure: usage printed, Dokan missing, ISO not found, invalid image, mounting error, or unhandled exception. |

Errors are written to `stderr`. Diagnostics are additionally written to the log files described in
[Services](Services).

---

## Console behavior

- The console is set to a black background with green text and cleared at startup.
- Serilog's console sink uses no color theme (plain text) with the template
  `[HH:mm:ss LEV] message`.
- Error messages use `Console.Error`, so they can be redirected independently.
- When standard input is redirected (for example, when run from a script), the update prompt is
  skipped automatically.

---

## Examples

```shell
# Print usage
SimpleXisoDrive.exe

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

# Mount the only ISO in a directory
SimpleXisoDrive.exe "D:\Games\HaloCollection" Z:
```

---

## Related pages

- [Getting Started](Getting-Started)
- [Troubleshooting](Troubleshooting)
- [Architecture](Architecture) - what happens internally after the arguments are parsed.
