using SimpleXisoDrive.Core;
using XISOSharp;

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

    /// <summary>
    /// Verifies a single-part CCI image resolves when the extension is omitted.
    /// </summary>
    [Fact]
    public void Resolve_AppendsCciExtensionWhenOnlyCciExists()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var cciPath = CreateCci(directory, "game");

        try
        {
            Assert.Equal(cciPath, ImagePathResolver.Resolve(Path.Combine(directory, "game")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a directory containing one CCI image resolves to it.
    /// </summary>
    [Fact]
    public void Resolve_DirectoryWithSingleCci_ResolvesToCci()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var cciPath = CreateCci(directory, "game");

        try
        {
            Assert.Equal(cciPath, ImagePathResolver.Resolve(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Verifies a split CCI set counts as one image and resolves to its first part.
    /// XISOSharp's `XisoImageSource` owns the split detection rules, so this
    /// exercises the container layer rather than a copied naming heuristic.
    /// </summary>
    [Fact]
    public void Resolve_DirectoryWithSplitCciSet_ResolvesToFirstPart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        CreateCci(directory, "game", sectors: 4, splitBytes: 2100);
        var firstPart = Path.Combine(directory, "game.1.cci");
        var secondPart = Path.Combine(directory, "game.2.cci");
        Assert.True(File.Exists(firstPart));
        Assert.True(File.Exists(secondPart));

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
    /// Verifies a numeric CCI name that is not part of a set starting at .1 is a
    /// standalone image (XISOSharp keeps it readable instead of rejecting the name).
    /// </summary>
    [Fact]
    public void Resolve_StandaloneNumericCci_ResolvesToIt()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        var cciPath = CreateCci(directory, "game.2");

        try
        {
            Assert.Equal(cciPath, ImagePathResolver.Resolve(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateCci(string directory, string baseName, int sectors = 8, long splitBytes = 0)
    {
        string isoPath = Path.Combine(directory, baseName + ".source.iso");
        File.WriteAllBytes(isoPath, new byte[sectors * Constants.SectorSize]);

        string outputPath = Path.Combine(directory, baseName);
        int result = XisoCci.CompressToCci(isoPath, outputPath, level: 1, splitBytes: splitBytes, overwrite: true);
        Assert.Equal(0, result);
        File.Delete(isoPath);

        return splitBytes > 0
            ? Path.Combine(directory, baseName + ".1.cci")
            : outputPath + ".cci";
    }
}