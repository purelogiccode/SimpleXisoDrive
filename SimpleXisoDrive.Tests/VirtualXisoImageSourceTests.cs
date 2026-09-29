using SimpleXisoDrive.Vfs;
using XISOSharp;
using ZArchiveSharp;

namespace SimpleXisoDrive.Tests;

/// <summary>
///     Tests for <see cref="VirtualXisoImageSource" />: the synthesized image must be
///     byte-identical to what the XISOSharp whole-image writer produces for the same
///     file tree, so emulators see a normal XISO without any extraction step.
/// </summary>
public class VirtualXisoImageSourceTests
{
    private const ulong FixedFileTime = 0x01D9E5B3C4A2F100;

    private static string CreateZar(Action<ZArchiveWriter> build)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zar");
        using var stream = File.Create(path);
        using var writer = new ZArchiveWriter(stream);
        build(writer);
        writer.Finalize();

        return path;
    }

    private static byte[] ReadAll(IRawImageSource source)
    {
        var buffer = new byte[source.Length];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = source.Read(buffer.AsSpan(total), total);
            if (read <= 0)
            {
                break;
            }

            total += read;
        }

        Assert.Equal(buffer.Length, total);
        return buffer;
    }

    [Fact]
    public void VirtualImage_MatchesXisoWriterOutput_ForNestedTree()
    {
        var fileA = "hello xbox from simplexisodrive"u8.ToArray();
        var fileB = new byte[5000];
        Random.Shared.NextBytes(fileB);
        var fileC = new byte[4096];
        Random.Shared.NextBytes(fileC);

        var zarPath = CreateZar(writer =>
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData(fileA);
            Assert.True(writer.MakeDir("sub", recursive: true));
            Assert.True(writer.StartNewFile("sub/data.bin"));
            writer.AppendData(fileB);
            Assert.True(writer.StartNewFile("sub/empty.bin"));
            writer.AppendData(ReadOnlySpan<byte>.Empty);
            Assert.True(writer.MakeDir("sub/nested", recursive: true));
            Assert.True(writer.StartNewFile("sub/nested/exact.bin"));
            writer.AppendData(fileC);
            Assert.True(writer.MakeDir("empty", recursive: true));
        });

        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var sourceDir = Path.Combine(tempRoot, "source");
            Directory.CreateDirectory(Path.Combine(sourceDir, "sub", "nested"));
            Directory.CreateDirectory(Path.Combine(sourceDir, "empty"));
            File.WriteAllBytes(Path.Combine(sourceDir, "default.xbe"), fileA);
            File.WriteAllBytes(Path.Combine(sourceDir, "sub", "data.bin"), fileB);
            File.WriteAllBytes(Path.Combine(sourceDir, "sub", "empty.bin"), []);
            File.WriteAllBytes(Path.Combine(sourceDir, "sub", "nested", "exact.bin"), fileC);

            var writerIso = Path.Combine(tempRoot, "writer.iso");
            var previousMediaEnable = Logger.MediaEnable;
            Logger.MediaEnable = false;
            try
            {
                Assert.Equal(0, XisoWriter.PackFromDirectory(sourceDir, writerIso, fileTime: FixedFileTime));
            }
            finally
            {
                Logger.MediaEnable = previousMediaEnable;
            }

            using var reader = ZArchiveReader.TryOpen(zarPath) ?? throw new InvalidOperationException("fixture");
            using var source = VirtualXisoImageSource.Create(reader, zarPath, FixedFileTime);
            var virtualBytes = ReadAll(source);
            var writerBytes = File.ReadAllBytes(writerIso);

            Assert.Equal(writerBytes.Length, virtualBytes.Length);
            Assert.Equal(writerBytes, virtualBytes);
        }
        finally
        {
            File.Delete(zarPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void VirtualImage_IsAValidXiso_WithTheArchivedFiles()
    {
        var zarPath = CreateZar(writer =>
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("boot"u8);
            Assert.True(writer.MakeDir("media", recursive: true));
            Assert.True(writer.StartNewFile("media/video.bin"));
            writer.AppendData("streamed blocks"u8);
        });

        try
        {
            using var reader = ZArchiveReader.TryOpen(zarPath) ?? throw new InvalidOperationException("fixture");
            using var source = VirtualXisoImageSource.Create(reader, zarPath, FixedFileTime);
            using var imageStream = new MemoryStream(ReadAll(source));

            Assert.True(XisoReader.GetVolumeInfo(imageStream, "image.iso").IsValid);

            var boot = XisoReader.GetEntryInfo(imageStream, "image.iso", "default.xbe");
            Assert.NotNull(boot);
            Assert.Equal((ulong)"boot"u8.Length, boot.FileSize);

            var video = XisoReader.GetEntryInfo(imageStream, "image.iso", "media/video.bin");
            Assert.NotNull(video);
            Assert.Equal((ulong)"streamed blocks"u8.Length, video.FileSize);
        }
        finally
        {
            File.Delete(zarPath);
        }
    }

    [Fact]
    public void Read_ReturnsCorrectBytes_ForUnalignedRangesAcrossExtents()
    {
        var fileA = "hello xbox"u8.ToArray();
        var fileB = new byte[9000];
        Random.Shared.NextBytes(fileB);

        var zarPath = CreateZar(writer =>
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData(fileA);
            Assert.True(writer.MakeDir("sub", recursive: true));
            Assert.True(writer.StartNewFile("sub/data.bin"));
            writer.AppendData(fileB);
        });

        try
        {
            using var reader = ZArchiveReader.TryOpen(zarPath) ?? throw new InvalidOperationException("fixture");
            using var source = VirtualXisoImageSource.Create(reader, zarPath, FixedFileTime);
            var full = ReadAll(source);

            foreach (var (offset, requestedLength) in new[] { (0, 100), (12345, 777), (70000, 4096), (530000, 8192) })
            {
                if (offset >= full.Length)
                {
                    continue;
                }

                var length = Math.Min(requestedLength, (int)(full.Length - offset));
                var buffer = new byte[length];
                Assert.Equal(length, source.Read(buffer, offset));
                Assert.Equal(full.AsSpan(offset, length).ToArray(), buffer);
            }
        }
        finally
        {
            File.Delete(zarPath);
        }
    }

    [Fact]
    public void Read_AfterDispose_ReturnsZero()
    {
        var zarPath = CreateZar(writer =>
        {
            Assert.True(writer.StartNewFile("default.xbe"));
            writer.AppendData("data"u8);
        });

        try
        {
            using var reader = ZArchiveReader.TryOpen(zarPath) ?? throw new InvalidOperationException("fixture");
            var source = VirtualXisoImageSource.Create(reader, zarPath, FixedFileTime);
            source.Dispose();

            Assert.Equal(0, source.Read(new byte[16], 0));
        }
        finally
        {
            File.Delete(zarPath);
        }
    }
}
