using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Pins the classification of XISOSharp lookup misses used to keep normal "path not
/// found" probes below the bug-report threshold.
/// </summary>
public class XisoPathNotFoundTests
{
    /// <summary>
    /// Verifies the library's documented missing-path exception is classified as a miss.
    /// </summary>
    [Fact]
    public void Is_ForInvalidDataPathNotFound_ReturnsTrue()
    {
        Assert.True(XisoPathNotFound.Is(new InvalidDataException("Path not found: '\\missing'")));
    }

    /// <summary>
    /// Verifies the match is case-insensitive so a library casing change does not break it.
    /// </summary>
    [Fact]
    public void Is_ForInvalidDataPathNotFoundDifferentCasing_ReturnsTrue()
    {
        Assert.True(XisoPathNotFound.Is(new InvalidDataException("path not found")));
    }

    /// <summary>
    /// Verifies an inner missing-path exception is found through the exception chain.
    /// </summary>
    [Fact]
    public void Is_ForWrappedPathNotFound_ReturnsTrue()
    {
        var inner = new InvalidDataException("Path not found");
        Assert.True(XisoPathNotFound.Is(new InvalidOperationException("outer", inner)));
    }

    /// <summary>
    /// Verifies standard file-system miss exceptions are classified as misses.
    /// </summary>
    [Fact]
    public void Is_ForFileSystemMissExceptions_ReturnsTrue()
    {
        Assert.True(XisoPathNotFound.Is(new FileNotFoundException("missing")));
        Assert.True(XisoPathNotFound.Is(new DirectoryNotFoundException("missing")));
    }

    /// <summary>
    /// Verifies other invalid-data messages are not mistaken for a miss.
    /// </summary>
    [Fact]
    public void Is_ForOtherInvalidDataMessage_ReturnsFalse()
    {
        Assert.False(XisoPathNotFound.Is(new InvalidDataException("XDVDFS magic string not found")));
    }

    /// <summary>
    /// Verifies unrelated exceptions are not classified as misses.
    /// </summary>
    [Fact]
    public void Is_ForUnrelatedException_ReturnsFalse()
    {
        Assert.False(XisoPathNotFound.Is(new InvalidOperationException("boom")));
    }

    /// <summary>
    /// Verifies a null exception is rejected.
    /// </summary>
    [Fact]
    public void Is_ForNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => XisoPathNotFound.Is(null!));
    }
}
