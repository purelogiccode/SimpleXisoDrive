using SimpleXisoDrive.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the administrator-privilege probe.
/// </summary>
public class CheckAccessTests
{
    /// <summary>
    /// Verifies the probe returns a boolean without throwing, on any privilege level.
    /// </summary>
    [Fact]
    public void IsAdministrator_ReturnsWithoutThrowing()
    {
        var result = CheckAccess.IsAdministrator();

        Assert.IsType<bool>(result);
    }
}