using System.Runtime.CompilerServices;
using Rhino.Geometry;
using Rhino.Runtime.InProcess;
using RhinoComputeLinuxRepro;

internal static class Program
{
    private const string DefaultRhinoSystemDirectory = "/usr/lib/rhino3d";
    private static readonly Uri DefaultComputeUri = new("http://127.0.0.1:6500/");

    // Keep Main free of RhinoCommon references. The resolver needs to install its
    // assembly-resolution hooks before JIT compilation reaches RhinoCore or Brep.
    public static async Task<int> Main(string[] args)
    {
        if (!TryParseOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine($"ERROR: {error}");
            PrintUsage();
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        if (options.Mode == Mode.ComputeApi)
        {
            return await RunComputeApiAsync(options.ComputeUri);
        }

        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("ERROR: The standalone reproduction is Linux-only.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHINO_TOKEN")))
        {
            Console.Error.WriteLine("ERROR: RHINO_TOKEN must be set for the standalone reproduction. Its value is not printed.");
            return 2;
        }

        InitializeResolver(options.RhinoSystemDirectory);
        return StartRhinoAndCreateBrep();
    }

    private static async Task<int> RunComputeApiAsync(Uri computeUri)
    {
        Console.WriteLine($"Calling {new Uri(computeUri, ComputeGeometryApiProbe.CreateFromBoxPath)}");
        var result = await ComputeGeometryApiProbe.CreateMeshFromBoxAsync(computeUri);
        Console.WriteLine($"HTTP {(int)result.StatusCode} ({result.StatusCode})");
        Console.WriteLine($"Serialized mesh returned: {result.SerializedMeshReturned}");
        Console.WriteLine($"Response length: {result.ResponseBody.Length} bytes");

        if (!result.SerializedMeshReturned)
        {
            Console.Error.WriteLine("ERROR: Compute did not return a successful serialized mesh.");
            Console.Error.WriteLine(result.ResponseBody);
            return 4;
        }

        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializeResolver(string rhinoSystemDirectory)
    {
        RhinoInside.Resolver.Initialize(rhinoSystemDirectory);
        Console.Error.WriteLine($"CHECKPOINT: Resolver initialized from {RhinoInside.Resolver.RhinoSystemDirectory}.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartRhinoAndCreateBrep()
    {
        using var core = new RhinoCore(null, WindowStyle.NoWindow);
        Console.Error.WriteLine("CHECKPOINT: RhinoCore constructed.");
        Console.Error.WriteLine("CHECKPOINT: Calling deterministic Brep.CreateFromBox operation.");

        // On the affected Linux setup, this native call terminates with
        // Rhino.Runtime.NotLicensedException (exit code 134).
        using var brep = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 20, 30));
        if (brep is null || !brep.IsValid || !brep.IsSolid)
        {
            Console.Error.WriteLine("ERROR: Brep.CreateFromBox did not produce a valid solid.");
            return 4;
        }

        Console.WriteLine("SUCCESS: Standalone Rhino.Inside geometry completed.");
        return 0;
    }

    private static bool TryParseOptions(string[] args, out Options options, out string error)
    {
        var mode = Mode.ComputeApi;
        var computeUri = DefaultComputeUri;
        var rhinoSystemDirectory = Environment.GetEnvironmentVariable("RHINO_SYSTEM_DIR") ?? DefaultRhinoSystemDirectory;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--mode" when index + 1 < args.Length:
                    var value = args[++index];
                    if (value == "compute-api") mode = Mode.ComputeApi;
                    else if (value == "standalone") mode = Mode.Standalone;
                    else
                    {
                        options = default;
                        error = "--mode must be compute-api or standalone.";
                        return false;
                    }
                    break;
                case "--url" when index + 1 < args.Length && Uri.TryCreate(args[++index], UriKind.Absolute, out var parsedUri):
                    computeUri = parsedUri;
                    break;
                case "--rhino-system-dir" when index + 1 < args.Length:
                    rhinoSystemDirectory = args[++index];
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    options = default;
                    error = $"Unknown or invalid option: {args[index]}";
                    return false;
            }
        }

        options = new Options(mode, computeUri, rhinoSystemDirectory, showHelp);
        error = string.Empty;
        return true;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: RhinoComputeLinuxRepro.Runner [--mode compute-api|standalone] [--url URI] [--rhino-system-dir PATH]");
        Console.WriteLine("  compute-api (default): calls Compute Mesh.CreateFromBox over HTTP; no local Rhino is loaded.");
        Console.WriteLine("  standalone: starts Rhino.Inside then calls Brep.CreateFromBox; requires RHINO_TOKEN on Linux.");
    }

    private enum Mode { ComputeApi, Standalone }
    private readonly record struct Options(Mode Mode, Uri ComputeUri, string RhinoSystemDirectory, bool ShowHelp);
}
