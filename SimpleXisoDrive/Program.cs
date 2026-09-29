using System.Diagnostics;
using DokanNet;
using Serilog;
using SimpleXisoDrive.Core;
using SimpleXisoDrive.Core.Services;
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
            // Give fire-and-forget bug reports a bounded grace period before the
            // process (and its HTTP client) goes away.
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

            if (!DokanInstallation.IsInstalled())
            {
                Log.Error("Dokan is not installed. Exiting.");
                await WaitForExitKeyPressAsync();
                return 1;
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
            if (args.Length == 0)
            {
                UsageText.Print();
                Console.WriteLine(
                    "\nAlternatively, you can drag and drop an ISO or ZAR file onto the executable to mount it automatically.");
                await WaitForExitKeyPressAsync();
                return 1;
            }

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

                // Report this to the API so the developer knows the path was invalid
                Log.Error(new FileNotFoundException(errorMsg), "Mount attempt failed: File not found.");

                await WaitForExitKeyPressAsync();
                return 1;
            }

            // Use the resolved path for mounting
            isoPath = resolvedIsoPath;

            // Check for updates only after the arguments and image path have been
            // validated, matching the Unix front end. The Windows front end notifies
            // the user with a message box instead of the console prompt.
            await UpdateChecker.CheckForUpdateAsync(WindowsUpdatePrompt.ConfirmOpenRelease);

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
            Console.Error.WriteLine("");
            Console.Error.WriteLine("To fix this:");
            Console.Error.WriteLine("  1. Uninstall Dokan via Windows Settings > Apps");
            Console.Error.WriteLine(
                "  2. Download the latest version from: https://github.com/dokan-dev/dokany/releases");
            Console.Error.WriteLine("  3. Install the package matching your system architecture (x64)");
            Console.Error.WriteLine("  4. Restart your computer");
            Console.Error.WriteLine("  5. Re-run SimpleXisoDrive");

            Log.Error(ex, "Unable to load dokan2.dll or its dependencies.");
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
        if (mountPath.EndsWith(":\\", StringComparison.Ordinal) && !CheckAccess.IsAdministrator())
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
                    Log.Error(ex, "Failed to launch explorer at '{MountPath}'", mountPath);
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
            Log.Information("Unmounted.");
        }
    }
}