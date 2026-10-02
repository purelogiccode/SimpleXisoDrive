namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the mount path classification used for the administrator warning and the
/// folder-existence check.
/// </summary>
public class MountPathValidatorTests
{
    /// <summary>
    /// Verifies both documented drive-letter forms are recognized.
    /// </summary>
    /// <param name="path">The mount path to test.</param>
    [Theory]
    [InlineData("Z:")]
    [InlineData("Z:\\")]
    [InlineData("m:")]
    [InlineData("m:\\")]
    public void IsDriveLetterPath_AcceptsDriveLetters(string path)
    {
        Assert.True(MountPathValidator.IsDriveLetterPath(path));
    }

    /// <summary>
    /// Verifies non-drive-letter paths are rejected.
    /// </summary>
    /// <param name="path">The mount path to test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("Z")]
    [InlineData("1:")]
    [InlineData("Z:/")]
    [InlineData("C:\\mount")]
    [InlineData("Z:\\extra")]
    public void IsDriveLetterPath_RejectsOtherPaths(string path)
    {
        Assert.False(MountPathValidator.IsDriveLetterPath(path));
    }

    /// <summary>
    /// Verifies a null path is rejected.
    /// </summary>
    [Fact]
    public void IsDriveLetterPath_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MountPathValidator.IsDriveLetterPath(null!));
    }
}
