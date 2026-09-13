using System.Security.AccessControl;
using DokanNet;
using FileAccess = DokanNet.FileAccess;

namespace SimpleXisoDrive.Tests;

public class XboxIsoVfsDokanTests : IDisposable
{
    private readonly string _imagePath;
    private readonly VfsContainer _vfs;
    private readonly XboxIsoVfsDokan _dokan;

    public XboxIsoVfsDokanTests()
    {
        _imagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".iso");
        File.WriteAllBytes(_imagePath, TestImageFactory.CreateXdvdfsImage(
        [
            new TestImageEntry("default.xbe", "hello xbox"u8.ToArray()),
            new TestImageEntry("sub", null),
            new TestImageEntry("sub/data.bin", "nested"u8.ToArray()),
        ]));

        _vfs = new VfsContainer(_imagePath);
        _dokan = new XboxIsoVfsDokan(_vfs);
    }

    public void Dispose()
    {
        _vfs.Dispose();
        File.Delete(_imagePath);
    }

    private static bool HasName(FileInformation file, string name) =>
        string.Equals(file.FileName, name, StringComparison.Ordinal);

    [Fact]
    public void GetVolumeInformation_ReportsXisoVolume()
    {
        var status = _dokan.GetVolumeInformation(out var label, out var features, out var fileSystemName,
            out var maximumComponentLength, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal("XBOX_ISO", label);
        Assert.Equal("XDVDFS", fileSystemName);
        Assert.Equal(255u, maximumComponentLength);
        Assert.True(features.HasFlag(FileSystemFeatures.ReadOnlyVolume));
        Assert.True(features.HasFlag(FileSystemFeatures.CasePreservedNames));
    }

    [Fact]
    public void GetDiskFreeSpace_ReportsReadOnlyCapacity()
    {
        var status = _dokan.GetDiskFreeSpace(out var freeBytesAvailable, out var totalNumberOfBytes,
            out var totalNumberOfFreeBytes, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal((long)_vfs.VolumeSize, totalNumberOfBytes);
        Assert.Equal(0, freeBytesAvailable);
        Assert.Equal(0, totalNumberOfFreeBytes);
    }

    [Fact]
    public void FindFiles_Root_IncludesDotButNotParent()
    {
        var status = _dokan.FindFiles("\\", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(3, files.Count);
        Assert.Contains(files, file => HasName(file, "."));
        Assert.DoesNotContain(files, file => HasName(file, ".."));
        Assert.Contains(files, file => HasName(file, "default.xbe") && file.Length == "hello xbox"u8.Length);
        Assert.True(files.Single(file => HasName(file, "sub")).Attributes.HasFlag(FileAttributes.Directory));
    }

    [Fact]
    public void FindFiles_Subdirectory_IncludesParentEntry()
    {
        var status = _dokan.FindFiles("\\sub", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Contains(files, file => HasName(file, "."));
        Assert.Contains(files, file => HasName(file, ".."));
        Assert.Contains(files, file => HasName(file, "data.bin") && file.Length == "nested"u8.Length);
    }

    [Fact]
    public void FindFiles_MissingPath_ReturnsNotADirectory()
    {
        Assert.Equal(DokanResult.NotADirectory, _dokan.FindFiles("\\missing", out _, new MockDokanFileInfo()));
    }

    [Fact]
    public void FindFilesWithPattern_FiltersByWildcard()
    {
        var status = _dokan.FindFilesWithPattern("\\", "*.xbe", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Contains(files, file => HasName(file, "."));
        Assert.Contains(files, file => HasName(file, "default.xbe"));
        Assert.DoesNotContain(files, file => HasName(file, "sub"));
    }

    [Fact]
    public void GetFileInformation_ForFile_ReportsSizeAttributesAndTimes()
    {
        var status = _dokan.GetFileInformation("\\default.xbe", out var fileInfo, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal("default.xbe", fileInfo.FileName);
        Assert.Equal("hello xbox"u8.Length, fileInfo.Length);
        Assert.True(fileInfo.Attributes.HasFlag(FileAttributes.ReadOnly));
        Assert.True(fileInfo.Attributes.HasFlag(FileAttributes.Archive));
        Assert.Equal(_vfs.VolumeCreationTime, fileInfo.CreationTime);
        Assert.Equal(_vfs.VolumeCreationTime, fileInfo.LastWriteTime);
    }

    [Fact]
    public void GetFileInformation_ForDirectory_ReportsZeroLength()
    {
        var status = _dokan.GetFileInformation("\\sub", out var fileInfo, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(0, fileInfo.Length);
        Assert.True(fileInfo.Attributes.HasFlag(FileAttributes.Directory));
    }

    [Fact]
    public void GetFileInformation_MissingEntry_ReturnsFileNotFound()
    {
        Assert.Equal(DokanResult.FileNotFound, _dokan.GetFileInformation("\\missing.xbe", out _, new MockDokanFileInfo()));
    }

    [Fact]
    public void NormalizePath_ResolvesSpecialSegments()
    {
        foreach (var path in new[] { "\\", "\\.", "\\..", @"\sub\.", @"\sub\..", "/sub" })
        {
            var status = _dokan.GetFileInformation(path, out _, new MockDokanFileInfo());
            Assert.Equal(DokanResult.Success, status);
        }
    }

    [Fact]
    public void CreateFile_ExistingFile_Open_SucceedsAndSetsContext()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        var status = _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open,
            FileOptions.None, FileAttributes.Normal, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.False(info.IsDirectory);
        Assert.NotNull(info.Context);
    }

    [Fact]
    public void CreateFile_MissingFile_ReturnsFileNotFound_ForOpenMode()
    {
        var status = _dokan.CreateFile("\\missing.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open,
            FileOptions.None, FileAttributes.Normal, new MockDokanFileInfo());

        Assert.Equal(DokanResult.FileNotFound, status);
    }

    [Fact]
    public void CreateFile_WriteAccess_ReturnsAccessDenied()
    {
        var status = _dokan.CreateFile("\\default.xbe", FileAccess.ReadData | FileAccess.WriteData, FileShare.Read,
            FileMode.Open, FileOptions.None, FileAttributes.Normal, new MockDokanFileInfo());

        Assert.Equal(DokanResult.AccessDenied, status);
    }

    [Fact]
    public void CreateFile_ExistingFile_CreateNew_ReturnsAlreadyExists()
    {
        var status = _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.CreateNew,
            FileOptions.None, FileAttributes.Normal, new MockDokanFileInfo());

        Assert.Equal(DokanResult.AlreadyExists, status);
    }

    [Fact]
    public void ReadFile_ReadsAtOffset()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Normal, info);

        var buffer = new byte[6];
        var status = _dokan.ReadFile("\\default.xbe", buffer, out var bytesRead, 6, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(4, bytesRead);
        Assert.Equal("xbox"u8.ToArray(), buffer[..4]);
    }

    [Fact]
    public void ReadFile_AtOrBeyondEnd_ReturnsZeroBytes()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Normal, info);

        var status = _dokan.ReadFile("\\default.xbe", new byte[8], out var bytesRead, "hello xbox"u8.Length, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void ReadFile_Directory_ReturnsInvalidHandle()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        _dokan.CreateFile("\\sub", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Normal, info);

        var status = _dokan.ReadFile("\\sub", new byte[8], out var bytesRead, 0, info);

        Assert.Equal(DokanResult.InvalidHandle, status);
        Assert.Equal(0, bytesRead);
    }

    [Fact]
    public void ReadOnlyOperations_ReturnAccessDenied()
    {
        var info = new MockDokanFileInfo();

        Assert.Equal(DokanResult.AccessDenied,
            _dokan.WriteFile("\\default.xbe", new byte[] { 1, 2 }, out var bytesWritten, 0, info));
        Assert.Equal(0, bytesWritten);
        Assert.Equal(DokanResult.AccessDenied, _dokan.FlushFileBuffers("\\default.xbe", info));
        Assert.Equal(DokanResult.AccessDenied,
            _dokan.SetFileAttributes("\\default.xbe", FileAttributes.Hidden, info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.SetFileTime("\\default.xbe", null, null, null, info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.DeleteFile("\\default.xbe", info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.DeleteDirectory("\\sub", info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.MoveFile("\\default.xbe", "\\moved.xbe", false, info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.SetEndOfFile("\\default.xbe", 0, info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.SetAllocationSize("\\default.xbe", 0, info));
    }

    [Fact]
    public void LockingAndStreams_BehaveAsDocumented()
    {
        var info = new MockDokanFileInfo();

        Assert.Equal(DokanResult.Success, _dokan.LockFile("\\default.xbe", 0, 1, info));
        Assert.Equal(DokanResult.Success, _dokan.UnlockFile("\\default.xbe", 0, 1, info));
        Assert.Equal(DokanResult.NotImplemented, _dokan.FindStreams("\\default.xbe", out var streams, info));
        Assert.Empty(streams);
    }

    [Fact]
    public void GetFileSecurity_GrantsReadAndExecute()
    {
        var status = _dokan.GetFileSecurity("\\default.xbe", out var security, AccessControlSections.Access,
            new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.NotNull(security);
    }
}
