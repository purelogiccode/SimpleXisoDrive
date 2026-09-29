# FAQ

Short answers to frequently asked questions.

---

### What is SimpleXisoDrive?

A utility that mounts original Xbox ISO/XISO images, Xbox ISO CHD images (`.chd`) and ZArchive
(`.zar`) files as read-only virtual drives or folder mount points, so their contents can be browsed
in the file manager or copied with normal tools.

### What is an XISO?

"XISO" commonly refers to an Xbox disc image in the XDVDFS layout. SimpleXisoDrive supports both
standard dumps (descriptor at sector 32) and rebuilt images (descriptor at sector 0).
CISO-compressed variants (`.cso`, either a single file or a split `.1.cso` part set) are supported
too and are decompressed on the fly — no extraction step is needed. Xbox ISO images stored as CHD
(`.chd`) are supported as well and are decompressed hunk-by-hunk on demand.

### What is a ZAR, and can I mount one?

A `.zar` file is a ZArchive: a directory tree stored with per-block zstd compression (used by the
scene to compress dumped game files). SimpleXisoDrive mounts the archived tree directly, or — when
the archive contains a single embedded XISO image — the image's contents. No extraction to disk is
performed; blocks are decompressed on demand.

### Does the application modify my ISO or ZAR?

No. The image or archive is opened with read access only, and every mutating file system operation is
denied. Dokan is also configured with write protection.

### Why can I not write to the mounted drive?

The volume is intentionally read-only. XDVDFS is a read-only format from the original Xbox era, and
keeping the source image immutable guarantees data safety.

### Can I copy files from the mounted drive?

Yes. Copying files out to a normal writable location works like any other read-only drive.

### Why does mounting a drive letter sometimes require administrator rights?

Creating global drive letters through Dokan is more reliable when the process is elevated
(`MountManager` option). Folder mounts usually work without elevation.

### Which drive letters does drag-and-drop use?

On Windows, the first free letter among `M:`, `N:`, `O:`, `P:`, `Q:`, and `R:`. If all are in use,
use the command line with a specific letter. Drag-and-drop does not exist on Linux and macOS.

### Can I mount multiple images at once?

Yes. Start a separate instance of the application for each image, each with its own console window
and mount point. Each instance uses its own `VfsContainer`.

### Does it support Xbox 360 or Xbox One images?

ISO and CHD mounting is limited to the original Xbox XDVDFS format; Xbox 360 and Xbox One discs use
different file systems and are not parsed (a CHD of an Xbox 360 disc is rejected). ZArchive mounting
is format-agnostic, though: any `.zar` directory tree (including one packed from Xbox 360 game files)
is exposed as-is. If a `.zar` contains a raw Xbox 360 ISO as a single file, that file is shown but
its internal file system is not parsed.

### What are XGD1, XGD3, and GLOBAL partitions?

Different disc layout variants used by original Xbox discs, mostly dual-layer games. SimpleXisoDrive
probes the known partition offsets and uses the first valid volume descriptor it finds. See
[XDVDFS Format](XDVDFS-Format).

### Why does Explorer show 0 bytes free space?

A read-only disc has no free space. `GetDiskFreeSpace` reports the image size as total capacity and
zero free bytes.

### Why do all files show the same timestamp?

XDVDFS directory entries do not store per-file timestamps. The application reports the volume
creation time from the volume descriptor for every entry.

### Why do some file names look different from other tools?

Names are stored as ASCII bytes and are decoded as such. Null terminators and surrounding
whitespace are trimmed, and matching is case-insensitive.

### Does SimpleXisoDrive send data over the internet?

It makes three types of requests: anonymous launch statistics, a GitHub update check, and
warning/fatal bug reports. Details and opt-out instructions are in
[Privacy and Networking](Privacy-and-Networking).

### Can I run it completely offline?

Yes. Block the hosts listed in [Privacy and Networking](Privacy-and-Networking); everything except
telemetry and the update prompt works normally.

### Can I disable the update prompt?

The prompt only appears when a newer release exists. Answer `n`, or block `api.github.com`.

### Is there a GUI?

No. The console is the only user interface: it reports status and waits for an unmount key or
`Ctrl+C`.

### Where are the logs?

Next to the executable: `logs\`, `error.log`, and `critical_error.log`. See [Services](Services).

### Why .NET 10?

The project targets the current LTS/current .NET line for modern language features and performance.
Self-contained builds remove the runtime prerequisite entirely.

### Does it run on Linux or macOS?

Yes. Linux and macOS use a FUSE 3 backend (`libfuse3` on Linux, macFUSE on macOS) instead of Dokan,
with native builds for x64 and ARM64. See [Linux and macOS](Linux-and-macOS) for prerequisites and
usage; drag-and-drop and drive letters are Windows-only.

### Why does an ISO that works elsewhere fail here?

Common reasons: the image is not XDVDFS (PC ISO), it is an encrypted Redump image, it is incomplete,
or it uses a layout variant that is not among the supported offsets. The error message lists every
probed location. A `.chd` that reports "not an Xbox ISO CHD" contains something other than an Xbox
ISO once decompressed (for example a CD, GD-ROM or Xbox 360 disc image); differential child CHDs
must be merged with their parent first.

### Can I use a folder on a network share as the mount point?

On Windows, use a local NTFS folder: drive letters and NTFS folder mount points are supported; UNC
mount points are not. On Linux and macOS any existing local directory can be used.

### Is the source code available?

Yes, under GPL-3.0: <https://github.com/purelogiccode/SimpleXisoDrive>.

### How can I contribute?

See [Contributing](Contributing). Bug reports with logs are especially valuable.
