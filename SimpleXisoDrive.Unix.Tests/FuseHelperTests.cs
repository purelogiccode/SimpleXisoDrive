using System.Runtime.InteropServices;
using SimpleXisoDrive.Fuse;

namespace SimpleXisoDrive.Unix.Tests;

/// <summary>
/// Tests the FUSE path, timestamp and directory-fill helpers directly.
/// </summary>
public class FuseHelperTests
{
    /// <summary>
    /// Verifies native FUSE paths are converted to backslash VFS paths, including the root.
    /// </summary>
    /// <param name="native">The native path to convert.</param>
    /// <param name="expected">The expected VFS path.</param>
    [Theory]
    [InlineData("/sub/file.bin", "\\sub\\file.bin")]
    [InlineData("/", "\\")]
    [InlineData("", "\\")]
    [InlineData("relative", "relative")]
    public void ToVfsPath_ConvertsNativePaths(string native, string expected)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(native);
        try
        {
            Assert.Equal(expected, FuseFileSystem.ToVfsPath(pointer));
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    /// <summary>
    /// Verifies a null pointer maps to the VFS root.
    /// </summary>
    [Fact]
    public void ToVfsPath_WithNullPointer_ReturnsRoot()
    {
        Assert.Equal("\\", FuseFileSystem.ToVfsPath(IntPtr.Zero));
    }

    /// <summary>
    /// Verifies known UTC and unspecified timestamps convert to the expected epoch seconds.
    /// </summary>
    [Fact]
    public void ToUnixTime_ConvertsKnownValues()
    {
        Assert.Equal(0, FuseFileSystem.ToUnixTime(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(1577934245, FuseFileSystem.ToUnixTime(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
        Assert.Equal(1577934245, FuseFileSystem.ToUnixTime(new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Unspecified)));
    }

    /// <summary>
    /// Verifies local timestamps are converted through their offset.
    /// </summary>
    [Fact]
    public void ToUnixTime_ForLocalTime_MatchesDateTimeOffset()
    {
        var local = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Local);

        Assert.Equal(new DateTimeOffset(local).ToUnixTimeSeconds(), FuseFileSystem.ToUnixTime(local));
    }

    /// <summary>
    /// Verifies the minimum date converts without throwing.
    /// </summary>
    [Fact]
    public void ToUnixTime_ForMinValue_DoesNotThrow()
    {
        var expected = new DateTimeOffset(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc))
            .ToUnixTimeSeconds();

        Assert.Equal(expected, FuseFileSystem.ToUnixTime(DateTime.MinValue));
    }

    /// <summary>
    /// Verifies filling starts at the requested offset and reports the next offset.
    /// </summary>
    [Fact]
    public void FillDirectory_StartsAtOffsetAndReportsNextOffsets()
    {
        var names = new List<string> { ".", "..", "file.bin" };
        var seen = new List<(string Name, long NextOffset)>();

        var result = FuseFileSystem.FillDirectory(names, 2, (name, nextOffset) =>
        {
            seen.Add((Marshal.PtrToStringUTF8(name) ?? string.Empty, nextOffset));
            return 0;
        });

        Assert.Equal(0, result);
        var entry = Assert.Single(seen);
        Assert.Equal("file.bin", entry.Name);
        Assert.Equal(3, entry.NextOffset);
    }

    /// <summary>
    /// Verifies a non-positive offset starts at the first entry.
    /// </summary>
    [Fact]
    public void FillDirectory_NegativeOffset_StartsAtFirstEntry()
    {
        var names = new List<string> { ".", "file.bin" };
        var count = 0;

        FuseFileSystem.FillDirectory(names, -1, (_, _) =>
        {
            count++;
            return 0;
        });

        Assert.Equal(2, count);
    }

    /// <summary>
    /// Verifies a non-zero filler result stops the enumeration.
    /// </summary>
    [Fact]
    public void FillDirectory_StopsWhenCallbackReturnsNonZero()
    {
        var names = new List<string> { ".", "..", "file.bin" };
        var count = 0;

        FuseFileSystem.FillDirectory(names, 0, (_, _) => ++count == 1 ? 1 : 0);

        Assert.Equal(1, count);
    }
}
