using FuseSharp;

namespace SimpleXisoDrive.Unix.Tests;

/// <summary>
/// Tests the classification of <c>fuse_loop</c> results. External unmounts make libfuse
/// return <c>-ENODEV</c> and must be treated as a clean end of the session after the
/// session was initialized; other errors stay failures.
/// </summary>
public class FuseSessionResultTests
{
    /// <summary>
    /// Verifies an initialized session that ends with a closed FUSE device is a clean unmount.
    /// </summary>
    [Fact]
    public void IsCleanUnmountResult_EnodevAfterInit_IsClean()
    {
        Assert.True(FuseFileSystem.IsCleanUnmountResult(-PosixError.Enodev, sessionInitialized: true));
    }

    /// <summary>
    /// Verifies a normal loop exit is always a clean unmount.
    /// </summary>
    [Fact]
    public void IsCleanUnmountResult_Zero_IsClean()
    {
        Assert.True(FuseFileSystem.IsCleanUnmountResult(0, sessionInitialized: false));
        Assert.True(FuseFileSystem.IsCleanUnmountResult(0, sessionInitialized: true));
    }

    /// <summary>
    /// Verifies ENODEV before the init callback is still reported as a failure, so a mount
    /// that never started is not silently treated as successful.
    /// </summary>
    [Fact]
    public void IsCleanUnmountResult_EnodevBeforeInit_IsFailure()
    {
        Assert.False(FuseFileSystem.IsCleanUnmountResult(-PosixError.Enodev, sessionInitialized: false));
    }

    /// <summary>
    /// Verifies other loop errors keep their failure semantics regardless of initialization.
    /// </summary>
    /// <param name="loopResult">The result returned by <c>fuse_loop</c>.</param>
    [Theory]
    [InlineData(-5)]
    [InlineData(-2)]
    [InlineData(-13)]
    [InlineData(1)]
    public void IsCleanUnmountResult_OtherErrors_AreFailures(int loopResult)
    {
        Assert.False(FuseFileSystem.IsCleanUnmountResult(loopResult, sessionInitialized: true));
        Assert.False(FuseFileSystem.IsCleanUnmountResult(loopResult, sessionInitialized: false));
    }

    /// <summary>
    /// Verifies the ENODEV value used for external unmounts is the POSIX errno 19.
    /// </summary>
    [Fact]
    public void PosixError_Enodev_IsNineteen()
    {
        Assert.Equal(19, PosixError.Enodev);
    }
}
