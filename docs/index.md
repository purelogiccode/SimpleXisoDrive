# SimpleXisoDrive documentation

SimpleXisoDrive is a lightweight, read-only virtual file system driver for Windows, Linux and macOS.
It mounts original Xbox ISO images (`.iso`, `.xiso`, `.cso`), Xbox ISO CHD images (`.chd`) and
ZArchive (`.zar`) files as virtual drive letters or folder mount points and exposes their contents
directly in the file manager.

Use the side menu to browse the documentation, or jump straight to a page:

## User guide

| Page | What it covers |
| --- | --- |
| [Installation](Installation) | Requirements, Dokan and .NET runtime setup, installing and upgrading |
| [Linux and macOS](Linux-and-macOS) | FUSE prerequisites, usage, unmounting and platform notes |
| [Getting Started](Getting-Started) | Your first mount, drag-and-drop, unmounting, example workflows |
| [Command-Line Reference](Command-Line-Reference) | Complete argument/option reference, path resolution, exit codes |
| [Troubleshooting](Troubleshooting) | Every known error message with causes and fixes |
| [FAQ](FAQ) | Short answers to common questions |

## Technical reference

| Page | What it covers |
| --- | --- |
| [Architecture](Architecture) | Component overview, startup and mount lifecycle, threading, error handling |
| [XDVDFS Format](XDVDFS-Format) | On-disk format: volume descriptor, directory entries, partition offsets |
| [Virtual File System](Virtual-File-System) | Path resolution, caching, mount operation behaviour, read-only enforcement |
| [Services](Services) | Logging, bug reporting, statistics, update checker, access checks |
| [Privacy and Networking](Privacy-and-Networking) | Every network request, its payload, and how to run fully offline |
| [Glossary](Glossary) | Definitions of terms used throughout the documentation |

## Development

| Page | What it covers |
| --- | --- |
| [Building](Building) | Building and publishing for every supported platform |
| [Testing](Testing) | Test project layout, running tests, coverage |
| [Contributing](Contributing) | Code style, analyzers, workflow, pull request checklist |
| [Release History](Release-History) | Tagged releases and notable changes |

---

- Repository: <https://github.com/purelogiccode/SimpleXisoDrive>
- [What's New](https://github.com/purelogiccode/SimpleXisoDrive/blob/master/WhatsNew.md)
- [Releases](https://github.com/purelogiccode/SimpleXisoDrive/releases)
