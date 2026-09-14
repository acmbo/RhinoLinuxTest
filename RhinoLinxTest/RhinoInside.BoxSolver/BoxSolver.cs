using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Rhino.FileIO;
using Rhino.Geometry;

namespace RhinoInside.BoxSolver;

public static class BoxSolver
{
    private const string DefaultRhinoSystemDirectory = "/usr/lib/rhino3d";
    private const double Width = 10.0;
    private const double Depth = 20.0;
    private const double Height = 30.0;
    private const double ExpectedVolume = Width * Depth * Height;
    private const double Tolerance = 1e-8;

    public static int Run(string[] args, Func<WorkflowConfiguration, Func<WorkflowConfiguration, int>, int> runWithRhinoCore)
    {
        if (args.Length == 1)
        {
            if (args[0] is "--help" or "-h")
            {
                PrintUsage();
                return (int)BoxSolverExitCode.Success;
            }

            if (args[0] == "--verify-token")
            {
                return VerifyTokenInProcess();
            }
        }

        if (runWithRhinoCore is null)
        {
            throw new ArgumentNullException(nameof(runWithRhinoCore));
        }

        try
        {
            var configuration = WorkflowConfiguration.Parse(args);
            ValidatePreflight(configuration);

            // The executable owns resolver initialization and RhinoCore lifetime. This library
            // receives that boundary as a callback and performs RhinoCommon work only afterward.
            return runWithRhinoCore(configuration, RunPrototype);
        }
        catch (BoxSolverException exception)
        {
            WriteError(exception.Message, exception.InnerException);
            return (int)exception.BoxSolverExitCode;
        }
        catch (Exception exception)
        {
            WriteError("An unexpected error occurred.", exception);
            return (int)BoxSolverExitCode.Unexpected;
        }
    }

    private static int VerifyTokenInProcess()
    {
        var token = Environment.GetEnvironmentVariable("RHINO_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("ERROR: RHINO_TOKEN is not present in this .NET process environment.");
            return (int)BoxSolverExitCode.Preflight;
        }

        Console.WriteLine("RHINO_TOKEN is present in this .NET process environment. Its value was not displayed.");
        return (int)BoxSolverExitCode.Success;
    }

    private static int RunPrototype(WorkflowConfiguration configuration)
    {
        using var brep = CreateValidatedBox(out var metrics);

        WriteAndVerify3dm(brep, configuration.OutputPath);

        var summary = new RunSummary(
            configuration.RhinoSystemDirectory,
            typeof(Rhino.RhinoApp).Assembly.GetName().Version?.ToString() ?? "unknown",
            metrics.IsSolid,
            new[] { metrics.Width, metrics.Depth, metrics.Height },
            metrics.Volume,
            configuration.OutputPath);

        Console.WriteLine(JsonSerializer.Serialize(
            summary,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));

        return (int)BoxSolverExitCode.Success;
    }

    private static Brep CreateValidatedBox(out BoxMetrics metrics)
    {
        Brep? brep = null;

        try
        {
            var polyline = new Polyline(new[]
            {
                new Point3d(0, 0, 0),
                new Point3d(Width, 0, 0),
                new Point3d(Width, Depth, 0),
                new Point3d(0, Depth, 0),
                new Point3d(0, 0, 0)
            });

            if (!polyline.IsValid || !polyline.IsClosed || polyline.Count != 5)
            {
                throw new BoxSolverException(
                    BoxSolverExitCode.GeometryValidation,
                    "The rectangular polyline profile is invalid or not closed.");
            }

            using var profile = new PolylineCurve(polyline);
            if (!profile.IsValid || !profile.IsClosed || !profile.TryGetPlane(out _))
            {
                throw new BoxSolverException(
                    BoxSolverExitCode.GeometryValidation,
                    "The polyline profile is not a valid closed planar curve.");
            }

            using var extrusion = Extrusion.Create(profile, Height, cap: true)
                ?? throw new BoxSolverException(
                    BoxSolverExitCode.GeometryValidation,
                    "Rhino could not create a capped extrusion from the polyline profile.");

            brep = extrusion.ToBrep()
                ?? throw new BoxSolverException(
                    BoxSolverExitCode.GeometryValidation,
                    "Rhino could not convert the extrusion to a Brep.");

            if (!brep.IsValid || !brep.IsSolid)
            {
                throw new BoxSolverException(
                    BoxSolverExitCode.GeometryValidation,
                    "The generated Brep is invalid or is not a solid.");
            }

            var bounds = brep.GetBoundingBox(accurate: true);
            var size = bounds.Max - bounds.Min;
            var massProperties = VolumeMassProperties.Compute(brep)
                ?? throw new BoxSolverException(
                    BoxSolverExitCode.GeometryValidation,
                    "Rhino could not calculate volume mass properties for the generated Brep.");

            if (!NearlyEqual(size.X, Width) ||
                !NearlyEqual(size.Y, Depth) ||
                !NearlyEqual(size.Z, Height) ||
                !NearlyEqual(massProperties.Volume, ExpectedVolume))
            {
                throw new BoxSolverException(
                    BoxSolverExitCode.GeometryValidation,
                    $"The generated Brep did not match the expected {Width} x {Depth} x {Height} dimensions and {ExpectedVolume} volume.");
            }

            metrics = new BoxMetrics(size.X, size.Y, size.Z, massProperties.Volume, brep.IsSolid);
            var completedBrep = brep;
            brep = null;
            return completedBrep!;
        }
        catch (BoxSolverException)
        {
            brep?.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            brep?.Dispose();
            throw new BoxSolverException(
                BoxSolverExitCode.GeometryValidation,
                "Unable to create or validate the box extrusion.",
                exception);
        }
    }

    private static void WriteAndVerify3dm(Brep brep, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(brep);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new BoxSolverException(BoxSolverExitCode.OutputValidation, "The output path is empty.");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new BoxSolverException(BoxSolverExitCode.OutputValidation, "The output path has no parent directory.");
        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp.3dm");
        var operation = "creating the temporary output directory";

        try
        {
            Directory.CreateDirectory(outputDirectory);

            operation = "adding the Brep to the temporary 3dm model";
            Guid objectId;
            using (var model = new File3dm())
            {
                objectId = model.Objects.AddBrep(brep);
                if (objectId == Guid.Empty)
                {
                    throw new BoxSolverException(
                        BoxSolverExitCode.OutputValidation,
                        "Rhino did not add the Brep to the temporary 3dm model.");
                }

                operation = "writing the temporary 3dm file";
                if (!model.Write(temporaryPath, 8))
                {
                    throw new BoxSolverException(
                        BoxSolverExitCode.OutputValidation,
                        $"Rhino could not write the temporary 3dm file '{temporaryPath}'.");
                }
            }

            operation = "checking the temporary 3dm file";
            var temporaryFile = new FileInfo(temporaryPath);
            if (!temporaryFile.Exists || temporaryFile.Length == 0)
            {
                throw new BoxSolverException(
                    BoxSolverExitCode.OutputValidation,
                    $"Rhino reported success, but the temporary 3dm file '{temporaryPath}' was not created or is empty.");
            }

            operation = "re-opening the temporary 3dm file";
            var readBack = File3dm.ReadWithLog(temporaryPath, out var readLog);
            if (readBack is null)
            {
                throw new BoxSolverException(
                    BoxSolverExitCode.OutputValidation,
                    WithRhinoReadLog(
                        $"Rhino could not re-open the temporary 3dm file '{temporaryPath}'.",
                        readLog));
            }

            using (readBack)
            {
                operation = "counting objects in the re-opened 3dm file";
                if (readBack.Objects.Count != 1)
                {
                    throw new BoxSolverException(
                        BoxSolverExitCode.OutputValidation,
                        WithRhinoReadLog(
                            "The written 3dm file does not contain exactly one object.",
                            readLog));
                }

                // Object IDs are persisted by the 3dm format. Prefer an ID lookup over
                // enumeration because File3dmObjectTable's Linux enumerator can report the
                // correct Count while failing to materialize an entry.
                operation = $"retrieving object '{objectId}' from the re-opened 3dm file";
                var persistedObject = readBack.Objects.FindId(objectId);
                if (persistedObject is null)
                {
                    throw new BoxSolverException(
                        BoxSolverExitCode.OutputValidation,
                        WithRhinoReadLog(
                            $"The written 3dm file reports one object, but Rhino could not retrieve the expected object '{objectId}'.",
                            readLog));
                }

                operation = "retrieving the object's geometry from the re-opened 3dm file";
                var persistedGeometry = persistedObject.Geometry;
                if (persistedGeometry is null)
                {
                    throw new BoxSolverException(
                        BoxSolverExitCode.OutputValidation,
                        WithRhinoReadLog(
                            "The written 3dm file contains an object with null geometry.",
                            readLog));
                }

                if (persistedGeometry is not Brep persistedBrep)
                {
                    throw new BoxSolverException(
                        BoxSolverExitCode.OutputValidation,
                        WithRhinoReadLog(
                            $"The written 3dm file contains a {persistedGeometry.GetType().Name}, not a Brep.",
                            readLog));
                }

                operation = "validating the re-opened Brep";
                if (!persistedBrep.IsValid)
                {
                    throw new BoxSolverException(
                        BoxSolverExitCode.OutputValidation,
                        WithRhinoReadLog(
                            "The written 3dm file does not contain a valid Brep.",
                            readLog));
                }

                if (!persistedBrep.IsSolid)
                {
                    throw new BoxSolverException(
                        BoxSolverExitCode.OutputValidation,
                        WithRhinoReadLog(
                            "The written 3dm file does not contain a solid Brep.",
                            readLog));
                }
            }

            operation = "publishing the validated 3dm file";
            File.Move(temporaryPath, outputPath, overwrite: true);
        }
        catch (BoxSolverException)
        {
            throw;
        }
        catch (NullReferenceException exception)
        {
            throw new BoxSolverException(
                BoxSolverExitCode.OutputValidation,
                $"Rhino returned an incomplete object while {operation}; the output file '{outputPath}' was not replaced.",
                exception);
        }
        catch (Exception exception)
        {
            throw new BoxSolverException(
                BoxSolverExitCode.OutputValidation,
                $"Unable to write or validate the output file '{outputPath}' while {operation}.",
                exception);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static string WithRhinoReadLog(string message, string? readLog) =>
        string.IsNullOrWhiteSpace(readLog)
            ? message
            : $"{message} Rhino read log: {readLog.Trim()}";

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception exception)
        {
            // Do not replace a more useful write or validation failure with cleanup failure.
            Console.Error.WriteLine($"WARNING: Could not delete temporary file '{temporaryPath}': {exception.Message}");
        }
    }

    private static void ValidatePreflight(WorkflowConfiguration configuration)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new BoxSolverException(BoxSolverExitCode.Preflight, "This prototype currently supports Linux only.");
        }

        if (!Environment.Is64BitProcess || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new BoxSolverException(
                BoxSolverExitCode.Preflight,
                "This initial prototype requires a 64-bit x86-64 process.");
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHINO_TOKEN")))
        {
            throw new BoxSolverException(
                BoxSolverExitCode.Preflight,
                "RHINO_TOKEN is not set. Export a Core-Hour Billing token before running the prototype.");
        }

        if (!Directory.Exists(configuration.RhinoSystemDirectory))
        {
            throw new BoxSolverException(
                BoxSolverExitCode.Preflight,
                $"Rhino system directory does not exist: '{configuration.RhinoSystemDirectory}'.");
        }

        RequireFile(configuration.RhinoSystemDirectory, "libRhinoLibrary.so");
        RequireFile(configuration.RhinoSystemDirectory, "RhinoCommon.dll");

        if (!Directory.EnumerateFiles(configuration.RhinoSystemDirectory, "dotnetstart.*.dll").Any())
        {
            throw new BoxSolverException(
                BoxSolverExitCode.Preflight,
                $"No compatible dotnetstart assembly was found in '{configuration.RhinoSystemDirectory}'.");
        }

        var outputDirectory = Path.GetDirectoryName(configuration.OutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new BoxSolverException(BoxSolverExitCode.Preflight, "The output path must include a parent directory.");
        }

        if (!string.Equals(Path.GetExtension(configuration.OutputPath), ".3dm", StringComparison.OrdinalIgnoreCase))
        {
            throw new BoxSolverException(BoxSolverExitCode.Preflight, "The output file must use the .3dm extension.");
        }

        if (File.Exists(configuration.OutputPath) && !configuration.Overwrite)
        {
            throw new BoxSolverException(
                BoxSolverExitCode.Preflight,
                $"Output file already exists: '{configuration.OutputPath}'. Re-run with --overwrite to replace it.");
        }
    }

    private static void RequireFile(string directory, string fileName)
    {
        var fullPath = Path.Combine(directory, fileName);
        if (!File.Exists(fullPath))
        {
            throw new BoxSolverException(BoxSolverExitCode.Preflight, $"Required Rhino file is missing: '{fullPath}'.");
        }
    }

    private static bool NearlyEqual(double actual, double expected) =>
        Math.Abs(actual - expected) <= Tolerance;

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Usage:
              dotnet run --project src/RhinoInside.BoxSample -- [--output <path>] [--overwrite]
              dotnet run --project src/RhinoInside.BoxSample -- --verify-token

            Required environment:
              RHINO_TOKEN        Core-Hour Billing token (never passed on the command line)

            Optional environment:
              RHINO_SYSTEM_DIR   Rhino installation directory (default: /usr/lib/rhino3d)
            """);
    }

    private static void WriteError(string message, Exception? exception)
    {
        Console.Error.WriteLine($"ERROR: {message}");
        if (exception is not null)
        {
            Console.Error.WriteLine(exception);
        }
    }

    public sealed record WorkflowConfiguration(string RhinoSystemDirectory, string OutputPath, bool Overwrite)
    {
        public static WorkflowConfiguration Parse(string[] args)
        {
            var outputPath = Path.GetFullPath(Path.Combine("artifacts", "extruded-box.3dm"));
            var overwrite = false;

            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--output":
                        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                        {
                            throw new BoxSolverException(BoxSolverExitCode.Preflight, "--output requires a path.");
                        }

                        outputPath = Path.GetFullPath(args[index]);
                        break;
                    case "--overwrite":
                        overwrite = true;
                        break;
                    default:
                        throw new BoxSolverException(BoxSolverExitCode.Preflight, $"Unknown argument: '{args[index]}'. Use --help for usage.");
                }
            }

            var rhinoSystemDirectory = Environment.GetEnvironmentVariable("RHINO_SYSTEM_DIR");
            if (string.IsNullOrWhiteSpace(rhinoSystemDirectory))
            {
                rhinoSystemDirectory = DefaultRhinoSystemDirectory;
            }

            return new WorkflowConfiguration(Path.GetFullPath(rhinoSystemDirectory), outputPath, overwrite);
        }
    }

    public sealed class BoxSolverException : Exception
    {
        public BoxSolverException(BoxSolverExitCode exitCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            BoxSolverExitCode = exitCode;
        }

        public BoxSolverExitCode BoxSolverExitCode { get; }
    }

    private sealed record BoxMetrics(double Width, double Depth, double Height, double Volume, bool IsSolid);

    private sealed record RunSummary(
        string RhinoSystemDirectory,
        string RhinoVersion,
        bool IsSolid,
        double[] BoundingBox,
        double Volume,
        string Output);

    public enum BoxSolverExitCode
    {
        Success = 0,
        Unexpected = 1,
        Preflight = 2,
        RhinoStartup = 3,
        GeometryValidation = 4,
        OutputValidation = 5
    }
}
