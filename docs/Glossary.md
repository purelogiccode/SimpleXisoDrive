# Glossary

Definitions of terms used throughout the documentation and the source code.

---

## A - E

**Bug report**
A diagnostic document containing environment details, the error message, and exception details,
written to `error.log` and optionally submitted to the developer API. See [Services](Services).

**CHD**
A compressed disc-image container (`.chd`, "Compressed Hunks of Data") read through the CHDSharp
library. SimpleXisoDrive decompresses Xbox ISO CHDs hunk-by-hunk on demand; CD and GD-ROM CHDs are
rejected, and the decompressed image must contain an XDVDFS filesystem. See
[Virtual File System](Virtual-File-System).

**CISO**
A compressed ISO container (`.cso`) that stores the image with block compression. SimpleXisoDrive
decompresses it on the fly; split sets use numbered parts (`game.1.cso`, `game.2.cso`, …) and are
mounted from the first part. See [XDVDFS Format](XDVDFS-Format).

**Dokan**
A Windows user-mode file system framework and kernel driver that lets applications expose virtual
file systems without writing a kernel driver. SimpleXisoDrive uses Dokan 2.x. See
<https://github.com/dokan-dev/dokany>.

**DokanNet**
The .NET wrapper around the Dokan library. The `IDokanOperations` interface implemented by
`XboxIsoVfsDokan` comes from this library.

**Drive letter mount**
Mapping a volume to a letter such as `Z:`. Requires a free letter and is most reliable with
administrator rights.

**Entry size**
The total number of bytes occupied by a directory entry on disk, including the 14-byte header, the
name, and padding to a 4-byte boundary.

---

## F - L

**FILETIME**
A Windows timestamp format representing 100-nanosecond intervals since 1601-01-01 UTC. The XDVDFS
volume descriptor stores the volume creation time as a FILETIME.

**FuseSharp**
The standalone `net10.0` FUSE 3 high-level mount library extracted from SimpleXisoDrive and packed
as `SimpleXisoDrive.FuseSharp`. It mounts any read-only volume that implements `IFuseVolume` and
has no dependency on `SimpleXisoDrive.Core`. See [FuseSharp](FuseSharp).

**FuseVolumeAdapter**
The internal class in `SimpleXisoDrive.Unix` that adapts an `IVfsVolume` (backslash-separated VFS
paths) to `IFuseVolume` (POSIX paths) and wraps each entry so reads can be forwarded to the Xbox
volume. See [FuseSharp](FuseSharp).

**Entry (XISO)**
A file or directory inside an Xbox image, surfaced by `XisoVfsVolume` as an `IVfsEntry`. Backed by
XISOSharp's `ExplorerNode` for path-based images or `EntryInfo` for stream-based images (the legacy
name for the concept was `FileEntry`). See [XDVDFS Format](XDVDFS-Format).

**GLOBAL partition**
One of the supported disc layout offsets (`0x0FD90000`) at which the volume descriptor may be found
on certain releases.

**Hunk**
The CHD compression unit: a fixed-size block of the decompressed image that is compressed and stored
independently. CHDSharp decompresses one hunk at a time and caches the most recent one.

**IFuseVolume**
The read-only POSIX-path volume contract consumed by FuseSharp's `FuseFileSystem`: volume label,
creation time and size, plus entry lookup, directory listing and file reads. The Unix front end
supplies an adapter over `IVfsVolume`. See [FuseSharp](FuseSharp).

**Iteration limit**
A safety cap that aborts enumeration of corrupted or circular directory trees. ZArchive directory
enumeration is capped at 100,000 entries; the XISO directory walk is bounded per table by XISOSharp.

---

## M - R

**Magic ID**
The signature `MICROSOFT*XBOX*MEDIA` stored twice in the volume descriptor. Both copies must match
for the descriptor to be valid.

**Mount point**
The location where the image becomes visible: a drive letter (`Z:`) or an NTFS folder path.

**NTFS folder mount**
Mapping a volume into an existing directory on an NTFS volume. Does not consume a drive letter.

**Partition offset**
The byte offset within the image at which the game partition (and therefore the volume descriptor)
begins. XISOSharp reports the winning offset as `VolumeInfo.DiscLseek` and every read adds it.

**Read-only volume**
A volume on which all mutating operations are rejected. SimpleXisoDrive mounts every image this way.

**Redump-style image**
A raw disc dump format that may contain additional/encrypted data and is not directly mountable as
XDVDFS until converted to XISO.

**Root directory table**
The sector referenced by the volume descriptor from which the entire directory tree is reachable.

---

## S - Z

**Sector**
The addressing unit for the image: 2048 bytes. File data and directory tables are sector-aligned.

**Serilog**
The structured logging library used by the application. Console, rolling file, and bug report sinks
are configured in `LoggingSetup`.

**Sink**
A Serilog output target. `BugReportSink` forwards Warning and higher events to the reporting
pipeline.

**Subtree index**
The 16-bit pointer to a child directory entry, expressed as an index that must be multiplied by 4 to
obtain a byte offset. The value `0` means "no child"; a first entry with `0xFFFF` marks an empty
directory table.

**Volume descriptor**
The structure that identifies an XDVDFS volume and points to the root directory table. It occupies
one sector and contains two magic IDs. See [XDVDFS Format](XDVDFS-Format).

**Volume offset**
The global byte offset applied to every stream read so the partition padding before the game
partition is ignored. XISOSharp reports it as `VolumeInfo.DiscLseek`.

**VFS (Virtual File System)**
The abstraction layer that resolves paths, caches entries, and serves file data to the Dokan
operation layer. `VfsContainer` is the facade; the actual storage is an `IVfsVolume` implementation
(`XisoVfsVolume` for XDVDFS images — including decompressed CHD images — and `ZarVfsVolume` for
ZArchive trees).

**XGD1 / XGD3**
Xbox Game Disc layout variants. XISOSharp probes their known partition offsets when looking for a
volume descriptor.

**XISO**
Common shorthand for an Xbox disc image in XDVDFS format. "Rebuilt XISO" images place the volume
descriptor at sector 0.

**XisoExplorer**
The XISOSharp explorer used for path-based mounts. Opened in keep-open mode, it holds one image
stream for the lifetime of the mount and serializes metadata operations on an internal lock.

**XisoReader**
The XISOSharp static API used for images embedded in archives: stream-based volume probing,
directory listing, entry lookup, and raw data reads.

**XDVDFS**
Xbox Disc Video File System, the file system used on original Xbox game discs. See
[XDVDFS Format](XDVDFS-Format).

**ZArchive / ZAR**
A compressed archive format (`.zar`, ZArchive 0.1.2) that stores a directory tree with per-block
zstd compression. SimpleXisoDrive mounts either the stored game tree or a single embedded XISO
image. See [Virtual File System](Virtual-File-System#zarchive-volumes).
