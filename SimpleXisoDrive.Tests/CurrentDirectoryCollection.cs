namespace SimpleXisoDrive.Tests;

/// <summary>
/// Groups tests that mutate the process-wide <see cref="Environment.CurrentDirectory"/>.
/// Parallelization is disabled so a concurrent test using relative paths cannot observe
/// the wrong directory.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CurrentDirectoryCollection
{
    /// <summary>The collection name shared by the current-directory tests.</summary>
    public const string Name = "CurrentDirectory";
}
