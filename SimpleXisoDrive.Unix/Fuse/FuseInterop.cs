using System.Reflection;
using System.Runtime.InteropServices;

#pragma warning disable MA0048 // Interop declarations share this file intentionally.

namespace SimpleXisoDrive.Fuse;

/// <summary>
/// Native FUSE 3 interop shared by the Linux and macOS mount backends. Only the
/// operations needed by a read-only volume are declared; the operation table is
/// passed with its exact prefix size, so libfuse leaves the remaining callbacks
/// unset (and therefore reports <c>ENOSYS</c> for them).
/// </summary>
internal static class FuseInterop
{
    /// <summary>
    /// The logical library name used by every <see cref="DllImportAttribute"/> in this class.
    /// </summary>
    internal const string LibraryName = "fuse3";

    private static int _resolverRegistered;

    /// <summary>
    /// Registers the DLL import resolver that maps <see cref="LibraryName"/> to the
    /// platform's FUSE 3 library (libfuse3 on Linux, macFUSE's libfuse3 on macOS).
    /// </summary>
    internal static void RegisterResolver()
    {
        if (Interlocked.Exchange(ref _resolverRegistered, 1) != 0)
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(FuseInterop).Assembly, Resolve);
    }

    /// <summary>
    /// Tries to load the FUSE 3 library from the known platform locations.
    /// </summary>
    /// <param name="libraryPath">When this method returns, the path or name that was loaded.</param>
    /// <returns><see langword="true"/> when the library can be loaded; otherwise <see langword="false"/>.</returns>
    internal static bool TryLoadLibrary(out string? libraryPath)
    {
        foreach (var candidate in EnumerateCandidates())
        {
            if (NativeLibrary.TryLoad(candidate, out _))
            {
                libraryPath = candidate;
                return true;
            }
        }

        libraryPath = null;
        return false;
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in EnumerateCandidates())
        {
            if (NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }

    private static IEnumerable<string> EnumerateCandidates()
    {
        var overridePath = Environment.GetEnvironmentVariable("SIMPLEXISODRIVE_FUSE_LIBRARY");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            yield return overridePath;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return "/usr/local/lib/libfuse3.dylib";
            yield return "/usr/local/lib/libfuse3.4.dylib";
            yield return "/opt/homebrew/lib/libfuse3.dylib";
            yield return "libfuse3.dylib";
            yield break;
        }

        yield return "libfuse3.so.4";
        yield return "libfuse3.so.3";
        yield return "libfuse3.so";

        foreach (var directory in LinuxLibraryDirectories)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "libfuse3.so.*");
            }
            catch
            {
                continue;
            }

            Array.Sort(files, StringComparer.Ordinal);
            for (var i = files.Length - 1; i >= 0; i--)
            {
                yield return files[i];
            }
        }
    }

    private static readonly string[] LinuxLibraryDirectories =
    [
        "/usr/lib/x86_64-linux-gnu",
        "/usr/lib/aarch64-linux-gnu",
        "/usr/lib64",
        "/usr/lib",
        "/lib/x86_64-linux-gnu",
        "/lib/aarch64-linux-gnu",
        "/usr/local/lib",
        "/lib"
    ];

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_new")]
    internal static extern IntPtr FuseNew(ref FuseArgs args, ref FuseOperationsLinux operations,
        nuint operationSize, IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_new")]
    internal static extern IntPtr FuseNew(ref FuseArgs args, ref FuseOperationsMac operations,
        nuint operationSize, IntPtr userData);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_mount")]
    internal static extern int FuseMount(IntPtr fuse, [MarshalAs(UnmanagedType.LPUTF8Str)] string mountPoint);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_unmount")]
    internal static extern void FuseUnmount(IntPtr fuse);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_destroy")]
    internal static extern void FuseDestroy(IntPtr fuse);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_loop")]
    internal static extern int FuseLoop(IntPtr fuse);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_exit")]
    internal static extern void FuseExit(IntPtr fuse);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_opt_free_args")]
    internal static extern void FuseOptFreeArgs(ref FuseArgs args);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "fuse_version")]
    internal static extern int FuseVersion();

    [DllImport("libc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "statfs", SetLastError = true)]
    private static extern int NativeStatFs([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr buffer);

    /// <summary>
    /// Issues a <c>statfs</c> syscall for the mount point. Unlike <c>stat</c>, the
    /// kernel never serves this from cache, so it reliably wakes a blocked FUSE loop.
    /// </summary>
    /// <param name="path">The mounted path to poke.</param>
    internal static void PokeMountPoint(string path)
    {
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            NativeStatFs(path, buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}

/// <summary>
/// The libfuse <c>fuse_args</c> structure. <see cref="Allocated"/> is zero because the
/// arguments are owned by the managed caller.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FuseArgs
{
    public int Argc;
    public IntPtr Argv;
    public int Allocated;
}

/// <summary>
/// The upstream libfuse 3 <c>fuse_operations</c> layout (Linux), truncated after
/// <c>destroy</c>. The order matches <c>include/fuse.h</c> exactly; unused callbacks
/// are left as null pointers.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FuseOperationsLinux
{
    public IntPtr GetAttr;
    public IntPtr ReadLink;
    public IntPtr MkNod;
    public IntPtr MkDir;
    public IntPtr Unlink;
    public IntPtr RmDir;
    public IntPtr SymLink;
    public IntPtr Rename;
    public IntPtr Link;
    public IntPtr Chmod;
    public IntPtr Chown;
    public IntPtr Truncate;
    public IntPtr Open;
    public IntPtr Read;
    public IntPtr Write;
    public IntPtr StatFs;
    public IntPtr Flush;
    public IntPtr Release;
    public IntPtr FSync;
    public IntPtr SetXAttr;
    public IntPtr GetXAttr;
    public IntPtr ListXAttr;
    public IntPtr RemoveXAttr;
    public IntPtr OpenDir;
    public IntPtr ReadDir;
    public IntPtr ReleaseDir;
    public IntPtr FSyncDir;
    public IntPtr Init;
    public IntPtr Destroy;
}

/// <summary>
/// The macFUSE <c>fuse_operations</c> layout, truncated after <c>destroy</c>.
/// macFUSE inserts a Darwin-only <c>setattr</c> callback after <c>getattr</c>.
/// The library is entered through the exported <c>fuse_new</c> symbol, which selects
/// the vanilla (non-Darwin) signatures for <c>getattr</c>, <c>statfs</c> and
/// <c>readdir</c>, so the remaining fields match the upstream layout.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FuseOperationsMac
{
    public IntPtr GetAttr;
    public IntPtr SetAttr;
    public IntPtr ReadLink;
    public IntPtr MkNod;
    public IntPtr MkDir;
    public IntPtr Unlink;
    public IntPtr RmDir;
    public IntPtr SymLink;
    public IntPtr Rename;
    public IntPtr Link;
    public IntPtr Chmod;
    public IntPtr Chown;
    public IntPtr Truncate;
    public IntPtr Open;
    public IntPtr Read;
    public IntPtr Write;
    public IntPtr StatFs;
    public IntPtr Flush;
    public IntPtr Release;
    public IntPtr FSync;
    public IntPtr SetXAttr;
    public IntPtr GetXAttr;
    public IntPtr ListXAttr;
    public IntPtr RemoveXAttr;
    public IntPtr OpenDir;
    public IntPtr ReadDir;
    public IntPtr ReleaseDir;
    public IntPtr FSyncDir;
    public IntPtr Init;
    public IntPtr Destroy;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int GetAttrDelegate(IntPtr path, IntPtr stat, IntPtr fileInfo);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int SetAttrMacDelegate(IntPtr path, IntPtr darwinAttr, int toSet, IntPtr fileInfo);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int OpenDelegate(IntPtr path, IntPtr fileInfo);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int ReadDelegate(IntPtr path, IntPtr buffer, nuint size, long offset, IntPtr fileInfo);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int StatFsDelegate(IntPtr path, IntPtr statvfs);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int ReadDirDelegate(IntPtr path, IntPtr buffer, IntPtr filler, long offset, IntPtr fileInfo,
    int flags);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int FillDirDelegate(IntPtr buffer, IntPtr name, IntPtr stat, long offset, int flags);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr InitDelegate(IntPtr connectionInfo, IntPtr fuseConfig);

/// <summary>
/// POSIX error numbers returned by the FUSE callbacks. The values used here are
/// identical on Linux and macOS.
/// </summary>
internal static class PosixError
{
    public const int Enoent = 2;
    public const int Eio = 5;
    public const int Eacces = 13;
    public const int Eisdir = 21;
    public const int Einval = 22;
    public const int Erofs = 30;
}