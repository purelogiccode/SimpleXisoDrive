# Glossary

Definitions of terms used throughout the documentation and the source code.

---

## A - E

**Bug report**
A diagnostic document containing environment details, the error message, and exception details,
written to `error.log` and optionally submitted to the developer API. See [Services](Services).

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

**Entry (XISO)**
A file or directory inside an Xbox image, surfaced by `XisoVfsVolume` as an `IVfsEntry`. Backed by
XISOSharp's `ExplorerNode` for path-based images or `EntryInfo` for stream-based images (the legacy
name for the concept was `FileEntry`). See [XDVDFS Format](XDVDFS-Format).

**GLOBAL partition**
One of the supported disc layout offsets (`0x0FD90000`) at which the volume descriptor may be found
on certain releases.

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
(`XisoVfsVolume` for XDVDFS images, `ZarVfsVolume` for ZArchive trees).

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
