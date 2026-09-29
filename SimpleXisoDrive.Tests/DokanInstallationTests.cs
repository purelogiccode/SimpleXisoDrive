namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the Dokan installation path helpers.
/// </summary>
public class DokanInstallationTests
{
    /// <summary>
    /// Verifies the library path points at dokan2.dll under the system directory.
    /// </summary>
    [Fact]
    public void LibraryPath_PointsAtSystem32Dokan2Dll()
    {
        Assert.True(Path.IsPathRooted(DokanInstallation.LibraryPath));
        Assert.Equal("dokan2.dll", Path.GetFileName(DokanInstallation.LibraryPath));
        Assert.StartsWith(Environment.SystemDirectory, DokanInstallation.LibraryPath,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies the driver path points at dokan2.sys under the drivers directory.
    /// </summary>
    [Fact]
    public void DriverPath_PointsAtSystem32DriversDokan2Sys()
    {
        Assert.True(Path.IsPathRooted(DokanInstallation.DriverPath));
        Assert.Equal("dokan2.sys", Path.GetFileName(DokanInstallation.DriverPath));
        Assert.EndsWith(Path.Combine("drivers", "dokan2.sys"), DokanInstallation.DriverPath,
            StringComparison.OrdinalIgnoreCase);
    }
}