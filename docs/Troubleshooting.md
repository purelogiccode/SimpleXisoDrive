# Troubleshooting

This page lists error messages and symptoms, their causes, and how to resolve them.

> **Diagnostics tip:** run with `--debug` for verbose Dokan output, and inspect the files in
> `logs\`, `error.log`, and `critical_error.log` next to `SimpleXisoDrive.exe`. See
> [Services](Services).

---

## Dokan problems

### "Error: The Dokan runtime library (dokan2.dll) was not found."

**Cause:** `%SystemRoot%\System32\dokan2.dll` is missing, so the application exits immediately.

**Resolution:**

1. Download Dokan from <https://github.com/dokan-dev/dokany/releases>.
2. Install it with the default options (the runtime library is included).
3. Restart Windows if prompted.
4. Verify:
   ```powershell
   Test-Path "$env:SystemRoot\System32\dokan2.dll"
   ```

### "Warning: The Dokan driver (dokan2.sys) was not found."

**Cause:** The runtime library exists but the kernel driver does not. Mounting may fail.

**Resolution:** Reinstall Dokan and restart the computer. Verify
`%SystemRoot%\System32\drivers\dokan2.sys` exists.

### "Error: Failed to load the Dokan runtime library (dokan2.dll)."

**Cause:** `DllNotFoundException` when DokanNet tries to load `dokan2.dll`. The file may be
corrupt, from the wrong architecture, or missing dependencies.

**Resolution:**

1. Uninstall Dokan via **Windows Settings > Apps**.
2. Download and install the latest release for your architecture.
3. Restart the computer.
4. Retry. If it persists, reinstall the Visual C++ runtime that Dokan depends on.

### "Dokan Error: ..." (mount fails)

**Causes and fixes:**

| Cause | Fix |
| --- | --- |
| A previous mount still holds the drive letter | Unmount the other instance or restart Explorer. |
| Drive letter already in use | Choose another letter. |
| Driver busy or crashed | Restart the machine; check Windows Event Viewer. |
| Running without admin for a drive letter | Run as Administrator. |
| Folder mount on a non-NTFS volume | Use an NTFS folder or a drive letter. |
| Mount folder does not exist | Create the folder first. |

### "Something's wrong with the Dokan driver"

This generic Dokan error was historically caused by enabling the `MountManager` option without
administrator rights. SimpleXisoDrive enables `MountManager` only when elevated; if you still see
this message, try the following:

1. Run the application as Administrator.
2. Reinstall or update Dokan.
3. Restart Windows.

---

## Drive letter problems

### "Error: Could not find an available drive letter (M-R)."

**Cause:** Drag-and-drop/single-argument mode only tries `M:` through `R:`, and all six are in use.

**Resolution:**

- Free one of the letters or unmount another volume.
- Use the two-argument form with a specific free drive letter:
  ```shell
  SimpleXisoDrive.exe "D:\Games\Halo.iso" X:
  ```

### "WARNING: Administrator privileges are recommended for mounting drive letters."

This is informational. The application avoids Dokan's mount manager when not elevated because it
frequently fails. If the mount then fails, right-click `SimpleXisoDrive.exe` and choose **Run as
administrator**.

---

## Image path problems

### "Error: Image file not found at '<path>'"

The resolver tried all four strategies (see
[Command-Line Reference](Command-Line-Reference#image-path-resolution)) and failed. Additional hints
are printed when applicable:

| Hint | Meaning |
| --- | --- |
| "The specified path is a directory..." | You passed a folder with zero or multiple image files. Point at a file, or ensure the folder contains exactly one `.iso`, `.xiso` or `.zar`. |
| "Tried looking for '<path>.iso', '<path>.xiso' and '<path>.zar'..." | The extensionless variant also does not exist. Check the file name. |
| "If your file path contains spaces..." | Quote the path: `"D:\My Games\Halo.iso"` |

Note that the failure is also logged as a `FileNotFoundException` for diagnostics.

### Drag-and-drop passes the wrong working directory

Drag-and-drop passes the full path, so this is only an issue if you typed a relative path in a shell.
Use an absolute path or change to the correct directory first.

---

## Image format problems

### "Error: '<path>' is not a valid Xbox ISO/XISO image."

The file exists but no valid volume descriptor could be found. The most common reasons:

1. It is a **PC ISO**, not an Xbox disc image.
2. It is an **encrypted/Redump-style image** that must be converted to XISO first.
3. It is a **corrupted or incomplete** download.
4. It uses an **unsupported variant** (for example, a container format rather than a raw ISO).

XISOSharp's underlying diagnostic (the probed locations and their failure reasons) is attached as the
inner exception and written to the log file for support.

### "Error: XDVDFS magic string not found."

This variant is used for an image **embedded in a ZArchive** (or a renamed archive probed as one):
the single archived file was streamed as an XISO but its volume descriptor did not validate. The
archive itself is valid; the embedded file is not an Xbox image, so the archive mounts as a
directory tree instead. See [XDVDFS Format](XDVDFS-Format) for the supported layouts.

### "Error: '<path>' is not a valid ZArchive (.zar) file."

The file has the `.zar` extension (or was detected as an archive) but its footer, name table, or file
tree failed validation. Common causes:

1. The file is **corrupt or incomplete** (an interrupted download or copy).
2. It is **not a ZArchive** at all (a renamed `.iso`, `.zip`, or other container).
3. It was produced by a **newer/unsupported ZArchive version** (the reader supports ZArchive 0.1.2).

To verify the source, list the archive with a ZArchive tool such as `zarchive.exe`. If the archive is
valid but mounts as a single `.iso` file, it uses the embedded-image layout and should have been
detected as an XDVDFS image instead.

### "Failed to read ZArchive: ..." or unreadable files inside a mounted ZAR

ZArchiveSharp decompresses 64 KiB blocks on demand and keeps a small cache. A block that fails to
decode causes a short read for that file (the mounted volume stays usable). If only some files are
affected, the archive is likely corrupt; re-create it from the original ISO. Empty-named and
over-long (`>= 0x80` character) ZAR entries are skipped by the underlying format reader.

### "Error: Invalid Entry" or unusual directory results

Some corrupt entries are logged and skipped. Look for warnings such as:

- `Suspicious FileSize detected: 4294967295 for '<name>'` - the entry claims to be 4 GiB;
- `TraverseBinaryTreeForAll: Max iterations reached...` - the directory tree appears to contain
  cycles.

These do not necessarily prevent browsing, but the affected branch may be incomplete. Re-dump the
image if you suspect corruption.

---

## Runtime symptoms

### The console window closes immediately

- When started with no valid arguments from Explorer, the application waits for a key on most error
  paths. If it still closes, run it from a terminal to capture the message.
- On success in command-line mode the window stays open until `Ctrl+C`.

### The mounted volume is empty or shows fewer files than expected

- Confirm the image is not corrupted (see above) and that the game partition is the intended one.
- Directory listings come directly from the image's directory tree; empty-named entries are skipped
  by design.

### Explorer does not open after `--launch`

`-l` asks Windows to open the mount path. If Explorer does not appear:

- make sure the mount actually succeeded (check the console and `logs\`);
- check that `explorer.exe` is available (customized systems may block it).

### Performance feels slow for very large images

Reads are streamed directly from the image; performance depends on disk speed and fragmentation. Mount
to a local drive for best results; network shares and external USB drives are slower. ZArchive reads
additionally decompress each 64 KiB block on first access and cache the most recent blocks, so the
first pass over a `.zar` is CPU-bound while repeated reads of the same region are fast.

### Antivirus interferes with mounting or reading

The application opens the ISO or ZAR with `FileShare.ReadWrite` specifically to coexist with
scanners. If a scanner still locks the file, add an exclusion for the image folder or the
application.

### The update prompt appears on every start

This is expected until the installed version matches the latest GitHub release. Answer `n` to skip.
The prompt is skipped automatically when standard input is redirected.

---

## Collecting information for a bug report

Include:

1. The exact command line used (redact personal paths if desired).
2. The complete console output.
3. The newest file in `logs\`.
4. `error.log` (and `critical_error.log` if present).
5. The image size and how it was produced (dump tool, format variant, and for `.zar` the packer used).

Remember that warning-level logs may already have been submitted automatically; see
[Privacy and Networking](Privacy-and-Networking).

---

## Still stuck?

- Review the [FAQ](FAQ) and [Command-Line Reference](Command-Line-Reference).
- Open an issue: <https://github.com/purelogiccode/SimpleXisoDrive/issues>
