namespace SimpleXisoDrive.Tests;

/// <summary>
/// Groups tests that override the process-wide <see cref="SimpleXisoDrive.Core.Services.BugReport"/>
/// log file paths. Parallelization is disabled so the classes never clobber each other's
/// temporary log files.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BugReportFileCollection
{
    /// <summary>The collection name shared by the bug-report file tests.</summary>
    public const string Name = "BugReportFiles";
}
