namespace SimpleXisoDrive.Tests;

/// <summary>
/// Groups tests that replace the process-wide Serilog <c>Log.Logger</c>. Parallelization is
/// disabled so these tests never clobber each other's logger or observe another test's events.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalLoggerCollection
{
    /// <summary>The collection name shared by the global-logger tests.</summary>
    public const string Name = "GlobalLogger";
}
