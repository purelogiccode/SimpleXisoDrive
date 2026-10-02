using System.Diagnostics;
using DokanNet;
using Serilog;
using SimpleXisoDrive.Core;
using SimpleXisoDrive.Core.Services;
using SimpleXisoDrive.Models;
using SimpleXisoDrive.Services;

namespace SimpleXisoDrive;

/// <summary>
/// Application entry point. Parses command-line arguments and mounts an Xbox ISO/XISO, Xbox ISO
/// CHD (.chd) or ZArchive (.zar) image as a read-only virtual file system using Dokan.
/// </summary>
internal static class Program
{
    private static VfsContainer? _vfsContainer;
    private static readonly CancellationTokenSource CancellationTokenSource = new();

    /// <summary>
    /// Runs the application, mounting the specified image file or displaying usage information.
    /// </summary>
    /// <param name="args">The command-line arguments: an image path, an optional mount path, and optional flags.</param>
    /// <returns>Zero on success; otherwise, a non-zero exit code.</returns>
    private static async Task<int> Main(string[] args)
    {
        try
        {
            LoggingSetup.ConfigureLogger();
        }
        catch (Exception ex)
        {
            // If Serilog cannot be configured, continue with the silent logger
            Console.Error.WriteLine($"Failed to configure logging: {ex.Message}");
        }

        // Decrypt the API key up front so the first report never pays for it.
        ApiKeyProvider.Preload();

        try
        {
            return await RunAsync(args);
        }
        finally
        {
            // Give fire-and-forget stats and bug reports a bounded grace period before
            // the process (and its HTTP client) goes away.
            await StatsService.WaitForPendingReportAsync(TimeSpan.FromSeconds(5));
            await BugReport.WaitForPendingReportsAsync(TimeSpan.FromSeconds(5));
            Log.CloseAndFlush();
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        try
        {
            // Set Green CRT theme immediately
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.Green;

            // Console.Clear throws IOException when the output is redirected (no console buffer).
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }

            // Hook global exception handlers immediately to catch crashes
            SetupGlobalExceptionHandlers();

            Log.Information("=== SimpleXisoDrive Started ===");
            Log.Information("Arguments: {Args}", string.Join(" | ", args));
            Log.Information("Working Directory: {WorkingDirectory}", Environment.CurrentDirectory);

            // Report launch statistics (fire and forget)
            StatsService.ReportLaunch();

            // Help never triggers a network call or a Dokan probe.
            if (args.Any(CommandLineParser.IsHelpOption))
            {
                UsageText.Print();
                return 0;
            }

            // At startup, query GitHub for a newer release and offer the download page.
            // The Windows front end notifies the user with a native message box; the
            // prompt is skipped for non-interactive runs.
            await UpdateChecker.CheckForUpdateAsync(WindowsUpdatePrompt.ConfirmOpenRelease);

            // Usage is shown before the Dokan probe so a missing runtime cannot hide
            // the usage text and the drag-and-drop hint.
            if (args.Length == 0)
            {
                UsageText.Print();
                Console.WriteLine(
                    "\nAlternatively, you can drag and drop an ISO or ZAR file onto the executable to mount it automatically.");
                await WaitForExitKeyPressAsync();
                return 1;
            }

            switch (DokanInstallation.Check())
            {
                case DokanInstallationStatus.RuntimeMissing:
                    // Expected user-setup condition (guidance is printed above); keep it
                    // below the bug-report threshold.
                    Log.Information("Dokan is not installed. Exiting.");
                    DokanDownloadPrompt.OfferDownload("runtime library (dokan2.dll)");
                    await WaitForExitKeyPressAsync();
                    return 1;
                case DokanInstallationStatus.DriverMissing:
                    // The library exists but the driver is missing: warn and offer the
                    // download before continuing (mounting may still work).
                    DokanDownloadPrompt.OfferDownload("driver (dokan2.sys)");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Startup initialization failed");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        try
        {
            var arguments = CommandLineParser.Parse(args);
            var isoPath = arguments.ImagePath;

            if (arguments.Debug)
            {
                LoggingSetup.ConsoleLevelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
                Log.Information("Debug logging enabled (-d/--debug).");
            }

            // Try to resolve the image path - handle cases where the user provides a path without an extension
            var resolvedIsoPath = ImagePathResolver.Resolve(isoPath);
            if (resolvedIsoPath == null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                var errorMsg = $"Image file not found at '{isoPath}'";
                await Console.Error.WriteLineAsync($"Error: {errorMsg}");

                // Add hints for common mistakes
                if (Directory.Exists(isoPath))
                {
                    await Console.Error.WriteLineAsync(
                        "Hint: The specified path is a directory. Please provide the path to a specific .iso, .xiso, .cso, .chd or .zar file.");
                }

                if (string.IsNullOrEmpty(Path.GetExtension(isoPath)))
                {
                    await Console.Error.WriteLineAsync(
                        $"Hint: Tried looking for '{isoPath}.iso', '{isoPath}.xiso', '{isoPath}.cso', '{isoPath}.chd' and '{isoPath}.zar' but none were found.");
                }

                if (args.Length > 2 && !isoPath.Contains(' '))
                {
                    await Console.Error.WriteLineAsync(
                        "Hint: If your file path contains spaces, ensure it is wrapped in \"quotes\".");
                }

                // A missing file is a routine user input condition (friendly message
                // printed above); keep it below the bug-report threshold.
                Log.Information(new FileNotFoundException(errorMsg), "Mount attempt failed: File not found.");

                await WaitForExitKeyPressAsync();
                return 1;
            }

            // Use the resolved path for mounting
            isoPath = resolvedIsoPath;

            // A folder mount path must already exist (drive letters are created by Dokan).
            if (arguments.MountPath is not null && !MountPathValidator.IsDriveLetterPath(arguments.MountPath) &&
                !Directory.Exists(arguments.MountPath))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                await Console.Error.WriteLineAsync(
                    $"Error: Mount path '{arguments.MountPath}' is not an existing directory.");
                await Console.Error.WriteLineAsync("Create the directory first (for example: mkdir \"C:\\mount\\xiso\").");
                await WaitForExitKeyPressAsync();
                return 1;
            }

            if (arguments.IsDragAndDrop)
            {
                var mountPath = DriveLetterSelector.FindAvailableDriveLetter();
                if (mountPath is null)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    await Console.Error.WriteLineAsync("Error: Could not find an available drive letter (M-R).");
                    await WaitForExitKeyPressAsync();
                    return 1;
                }

                var mountTask = RunMountAsync(isoPath, mountPath, arguments.Debug, arguments.Launch,
                    arguments.ImageIso);

                // Wait for either the mount to fail OR the user to press a key
                var keyPressTask = ConsoleKeyPress.WaitAsync();

                var completedTask = await Task.WhenAny(mountTask, keyPressTask);

                if (completedTask == mountTask)
                {
                    // The mount task finished (likely failed) before a key was pressed.
                    // Await it to propagate the exception to the catch blocks below.
                    await mountTask;
                }
                else
                {
                    // User pressed a key first.
                    Log.Information("Unmount key pressed. Unmounting...");
                    await CancellationTokenSource.CancelAsync();
                    await mountTask;
                }
            }
            else
            {
                // For standard command-line use, await the task directly.
                // The user will stop it with Ctrl+C.
                await RunMountAsync(isoPath, arguments.MountPath!, arguments.Debug, arguments.Launch,
                    arguments.ImageIso);
            }

            return 0;
        }
        catch (CommandLineException ex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");

            if (ex.ShowUsage)
            {
                UsageText.Print();
            }

            await WaitForExitKeyPressAsync();
            return 1;
        }
        catch (InvalidImageException ex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Log.Debug(ex, "Invalid Xbox ISO image");

            await WaitForExitKeyPressAsync();
            return 1;
        }
        catch (DokanException ex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            await Console.Error.WriteLineAsync($"Dokan Error: {ex.Message}");
            Log.Error(ex, "A Dokan-specific error occurred during mounting.");

            await WaitForExitKeyPressAsync();

            return 1;
        }
        catch (DllNotFoundException ex) when (ex.Message.Contains("dokan2.dll", StringComparison.OrdinalIgnoreCase))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine("Error: Failed to load the Dokan runtime library (dokan2.dll).");
            Console.Error.WriteLine(
                "The file may be corrupted, of the wrong architecture, or its dependencies are missing.");

            // Expected setup condition: keep it below the bug-report threshold and
            // reuse the same download offer as the other missing-Dokan paths.
            Log.Information(ex, "Unable to load dokan2.dll or its dependencies.");
            DokanDownloadPrompt.OfferDownload("runtime library (dokan2.dll)");

            await WaitForExitKeyPressAsync();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");

            Log.Error(ex, "Fatal error in Main");

            // Wait when the process owns an interactive console (double-click, drag &
            // drop or shortcut); scripted/redirected runs exit immediately.
            await WaitForExitKeyPressAsync();

            return 1;
        }
    }

    /// <summary>
    /// Waits for a key press before exiting when the process owns an interactive
    /// console, so the error message cannot vanish with the console window. Returns
    /// immediately for redirected or non-interactive runs.
    /// </summary>
    private static async Task WaitForExitKeyPressAsync()
    {
        if (!Environment.UserInteractive || Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            return;
        }

        Console.WriteLine("\nPress any key to exit.");
        await ConsoleKeyPress.WaitAsync();
    }

    private static void SetupGlobalExceptionHandlers()
    {
        // Catches exceptions thrown on the main thread that are not caught
        AppDomain.CurrentDomain.UnhandledException += static (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                BugReport.LogFatalException(ex, "CRITICAL: Unhandled Global Exception");
            }
        };

        // Catches exceptions thrown in background Tasks that were not awaited
        TaskScheduler.UnobservedTaskException += static (_, e) =>
        {
            BugReport.LogFatalException(e.Exception, "CRITICAL: Unobserved Task Exception");
            e.SetObserved();
        };
    }

    private static async Task RunMountAsync(string isoPath, string mountPath, bool debug, bool launch, bool imageIso)
    {
        // Check for admin rights for drive letter mounting
        if (MountPathValidator.IsDriveLetterPath(mountPath) && !CheckAccess.IsAdministrator())
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("WARNING: Administrator privileges are recommended for mounting drive letters.");
            Console.WriteLine("If mounting fails, try running as Administrator.");
            Log.Information("Running without administrator privileges");
        }

        Console.CancelKeyPress += static (_, e) =>
        {
            e.Cancel = true;
            Log.Information("Ctrl+C detected. Unmounting...");
            CancellationTokenSource.Cancel();
        };

        try
        {
            Log.Information("Attempting to mount '{IsoPath}' to '{MountPath}'...", isoPath, mountPath);

            // Dokan fails if a drive letter has a trailing backslash (e.g. "Z:\" fails, "Z:" works)
            if (mountPath.Length == 3 && mountPath.EndsWith(":\\", StringComparison.Ordinal))
            {
                mountPath = mountPath.Substring(0, 2);
            }

            _vfsContainer = new VfsContainer(isoPath, imageIso);

            // Use MountManager only if we have Admin rights, otherwise it often fails with "Something's wrong with the Dokan driver"
            var dokanOptions = DokanOptions.WriteProtection | DokanOptions.CurrentSession;

            if (CheckAccess.IsAdministrator())
            {
                dokanOptions |= DokanOptions.MountManager;
            }

            if (debug)
            {
                dokanOptions |= DokanOptions.DebugMode | DokanOptions.StderrOutput;
            }

            var dokan = new Dokan(new SerilogDokanLogger());
            var dokanBuilder = new DokanInstanceBuilder(dokan)
                .ConfigureOptions(options =>
                {
                    options.Options = dokanOptions;
                    options.MountPoint = mountPath;
                });

            using var dokanInstance = dokanBuilder.Build(new XboxIsoVfsDokan(_vfsContainer));

            Log.Information("Mount successful: '{IsoPath}' -> '{MountPath}'", isoPath, mountPath);
            Log.Information("Press Ctrl+C to unmount (if run from command line).");

            if (launch)
            {
                try
                {
                    Process.Start("explorer.exe", mountPath);
                }
                catch (Exception ex)
                {
                    // A shell/environment failure, not an application defect.
                    Log.Information(ex, "Failed to launch explorer at '{MountPath}'", mountPath);
                }
            }

            var tcs = new TaskCompletionSource();
            await using (CancellationTokenSource.Token.Register(tcs.SetResult))
            {
                await tcs.Task;
            }

            Log.Information("Unmount signal received. Cleaning up...");
        }
        catch (Exception ex)
        {
            // Rethrown so Main can handle the UI/Console feedback (and reporting) in one place
            Log.Debug(ex, "Mount process failed (rethrown)");
            throw;
        }
        finally
        {
            _vfsContainer?.Dispose();
            _vfsContainer = null;
            Log.Information("Unmounted.");
        }
    }
}