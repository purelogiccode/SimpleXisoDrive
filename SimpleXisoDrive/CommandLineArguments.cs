namespace SimpleXisoDrive;

/// <summary>
/// The validated command-line arguments for a mount.
/// </summary>
internal sealed class CommandLineArguments
{
    /// <summary>
    /// Gets the image path as supplied on the command line.
    /// </summary>
    public required string ImagePath { get; init; }

    /// <summary>
    /// Gets the mount path, or <see langword="null"/> in drag-and-drop mode where a free
    /// drive letter is selected automatically.
    /// </summary>
    public string? MountPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the application runs in single-argument
    /// (drag-and-drop) mode.
    /// </summary>
    public bool IsDragAndDrop { get; init; }

    /// <summary>
    /// Gets a value indicating whether debug logging was requested.
    /// </summary>
    public bool Debug { get; init; }

    /// <summary>
    /// Gets a value indicating whether Explorer should open after mounting.
    /// </summary>
    public bool Launch { get; init; }

    /// <summary>
    /// Gets a value indicating whether the raw image should also be exposed as
    /// <c>image.iso</c>.
    /// </summary>
    public bool ImageIso { get; init; }
}