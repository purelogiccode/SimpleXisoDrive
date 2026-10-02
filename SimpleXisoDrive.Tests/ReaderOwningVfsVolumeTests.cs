using SimpleXisoDrive.Core.Vfs;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the decorator that disposes an archive reader together with the volume it backs.
/// </summary>
public class ReaderOwningVfsVolumeTests
{
    /// <summary>
    /// Verifies volume metadata is delegated to the wrapped volume.
    /// </summary>
    [Fact]
    public void Properties_DelegateToInnerVolume()
    {
        var inner = new FakeVfsVolume
        {
            VolumeSize = 512,
            VolumeCreationTime = new DateTime(2021, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            VolumeLabel = "INNER",
            FileSystemName = "INNERFS"
        };

        using var volume = new ReaderOwningVfsVolume(inner, new RecordingDisposable());

        Assert.Equal(512ul, volume.VolumeSize);
        Assert.Equal(inner.VolumeCreationTime, volume.VolumeCreationTime);
        Assert.Equal("INNER", volume.VolumeLabel);
        Assert.Equal("INNERFS", volume.FileSystemName);
    }

    /// <summary>
    /// Verifies entry lookup is delegated to the wrapped volume.
    /// </summary>
    [Fact]
    public void GetEntry_DelegatesToInnerVolume()
    {
        using var volume = new ReaderOwningVfsVolume(new FakeVfsVolume(), new RecordingDisposable());

        var entry = volume.GetEntry("\\");

        Assert.NotNull(entry);
        Assert.Equal("entry.bin", entry.FileName);
        Assert.Null(volume.GetEntry(@"\missing"));
    }

    /// <summary>
    /// Verifies directory listing is delegated to the wrapped volume.
    /// </summary>
    [Fact]
    public void GetFolderList_DelegatesToInnerVolume()
    {
        using var volume = new ReaderOwningVfsVolume(new FakeVfsVolume(), new RecordingDisposable());

        var child = Assert.Single(volume.GetFolderList("\\"));

        Assert.Equal("entry.bin", child.FileName);
    }

    /// <summary>
    /// Verifies reads are delegated to the wrapped volume.
    /// </summary>
    [Fact]
    public void ReadFile_DelegatesToInnerVolume()
    {
        using var volume = new ReaderOwningVfsVolume(new FakeVfsVolume(), new RecordingDisposable());
        var entry = volume.GetEntry("\\");
        Assert.NotNull(entry);

        var buffer = new byte[4];
        var read = volume.ReadFile(entry, buffer, 0);

        Assert.Equal(1, read);
        Assert.Equal(0xAB, buffer[0]);
    }

    /// <summary>
    /// Verifies disposal disposes the inner volume and then the owner.
    /// </summary>
    [Fact]
    public void Dispose_DisposesInnerVolumeAndOwner()
    {
        var inner = new FakeVfsVolume();
        var owner = new RecordingDisposable();
        var volume = new ReaderOwningVfsVolume(inner, owner);

        volume.Dispose();

        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, owner.DisposeCount);
    }

    /// <summary>
    /// Verifies disposal is idempotent for the wrapped volume and the owner.
    /// </summary>
    [Fact]
    public void Dispose_IsIdempotent()
    {
        var inner = new FakeVfsVolume();
        var owner = new RecordingDisposable();
        var volume = new ReaderOwningVfsVolume(inner, owner);

        volume.Dispose();
        volume.Dispose();

        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, owner.DisposeCount);
    }

    /// <summary>
    /// Verifies the owner is still disposed when the inner volume throws.
    /// </summary>
    [Fact]
    public void Dispose_DisposesOwner_WhenInnerVolumeThrows()
    {
        var inner = new FakeVfsVolume { ThrowOnDispose = true };
        var owner = new RecordingDisposable();
        var volume = new ReaderOwningVfsVolume(inner, owner);

        volume.Dispose();

        Assert.Equal(1, inner.DisposeCount);
        Assert.Equal(1, owner.DisposeCount);
    }

    /// <summary>
    /// Verifies an owner disposal failure is swallowed.
    /// </summary>
    [Fact]
    public void Dispose_DoesNotThrow_WhenOwnerThrows()
    {
        var owner = new RecordingDisposable(throwOnDispose: true);
        var volume = new ReaderOwningVfsVolume(new FakeVfsVolume(), owner);

        volume.Dispose();

        Assert.Equal(1, owner.DisposeCount);
    }

    /// <summary>
    /// Verifies lookup failures are logged and rethrown.
    /// </summary>
    [Fact]
    public void GetEntry_WhenInnerThrows_Rethrows()
    {
        using var volume = new ReaderOwningVfsVolume(
            new FakeVfsVolume { ThrowOnGetEntry = true }, new RecordingDisposable());

        Assert.Throws<InvalidOperationException>(() => volume.GetEntry("\\"));
    }

    /// <summary>
    /// Verifies listing failures are logged and rethrown.
    /// </summary>
    [Fact]
    public void GetFolderList_WhenInnerThrows_Rethrows()
    {
        using var volume = new ReaderOwningVfsVolume(
            new FakeVfsVolume { ThrowOnGetFolderList = true }, new RecordingDisposable());

        Assert.Throws<InvalidOperationException>(() => volume.GetFolderList("\\"));
    }

    /// <summary>
    /// Verifies read failures are logged and rethrown.
    /// </summary>
    [Fact]
    public void ReadFile_WhenInnerThrows_Rethrows()
    {
        using var volume = new ReaderOwningVfsVolume(
            new FakeVfsVolume { ThrowOnReadFile = true }, new RecordingDisposable());
        var entry = volume.GetEntry("\\");
        Assert.NotNull(entry);

        Assert.Throws<InvalidOperationException>(() => volume.ReadFile(entry, new byte[4], 0));
    }
}