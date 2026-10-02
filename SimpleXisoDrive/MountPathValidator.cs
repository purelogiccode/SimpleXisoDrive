namespace SimpleXisoDrive;

/// <summary>
/// Validates the mount path supplied on the command line before Dokan is invoked.
/// </summary>
internal static class MountPathValidator
{
    /// <summary>
    /// Determines whether <paramref name="mountPath"/> is a drive-letter path such as
    /// <c>Z:</c> or <c>Z:\</c>. Drive letters are created by Dokan and therefore do not
    /// have to exist as directories, unlike folder mount paths.
    /// </summary>
    /// <param name="mountPath">The mount path to test.</param>
    /// <returns><see langword="true"/> when the path is a drive letter.</returns>
    public static bool IsDriveLetterPath(string mountPath)
    {
        ArgumentNullException.ThrowIfNull(mountPath);

        return mountPath.Length is 2 or 3
               && char.IsAsciiLetter(mountPath[0])
               && mountPath[1] == ':'
               && (mountPath.Length == 2 || mountPath[2] == '\\');
    }
}
