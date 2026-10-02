using SimpleXisoDrive.Core;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Additional image path resolution cases: whitespace, extension preference and split
/// CISO part handling.
/// </summary>
public class ImagePathResolverExtraTests
{
    /// <summary>
    /// Verifies a whitespace-only path resolves to null.
    /// </summary>
    [Fact]
    public void Resolve_WhitespacePath_ReturnsNull()
    {
        Assert.Null(ImagePathResolver.Resolve("   "));
    }

    /// <summary>
    /// Verifies the documented extension preference order: .iso wins over the rest.
    /// </summary>
    [Fact]
    public void Resolve_PrefersIsoOverOtherExtensions()
    {
        var baseName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var iso = baseName + ".iso";
        var xiso = baseName + ".xiso";
        var cso = baseName + ".cso";
        File.WriteAllText(iso, string.Empty);
        File.WriteAllText(xiso, string.Empty);
        File.WriteAllText(cso, string.Empty);

        try
        {
            Assert.Equal(iso, ImagePathResolver.Resolve(baseName));
        }
        finally
        {
            File.Delete(iso);
            File.Delete(xiso);
            File.Delete(cso);
        }
    }

    /// <summary>
    /// Verifies .xiso wins when the .iso candidate is absent.
    /// </summary>
    [Fact]
    public void Resolve_PrefersXisoOverCsoAndChd()
    {
        var baseName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var xiso = baseName + ".xiso";
        var cso = baseName + ".cso";
        var chd = baseName + ".chd";
        File.WriteAllText(xiso, string.Empty);
        File.WriteAllText(cso, string.Empty);
        File.WriteAllText(chd, string.Empty);

        try
        {
            Assert.Equal(xiso, ImagePathResolver.Resolve(baseName));
        }
        finally
        {
            File.Delete(xiso);
            File.Delete(cso);
            File.Delete(chd);
        }
    }

    /// <summary>
    /// Verifies a split CISO set whose first part uses the ".1.cso" naming resolves to
    /// that first part.
    /// </summary>
    [Fact]
    public void Resolve_DirectoryWithFirstCsoPart_ResolvesToFirstPart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var firstPart = Path.Combine(directory, "game.1.cso");
        File.WriteAllText(firstPart, string.Empty);
        File.WriteAllText(Path.Combine(directory, "game.2.cso"), string.Empty);

        try
        {
            Assert.Equal(firstPart, ImagePathResolver.Resolve(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a directory containing only continuation parts resolves to null.
    /// </summary>
    /// <param name="fileName">The continuation part name to create.</param>
    [Theory]
    [InlineData("game.2.cso")]
    [InlineData("game.10.cso")]
    [InlineData("game.99.cso")]
    public void Resolve_DirectoryWithOnlyContinuationPart_ReturnsNull(string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), string.Empty);

        try
        {
            Assert.Null(ImagePathResolver.Resolve(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}