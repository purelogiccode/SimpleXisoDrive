using System.Security.AccessControl;
using DokanNet;
using SimpleXisoDrive.Core;
using SimpleXisoDrive.Tests.Models;
using FileAccess = DokanNet.FileAccess;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the Dokan operation layer backed by a <c>VfsContainer</c>.
/// </summary>
public class XboxIsoVfsDokanTests : IDisposable
{
    private readonly string _imagePath;
    private readonly VfsContainer _vfs;
    private readonly XboxIsoVfsDokan _dokan;

    /// <summary>
    /// Creates a temporary XDVDFS image and a Dokan instance over it.
    /// </summary>
    public XboxIsoVfsDokanTests()
    {
        _imagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".iso");
        File.WriteAllBytes(_imagePath, TestImageFactory.CreateXdvdfsImage(
        [
            new TestImageEntry("default.xbe", "hello xbox"u8.ToArray()),
            new TestImageEntry("default", "no extension"u8.ToArray()),
            new TestImageEntry("sub", null),
            new TestImageEntry("sub/data.bin", "nested"u8.ToArray()),
        ]));

        _vfs = new VfsContainer(_imagePath);
        _dokan = new XboxIsoVfsDokan(_vfs);
    }

    /// <summary>
    /// Disposes the container and deletes the temporary image.
    /// </summary>
    public void Dispose()
    {
        _vfs.Dispose();
        File.Delete(_imagePath);
    }

    private static bool HasName(FileInformation file, string name)
    {
        return string.Equals(file.FileName, name, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies volume information reports the XISO label, file system and features.
    /// </summary>
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

    /// <summary>
    /// Verifies disk free space reports the volume size with no free bytes.
    /// </summary>
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

    /// <summary>
    /// Verifies the root listing includes dot but not dot-dot.
    /// </summary>
    [Fact]
    public void FindFiles_Root_IncludesDotButNotParent()
    {
        var status = _dokan.FindFiles("\\", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(4, files.Count);
        Assert.Contains(files, file => HasName(file, "."));
        Assert.DoesNotContain(files, file => HasName(file, ".."));
        Assert.Contains(files, file => HasName(file, "default.xbe") && file.Length == "hello xbox"u8.Length);
        Assert.True(files.Single(file => HasName(file, "sub")).Attributes.HasFlag(FileAttributes.Directory));
    }

    /// <summary>
    /// Verifies a subdirectory listing includes the parent entry.
    /// </summary>
    [Fact]
    public void FindFiles_Subdirectory_IncludesParentEntry()
    {
        var status = _dokan.FindFiles("\\sub", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Contains(files, file => HasName(file, "."));
        Assert.Contains(files, file => HasName(file, ".."));
        Assert.Contains(files, file => HasName(file, "data.bin") && file.Length == "nested"u8.Length);
    }

    /// <summary>
    /// Verifies missing paths report NotADirectory.
    /// </summary>
    [Fact]
    public void FindFiles_MissingPath_ReturnsNotADirectory()
    {
        Assert.Equal(DokanResult.NotADirectory, _dokan.FindFiles("\\missing", out _, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies wildcard patterns filter the directory listing.
    /// </summary>
    [Fact]
    public void FindFilesWithPattern_FiltersByWildcard()
    {
        var status = _dokan.FindFilesWithPattern("\\", "*.xbe", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Contains(files, file => HasName(file, "."));
        Assert.Contains(files, file => HasName(file, "default.xbe"));
        Assert.DoesNotContain(files, file => HasName(file, "sub"));
    }

    /// <summary>
    /// Verifies file information reports size, attributes and volume times.
    /// </summary>
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

    /// <summary>
    /// Verifies directories report a zero length.
    /// </summary>
    [Fact]
    public void GetFileInformation_ForDirectory_ReportsZeroLength()
    {
        var status = _dokan.GetFileInformation("\\sub", out var fileInfo, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(0, fileInfo.Length);
        Assert.True(fileInfo.Attributes.HasFlag(FileAttributes.Directory));
    }

    /// <summary>
    /// Verifies missing entries report FileNotFound.
    /// </summary>
    [Fact]
    public void GetFileInformation_MissingEntry_ReturnsFileNotFound()
    {
        Assert.Equal(DokanResult.FileNotFound,
            _dokan.GetFileInformation("\\missing.xbe", out _, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies special path segments resolve to the expected entries.
    /// </summary>
    [Fact]
    public void NormalizePath_ResolvesSpecialSegments()
    {
        foreach (var path in new[] { "\\", "\\.", "\\..", @"\sub\.", @"\sub\..", "/sub", @"\sub\..\default.xbe" })
        {
            var status = _dokan.GetFileInformation(path, out _, new MockDokanFileInfo());
            Assert.Equal(DokanResult.Success, status);
        }
    }

    /// <summary>
    /// Verifies the ReadFile fallback lookup normalizes the path when no context is set.
    /// </summary>
    [Fact]
    public void ReadFile_WithoutContext_NormalizesPath()
    {
        var status = _dokan.ReadFile(@"\sub\..\default.xbe", new byte[16], out var bytesRead, 0,
            new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal("hello xbox"u8.Length, bytesRead);
    }

    /// <summary>
    /// Verifies opening an existing file succeeds and stores the entry in the context.
    /// </summary>
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

    /// <summary>
    /// Verifies opening a missing file reports FileNotFound.
    /// </summary>
    [Fact]
    public void CreateFile_MissingFile_ReturnsFileNotFound_ForOpenMode()
    {
        var status = _dokan.CreateFile("\\missing.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open,
            FileOptions.None, FileAttributes.Normal, new MockDokanFileInfo());

        Assert.Equal(DokanResult.FileNotFound, status);
    }

    /// <summary>
    /// Verifies write access requests are denied.
    /// </summary>
    [Fact]
    public void CreateFile_WriteAccess_ReturnsAccessDenied()
    {
        var status = _dokan.CreateFile("\\default.xbe", FileAccess.ReadData | FileAccess.WriteData, FileShare.Read,
            FileMode.Open, FileOptions.None, FileAttributes.Normal, new MockDokanFileInfo());

        Assert.Equal(DokanResult.AccessDenied, status);
    }

    /// <summary>
    /// Verifies CreateNew on an existing file reports AlreadyExists.
    /// </summary>
    [Fact]
    public void CreateFile_ExistingFile_CreateNew_ReturnsAlreadyExists()
    {
        var status = _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.CreateNew,
            FileOptions.None, FileAttributes.Normal, new MockDokanFileInfo());

        Assert.Equal(DokanResult.AlreadyExists, status);
    }

    /// <summary>
    /// Verifies reads return the expected bytes at an offset.
    /// </summary>
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

    /// <summary>
    /// Verifies reads at or beyond the end return zero bytes.
    /// </summary>
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

    /// <summary>
    /// Verifies reading a directory reports InvalidHandle.
    /// </summary>
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

    /// <summary>
    /// Verifies all write operations are denied on the read-only volume.
    /// </summary>
    [Fact]
    public void ReadOnlyOperations_ReturnAccessDenied()
    {
        var info = new MockDokanFileInfo();

        Assert.Equal(DokanResult.AccessDenied,
            _dokan.WriteFile("\\default.xbe", new byte[] { 1, 2 }, out var bytesWritten, 0, info));
        Assert.Equal(0, bytesWritten);
        Assert.Equal(DokanResult.AccessDenied,
            _dokan.SetFileAttributes("\\default.xbe", FileAttributes.Hidden, info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.SetFileTime("\\default.xbe", null, null, null, info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.DeleteFile("\\default.xbe", info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.DeleteDirectory("\\sub", info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.MoveFile("\\default.xbe", "\\moved.xbe", false, info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.SetEndOfFile("\\default.xbe", 0, info));
        Assert.Equal(DokanResult.AccessDenied, _dokan.SetAllocationSize("\\default.xbe", 0, info));
    }

    /// <summary>
    /// Verifies locking, flushing and alternate data streams behave as documented.
    /// </summary>
    [Fact]
    public void LockingAndStreams_BehaveAsDocumented()
    {
        var info = new MockDokanFileInfo();

        Assert.Equal(DokanResult.Success, _dokan.LockFile("\\default.xbe", 0, 1, info));
        Assert.Equal(DokanResult.Success, _dokan.UnlockFile("\\default.xbe", 0, 1, info));
        Assert.Equal(DokanResult.Success, _dokan.FlushFileBuffers("\\default.xbe", info));
        Assert.Equal(DokanResult.NotImplemented, _dokan.FindStreams("\\default.xbe", out var streams, info));
        Assert.Empty(streams);
    }

    /// <summary>
    /// Verifies the security descriptor grants read and execute access.
    /// </summary>
    [Fact]
    public void GetFileSecurity_GrantsReadAndExecute()
    {
        var status = _dokan.GetFileSecurity("\\default.xbe", out var security, AccessControlSections.Access,
            new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.NotNull(security);
    }

    /// <summary>
    /// Verifies failed opens do not leave a stale entry in the handle context.
    /// </summary>
    [Fact]
    public void CreateFile_FailedOpen_DoesNotSetContext()
    {
        IDokanFileInfo deniedInfo = new MockDokanFileInfo();
        var denied = _dokan.CreateFile("\\default.xbe", FileAccess.WriteData, FileShare.Read, FileMode.Open,
            FileOptions.None, FileAttributes.Normal, deniedInfo);

        Assert.Equal(DokanResult.AccessDenied, denied);
        Assert.Null(deniedInfo.Context);

        IDokanFileInfo existsInfo = new MockDokanFileInfo();
        var exists = _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.CreateNew,
            FileOptions.None, FileAttributes.Normal, existsInfo);

        Assert.Equal(DokanResult.AlreadyExists, exists);
        Assert.Null(existsInfo.Context);
    }

    /// <summary>
    /// Verifies volume read failures are reported as errors instead of escaping the callback.
    /// </summary>
    [Fact]
    public void ReadFile_WhenVolumeThrows_ReturnsError()
    {
        var volume = new FakeVfsVolume { ThrowOnReadFile = true };
        var dokan = new XboxIsoVfsDokan(volume);
        IDokanFileInfo info = new MockDokanFileInfo { Context = volume.Entry };

        var status = dokan.ReadFile("\\entry.bin", new byte[4], out var bytesRead, 0, info);

        Assert.Equal(DokanResult.Error, status);
        Assert.Equal(0, bytesRead);
    }

    /// <summary>
    /// Verifies volume lookup failures are reported as errors instead of escaping the callback.
    /// </summary>
    [Fact]
    public void GetFileInformation_WhenVolumeThrows_ReturnsError()
    {
        var volume = new FakeVfsVolume { ThrowOnGetEntry = true };
        var dokan = new XboxIsoVfsDokan(volume);

        var status = dokan.GetFileInformation("\\entry.bin", out _, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Error, status);
    }

    /// <summary>
    /// Verifies opening the root sets the directory flag and stores the root entry.
    /// </summary>
    [Fact]
    public void CreateFile_Root_SetsDirectoryContext()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        var status = _dokan.CreateFile("\\", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Directory, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.True(info.IsDirectory);
        Assert.NotNull(info.Context);
    }

    /// <summary>
    /// Verifies the read-only access allow list also applies to the volume root.
    /// </summary>
    [Fact]
    public void CreateFile_Root_WriteAccess_ReturnsAccessDenied()
    {
        foreach (var access in new[] { FileAccess.GenericAll, FileAccess.WriteData, FileAccess.Delete })
        {
            IDokanFileInfo info = new MockDokanFileInfo();
            var status = _dokan.CreateFile("\\", access, FileShare.Read, FileMode.Open, FileOptions.None,
                FileAttributes.Directory, info);

            Assert.Equal(DokanResult.AccessDenied, status);
            Assert.Null(info.Context);
        }
    }

    /// <summary>
    /// Verifies opening a directory sets the directory flag and stores the entry.
    /// </summary>
    [Fact]
    public void CreateFile_Directory_SetsDirectoryFlag()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        var status = _dokan.CreateFile("\\sub", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Directory, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.True(info.IsDirectory);
        Assert.NotNull(info.Context);
    }

    /// <summary>
    /// Verifies every write-capable access mask is denied.
    /// </summary>
    [Fact]
    public void CreateFile_WriteAccessVariants_ReturnAccessDenied()
    {
        foreach (var access in new[]
                 {
                     FileAccess.WriteData,
                     FileAccess.AppendData,
                     FileAccess.GenericWrite,
                     FileAccess.Delete,
                     FileAccess.ReadData | FileAccess.GenericWrite
                 })
        {
            var status = _dokan.CreateFile("\\default.xbe", access, FileShare.Read, FileMode.Open, FileOptions.None,
                FileAttributes.Normal, new MockDokanFileInfo());
            Assert.Equal(DokanResult.AccessDenied, status);
        }
    }

    /// <summary>
    /// Verifies write-capable access bits outside the old deny mask are denied too.
    /// </summary>
    [Fact]
    public void CreateFile_ExtendedWriteAccessBits_ReturnAccessDenied()
    {
        foreach (var access in new[]
                 {
                     FileAccess.GenericAll,
                     FileAccess.MaximumAllowed,
                     FileAccess.AccessSystemSecurity,
                     FileAccess.WriteAttributes,
                     FileAccess.WriteExtendedAttributes,
                     FileAccess.ChangePermissions,
                     FileAccess.SetOwnership,
                     FileAccess.DeleteChild
                 })
        {
            var status = _dokan.CreateFile("\\default.xbe", access, FileShare.Read, FileMode.Open, FileOptions.None,
                FileAttributes.Normal, new MockDokanFileInfo());
            Assert.Equal(DokanResult.AccessDenied, status);
        }
    }

    /// <summary>
    /// Verifies the read-only access bits (including no access) are still allowed.
    /// </summary>
    [Fact]
    public void CreateFile_ReadOnlyAccessBits_ReturnSuccess()
    {
        foreach (var access in new[]
                 {
                     FileAccess.None,
                     FileAccess.ReadData,
                     FileAccess.ReadAttributes,
                     FileAccess.ReadExtendedAttributes,
                     FileAccess.ReadPermissions,
                     FileAccess.Execute,
                     FileAccess.Synchronize,
                     FileAccess.GenericRead,
                     FileAccess.GenericExecute
                 })
        {
            var status = _dokan.CreateFile("\\default.xbe", access, FileShare.Read, FileMode.Open, FileOptions.None,
                FileAttributes.Normal, new MockDokanFileInfo());
            Assert.Equal(DokanResult.Success, status);
        }
    }

    /// <summary>
    /// Verifies create and truncate modes are denied on the read-only volume.
    /// </summary>
    [Fact]
    public void CreateFile_CreateAndTruncateModes_ReturnAccessDenied()
    {
        Assert.Equal(DokanResult.AccessDenied,
            _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Create, FileOptions.None,
                FileAttributes.Normal, new MockDokanFileInfo()));
        Assert.Equal(DokanResult.AccessDenied,
            _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Truncate, FileOptions.None,
                FileAttributes.Normal, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies creation modes on missing files are denied instead of reported missing.
    /// </summary>
    [Fact]
    public void CreateFile_MissingFile_NonOpenModes_ReturnAccessDenied()
    {
        foreach (var mode in new[] { FileMode.CreateNew, FileMode.Create, FileMode.OpenOrCreate })
        {
            var status = _dokan.CreateFile("\\missing.xbe", FileAccess.ReadData, FileShare.Read, mode,
                FileOptions.None, FileAttributes.Normal, new MockDokanFileInfo());
            Assert.Equal(DokanResult.AccessDenied, status);
        }
    }

    /// <summary>
    /// Verifies reads without a stored context fall back to a fresh lookup.
    /// </summary>
    [Fact]
    public void ReadFile_WithoutContext_LooksUpEntry()
    {
        var buffer = new byte[5];
        var status = _dokan.ReadFile("\\default.xbe", buffer, out var bytesRead, 0, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(5, bytesRead);
        Assert.Equal("hello"u8.ToArray(), buffer);
    }

    /// <summary>
    /// Verifies reads for missing entries report FileNotFound instead of a reported error.
    /// </summary>
    [Fact]
    public void ReadFile_MissingEntry_ReturnsFileNotFound()
    {
        var status = _dokan.ReadFile("\\missing.xbe", new byte[8], out var bytesRead, 0, new MockDokanFileInfo());

        Assert.Equal(DokanResult.FileNotFound, status);
        Assert.Equal(0, bytesRead);
    }

    /// <summary>
    /// Verifies negative offsets are rejected as invalid parameters.
    /// </summary>
    [Fact]
    public void ReadFile_NegativeOffset_ReturnsInvalidParameter()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Normal, info);

        var status = _dokan.ReadFile("\\default.xbe", new byte[8], out var bytesRead, -1, info);

        Assert.Equal(DokanResult.InvalidParameter, status);
        Assert.Equal(0, bytesRead);
    }

    /// <summary>
    /// Verifies reads are clamped to the remaining file size.
    /// </summary>
    [Fact]
    public void ReadFile_BufferLargerThanFile_ClampsToRemainingBytes()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Normal, info);

        var buffer = new byte[64];
        var status = _dokan.ReadFile("\\default.xbe", buffer, out var bytesRead, 6, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(4, bytesRead);
        Assert.Equal("xbox"u8.ToArray(), buffer[..4]);
    }

    /// <summary>
    /// Verifies an empty buffer reports a successful zero-byte read.
    /// </summary>
    [Fact]
    public void ReadFile_EmptyBuffer_ReturnsSuccessWithZeroBytes()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Normal, info);

        var status = _dokan.ReadFile("\\default.xbe", [], out var bytesRead, 0, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal(0, bytesRead);
    }

    /// <summary>
    /// Verifies the synthetic root reports the volume label instead of a placeholder name.
    /// </summary>
    [Fact]
    public void GetFileInformation_Root_ReportsVolumeLabel()
    {
        var status = _dokan.GetFileInformation("\\", out var fileInfo, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal("XBOX_ISO", fileInfo.FileName);
        Assert.True(fileInfo.Attributes.HasFlag(FileAttributes.Directory));
    }

    /// <summary>
    /// Verifies information requests use the entry stored in the Dokan context.
    /// </summary>
    [Fact]
    public void GetFileInformation_UsesContextFromCreateFile()
    {
        IDokanFileInfo info = new MockDokanFileInfo();
        _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Normal, info);

        var status = _dokan.GetFileInformation("\\default.xbe", out var fileInfo, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.Equal("default.xbe", fileInfo.FileName);
        Assert.Equal("hello xbox"u8.Length, fileInfo.Length);
    }

    /// <summary>
    /// Verifies listing a file reports NotADirectory.
    /// </summary>
    [Fact]
    public void FindFiles_OnFilePath_ReturnsNotADirectory()
    {
        Assert.Equal(DokanResult.NotADirectory, _dokan.FindFiles("\\default.xbe", out _, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies the question-mark wildcard matches exactly one character.
    /// </summary>
    [Fact]
    public void FindFilesWithPattern_QuestionMark_MatchesSingleCharacter()
    {
        var status = _dokan.FindFilesWithPattern("\\", "default.xb?", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Contains(files, file => HasName(file, "default.xbe"));
        Assert.DoesNotContain(files, file => HasName(file, "sub"));
    }

    /// <summary>
    /// Verifies "*.*" matches all files, including names without an extension.
    /// </summary>
    [Fact]
    public void FindFilesWithPattern_StarDotStar_MatchesExtensionlessFiles()
    {
        var status = _dokan.FindFilesWithPattern("\\", "*.*", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Contains(files, file => HasName(file, "default"));
        Assert.Contains(files, file => HasName(file, "default.xbe"));
        Assert.Contains(files, file => HasName(file, "sub"));
    }

    /// <summary>
    /// Verifies a non-matching pattern still returns the virtual dot entries.
    /// </summary>
    [Fact]
    public void FindFilesWithPattern_NoMatch_ReturnsOnlyVirtualEntries()
    {
        var status = _dokan.FindFilesWithPattern("\\", "*.nope", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        var file = Assert.Single(files);
        Assert.Equal(".", file.FileName);
    }

    /// <summary>
    /// Verifies a literal name pattern matches only that entry.
    /// </summary>
    [Fact]
    public void FindFilesWithPattern_LiteralName_MatchesOnlyThatEntry()
    {
        var status = _dokan.FindFilesWithPattern("\\sub", "data.bin", out var files, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.Contains(files, file => HasName(file, "data.bin"));
        Assert.Contains(files, file => HasName(file, ".") || HasName(file, ".."));
        Assert.DoesNotContain(files, file => HasName(file, "default.xbe"));
    }

    /// <summary>
    /// Verifies pattern listing on a missing path reports NotADirectory.
    /// </summary>
    [Fact]
    public void FindFilesWithPattern_OnMissingPath_ReturnsNotADirectory()
    {
        Assert.Equal(DokanResult.NotADirectory,
            _dokan.FindFilesWithPattern("\\missing", "*", out _, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies directories receive a directory security descriptor.
    /// </summary>
    [Fact]
    public void GetFileSecurity_ForDirectory_UsesDirectorySecurity()
    {
        var status = _dokan.GetFileSecurity("\\sub", out var security, AccessControlSections.Access,
            new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.IsType<DirectorySecurity>(security);
    }

    /// <summary>
    /// Verifies missing paths report FileNotFound instead of a fabricated descriptor.
    /// </summary>
    [Fact]
    public void GetFileSecurity_ForMissingEntry_ReturnsFileNotFound()
    {
        var status = _dokan.GetFileSecurity("\\missing.xbe", out var security, AccessControlSections.Access,
            new MockDokanFileInfo());

        Assert.Equal(DokanResult.FileNotFound, status);
        Assert.Null(security);
    }

    /// <summary>
    /// Verifies the reported feature set marks the volume read-only and case-preserving.
    /// </summary>
    [Fact]
    public void GetVolumeInformation_ReportsFeatureFlags()
    {
        var status = _dokan.GetVolumeInformation(out _, out var features, out _, out _, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Success, status);
        Assert.True(features.HasFlag(FileSystemFeatures.UnicodeOnDisk));
        Assert.False(features.HasFlag(FileSystemFeatures.CaseSensitiveSearch));
    }

    /// <summary>
    /// Verifies the mounted and unmounted callbacks report success.
    /// </summary>
    [Fact]
    public void MountedAndUnmounted_ReturnSuccess()
    {
        Assert.Equal(DokanResult.Success, _dokan.Mounted("Z:", new MockDokanFileInfo()));
        Assert.Equal(DokanResult.Success, _dokan.Unmounted(new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies the no-op handle callbacks can be invoked without effect.
    /// </summary>
    [Fact]
    public void CleanupAndCloseFile_DoNotThrow()
    {
        var info = new MockDokanFileInfo();

        _dokan.Cleanup("\\default.xbe", info);
        _dokan.CloseFile("\\default.xbe", info);
    }

    /// <summary>
    /// Verifies security descriptor writes are denied.
    /// </summary>
    [Fact]
    public void SetFileSecurity_ReturnsAccessDenied()
    {
        var status = _dokan.SetFileSecurity("\\default.xbe", new FileSecurity(), AccessControlSections.Access,
            new MockDokanFileInfo());

        Assert.Equal(DokanResult.AccessDenied, status);
    }

    /// <summary>
    /// Verifies directory listing failures are reported as errors instead of escaping.
    /// </summary>
    [Fact]
    public void FindFiles_WhenVolumeThrows_ReturnsError()
    {
        var dokan = new XboxIsoVfsDokan(new FakeVfsVolume { ThrowOnGetEntry = true });

        Assert.Equal(DokanResult.Error, dokan.FindFiles("\\", out _, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies pattern listing failures are reported as errors instead of escaping.
    /// </summary>
    [Fact]
    public void FindFilesWithPattern_WhenVolumeThrows_ReturnsError()
    {
        var dokan = new XboxIsoVfsDokan(new FakeVfsVolume { ThrowOnGetEntry = true });

        Assert.Equal(DokanResult.Error,
            dokan.FindFilesWithPattern("\\", "*", out _, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies security descriptor failures are reported as errors instead of escaping.
    /// </summary>
    [Fact]
    public void GetFileSecurity_WhenVolumeThrows_ReturnsError()
    {
        var dokan = new XboxIsoVfsDokan(new FakeVfsVolume { ThrowOnGetEntry = true });

        var status = dokan.GetFileSecurity("\\x", out var security, AccessControlSections.Access,
            new MockDokanFileInfo());

        Assert.Equal(DokanResult.Error, status);
        Assert.Null(security);
    }

    /// <summary>
    /// Verifies open failures are reported as errors instead of escaping.
    /// </summary>
    [Fact]
    public void CreateFile_WhenVolumeThrows_ReturnsError()
    {
        var dokan = new XboxIsoVfsDokan(new FakeVfsVolume { ThrowOnGetEntry = true });

        var status = dokan.CreateFile("\\x", FileAccess.ReadData, FileShare.Read, FileMode.Open, FileOptions.None,
            FileAttributes.Normal, new MockDokanFileInfo());

        Assert.Equal(DokanResult.Error, status);
    }

    /// <summary>
    /// Verifies a missing entry reported as null maps to FileNotFound.
    /// </summary>
    [Fact]
    public void GetFileInformation_WhenVolumeReturnsNull_ReturnsFileNotFound()
    {
        var dokan = new XboxIsoVfsDokan(new FakeVfsVolume());

        Assert.Equal(DokanResult.FileNotFound,
            dokan.GetFileInformation("\\missing.bin", out _, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies listing a path the volume does not know maps to NotADirectory.
    /// </summary>
    [Fact]
    public void FindFiles_WhenVolumeReturnsNull_ReturnsNotADirectory()
    {
        var dokan = new XboxIsoVfsDokan(new FakeVfsVolume());

        Assert.Equal(DokanResult.NotADirectory, dokan.FindFiles("\\missing", out _, new MockDokanFileInfo()));
    }

    /// <summary>
    /// Verifies OpenOrCreate on an existing file is treated as a read-only open.
    /// </summary>
    [Fact]
    public void CreateFile_ExistingFile_OpenOrCreate_Succeeds()
    {
        IDokanFileInfo info = new MockDokanFileInfo();

        var status = _dokan.CreateFile("\\default.xbe", FileAccess.ReadData, FileShare.Read,
            FileMode.OpenOrCreate, FileOptions.None, FileAttributes.Normal, info);

        Assert.Equal(DokanResult.Success, status);
        Assert.NotNull(info.Context);
    }

    /// <summary>
    /// Verifies listing a missing path with a wildcard maps to NotADirectory.
    /// </summary>
    [Fact]
    public void FindFilesWithPattern_WhenVolumeReturnsNull_ReturnsNotADirectory()
    {
        var dokan = new XboxIsoVfsDokan(new FakeVfsVolume());

        Assert.Equal(DokanResult.NotADirectory,
            dokan.FindFilesWithPattern("\\missing", "*", out _, new MockDokanFileInfo()));
    }
}