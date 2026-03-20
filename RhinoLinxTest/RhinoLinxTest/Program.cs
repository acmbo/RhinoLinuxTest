using Microsoft.Win32;
using Newtonsoft.Json;
using Rhino.Runtime.InProcess;
using Serilog;
using System;
using System.IO;
using System.Reflection;
using Rhino.Geometry;

namespace GDEnergyWorker
{

    /// <summary>
    /// GDEnergy as a process. Initilizes Rhino and then start GDEnergyCore by reading inputs
    /// From an JSON file of the hard drive. Json path needs to be given via arguments of the console
    /// </summary>
    internal class Program
    {
        
        public static IDisposable RhinoCore { get; set; }
        private static string rhinoDir;
        private static readonly object _cleanupLock = new object();
        private static bool _cleanedUp = false;
        private const bool AttachDebbuger = false;

        private static void CleanupAndExit(int exitCode, ILogger logger)
        {
            lock ( _cleanupLock )
            {
                if ( _cleanedUp )
                {
                    Environment.Exit(exitCode);
                    return;
                }
                _cleanedUp = true;
            }

            // Force garbage collection before disposing RhinoCore
            // to ensure all Rhino objects are cleaned up while Rhino is still alive
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            try
            {
                RhinoCore?.Dispose();
            }
            catch ( Exception ex )
            {
                logger.Error($"Error disposing RhinoCore: {ex.Message}");
            }
            RhinoCore = null;

            Environment.Exit(exitCode);
        }

        /// <summary>
        /// For debbung dll load paths. Keep if you want to see the resolving in console
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Console.WriteLine($"Trying to resolve: {args.Name}");
            Console.WriteLine($"Requested by: {args.RequestingAssembly?.FullName}");

            // Try various locations
            var assemblyName = new AssemblyName(args.Name);
            var possiblePaths = new[]
            {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName.Name + ".dll"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", assemblyName.Name + ".dll"),
            Path.Combine(Environment.CurrentDirectory, assemblyName.Name + ".dll"),
        };

            foreach ( var path in possiblePaths )
            {
                Console.WriteLine($"Checking: {path}");
                if ( File.Exists(path) )
                {
                    Console.WriteLine($"Found at: {path}");
                    return Assembly.LoadFrom(path);
                }
            }

            Console.WriteLine($"Assembly not found anywhere!");
            return null;
        }

         
        public static void StartRhino(ILogger logger)
        {
            if ( RhinoCore != null )
                RhinoCore.Dispose();

            logger.Information($"Start new RhinoCore");

            RhinoCore = new RhinoCore(null, WindowStyle.NoWindow);

            logger.Information($"Start new RhinoCore ended");

            Rhino.Runtime.HostUtils.OnExceptionReport += (source, ex) => {
                logger.Error($"An exception occurred while processing request: {ex}");
            };
        }

        /// <summary>
        /// Configure Serilog to log to a file and to console for 
        /// </summary>
        /// <returns></returns>
        public static ILogger InitializeLogger()
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "log", "Logs", "gdenergyWorker-.log");
            var logger = new LoggerConfiguration()
#if RELEASE   
                            .MinimumLevel.Warning()
                            .WriteTo.File(logPath,
                                rollingInterval: RollingInterval.Day,
                                retainedFileCountLimit: 31,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
#else
                            .MinimumLevel.Debug()
                            .WriteTo.Console()

#endif
                            .CreateLogger();
            return logger;
        }


        // Its possible that this needs to be a STAThread. I'm not sure though, so keep this as Hint if the thread isn't running
        //[STAThread]
        static void Main(string[] args)
        {

            ILogger logger = InitializeLogger();

            // Debug assembly resolve
            //AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            logger.Information($"Assembly Directory:{AppContext.BaseDirectory}");

            string rhinoVersion = "8.0";

#if DEBULINUX

#else
            rhinoDir = Registry.GetValue($@"HKEY_LOCAL_MACHINE\SOFTWARE\McNeel\Rhinoceros\{rhinoVersion}\Install", "Path", null) as string ?? string.Empty;

#endif

            logger.Information("Starting RhinoInside");

            RhinoInside.Resolver.Initialize();

            logger.Information("RhinoInside solved");
#if DEBULINUX

#else
            string envPath = Environment.GetEnvironmentVariable("path");
            Environment.SetEnvironmentVariable("path", envPath + ";" + rhinoDir);

#endif
            var test = RhinoInside.Resolver.RhinoSystemDirectory;

            Log.Information("Rhino system directory: {Path}", RhinoInside.Resolver.RhinoSystemDirectory);


            logger.Information("Env Setted");

            StartRhino(logger);

            logger.Information($"Rhino system directory: {RhinoInside.Resolver.RhinoSystemDirectory}");
            logger.Information($"Launching RhinoCore library as {Environment.UserName}");


#pragma warning disable CS0162 // Unreachable code detected - Keep for debbuging
            if ( AttachDebbuger ) System.Diagnostics.Debugger.Launch();
#pragma warning restore CS0162

            logger.Information("Starting Reading Job");



            try
            {
                logger.Information("Starting GDEnergyCore Process!");

                var sucess =  RhinoLib.Test.maintest();

                logger.Information($"\n\n\n sucess \n\n\n");

                logger.Information("Finished Process!");
                CleanupAndExit(0, logger);
            }
            catch ( Exception ex )
            {
                logger.Error($"Error: {ex.Message}");
                CleanupAndExit(3, logger);
            }



        }
    }
}
