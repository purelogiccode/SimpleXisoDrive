namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the drive-letter selection used by drag-and-drop mounts.
/// </summary>
public class DriveLetterSelectorTests
{
    /// <summary>
    /// Verifies the preferred letters are the documented M-R range in order.
    /// </summary>
    [Fact]
    public void PreferredLetters_AreTheDocumentedRange()
    {
        Assert.Equal("MNOPQR", new string(DriveLetterSelector.PreferredLetters));
    }

    /// <summary>
    /// Verifies the selector returns either null or a free preferred drive letter.
    /// </summary>
    [Fact]
    public void FindAvailableDriveLetter_ReturnsFreePreferredLetterOrNull()
    {
        var result = DriveLetterSelector.FindAvailableDriveLetter();

        if (result is null)
        {
            return;
        }

        Assert.Matches(@"^[M-R]:\\$", result);

        var usedLetters = DriveInfo.GetDrives()
            .Select(static d => d.Name)
            .Where(static name => name.Length > 0)
            .Select(static name => name[0])
            .ToHashSet();
        Assert.DoesNotContain(result[0], usedLetters);
    }
}
