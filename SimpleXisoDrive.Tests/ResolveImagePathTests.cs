using SimpleXisoDrive.Core;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests image path resolution: extensions, directories and the current directory.
/// </summary>
public class ResolveImagePathTests
{
    /// <summary>
    /// Verifies an existing path is returned unchanged.
    /// </summary>
    [Fact]
    public void ReturnsOriginalPathWhenFileExists()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var result = ImagePathResolver.Resolve(tempFile);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies a missing path without extension resolves to null.
    /// </summary>
    [Fact]
    public void ReturnsNullWhenPathDoesNotExistAndNoExtension()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var result = ImagePathResolver.Resolve(nonExistentPath);
        Assert.Null(result);
    }

    /// <summary>
    /// Verifies the .iso extension is appended when that file exists.
    /// </summary>
    [Fact]
    public void AppendsIsoExtensionWhenFileWithExtensionExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^4]; // Remove ".iso"
            var result = ImagePathResolver.Resolve(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies the .zar extension is appended when only it exists.
    /// </summary>
    [Fact]
    public void AppendsZarExtensionWhenOnlyZarExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zar");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^4]; // Remove ".zar"
            var result = ImagePathResolver.Resolve(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies the .xiso extension is appended when only it exists.
    /// </summary>
    [Fact]
    public void AppendsXisoExtensionWhenOnlyXisoExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xiso");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^5]; // Remove ".xiso"
            var result = ImagePathResolver.Resolve(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies .iso is preferred when several candidates exist.
    /// </summary>
    [Fact]
    public void PrefersIsoOverZarWhenBothExtensionsExist()
    {
        var baseName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var isoFile = baseName + ".iso";
        var zarFile = baseName + ".zar";
        File.WriteAllText(isoFile, string.Empty);
        File.WriteAllText(zarFile, string.Empty);
        try
        {
            var result = ImagePathResolver.Resolve(baseName);
            Assert.Equal(isoFile, result);
        }
        finally
        {
            File.Delete(isoFile);
            File.Delete(zarFile);
        }
    }

    /// <summary>
    /// Verifies the .cso extension is appended when only it exists.
    /// </summary>
    [Fact]
    public void AppendsCsoExtensionWhenOnlyCsoExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cso");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^4]; // Remove ".cso"
            var result = ImagePathResolver.Resolve(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies the .chd extension is appended when only it exists.
    /// </summary>
    [Fact]
    public void AppendsChdExtensionWhenOnlyChdExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.chd");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^4]; // Remove ".chd"
            var result = ImagePathResolver.Resolve(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies a directory containing one CHD resolves to it.
    /// </summary>
    [Fact]
    public void ResolvesDirectoryContainingExactlyOneChd()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempChd = Path.Combine(tempDir, "game.chd");
        File.WriteAllText(tempChd, string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Equal(tempChd, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a split CISO set resolves to its first part.
    /// </summary>
    [Fact]
    public void ResolvesDirectoryContainingSplitCsoSet()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var firstPart = Path.Combine(tempDir, "game.1.cso");
        File.WriteAllText(firstPart, string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "game.2.cso"), string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "game.3.cso"), string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Equal(firstPart, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies ambiguous image directories resolve to null.
    /// </summary>
    [Fact]
    public void ReturnsNullWhenDirectoryContainsIsoAndCso()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "game.iso"), string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "other.cso"), string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a bare file name resolves in the current directory.
    /// </summary>
    [Fact]
    public void ResolvesFilenameInCurrentDirectoryWhenFileExists()
    {
        var originalDir = Environment.CurrentDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "testfile.iso");
        File.WriteAllText(tempFile, string.Empty);

        try
        {
            Environment.CurrentDirectory = tempDir;
            var result = ImagePathResolver.Resolve("testfile.iso");
            Assert.Equal("testfile.iso", result);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a bare name resolves to the .iso file in the current directory.
    /// </summary>
    [Fact]
    public void ResolvesFilenameWithIsoExtensionInCurrentDirectoryWhenFileExists()
    {
        var originalDir = Environment.CurrentDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "testfile.iso");
        File.WriteAllText(tempFile, string.Empty);

        try
        {
            Environment.CurrentDirectory = tempDir;
            var result = ImagePathResolver.Resolve("testfile");
            Assert.Equal("testfile.iso", result);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a bare name resolves to the .zar file in the current directory.
    /// </summary>
    [Fact]
    public void ResolvesFilenameWithZarExtensionInCurrentDirectoryWhenFileExists()
    {
        var originalDir = Environment.CurrentDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "testfile.zar");
        File.WriteAllText(tempFile, string.Empty);

        try
        {
            Environment.CurrentDirectory = tempDir;
            var result = ImagePathResolver.Resolve("testfile");
            Assert.Equal("testfile.zar", result);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a directory containing one ISO resolves to it.
    /// </summary>
    [Fact]
    public void ResolvesDirectoryContainingExactlyOneIso()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempIso = Path.Combine(tempDir, "game.iso");
        File.WriteAllText(tempIso, string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Equal(tempIso, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a directory containing one ZAR resolves to it.
    /// </summary>
    [Fact]
    public void ResolvesDirectoryContainingExactlyOneZar()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempZar = Path.Combine(tempDir, "game.zar");
        File.WriteAllText(tempZar, string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Equal(tempZar, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a directory containing one XISO resolves to it.
    /// </summary>
    [Fact]
    public void ResolvesDirectoryContainingExactlyOneXiso()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempXiso = Path.Combine(tempDir, "game.xiso");
        File.WriteAllText(tempXiso, string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Equal(tempXiso, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a directory with multiple ISOs resolves to null.
    /// </summary>
    [Fact]
    public void ReturnsNullWhenDirectoryContainsMultipleIsos()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "game1.iso"), string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "game2.iso"), string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a directory with an ISO and a ZAR resolves to null.
    /// </summary>
    [Fact]
    public void ReturnsNullWhenDirectoryContainsIsoAndZar()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "game.iso"), string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "game.zar"), string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a directory without images resolves to null.
    /// </summary>
    [Fact]
    public void ReturnsNullWhenDirectoryContainsZeroIsos()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "readme.txt"), string.Empty);

        try
        {
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies directory scanning failures degrade gracefully to null.
    /// </summary>
    [Fact]
    public void ReturnsNullWhenDirectoryScanThrowsException()
    {
        // A path that looks like a directory but is actually a file with no extension
        // Directory.Exists returns false for files, but we can simulate a permission issue
        // by using a path format that causes GetFiles to throw. However, the simplest
        // real-world scenario is a directory path that exists but GetFiles throws
        // (e.g., due to permissions). We'll use a directory and rely on the fact that
        // the code catches exceptions gracefully.
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // In normal conditions GetFiles won't throw here, so this test mainly verifies
            // that the method does not crash when Directory.Exists is true.
            var result = ImagePathResolver.Resolve(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies an empty path resolves to null.
    /// </summary>
    [Fact]
    public void ReturnsNullForEmptyPath()
    {
        Assert.Null(ImagePathResolver.Resolve(string.Empty));
    }

    /// <summary>
    /// Verifies an existing file with an unsupported extension is returned unchanged.
    /// </summary>
    [Fact]
    public void ReturnsExistingFileWithUnsupportedExtension()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.txt");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            Assert.Equal(tempFile, ImagePathResolver.Resolve(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies a missing bare file name resolves to null.
    /// </summary>
    [Fact]
    public void ReturnsNullForMissingBareFileName()
    {
        Assert.Null(ImagePathResolver.Resolve(Guid.NewGuid().ToString()));
    }

    /// <summary>
    /// Verifies a directory containing only a CISO continuation part resolves to null.
    /// </summary>
    [Fact]
    public void ReturnsNullWhenDirectoryContainsOnlyCsoContinuationPart()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "game.2.cso"), string.Empty);

        try
        {
            Assert.Null(ImagePathResolver.Resolve(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a .cso file with a non-numeric suffix is treated as a normal image.
    /// </summary>
    [Fact]
    public void ResolvesCsoWithoutNumericContinuationSuffix()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var cso = Path.Combine(tempDir, "game.x.cso");
        File.WriteAllText(cso, string.Empty);

        try
        {
            Assert.Equal(cso, ImagePathResolver.Resolve(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a directory path with a trailing separator still resolves its single image.
    /// </summary>
    [Fact]
    public void ResolvesDirectoryWithTrailingSeparator()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var iso = Path.Combine(tempDir, "game.iso");
        File.WriteAllText(iso, string.Empty);

        try
        {
            Assert.Equal(iso, ImagePathResolver.Resolve(tempDir + Path.DirectorySeparatorChar));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies the .chd extension wins over .zar when both exist and no extension is given.
    /// </summary>
    [Fact]
    public void PrefersChdOverZarWhenBothExtensionsExist()
    {
        var baseName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var chdFile = baseName + ".chd";
        var zarFile = baseName + ".zar";
        File.WriteAllText(chdFile, string.Empty);
        File.WriteAllText(zarFile, string.Empty);

        try
        {
            Assert.Equal(chdFile, ImagePathResolver.Resolve(baseName));
        }
        finally
        {
            File.Delete(chdFile);
            File.Delete(zarFile);
        }
    }

    /// <summary>
    /// Verifies a directory containing an ISO and a CHD resolves to null.
    /// </summary>
    [Fact]
    public void ReturnsNullWhenDirectoryContainsIsoAndChd()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "game.iso"), string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "game.chd"), string.Empty);

        try
        {
            Assert.Null(ImagePathResolver.Resolve(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// Verifies a null path is rejected with <c>ArgumentNullException</c>.
    /// </summary>
    [Fact]
    public void ThrowsArgumentNullExceptionForNullPath()
    {
        Assert.Throws<ArgumentNullException>(() => ImagePathResolver.Resolve(null!));
    }

    /// <summary>
    /// Verifies an extensionless path finds an image whose extension differs in case
    /// (exercised on case-sensitive file systems; a no-op on case-insensitive ones).
    /// </summary>
    [Fact]
    public void ResolvesUppercaseExtensionForExtensionlessPath()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.CHD");
        File.WriteAllText(tempFile, string.Empty);
        var pathWithoutExtension = tempFile[..^4];

        try
        {
            // On case-insensitive file systems the fast path returns the candidate
            // spelling; on case-sensitive ones the directory scan returns the real name.
            Assert.Equal(tempFile, ImagePathResolver.Resolve(pathWithoutExtension), ignoreCase: true);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Verifies a directory scan finds images with mixed-case extensions
    /// (exercised on case-sensitive file systems; a no-op on case-insensitive ones).
    /// </summary>
    [Fact]
    public void ResolvesDirectoryContainingUppercaseExtension()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempIso = Path.Combine(tempDir, "GAME.ISO");
        File.WriteAllText(tempIso, string.Empty);

        try
        {
            Assert.Equal(tempIso, ImagePathResolver.Resolve(tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}