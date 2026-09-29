using System.Diagnostics;
using Serilog;
using SimpleXisoDrive.Fuse;
using SimpleXisoDrive.Services;

namespace SimpleXisoDrive;

/// <summary>
/// Application entry point for Linux and macOS. Parses command-line arguments and
/// mounts an Xbox ISO/XISO or ZArchive (.zar) image as a read-only virtual file
/// system using FUSE 3 (libfuse3 or macFUSE).
/// </summary>
internal static class Program
{
    private static VfsContainer? _vfsContainer;

    /// <summary>
    /// Runs the application, mounting the specified image file or displaying usage information.
    /// </summary>
    /// <param name="args">The command-line arguments: an image path, an optional mount path, and optional flags.</param>
    /// <returns>Zero on success; otherwise, a non-zero exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            LoggingSetup.ConfigureLogger();
        }
        catch
        {
            // If Serilog cannot be configured, continue with the silent logger
        }

        // Decrypt the API key up front so the first report never pays for it.
        ApiKeyProvider.Preload();

        try
        {
            return await RunAsync(args);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static async Task<int> RunAsync(string[] args)
    {
        SetupGlobalExceptionHandlers();

        Log.Information("=== SimpleXisoDrive Started (Unix) ===");
        Log.Information("Arguments: {Args}", string.Join(" | ", args));
        Log.Information("Working Directory: {WorkingDirectory}", Environment.CurrentDirectory);

        // Report launch statistics (fire and forget)
        StatsService.ReportLaunchAsync();

        if (args.Any(static argument => argument is "-h" or "--help"))
        {
            PrintUsage();
            return 0;
        }

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var options = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? mountPath = null;

        foreach (var argument in args.Skip(1))
        {
            if (argument.StartsWith('-'))
            {
                options.Add(argument);
            }
            else if (mountPath is null)
            {
                mountPath = argument;
            }
            else
            {
                Console.Error.WriteLine($"Error: unexpected argument '{argument}'.");
                return 1;
            }
        }

        var unknownOptions = options
            .Where(static option => option is not ("-d" or "--debug" or "-l" or "--launch" or "-i" or "--image-iso"))
            .ToArray();
        if (unknownOptions.Length > 0)
        {
            Console.Error.WriteLine($"Error: unknown option(s): {string.Join(", ", unknownOptions)}");
            PrintUsage();
            return 1;
        }

        var debug = options.Contains("-d") || options.Contains("--debug");
        var launch = options.Contains("-l") || options.Contains("--launch");
        var imageIso = options.Contains("-i") || options.Contains("--image-iso");

        try
        {
            var resolvedIsoPath = ImagePathResolver.Resolve(args[0]);
            if (resolvedIsoPath is null)
            {
                await Console.Error.WriteLineAsync($"Error: Image file not found at '{args[0]}'");

                if (Directory.Exists(args[0]))
                {
                    await Console.Error.WriteLineAsync(
                        "Hint: The specified path is a directory. Please provide the path to a specific .iso, .xiso, .cso or .zar file.");
                }

                if (string.IsNullOrEmpty(Path.GetExtension(args[0])))
                {
                    await Console.Error.WriteLineAsync(
                        $"Hint: Tried looking for '{args[0]}.iso', '{args[0]}.xiso', '{args[0]}.cso' and '{args[0]}.zar' but none were found.");
                }

                Log.Error(new FileNotFoundException($"Image file not found at '{args[0]}'"),
                    "Mount attempt failed: File not found.");
                return 1;
            }

            if (mountPath is null)
            {
                mountPath = Path.Combine(Path.GetTempPath(), $"simplexisodrive-{Environment.ProcessId}");
                Directory.CreateDirectory(mountPath);
            }
            else if (!Directory.Exists(mountPath))
            {
                await Console.Error.WriteLineAsync($"Error: Mount path '{mountPath}' is not an existing directory.");
                await Console.Error.WriteLineAsync("Create the directory first (for example: mkdir -p /mnt/xiso).");
                return 1;
            }

            if (!FuseAvailability.Check(out _))
            {
                return 1;
            }

            await UpdateChecker.CheckForUpdateAsync();

            _vfsContainer = new VfsContainer(resolvedIsoPath, imageIso);
            try
            {
                var fileSystem = new FuseFileSystem(_vfsContainer);
                var exitCode = fileSystem.Run(mountPath, debug, () =>
                {
                    Console.WriteLine($"Mounted '{resolvedIsoPath}' at '{mountPath}'.");
                    if (imageIso)
                    {
                        Console.WriteLine($"Raw image available at: {Path.Combine(mountPath, "image.iso")}");
                    }

                    if (launch)
                    {
                        LaunchFileManager(mountPath);
                    }
                });

                if (exitCode != 0)
                {
                    Console.Error.WriteLine($"Error: FUSE exited with code {exitCode}.");
                    Log.Error("FUSE exited with code {ExitCode}", exitCode);
                    return 1;
                }

                Log.Information("Unmounted.");
                return 0;
            }
            finally
            {
                _vfsContainer.Dispose();
                _vfsContainer = null;
            }
        }
        catch (InvalidImageException ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Log.Debug(ex, "Invalid Xbox ISO image");
            return 1;
        }
        catch (DllNotFoundException ex)
        {
            await Console.Error.WriteLineAsync($"Error: Failed to load the FUSE runtime library: {ex.Message}");
            Log.Error(ex, "Unable to load the FUSE 3 runtime library.");
            return 1;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Log.Error(ex, "Fatal error in Main");
            return 1;
        }
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

    private static void LaunchFileManager(string mountPath)
    {
        try
        {
            var command = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            var startInfo = new ProcessStartInfo(command) { UseShellExecute = false };
            startInfo.ArgumentList.Add(mountPath);
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open the file manager at '{MountPath}'", mountPath);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            "Mounts an Xbox ISO/XISO (.iso, .xiso, .cso) or ZArchive (.zar) file as a read-only virtual file system.");
        Console.WriteLine("");
        Console.WriteLine("Usage: SimpleXisoDrive <image-file> [mount-path] [options]");
        Console.WriteLine("");
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <image-file>    Path to the Xbox image (.iso, .xiso, .cso) or ZArchive (.zar) file to mount.");
        Console.WriteLine("  <mount-path>    Existing empty directory to mount on. When omitted, a temporary");
        Console.WriteLine("                  directory is created and printed after mounting.");
        Console.WriteLine("");
        Console.WriteLine("Options:");
        Console.WriteLine("  -d, --debug     Display debug FUSE output in the console window.");
        Console.WriteLine("  -l, --launch    Open the file manager at the mount path after mounting.");
        Console.WriteLine("  -i, --image-iso Also expose the raw Xbox image as image.iso at the mount root");
        Console.WriteLine("                  (for emulators such as xemu; ZArchive trees are synthesized).");
    }
}
