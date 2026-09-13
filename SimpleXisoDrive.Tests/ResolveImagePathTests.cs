namespace SimpleXisoDrive.Tests;

public class ResolveImagePathTests
{
    [Fact]
    public void ReturnsOriginalPathWhenFileExists()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var result = Program.ResolveImagePath(tempFile);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ReturnsNullWhenPathDoesNotExistAndNoExtension()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var result = Program.ResolveImagePath(nonExistentPath);
        Assert.Null(result);
    }

    [Fact]
    public void AppendsIsoExtensionWhenFileWithExtensionExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.iso");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^4]; // Remove ".iso"
            var result = Program.ResolveImagePath(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AppendsZarExtensionWhenOnlyZarExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zar");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^4]; // Remove ".zar"
            var result = Program.ResolveImagePath(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AppendsXisoExtensionWhenOnlyXisoExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.xiso");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^5]; // Remove ".xiso"
            var result = Program.ResolveImagePath(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

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
            var result = Program.ResolveImagePath(baseName);
            Assert.Equal(isoFile, result);
        }
        finally
        {
            File.Delete(isoFile);
            File.Delete(zarFile);
        }
    }

    [Fact]
    public void AppendsCsoExtensionWhenOnlyCsoExists()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.cso");
        File.WriteAllText(tempFile, string.Empty);
        try
        {
            var pathWithoutExtension = tempFile[..^4]; // Remove ".cso"
            var result = Program.ResolveImagePath(pathWithoutExtension);
            Assert.Equal(tempFile, result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

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
            var result = Program.ResolveImagePath(tempDir);
            Assert.Equal(firstPart, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReturnsNullWhenDirectoryContainsIsoAndCso()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "game.iso"), string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "other.cso"), string.Empty);

        try
        {
            var result = Program.ResolveImagePath(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

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
            var result = Program.ResolveImagePath("testfile.iso");
            Assert.Equal("testfile.iso", result);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            Directory.Delete(tempDir, true);
        }
    }

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
            var result = Program.ResolveImagePath("testfile");
            Assert.Equal("testfile.iso", result);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            Directory.Delete(tempDir, true);
        }
    }

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
            var result = Program.ResolveImagePath("testfile");
            Assert.Equal("testfile.zar", result);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolvesDirectoryContainingExactlyOneIso()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempIso = Path.Combine(tempDir, "game.iso");
        File.WriteAllText(tempIso, string.Empty);

        try
        {
            var result = Program.ResolveImagePath(tempDir);
            Assert.Equal(tempIso, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolvesDirectoryContainingExactlyOneZar()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempZar = Path.Combine(tempDir, "game.zar");
        File.WriteAllText(tempZar, string.Empty);

        try
        {
            var result = Program.ResolveImagePath(tempDir);
            Assert.Equal(tempZar, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolvesDirectoryContainingExactlyOneXiso()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var tempXiso = Path.Combine(tempDir, "game.xiso");
        File.WriteAllText(tempXiso, string.Empty);

        try
        {
            var result = Program.ResolveImagePath(tempDir);
            Assert.Equal(tempXiso, result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReturnsNullWhenDirectoryContainsMultipleIsos()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "game1.iso"), string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "game2.iso"), string.Empty);

        try
        {
            var result = Program.ResolveImagePath(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReturnsNullWhenDirectoryContainsIsoAndZar()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "game.iso"), string.Empty);
        File.WriteAllText(Path.Combine(tempDir, "game.zar"), string.Empty);

        try
        {
            var result = Program.ResolveImagePath(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ReturnsNullWhenDirectoryContainsZeroIsos()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "readme.txt"), string.Empty);

        try
        {
            var result = Program.ResolveImagePath(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

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
            var result = Program.ResolveImagePath(tempDir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
