using System.Runtime.CompilerServices;
using Rhino.Runtime.InProcess;
using Solver = RhinoInside.BoxSolver.BoxSolver;

namespace RhinoInside.BoxSample;

internal static class Program
{
    // Rhino.Inside resolver installation and RhinoCore lifetime stay in this executable.
    // The solver library contains all subsequent RhinoCommon workflow and geometry logic.
    public static int Main(string[] args) => Solver.Run(args, RunWithRhinoCore);

    private static int RunWithRhinoCore(
        Solver.WorkflowConfiguration configuration,
        Func<Solver.WorkflowConfiguration, int> workflow)
    {
        InitializeResolver(configuration.RhinoSystemDirectory);
        using var rhinoCore = StartRhinoCore();
        return workflow(configuration);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IDisposable StartRhinoCore()
    {
        try
        {
            return new RhinoCore(null, WindowStyle.NoWindow);
        }
        catch (Exception exception)
        {
            throw new Solver.BoxSolverException(
                Solver.BoxSolverExitCode.RhinoStartup,
                "RhinoCore could not start in headless mode.",
                exception);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializeResolver(string rhinoSystemDirectory)
    {
        try
        {
            global::RhinoInside.Resolver.Initialize(rhinoSystemDirectory);
            var resolvedRhinoSystemDirectory = global::RhinoInside.Resolver.RhinoSystemDirectory;
            Console.Error.WriteLine($"Rhino system directory: {resolvedRhinoSystemDirectory}");
        }
        catch (Exception exception)
        {
            throw new Solver.BoxSolverException(
                Solver.BoxSolverExitCode.RhinoStartup,
                $"Unable to initialize Rhino.Inside using '{rhinoSystemDirectory}'.",
                exception);
        }
    }
}
