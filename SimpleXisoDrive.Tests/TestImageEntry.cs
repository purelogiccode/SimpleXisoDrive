namespace SimpleXisoDrive.Tests;

/// <summary>
/// Describes a file or directory to place in a test image. A <see langword="null"/>
/// <see cref="Data"/> marks a directory; non-null data marks a file (an empty array is
/// a zero-length file).
/// </summary>
/// <param name="Path">Image-internal path using <c>/</c> or <c>\</c> separators.</param>
/// <param name="Data">File contents, or <see langword="null"/> for a directory.</param>
/// <param name="Attributes">Raw XDVDFS attribute byte.</param>
internal sealed record TestImageEntry(string Path, byte[]? Data, byte Attributes = 0x20);
