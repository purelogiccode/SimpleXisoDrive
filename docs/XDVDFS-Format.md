# XDVDFS Format

Xbox Disc Video File System (XDVDFS) is the file system used on original Xbox game discs. This page
documents the format that SimpleXisoDrive exposes, including the on-disk structures and the
detection logic. Parsing is performed by the XISOSharp library.

All multi-byte integers are **little-endian**.

---

## Overview

| Property | Value |
| --- | --- |
| Sector size | 2048 bytes |
| Volume descriptor size | 1 sector (2048 bytes) |
| Magic identifier | `MICROSOFT*XBOX*MEDIA` (20 bytes) |
| Directory structure | Binary search tree of directory entries |
| File names | ASCII, up to 255 bytes, optionally null-terminated |
| Supported access | Read-only |

The image is divided into 2048-byte sectors. File data, directories, and the volume descriptor all
live at sector-aligned positions. Directory entries store offsets as **indexes multiplied by 4**, a
quirk inherited from the original format.

---

## Volume descriptor

The volume descriptor is a single 2048-byte sector with the following layout:

| Offset | Size | Field | Description |
| --- | --- | --- | --- |
| `0x000` | 20 | Magic ID | `MICROSOFT*XBOX*MEDIA` |
| `0x014` | 4 | Root directory table sector | Sector where the root directory table begins |
| `0x018` | 4 | Root directory table size | Byte size of the root directory table |
| `0x01C` | 8 | Volume creation time | Windows FILETIME (`int64`) |
| `0x024` - `0x7EB` | - | Reserved | Not used |
| `0x7EC` | 20 | Second magic ID | `MICROSOFT*XBOX*MEDIA` |

A descriptor is considered valid only when **both** magic IDs match the expected signature. The
stored FILETIME becomes the volume creation time; the raw value `0` maps to 1601-01-01 UTC.

### Discovery locations

XISOSharp probes the following locations, in this order:

| Order | Variant | Sector | Byte offset | Notes |
| --- | --- | --- | --- | --- |
| 1 | Standard Xbox ISO | 32 | `0` | The common layout, descriptor at byte 65,536 |
| 2 | GLOBAL partition | 32 | `0x0FD90000` (265,879,552) | Matches extract-xiso's `GLOBAL_LSEEK_OFFSET` |
| 3 | XGD3 | 32 | `0x02080000` (34,078,720) | Matches extract-xiso's `XGD3_LSEEK_OFFSET` |
| 4 | XGD2 hybrid | 32 | `0x89D80000` | Hybrid dual-layer layout |
| 5 | XGD1 dual-layer | 32 | `2048 x 32 x 6192` (405,798,912) | Game partition offset for dual-layer discs |
| 6 | Rebuilt XISO | 0 | `0` | Descriptor written at file offset 0; common for rebuilt images |

The first location that reads **and** validates wins. The chosen byte offset is reported as
`VolumeInfo.DiscLseek` and every subsequent read adds it. `VolumeInfo.DescriptorSector` records
whether the descriptor was found at sector 32 or sector 0.

### Failure diagnostics

If no location validates, XISOSharp reports an invalid volume (or throws `XisoFormatException`), and
`XisoVfsVolume` surfaces it as an `InvalidImageException` stating that the file is not a valid Xbox
ISO/XISO image, with the underlying reason as the inner exception.

---

## Directory entries

Each directory table is an array of entries stored at the start of the directory's sector. The root
entry is synthesized from `RootDirTableSector`; all other entries are read from disk.

### Entry layout

| Offset | Size | Field | Description |
| --- | --- | --- | --- |
| `0x00` | 2 | Left subtree | Index of the left child; `0` means none |
| `0x02` | 2 | Right subtree | Index of the right child; `0` means none |
| `0x04` | 4 | Start sector | First sector of the file data, or of the child directory table |
| `0x08` | 4 | File size | Size in bytes; `0` for directories |
| `0x0C` | 1 | Attributes | Raw XDVDFS attribute flags (see below) |
| `0x0D` | 1 | Name length | Number of bytes in the name |
| `0x0E` | N | Name | ASCII/Latin-1 bytes |
| ... | 0-3 | Padding | Zero bytes aligning the entry to a 4-byte boundary |

### Entry size calculation

```text
EntrySize = 14 + NameLength
EntrySize += (4 - (EntrySize % 4)) % 4    // padding to 4-byte alignment
```

### Name handling

- Names are decoded from the length-prefixed bytes as Latin-1 (ASCII-compatible).
- Names are matched case-insensitively when resolving paths.
- Names containing path separators are rejected by the XISOSharp directory walk.

### Attributes

| Flag | Value | Meaning |
| --- | --- | --- |
| `None` | `0x00` | No attributes set |
| `ReadOnly` | `0x01` | Read-only file |
| `Hidden` | `0x02` | Hidden entry |
| `System` | `0x04` | System entry |
| `Directory` | `0x10` | Entry is a directory |
| `Archive` | `0x20` | Archive/backup flag |
| `Normal` | `0x80` | Normal file |

The values map to Windows `FileAttributes` as follows:

- `ReadOnly` is always applied (the volume is read-only);
- `Directory`, `Hidden`, `System`, and `Archive` are applied when set;
- `Normal` is applied when none of Directory, Hidden, System, or Archive are present.

---

## Tree traversal

Child pointers are stored as indexes; the byte offset of a child entry within its directory table is:

```text
childOffset = Index * 4
```

The first entry of a directory table is always at offset `0` of its start sector. XISOSharp walks a
directory iteratively:

1. read the entry at the current table offset;
2. skip `.` and `..` entries while still following their children;
3. follow the left and right child indexes until an empty sentinel is reached.

The walk is hardened:

- every visited offset is tracked, so a circular or DAG-shaped table fails with a named error;
- each table is capped at a fixed number of entries;
- a first entry of all `0xFF` (left child `0xFFFF`) is treated as an empty directory;
- separator-bearing file names abort the walk.

### Special cases

| Case | Behavior |
| --- | --- |
| `Left/RightSubTree == 0` | No child; traversal stops down that branch. |
| First entry with left `0xFFFF` | Empty directory table (all-`0xFF` padding). |
| Empty name | The root entry is synthesized; empty-named children are skipped. |
| `FileSize` larger than the image | Reads are clamped to the stream length. |

---

## File data access

File contents live at `StartSector`, and a read at logical offset `O` maps to the absolute byte
position:

```text
absolute = DiscLseek + (StartSector * 2048) + O
```

`XisoVfsVolume` clamps reads to the file size reported by the directory entry and stops at end of
stream. Multiple files may share sectors when their sizes do not fill a sector; this is normal for
XDVDFS and requires no special handling for read-only access.

---

## Supported variants and limitations

Supported:

- standard original Xbox game ISOs with the descriptor at sector 32;
- rebuilt XISO images with the descriptor at sector 0;
- dual-layer and hybrid dumps using the XGD1/XGD2-hybrid game partition offsets;
- XGD3 and GLOBAL partition layouts.

Not supported:

- writing, renaming, deleting, or metadata changes of any kind;
- non-XDVDFS disc layouts and non-Xbox ISO formats;
- encrypted or Redump-style images that have not been converted to XISO;
- audio/video partition content (only the game partition file system is exposed);
- split or compressed image containers (the `.iso` must be a single file).

If an image fails to mount, the error states that the file is not a valid Xbox ISO/XISO image and
carries XISOSharp's diagnostic as the inner exception. See [Troubleshooting](Troubleshooting).
