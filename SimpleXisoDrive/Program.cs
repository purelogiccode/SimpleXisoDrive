using System.Diagnostics;
using DokanNet;
using Serilog;
using SimpleXisoDrive.Services;

namespace SimpleXisoDrive;

/// <summary>
/// Application entry point. Parses command-line arguments and mounts an Xbox ISO/XISO or
/// ZArchive (.zar) image as a read-only virtual file system using Dokan.
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
        // Set Green CRT theme immediately
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Clear();

        // Hook global exception handlers immediately to catch crashes
        SetupGlobalExceptionHandlers();

        Log.Information("=== SimpleXisoDrive Started ===");
        Log.Information("Arguments: {Args}", string.Join(" | ", args));
        Log.Information("Working Directory: {WorkingDirectory}", Environment.CurrentDirectory);

        // Report launch statistics (fire and forget)
        StatsService.ReportLaunchAsync();

        if (!IsDokanInstalled())
        {
            Log.Error("Dokan is not installed. Exiting.");
            Console.WriteLine("\nPress any key to exit.");
            await ConsoleKeyPress.WaitAsync();
            return 1;
        }

        await UpdateChecker.CheckForUpdateAsync();

        var isDragAndDrop = false;
        var debug = false;

        try
        {
            string isoPath;
            string mountPath;
            bool launch; // Initialize launch to false
            switch (args.Length)
            {
                case 0:
                    PrintUsage();
                    Console.WriteLine(
                        "\nAlternatively, you can drag and drop an ISO or ZAR file onto the executable to mount it automatically.");
                    Console.WriteLine("\nPress any key to exit.");
                    await ConsoleKeyPress.WaitAsync();
                    return 1;

                case 1:
                    isDragAndDrop = true;
                    isoPath = args[0];
                    if (string.IsNullOrEmpty(isoPath))
                        throw new ArgumentException("ISO path cannot be null or empty");
                    if (isoPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                        throw new ArgumentException("Invalid path characters detected");

                    var availableMountPath = FindAvailableDriveLetter();
                    if (availableMountPath is null)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        await Console.Error.WriteLineAsync("Error: Could not find an available drive letter (M-R).");
                        // For drag-and-drop, wait for a key press before exiting on error.
                        Console.WriteLine("\nPress any key to exit.");
                        await ConsoleKeyPress.WaitAsync();
                        return 1;
                    }

                    mountPath = availableMountPath;
                    launch = true;
                    break;

                default:
                    isoPath = args[0];
                    mountPath = args[1];
                    var options = new HashSet<string>(args.Skip(2), StringComparer.OrdinalIgnoreCase);
                    debug = options.Contains("-d") || options.Contains("--debug");
                    launch = options.Contains("-l") || options.Contains("--launch");
                    break;
            }

            // Try to resolve the image path - handle cases where the user provides a path without an extension
            var resolvedIsoPath = ResolveImagePath(isoPath);
            if (resolvedIsoPath == null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                var errorMsg = $"Image file not found at '{isoPath}'";
                await Console.Error.WriteLineAsync($"Error: {errorMsg}");

                // Add hints for common mistakes
                if (Directory.Exists(isoPath))
                {
                    await Console.Error.WriteLineAsync(
                        "Hint: The specified path is a directory. Please provide the path to a specific .iso, .xiso or .zar file.");
                }

                if (string.IsNullOrEmpty(Path.GetExtension(isoPath)))
                {
                    await Console.Error.WriteLineAsync(
                        $"Hint: Tried looking for '{isoPath}.iso', '{isoPath}.xiso', '{isoPath}.cso' and '{isoPath}.zar' but none were found.");
                }

                if (args.Length > 2 && !isoPath.Contains(' '))
                {
                    await Console.Error.WriteLineAsync(
                        "Hint: If your file path contains spaces, ensure it is wrapped in \"quotes\".");
                }

                // Report this to the API so the developer knows the path was invalid
                Log.Error(new FileNotFoundException(errorMsg), "Mount attempt failed: File not found.");

                if (!isDragAndDrop) return 1;

                Console.WriteLine("\nPress any key to exit.");
                await ConsoleKeyPress.WaitAsync();
                return 1;
            }

            // Use the resolved path for mounting
            isoPath = resolvedIsoPath;

            if (isDragAndDrop)
            {
                var mountTask = RunMount(isoPath, mountPath, debug, launch);

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
                await RunMount(isoPath, mountPath, debug, launch);
            }

            return 0;
        }
        catch (InvalidImageException ex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");
            Log.Debug(ex, "Invalid Xbox ISO image");

            if (!isDragAndDrop) return 1;

            Console.WriteLine("\nPress any key to exit.");
            await ConsoleKeyPress.WaitAsync();
            return 1;
        }
        catch (DokanException ex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            await Console.Error.WriteLineAsync($"Dokan Error: {ex.Message}");
            Log.Error(ex, "A Dokan-specific error occurred during mounting.");
            if (!isDragAndDrop) return 1;

            Console.WriteLine("\nPress any key to exit.");
            await ConsoleKeyPress.WaitAsync();

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
            if (!isDragAndDrop) return 1;

            Console.WriteLine("\nPress any key to exit.");
            await ConsoleKeyPress.WaitAsync();
            return 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            await Console.Error.WriteLineAsync($"Error: {ex.Message}");

            Log.Error(ex, "Fatal error in Main");

            // If we are in a context where the window might disappear (Drag & Drop or single arg)
            if (isDragAndDrop || args.Length <= 1)
            {
                Console.WriteLine("\nPress any key to exit.");
                await ConsoleKeyPress.WaitAsync();
            }

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

    /// <summary>
    /// Checks whether the Dokan runtime library (dokan2.dll) and driver (dokan2.sys) are installed.
    /// Displays an error and exits if dokan2.dll is missing, since the application cannot function without it.
    /// </summary>
    /// <returns>True if dokan2.dll is found; false otherwise.</returns>
    private static bool IsDokanInstalled()
    {
        var dokanDllPath = Path.Combine(Environment.SystemDirectory, "dokan2.dll");
        var dokanSysPath = Path.Combine(Environment.SystemDirectory, "drivers", "dokan2.sys");

        var dllExists = File.Exists(dokanDllPath);
        var sysExists = File.Exists(dokanSysPath);

        if (!dllExists)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine("Error: The Dokan runtime library (dokan2.dll) was not found.");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("SimpleXisoDrive requires the Dokan User-Mode File System Library to operate.");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("To fix this:");
            Console.Error.WriteLine("  1. Download Dokan from: https://github.com/dokan-dev/dokany/releases");
            Console.Error.WriteLine("  2. Install the package (the default installation includes dokan2.dll)");
            Console.Error.WriteLine("  3. Restart your computer if prompted");
            Console.Error.WriteLine("  4. Re-run SimpleXisoDrive");
            Console.Error.WriteLine("");
            Console.Error.WriteLine($"Expected file location: {dokanDllPath}");

            Log.Error("Dokan check FAILED: {DllPath} not found.", dokanDllPath);
            return false;
        }

        if (!sysExists)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Error.WriteLine("Warning: The Dokan driver (dokan2.sys) was not found.");
            Console.Error.WriteLine("Mounting may fail. Please reinstall Dokan if you encounter issues.");
            Log.Warning("Dokan driver warning: {SysPath} not found.", dokanSysPath);
        }

        Log.Information("Dokan check passed: {DllPath} found.", dokanDllPath);
        return true;
    }

    private static string? FindAvailableDriveLetter()
    {
        try
        {
            // Get all existing drive letters
            var usedLetters = DriveInfo.GetDrives()
                .Select(static d => d.Name[0])
                .ToHashSet();

            char[] preferredLetters = ['M', 'N', 'O', 'P', 'Q', 'R'];

            foreach (var letter in preferredLetters)
            {
                if (!usedLetters.Contains(letter))
                {
                    var drivePath = $"{letter}:\\";
                    Log.Debug("Found available drive letter: {DrivePath}", drivePath);
                    return drivePath;
                }
            }

            Log.Warning("No available drive letters found in preferred range M-R");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error checking drive letters");
            return null;
        }
    }

    private static void PrintUsage()
    {
        var mainModule = Process.GetCurrentProcess().MainModule;
        var exeName = mainModule != null
            ? Path.GetFileNameWithoutExtension(mainModule.FileName)
            : "SimpleXisoDrive";
        Console.WriteLine("Mounts an Xbox ISO/XISO (.iso, .xiso) or ZArchive (.zar) file as a virtual file system on Windows.");
        Console.WriteLine("");
        Console.WriteLine($"Usage: {exeName} <image-file> <mount-path> [options]");
        Console.WriteLine("");
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <image-file>    Path to the Xbox image (.iso, .xiso) or ZArchive (.zar) file to mount.");
        Console.WriteLine("  <mount-path>    Drive letter (\"M:\\\") or folder path on an NTFS partition.");
        Console.WriteLine("");
        Console.WriteLine("Options:");
        Console.WriteLine("  -d, --debug     Display debug Dokan output in the console window.");
        Console.WriteLine("  -l, --launch    Open Windows Explorer to the mount path after mounting.");
    }

    private static async Task RunMount(string isoPath, string mountPath, bool debug, bool launch)
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

            _vfsContainer = new VfsContainer(isoPath);

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

    /// <summary>
    /// Resolves the image file path, handling cases where the user provides a path without an
    /// extension. Supports Xbox ISO/XISO images (<c>.iso</c>, <c>.xiso</c>), CISO-compressed
    /// images (<c>.cso</c>, including split <c>.1.cso</c> sets) and ZArchive (<c>.zar</c>)
    /// files. Tries multiple strategies to find the file:
    /// 1. Return original path if file exists
    /// 2. If path is a directory containing exactly one image file, resolve to it
    /// 3. If no extension, try appending each supported extension
    /// 4. If just a filename, try looking in current directory
    /// </summary>
    internal static string? ResolveImagePath(string imagePath)
    {
        // 1. Check if the file exists as-is
        if (File.Exists(imagePath))
        {
            return imagePath;
        }

        // 2. If the path is a directory, look for a single image file inside it
        if (Directory.Exists(imagePath))
        {
            try
            {
                var imageFiles = FindImageFiles(imagePath);
                switch (imageFiles.Count)
                {
                    case 1:
                        Log.Debug("Resolved directory '{ImagePath}' to image file '{Resolved}'", imagePath,
                            imageFiles[0]);
                        return imageFiles[0];
                    case > 1:
                        Log.Debug(
                            "Directory '{ImagePath}' contains multiple image files; cannot auto-resolve.", imagePath);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error scanning directory '{ImagePath}' for image files", imagePath);
            }
        }

        // 3. If no extension provided, try appending each supported extension
        if (string.IsNullOrEmpty(Path.GetExtension(imagePath)))
        {
            foreach (var candidate in EnumerateExtensionCandidates(imagePath))
            {
                if (File.Exists(candidate))
                {
                    Log.Debug("Resolved '{ImagePath}' to '{Resolved}'", imagePath, candidate);
                    return candidate;
                }
            }
        }

        // 4. If it's just a filename (no path), try looking in current directory
        if (!imagePath.Contains(Path.DirectorySeparatorChar) &&
            !imagePath.Contains(Path.AltDirectorySeparatorChar))
        {
            var inCurrentDir = Path.Combine(Environment.CurrentDirectory, imagePath);
            if (File.Exists(inCurrentDir))
            {
                Log.Debug("Resolved '{ImagePath}' to '{Resolved}'", imagePath, inCurrentDir);
                return inCurrentDir;
            }

            // Also try each supported extension in the current directory
            if (string.IsNullOrEmpty(Path.GetExtension(imagePath)))
            {
                foreach (var candidate in EnumerateExtensionCandidates(inCurrentDir))
                {
                    if (File.Exists(candidate))
                    {
                        Log.Debug("Resolved '{ImagePath}' to '{Resolved}'", imagePath, candidate);
                        return candidate;
                    }
                }
            }
        }

        // File not found
        return null;
    }

    /// <summary>
    /// The file extensions the resolver recognizes, in preference order.
    /// </summary>
    private static readonly string[] ImageExtensions = [".iso", ".xiso", ".cso", ".zar"];

    private static List<string> FindImageFiles(string directory)
    {
        var imageFiles = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in ImageExtensions)
        {
            foreach (var file in Directory.GetFiles(directory, "*" + extension, SearchOption.TopDirectoryOnly))
            {
                // A split CISO set (game.1.cso, game.2.cso, ...) is one image; the
                // entry point is the first part, so continuation parts are ignored.
                if (string.Equals(extension, ".cso", StringComparison.OrdinalIgnoreCase) && IsCsoContinuationPart(file))
                {
                    continue;
                }

                if (seen.Add(file))
                {
                    imageFiles.Add(file);
                }
            }
        }

        return imageFiles;
    }

    private static bool IsCsoContinuationPart(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var separator = name.LastIndexOf('.');
        if (separator < 0 || separator == name.Length - 1)
        {
            return false;
        }

        return int.TryParse(name.AsSpan(separator + 1), System.Globalization.CultureInfo.InvariantCulture, out var part) && part >= 2;
    }

    private static IEnumerable<string> EnumerateExtensionCandidates(string path)
    {
        foreach (var extension in ImageExtensions)
        {
            yield return path + extension;
        }
    }
}
