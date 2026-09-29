# Simple Xiso Drive

[![CI](https://github.com/purelogiccode/SimpleXisoDrive/actions/workflows/ci.yml/badge.svg)](https://github.com/purelogiccode/SimpleXisoDrive/actions/workflows/ci.yml)
[![Release](https://github.com/purelogiccode/SimpleXisoDrive/actions/workflows/release.yml/badge.svg)](https://github.com/purelogiccode/SimpleXisoDrive/actions/workflows/release.yml)
[![Latest release](https://img.shields.io/github/v/release/purelogiccode/SimpleXisoDrive?display_name=release&label=latest)](https://github.com/purelogiccode/SimpleXisoDrive/releases)
[![Downloads](https://img.shields.io/github/downloads/purelogiccode/SimpleXisoDrive/total?label=downloads)](https://github.com/purelogiccode/SimpleXisoDrive/releases)
[![Stars](https://img.shields.io/github/stars/purelogiccode/SimpleXisoDrive?style=flat&logo=github)](https://github.com/purelogiccode/SimpleXisoDrive/stargazers)
[![Issues](https://img.shields.io/github/issues/purelogiccode/SimpleXisoDrive?logo=github)](https://github.com/purelogiccode/SimpleXisoDrive/issues)
[![Last commit](https://img.shields.io/github/last-commit/purelogiccode/SimpleXisoDrive?logo=git)](https://github.com/purelogiccode/SimpleXisoDrive/commits/master)

[![License](https://img.shields.io/badge/license-GPL--3.0-blue.svg)](#license)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
![Windows](https://img.shields.io/badge/Windows-x64_%7C_ARM64-0078D6?logo=windows&logoColor=white)
![Linux](https://img.shields.io/badge/Linux-x64_%7C_ARM64-FCC624?logo=linux&logoColor=black)
![macOS](https://img.shields.io/badge/macOS-x64_%7C_ARM64-000000?logo=apple&logoColor=white)

Simple Xiso Drive is a lightweight utility that allows you to mount original Xbox ISO files (`.iso`, `.xiso`, `.cso`), Xbox ISO CHD files (`.chd`) and ZArchive (`.zar`) files as read-only virtual drives or directory mount points. Built on Dokan for Windows and FUSE 3 for Linux and macOS, it provides high-performance, read-only access to Xbox Disc Video File System (XDVDFS) contents directly from your file manager.

The application is designed for extreme memory efficiency and supports **Windows x64/ARM64**, **Linux x64/ARM64** and **macOS x64/ARM64**.

## Features

*   **Cross-Platform:** Native executables for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` and `osx-arm64`.
*   **Broad Format Support:** Handles standard Xbox ISO dumps (Sector 32), rebuilt "XISO" formats (Sector 0), Dual-Layer/Hybrid discs (Game Partition offsets), and CISO-compressed images (`.cso`, including split `.1.cso` part sets).
*   **Xbox ISO CHD Support:** Mounts Xbox ISO images stored as CHD (`.chd`) — any CHD format version and codec (zlib, lzma, huffman, flac, zstd) — with on-demand hunk decompression via the CHDSharp library. CD and GD-ROM CHDs are rejected; the decompressed image must contain an XDVDFS filesystem.
*   **ZArchive Support:** Mounts `.zar` archives directly — either the archived game tree or a single embedded XISO image — with on-demand zstd decompression (no extraction or temp files).
*   **Virtual `image.iso`:** The `--image-iso` option also exposes the raw Xbox image as `image.iso` at the mount root, for emulators that only accept a disc image (such as xemu). CISO and CHD images are decompressed on demand; a ZArchive directory tree is synthesized into an XISO in memory with XISOSharp's layout primitives — nothing is extracted to disk.
*   **Zero-Config Mounting:** On Windows, drag-and-drop an ISO, CISO or ZAR onto the executable to automatically mount it to the first available drive letter (M: through R:).
*   **Flexible Mount Points:** Mount ISOs as Windows drive letters (e.g., `Z:`) or NTFS folders, and as any directory on Linux and macOS.
*   **Automated Bug Reporting:** Includes a built-in telemetry system that securely reports filesystem crashes to the developer via the PureLogic Code API.
*   **Update Checker:** Automatically checks for newer versions on GitHub to ensure you have the latest compatibility fixes.
*   **Read-Only Safety:** Ensures the source ISO or ZAR remains unmodified.

## Prerequisites

1.  **.NET Runtime:** Requires the **.NET 10.0 Runtime** (the base runtime; the Desktop Runtime is not required).
2.  **Mount driver** (one of the following, depending on your operating system):
    *   **Windows:** the Dokan user-mode file system library (version 2.x.x).
        Download: [https://github.com/dokan-dev/dokany/releases](https://github.com/dokan-dev/dokany/releases).
    *   **Linux:** FUSE 3 (the `libfuse3` library plus the `fuse3` tools that provide `fusermount3`), for example `sudo apt install libfuse3-3 fuse3`.
    *   **macOS:** macFUSE. Download: [https://macfuse.io](https://macfuse.io). On macOS 15.4 or later the FSKit backend needs no kernel extension.

## Documentation

Comprehensive documentation is available in the [`docs`](docs/Home.md) folder and mirrors the
project wiki:

*   [Installation](docs/Installation.md) - requirements, Dokan setup, installing and upgrading.
*   [Linux and macOS](docs/Linux-and-macOS.md) - FUSE prerequisites, usage, and platform notes.
*   [Getting Started](docs/Getting-Started.md) - your first mount, drag-and-drop, unmounting.
*   [Command-Line Reference](docs/Command-Line-Reference.md) - arguments, options, exit codes.
*   [Architecture](docs/Architecture.md) - components, mount lifecycle, threading.
*   [Building](docs/Building.md) - build, test, and the CI/release workflow.
*   [What's New](WhatsNew.md) - release highlights.
*   [XDVDFS Format](docs/XDVDFS-Format.md) - on-disk structures and supported variants.
*   [Troubleshooting](docs/Troubleshooting.md) - every known error with causes and fixes.
*   [Privacy and Networking](docs/Privacy-and-Networking.md) - telemetry, endpoints, offline use.

## How to Use

### Windows

#### 1. Drag-and-Drop (Easiest)
*   Drag your `.iso`, `.xiso`, `.cso`, `.chd` or `.zar` file and drop it onto `SimpleXisoDrive.exe`.
*   The app will automatically find an available drive letter, mount the image, and open Windows Explorer.
*   **To Unmount:** Return to the console window and press any key.

#### 2. Command-Line
Run the application from a terminal for specific mount points:

```shell
SimpleXisoDrive.exe <PathToImageFile> <MountPoint> [options]
```

**Arguments:**
*   `<PathToImageFile>`: Full path to the `.iso`, `.xiso`, `.cso`, `.chd` or `.zar` file.
*   `<MountPoint>`: A drive letter (e.g., `Z:`) or a path to an empty NTFS folder.

### Linux and macOS

```shell
SimpleXisoDrive <image-file> [mount-path] [options]
```

**Arguments:**
*   `<image-file>`: Path to the `.iso`, `.xiso`, `.cso`, `.chd` or `.zar` file.
*   `<mount-path>`: An existing empty directory. When omitted, a temporary directory is created and printed after mounting.

**Options (all platforms):**
*   `-l`, `--launch`: Automatically opens the file manager at the mount point.
*   `-d`, `--debug`: Enables verbose driver debug output in the console.
*   `-i`, `--image-iso`: Also exposes the raw Xbox image as `image.iso` at the mount root (for emulators such as xemu).

Press **Ctrl+C** (or run `fusermount3 -u <mount-path>` / `umount <mount-path>`) to unmount.

## Technical Details

*   **XDVDFS Parsing:** Uses the XISOSharp library to traverse the Xbox-specific binary tree structure, including rebuilt sector-0 images.
*   **CHD Parsing:** Uses the CHDSharp library to decompress CHD hunks on demand (all CHD versions V1–V5 and codecs), exposing the decompressed Xbox image to the XDVDFS parser. Only CHDs whose decompressed image is a valid Xbox ISO are mounted; differential child CHDs must be merged with their parent first.
*   **ZArchive Parsing:** Mounts the ZArchive directory tree with on-demand zstd block decompression, and detects a single embedded XISO image automatically.
*   **Mount Backends:** Dokan on Windows (`DokanNet`); a small, self-contained FUSE 3 interop layer on Linux and macOS (`libfuse3` / macFUSE), with the platform-specific structures for Linux x64/ARM64 and macOS.
*   **Shared Core:** The image parsing, virtual file system and services live in `SimpleXisoDrive.Core`; the Windows and Unix front ends only implement the mount backend and CLI.
*   **Cycle Detection:** Includes safety checks to prevent infinite loops in corrupted or malformed ISO images.
*   **Mount Sanitization:** Automatically handles mount point strings (e.g., converts `Z:\` to `Z:`) to satisfy Dokan driver requirements.
*   **Smart Permissions:** Automatically adjusts Dokan options based on Administrator privileges to ensure the highest success rate for mounting.

## Troubleshooting

*   **Administrator Privileges (Windows):** While the tool attempts to mount in user-mode, mounting a global drive letter often requires Administrator rights. If the mount fails, right-click the `.exe` and select "Run as Administrator."
*   **Dokan Errors (Windows):** If you see "Dokan driver not found," ensure you have restarted your computer after installing the Dokan library.
*   **FUSE Errors (Linux):** If `libfuse3` is reported missing, install the FUSE 3 package for your distribution. Ensure `/dev/fuse` exists (`sudo modprobe fuse`) and that `fusermount3` is on your `PATH`.
*   **macFUSE Errors (macOS):** If macFUSE is reported missing, install it from [https://macfuse.io](https://macfuse.io) and allow the system extension when prompted. macOS may quarantine the downloaded binary; if it refuses to run, remove the quarantine attribute with `xattr -d com.apple.quarantine SimpleXisoDrive`.
*   **Invalid Image:** If the app reports that the file is not a valid Xbox ISO/XISO image, the file is likely a standard PC ISO or an encrypted Redump-style image that has not been processed for XISO compatibility. A `.zar` file that fails to open is reported as an invalid ZArchive instead; a `.chd` file is reported as not an Xbox ISO CHD when its decompressed content is not XDVDFS (for example a CD or GD-ROM CHD).

## Support the Project

If you find this tool useful, consider supporting development:
*   **Star the Repo:** [GitHub Repository](https://github.com/purelogiccode/SimpleXisoDrive)
*   **Donate:** [https://purelogiccode.com/Donate](https://purelogiccode.com/Donate)

## License

This project is licensed under **GPL-3.0**.

SimpleXisoDrive is an independent, community project and is **not affiliated with, endorsed by, or
sponsored by Microsoft Corporation**. "Xbox" and "Microsoft" are trademarks of the Microsoft group
of companies and are used here only to describe compatibility and file formats.

*   **DokanNet:** MIT License.
*   **Dokan Library:** LGPL/MIT.
*   **CHDSharp:** MIT License.
*   **libfuse:** LGPL-2.1 (Linux).
*   **macFUSE:** BSD-style licenses.

---
*Developed by [Pure Logic Code](https://purelogiccode.com/).*
