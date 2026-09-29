using SimpleXisoDrive.Fuse;

namespace SimpleXisoDrive.Unix.Tests;

/// <summary>
/// Tests the FUSE library resolver and availability probe. Tests that can only be
/// deterministic on a host without FUSE 3 (Windows) return early on other systems.
/// </summary>
public class FuseInteropTests
{
    /// <summary>
    /// The environment variable that overrides the FUSE library path.
    /// </summary>
    private const string LibraryOverrideVariable = "SIMPLEXISODRIVE_FUSE_LIBRARY";

    /// <summary>
    /// Verifies the resolver registration is idempotent and never throws.
    /// </summary>
    [Fact]
    public void RegisterResolver_IsIdempotent()
    {
        FuseInterop.RegisterResolver();
        FuseInterop.RegisterResolver();
    }

    /// <summary>
    /// Verifies a bogus override path does not break the library probe on hosts without FUSE 3.
    /// </summary>
    [Fact]
    public void TryLoadLibrary_WithMissingOverride_ReturnsFalseOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        WithLibraryOverride(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll"), () =>
        {
            Assert.False(FuseInterop.TryLoadLibrary(out var libraryPath));
            Assert.Null(libraryPath);
        });
    }

    /// <summary>
    /// Verifies the availability probe reports failure (and prints guidance) without FUSE 3.
    /// </summary>
    [Fact]
    public void Check_WithoutFuseLibrary_ReturnsFalseOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        WithLibraryOverride(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll"), () =>
        {
            Assert.False(FuseAvailability.Check(out var libraryPath));
            Assert.Null(libraryPath);
        });
    }

    /// <summary>
    /// Verifies calling into the FUSE library without it installed surfaces the
    /// platform loader exception rather than crashing the resolver.
    /// </summary>
    [Fact]
    public void FuseVersion_WithoutFuseLibrary_ThrowsLoaderExceptionOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        FuseInterop.RegisterResolver();

        WithLibraryOverride(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll"),
            () => Assert.Throws<DllNotFoundException>(() => _ = FuseInterop.FuseVersion()));
    }

    /// <summary>
    /// Verifies library candidates are ordered by numeric version so double-digit
    /// suffixes outrank single-digit ones and unparsable names sort lowest.
    /// </summary>
    [Fact]
    public void CompareLibraryFileNames_OrdersByNumericVersion()
    {
        Assert.True(FuseInterop.CompareLibraryFileNames("libfuse3.so.9", "libfuse3.so.10") < 0);
        Assert.True(FuseInterop.CompareLibraryFileNames("libfuse3.so.3", "libfuse3.so.3.14.0") < 0);
        Assert.True(FuseInterop.CompareLibraryFileNames("libfuse3.so.10", "libfuse3.so.3.14.0") > 0);
        Assert.True(FuseInterop.CompareLibraryFileNames("libfuse3.dylib", "libfuse3.so.3") < 0);
        Assert.Equal(0, FuseInterop.CompareLibraryFileNames("libfuse3.so.3", "/usr/lib/other/libfuse3.so.3"));
    }

    /// <summary>
    /// Runs an action with the library override variable set to the specified path.
    /// </summary>
    /// <param name="path">The override path to expose to the probe.</param>
    /// <param name="action">The assertions to run.</param>
    private static void WithLibraryOverride(string path, Action action)
    {
        var original = Environment.GetEnvironmentVariable(LibraryOverrideVariable);
        Environment.SetEnvironmentVariable(LibraryOverrideVariable, path);

        try
        {
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(LibraryOverrideVariable, original);
        }
    }
}