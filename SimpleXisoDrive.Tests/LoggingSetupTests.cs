using Serilog.Events;
using SimpleXisoDrive.Core.Services;

namespace SimpleXisoDrive.Tests;

/// <summary>
/// Tests the logging configuration surface. The global logger is deliberately not
/// reconfigured here: attaching the real sinks would forward test log events to the
/// remote bug report API.
/// </summary>
public class LoggingSetupTests
{
    /// <summary>
    /// Verifies the console level switch starts at Information and can be raised.
    /// </summary>
    [Fact]
    public void ConsoleLevelSwitch_StartsAtInformation_AndCanBeRaised()
    {
        var original = LoggingSetup.ConsoleLevelSwitch.MinimumLevel;

        try
        {
            Assert.Equal(LogEventLevel.Information, original);

            LoggingSetup.ConsoleLevelSwitch.MinimumLevel = LogEventLevel.Debug;
            Assert.Equal(LogEventLevel.Debug, LoggingSetup.ConsoleLevelSwitch.MinimumLevel);
        }
        finally
        {
            LoggingSetup.ConsoleLevelSwitch.MinimumLevel = original;
        }
    }
}