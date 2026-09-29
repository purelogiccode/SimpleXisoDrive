# Linux and macOS

SimpleXisoDrive mounts Xbox ISO/XISO/CISO/CHD images and ZArchive files as read-only
directories on Linux and macOS using FUSE 3.

---

## Prerequisites

| Platform | Requirement |
| --- | --- |
| Both | .NET 10.0 Runtime (the base runtime). |
| Linux | FUSE 3: the `libfuse3` library and the `fuse3` tools (`fusermount3`). |
| macOS | [macFUSE](https://macfuse.io). macOS 15.4+ can use its FSKit backend, which needs no kernel extension. |

Install FUSE 3 with your distribution's package manager:

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

If it is missing, load the module with `sudo modprobe fuse`.

The application searches for the FUSE library in the standard locations. Set
`SIMPLEXISODRIVE_FUSE_LIBRARY` to an explicit path if it lives somewhere else.

---

## Usage

```shell
SimpleXisoDrive <image-file> [mount-path] [options]
```

*   `<image-file>` — path to the `.iso`, `.xiso`, `.cso`, `.chd` or `.zar` file. A directory
    containing exactly one image, or a path without an extension, is also resolved.
*   `<mount-path>` — an existing empty directory. When omitted, the application creates
    a temporary directory and prints it after mounting.
*   `-l`, `--launch` — open the file manager (`xdg-open` or `open`) at the mount point.
*   `-d`, `--debug` — show verbose FUSE debug output.
*   `-i`, `--image-iso` — also expose the raw Xbox image as `image.iso` at the mount root.

Example:

```shell
mkdir -p ~/mnt/halo
SimpleXisoDrive ~/Games/Halo.iso ~/mnt/halo
ls ~/mnt/halo
```

To mount an Xbox ISO stored as CHD and expose the decompressed disc image for an emulator:

```shell
SimpleXisoDrive ~/Games/Halo.chd ~/mnt/halo --image-iso
# The emulator can open ~/mnt/halo/image.iso
```

To mount a ZArchive and expose the synthesized disc image for an emulator:

```shell
SimpleXisoDrive ~/Games/Halo.zar ~/mnt/halo --image-iso
# The emulator can open ~/mnt/halo/image.iso
```

---

## Unmounting

Use any of the following:

*   Press **Ctrl+C** in the terminal running SimpleXisoDrive.
*   Run `fusermount3 -u <mount-path>` on Linux.
*   Run `umount <mount-path>` on macOS.

SIGTERM and SIGHUP also trigger a clean unmount.

---

## Platform notes

*   The volume is mounted read-only. `open` rejects write access and macOS `setattr`
    returns `EROFS`.
*   Files are exposed with read permissions for everyone (`0444` for files, `0555` for
    directories). Timestamps are taken from the image or archive creation time.
*   On macOS, the volume name is taken from the image; use the `volname` mount option
    indirectly through the application (the label is sanitized automatically).
*   Linux mounts are performed through `fusermount3`, so unprivileged users can mount
    without `sudo`.
*   macOS may quarantine a downloaded binary. If it refuses to start, remove the
    attribute:

    ```shell
    xattr -d com.apple.quarantine SimpleXisoDrive
    ```

---

## Troubleshooting

| Symptom | Cause / fix |
| --- | --- |
| `libfuse3 was not found` | Install the FUSE 3 package for your distribution (see above). |
| `/dev/fuse was not found` | Load the FUSE kernel module: `sudo modprobe fuse`. |
| `fusermount3 was not found on PATH` | Install the `fuse3` tools package. |
| `macFUSE (libfuse3) was not found` | Install macFUSE from <https://macfuse.io> and allow the system extension. |
| Mount succeeds but the directory is empty | The image may be invalid; check the console/log output. |
| `fuse: mountpoint is not empty` | Use an empty directory as the mount point. |

See also [Troubleshooting](Troubleshooting).
