using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Rhino.Geometry;
using Rhino.Runtime.InProcess;

internal static class Program
{
    private const string DefaultRhinoSystemDirectory = "/usr/lib/rhino3d";
    private const int DefaultPort = 5057;

    // Keep Main free of RhinoCommon references. Resolver initialization must occur before
    // JIT compilation can bind RhinoCore or geometry types.
    public static int Main(string[] args)
    {
        if (!TryParseOptions(args, out var options, out var parseError))
        {
            Console.Error.WriteLine($"ERROR: {parseError}");
            PrintUsage();
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        InitializeResolver(options.RhinoSystemDirectory);
        return StartRhinoAndRunWebHost(options);
    }

    private static bool TryParseOptions(string[] args, out HostOptions options, out string error)
    {
        var port = DefaultPort;
        var showHelp = false;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--port" when index + 1 < args.Length && int.TryParse(args[++index], out var parsedPort) && parsedPort is > 0 and <= 65535:
                    port = parsedPort;
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

        options = new HostOptions(
            Environment.GetEnvironmentVariable("RHINO_SYSTEM_DIR") ?? DefaultRhinoSystemDirectory,
            port,
            showHelp);
        error = string.Empty;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializeResolver(string rhinoSystemDirectory)
    {
        RhinoInside.Resolver.Initialize(rhinoSystemDirectory);
        Console.Error.WriteLine($"CHECKPOINT: Resolver initialized from {RhinoInside.Resolver.RhinoSystemDirectory}.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartRhinoAndRunWebHost(HostOptions options)
    {
        using var core = new RhinoCore(null, WindowStyle.NoWindow);
        Console.Error.WriteLine("CHECKPOINT: RhinoCore constructed.");

        // Match Compute's post-start token handling without exposing the value.
        Environment.SetEnvironmentVariable("RHINO_TOKEN", null, EnvironmentVariableTarget.Process);
        Console.Error.WriteLine("CHECKPOINT: RHINO_TOKEN cleared from this process environment.");

        Rhino.Runtime.HostUtils.OnExceptionReport += (_, exception) =>
            Console.Error.WriteLine($"CHECKPOINT: Rhino exception callback reported {exception.GetType().FullName}.");

        return RunApplicationStartedCheckpoint(options);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunApplicationStartedCheckpoint(HostOptions options)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");

        using var app = builder.Build();
        var result = 1;

        app.MapGet("/healthcheck", () => Results.Text("Healthy"));
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            Console.Error.WriteLine("CHECKPOINT: ASP.NET host started; beginning deterministic native Brep.CreateFromBox operation.");
            try
            {
                result = RunNativeGeometry();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"CHECKPOINT: ApplicationStarted geometry threw {exception.GetType().FullName}: {exception.Message}");
                result = 4;
            }
            finally
            {
                app.Lifetime.StopApplication();
            }
        });

        Console.Error.WriteLine($"CHECKPOINT: Starting loopback-only ASP.NET host on port {options.Port}.");
        app.Run();
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunNativeGeometry()
    {
        var brep = Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 20, 30));
        if (brep is null)
        {
            Console.Error.WriteLine("ERROR: Brep.CreateFromBox returned null.");
            return 4;
        }

        using (brep)
        {
            if (!brep.IsValid || !brep.IsSolid)
            {
                Console.Error.WriteLine("ERROR: Brep.CreateFromBox did not create a valid solid Brep.");
                return 4;
            }
        }

        Console.WriteLine("SUCCESS: ApplicationStarted native geometry checkpoint completed.");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: RhinoInside.AspNetHost [--port PORT]");
    }

    private readonly record struct HostOptions(string RhinoSystemDirectory, int Port, bool ShowHelp);
}
