# XDVDFS Format

Xbox Disc Video File System (XDVDFS) is the file system used on original Xbox game discs. This page
documents the subset of the format that SimpleXisoDrive parses, including the on-disk structures
and the detection logic.

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
| `0x018` | 4 | Root directory table size | Read and skipped by SimpleXisoDrive |
| `0x01C` | 8 | Volume creation time | Windows FILETIME (`int64`) |
| `0x024` - `0x7EB` | - | Reserved | Not used by SimpleXisoDrive |
| `0x7EC` | 20 | Second magic ID | `MICROSOFT*XBOX*MEDIA` |

A descriptor is considered valid only when **both** magic IDs match the expected signature. If the
stored FILETIME cannot be converted, `CreationTime` falls back to `DateTime.MinValue` without
failing the mount.

### Discovery locations

SimpleXisoDrive probes five locations, in this order, using the same sector-32 descriptor layout:

| Order | Variant | Sector | Byte offset | Notes |
| --- | --- | --- | --- | --- |
| 1 | Standard Xbox ISO | 32 | `0` | The common layout, descriptor at byte 65,536 |
| 2 | GLOBAL partition | 32 | `0x0FD90000` (265,879,552) | Matches extract-xiso's `GLOBAL_LSEEK_OFFSET` |
| 3 | XGD3 | 32 | `0x02080000` (34,078,720) | Matches extract-xiso's `XGD3_LSEEK_OFFSET` |
| 4 | XGD1 dual-layer / hybrid | 32 | `2048 x 32 x 6192` (405,798,912) | Game partition offset for dual-layer discs |
| 5 | Rebuilt XISO | 0 | `0` | Descriptor written as sector 0; common for rebuilt images |

The first location that reads **and** validates wins. On success the chosen byte offset is stored in
`IsoSt.VolumeOffset`, and every subsequent read is shifted by it. `VolumeDescriptor.Sector` records
whether the descriptor was found at sector 32 or sector 0; `IsRebuiltXisoFormat()` returns `true`
only for sector 0.

### Failure diagnostics

If no location validates, the application throws `InvalidImageException` with a message that
includes:

- the image size in bytes and MB;
- one line per probed location describing why it failed (file too small, magic mismatch, I/O error);
- possible causes (not an Xbox ISO, corrupted or incomplete image, unsupported variant);
- the expected magic string.

If an early probe threw an exception **and** the final sector-0 probe also throws, the exceptions are
combined into an `AggregateException`.

---

## Directory entries

Each directory table is an array of entries stored at the start of the directory's sector. The root
entry is synthesized from `RootDirTableSector`; all other entries are read from disk.

### Entry layout

| Offset | Size | Field | Description |
| --- | --- | --- | --- |
| `0x00` | 2 | Left subtree | Index of the left child, or `0xFFFF` if none |
| `0x02` | 2 | Right subtree | Index of the right child, or `0xFFFF` if none |
| `0x04` | 4 | Start sector | First sector of the file data, or of the child directory table |
| `0x08` | 4 | File size | Size in bytes; `0` for directories |
| `0x0C` | 1 | Attributes | `XisoFsFileAttributes` flags (see below) |
| `0x0D` | 1 | Name length | Number of bytes in the name |
| `0x0E` | N | Name | ASCII bytes, optionally null-terminated |
| ... | 0-3 | Padding | Zero bytes aligning the entry to a 4-byte boundary |

### Entry size calculation

```text
EntrySize = 14 + NameLength
EntrySize += (4 - (EntrySize % 4)) % 4    // padding to 4-byte alignment
```

SimpleXisoDrive validates that the whole name and padding can be read; otherwise the read fails with
`EndOfStreamException`.

### Name handling

- Names are decoded as ASCII.
- If a null terminator (`\0`) appears, everything after it is discarded.
- The result is trimmed of surrounding whitespace.
- Names are matched case-insensitively when resolving paths.

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

The first entry of a directory table is always at offset `0` of its start sector. Walking a
directory therefore means:

1. read the entry at `(StartSector of directory, 0)`;
2. process it;
3. follow the left and right child indexes until `0xFFFF` sentinels are reached.

SimpleXisoDrive uses an iterative, stack-based traversal (not recursion) for both listing and lookup:

- Right children are pushed before left children so that left-hand entries are processed first.
- A visited set keyed by `(EntrySector, EntryOffset)` prevents infinite loops on circular trees.
- A self-reference check rejects child pointers that point back to the entry itself.
- A traversal that exceeds 100,000 nodes is aborted and logged as a possible corruption.

### Special cases

| Case | Behavior |
| --- | --- |
| `Left/RightSubTree == 0xFFFF` | No child; traversal stops down that branch. |
| Child offset equals the entry's own offset | Treated as invalid self-reference; ignored. |
| `FileSize == uint.MaxValue` (`0xFFFFFFFF`) | Logged as suspicious (a warning), entry is still used. |
| Empty name | Root entry uses an empty name; empty-named children are skipped when listing. |
| Directory read where `StartSector` is beyond EOF | Read fails, the entry is treated as missing. |
| `GetFirstChild` on a non-directory | Throws `InvalidOperationException`. |

---

## File data access

File contents live at `StartSector`, and a read at logical offset `O` maps to the absolute byte
position:

```text
absolute = VolumeOffset + (StartSector * 2048) + O
```

The application clamps reads to the file size reported by the directory entry and stops at end of
stream. Multiple files may share sectors when their sizes do not fill a sector; this is normal for
XDVDFS and requires no special handling for read-only access.

---

## Supported variants and limitations

Supported:

- standard original Xbox game ISOs with the descriptor at sector 32;
- rebuilt XISO images with the descriptor at sector 0;
- dual-layer and hybrid dumps using the XGD1 game partition offset;
- XGD3 and GLOBAL partition layouts.

Not supported:

- writing, renaming, deleting, or metadata changes of any kind;
- non-XDVDFS disc layouts and non-Xbox ISO formats;
- encrypted or Redump-style images that have not been converted to XISO;
- audio/video partition content (only the game partition file system is exposed);
- split or compressed image containers (the `.iso` must be a single file).

If an image fails to mount, the error message lists every probed descriptor location, which is the
fastest way to tell "wrong format" apart from "corrupted image". See [Troubleshooting](Troubleshooting).
