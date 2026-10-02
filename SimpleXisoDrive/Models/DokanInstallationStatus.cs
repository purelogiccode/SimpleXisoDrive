namespace SimpleXisoDrive.Models;

/// <summary>
/// The installation state of the Dokan runtime detected at startup.
/// </summary>
internal enum DokanInstallationStatus
{
    /// <summary>The runtime library and driver are both present.</summary>
    Installed,

    /// <summary>The user-mode library (<c>dokan2.dll</c>) is present but the driver (<c>dokan2.sys</c>) is not.</summary>
    DriverMissing,

    /// <summary>The user-mode library (<c>dokan2.dll</c>) is missing, so mounting cannot work.</summary>
    RuntimeMissing,

    /// <summary>The installation state could not be determined because the probe failed.</summary>
    Unknown,
}
