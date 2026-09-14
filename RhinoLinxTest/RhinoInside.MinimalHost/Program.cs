using System.Runtime.CompilerServices;
using Rhino.Geometry;
using Rhino.Runtime.InProcess;

internal static class Program
{
    private const string RhinoSystemDirectory = "/usr/lib/rhino3d";

    // Keep Main free of RhinoCommon references. The resolver must have an opportunity
    // to install its assembly resolution hooks before JIT compiles RhinoCore usage.
    public static int Main()
    {
        InitializeResolver();
        return StartRhinoAndRunGeometry();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializeResolver()
    {
        RhinoInside.Resolver.Initialize(RhinoSystemDirectory);
        Console.Error.WriteLine($"CHECKPOINT: Resolver initialized from {RhinoInside.Resolver.RhinoSystemDirectory}.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartRhinoAndRunGeometry()
    {
        using var core = new RhinoCore(null, WindowStyle.NoWindow);
        Console.Error.WriteLine("CHECKPOINT: RhinoCore constructed.");

        // Match Compute's post-start security behavior without exposing the token.
        Environment.SetEnvironmentVariable(
            "RHINO_TOKEN",
            null,
            EnvironmentVariableTarget.Process);
        Console.Error.WriteLine("CHECKPOINT: RHINO_TOKEN cleared from this process environment.");

        return RunNativeGeometry();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunNativeGeometry()
    {
        Console.Error.WriteLine("CHECKPOINT: Starting deterministic native Brep.CreateFromBox operation.");

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

        Console.WriteLine("SUCCESS: Deterministic native geometry operation completed.");
        return 0;
    }
}
