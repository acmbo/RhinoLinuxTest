# Recipe: Share Rhino plug-in logic between Windows and Linux


## Important platform limit — read this first

As of this review, RhinoCommon's published plug-in platforms are **Rhino for Windows and Rhino for Mac**. McNeel's published Linux route is Rhino.Compute/Rhino.Inside, not a general Rhino desktop plug-in host. The Linux Rhino.Compute guide currently calls the project work in progress, does not recommend it for production, and says third-party plug-in loading is limited to certain Yak packages containing Grasshopper add-ons. Therefore:

- You can build a normal `.rhp` plug-in for a supported Rhino host and share its geometry/business logic with a Linux CLI executable.
- You can compile and test Linux-specific code paths in that CLI and run Rhino geometry through Rhino.Inside where the installed Linux Rhino runtime supports it.
- Keep the plug-in wrapper separate from the CLI wrapper. Rhino owns RhinoCore and the active document when it loads a plug-in. A standalone CLI owns its resolver and RhinoCore lifetime.

This recipe is about making the *code and execution architecture* portable. This is an introduction into, how to mmake plugins future ready for linux builds, if you development environment needs it.


## 1. Understand the project shape

A useful beginner-friendly layout separates the algorithm from the way it is started:

```text
MyRhinoTool/
├── MyRhinoTool.sln
├── src/
│   ├── MyRhinoTool.Core/       # business rules and data types; no Rhino UI
│   ├── MyRhinoTool.Geometry/   # shared RhinoCommon geometry operations
│   ├── MyRhinoTool.Plugin/     # .rhp entry point: PlugIn, Command, UI integration
│   └── MyRhinoTool.Cli/        # optional executable: arguments, resolver, RhinoCore
└── Directory.Build.props      # shared build settings, if needed
```

For a very small tool, `Core` and `Geometry` can be one library. Do not put Rhino startup or document UI work in that library.

The important dependency direction is:

```text
Rhino plug-in wrapper ─┐
                       ├── shared geometry / business library
Linux CLI host ────────┘
```

- **Core/geometry library:** implements the operation once. If it uses `Rhino.Geometry` types, it needs a RhinoCommon compile reference. Keep Windows registry calls, Linux filesystem assumptions, WinForms/WPF, and application startup out of it.
- **Plug-in wrapper:** contains the Rhino `PlugIn` and `Command` classes. Rhino loads it, so it uses the already-running Rhino process and active document. It must not create or dispose another `RhinoCore`.
- **CLI host:** handles command-line arguments and process setup. For a standalone Rhino.Inside run, it initializes `RhinoInside.Resolver` before RhinoCommon work, creates `RhinoCore`, calls the shared library, then disposes the core.

This mirrors a useful pattern in a small repository: a geometry class library can be referenced both by a console execution project today and by a future plug-in wrapper. The console project and a Rhino `.rhp` project are different entry points, even when they call the same library.

## 2. Choose the Rhino and .NET targets

A Rhino plug-in targets the runtime of the **Rhino version that will load it**. Choose by Rhino generation first; choose platform-specific code only where the source really differs by operating system.

| Host/runtime | Recommended target | Notes |
| --- | --- | --- |
| Rhino 8 using its .NET Core runtime (Windows or Mac) | `net8.0` | Rhino 8.20 and later default to .NET 8. This is the portable TFM; do not add `-windows` unless the assembly is intentionally Windows-only. |
| Rhino 9 (Windows or Mac) | `net10.0` | Rhino 9 uses .NET 10 by default; use the RhinoCommon package that matches the Rhino 9 SDK/runtime you are targeting. |
| Rhino 7 or Rhino 8's legacy .NET Framework mode | `net48` | Legacy compatibility only. Rhino 9 deprecates the .NET Framework runtime; avoid it for new work unless legacy support is a requirement. |
| Linux CLI using the repository's Rhino 9 / Rhino.Inside sample pattern | `net10.0` | This is a separate CLI host, not an `.rhp` Linux deployment target. Validate against the installed Linux runtime. |

### Is `net8.0-windows` still supported?

**Yes, but it is a Windows-specific target and .NET 8 is near the end of its support window.** Microsoft's lifecycle page lists .NET 8 support through **November 11, 2026**. Rhino's current migration guide recommends plain `net8.0` for Rhino 8's .NET Core runtime (default beginning with Rhino 8.20), and `net10.0` for Rhino 9. For a plug-in that shares source or assemblies across platforms, prefer `net8.0`, not `net8.0-windows`, when targeting Rhino 8's .NET Core runtime.

Use `net8.0-windows` only when the project really depends on Windows-only APIs or Windows-only UI. That TFM is not a Linux compatibility switch. If your Windows UI uses WPF or Windows Forms, isolate it in a Windows-only project or replace it with a cross-platform UI approach such as Eto where appropriate.

If you support both Rhino 8 and Rhino 9, multi-target by Rhino generation (`net8.0;net10.0`) and use the matching RhinoCommon compile package for each target. Do not infer that the Rhino 8 Windows branch and Rhino 9 Linux branch in an existing test project are the same Rhino version: they are different host/runtime baselines.

## 3. Start from a real Rhino plug-in template

Use McNeel's RhinoCommon plug-in template or the equivalent current official template for your development environment. It creates the required plug-in entry point, command, metadata, and build/deployment settings. Keep those generated plug-in-specific settings; a plain class library or console project is not an `.rhp` plug-in.

Add your shared library as a project reference, then add the compile-time RhinoCommon version for every target framework. The following is an **illustrative project-file fragment**, not a replacement for the generated plug-in template:

```xml
<PropertyGroup>
  <!-- Rhino 8 .NET Core and Rhino 9 targets. -->
  <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <PlatformTarget>AnyCPU</PlatformTarget>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="..\MyRhinoTool.Geometry\MyRhinoTool.Geometry.csproj" />
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <PackageReference Include="RhinoCommon"
                    Version="8.25.25314.11001"
                    ExcludeAssets="runtime" />
</ItemGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="RhinoCommon"
                    Version="9.0.25350.305-wip"
                    ExcludeAssets="runtime" />
</ItemGroup>
```

The package versions above are **example pins from the reviewed project**, not a statement that they are the newest releases or the right versions for every installed Rhino. Check the package version and target-framework guidance for the Rhino version you actually support. RhinoCommon is the compile-time API; the Rhino installation supplies the runtime copy. Do not copy a Windows RhinoCommon/native runtime into a Linux output folder.

Any project that directly uses RhinoCommon types should declare an appropriate RhinoCommon compile reference itself. In particular, `PrivateAssets="all"` on a library's PackageReference prevents that dependency from flowing transitively to consumers; do not assume a wrapper can compile merely because a referenced library has RhinoCommon in its project file.

`AnyCPU` is appropriate for managed plug-in assemblies unless the plug-in has a specific, documented native architecture requirement. Any native dependency must be supplied for the operating system and CPU architecture where it will run. The Linux installer described below targets Ubuntu 24.04 amd64; that does not make all native plug-ins x64-portable.

## 4. Add Linux conditional compilation deliberately

### 4.1 Define the symbol for the *target build*, not the build computer

`LINUX` is a custom C# preprocessor symbol. Neither a plain `net8.0`/`net10.0` target nor a Linux Runtime Identifier automatically creates it. Also, checking the OS on which MSBuild is running is not the same as selecting the OS you intend to build for; that breaks cross-compilation from Windows.

For example, add a `Directory.Build.props` beside the solution so every project gets the same choice:

```xml
<Project>
  <PropertyGroup Condition="'$(RhinoTargetOS)' == 'Linux'">
    <DefineConstants>$(DefineConstants);LINUX</DefineConstants>
  </PropertyGroup>

  <PropertyGroup Condition="'$(RhinoTargetOS)' == 'Windows'">
    <DefineConstants>$(DefineConstants);WINDOWS_BUILD</DefineConstants>
  </PropertyGroup>
</Project>
```

The `$(DefineConstants);` prefix is important: it appends your symbol rather than replacing existing configuration symbols. `WINDOWS_BUILD` is also a custom symbol here, so it has the same explicit meaning as `LINUX`.

Select the intended branch explicitly when building:

```bash
# Linux flavor of the .NET 10 code (for the CLI/in-process Linux test host)
dotnet build src/MyRhinoTool.Cli/MyRhinoTool.Cli.csproj \
  -f net10.0 -p:RhinoTargetOS=Linux

# Windows flavor of the plug-in build; choose net8.0 or net10.0 to match Rhino
dotnet build src/MyRhinoTool.Plugin/MyRhinoTool.Plugin.csproj \
  -f net10.0 -p:RhinoTargetOS=Windows
```

Change the paths and target framework for your solution. The Linux command compiles a Linux-specific code path; it does **not** make the resulting assembly loadable as a Linux Rhino plug-in.

If you do not need to exclude platform-specific types/packages at compile time, prefer one portable code path and runtime checks such as `OperatingSystem.IsLinux()` / `OperatingSystem.IsWindows()`. Use `#if` when the compiler must not see a platform-only API, reference, or implementation.

### 4.2 Guard only the code that differs

For example, if an old Windows implementation uses the registry to find a Rhino directory, exclude that API from the Linux compilation and make the Linux path configurable:

```csharp
internal static class RhinoPaths
{
    internal static string GetSystemDirectory()
    {
#if LINUX
        return Environment.GetEnvironmentVariable("RHINO_SYSTEM_DIR")
            ?? "/usr/lib/rhino3d";
#elif WINDOWS_BUILD
        // Keep any Windows-only lookup in this branch and validate its result.
        return GetWindowsRhinoDirectory();
#else
        throw new PlatformNotSupportedException(
            "Build with RhinoTargetOS=Windows or RhinoTargetOS=Linux.");
#endif
    }

#if WINDOWS_BUILD
    private static string GetWindowsRhinoDirectory()
    {
        // Windows-only registry/path code goes here.
        throw new NotImplementedException("Replace with your Windows lookup.");
    }
#endif
}
```

`#else` means “every build without `LINUX`,” not “Windows.” Use an explicit `#elif WINDOWS_BUILD` when the code truly requires Windows, and keep an unsupported-platform fallback. Prefer `Path.Combine`, `Path.DirectorySeparatorChar`, environment/configuration, and managed cross-platform APIs over handwritten backslash paths, semicolon-separated `PATH` edits, Windows registry reads, COM, or P/Invoke.

Other good candidates for platform separation include native DLL versus `.so` loading, process executable names (`tool.exe` versus `tool`), and optional platform-specific integrations. Keep shared geometry/business rules outside these branches so both execution paths call the same implementation.

## 5. Keep the plug-in wrapper thin

The Rhino plug-in command should translate Rhino's document/UI input into a call to the shared operation, then translate the result back into Rhino document/UI changes. For example:

```csharp
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.PlugIns;

public sealed class MyRhinoToolPlugin : PlugIn
{
    // Keep plug-in initialization here; do not create RhinoCore.
}

public sealed class CreateSharedBoxCommand : Command
{
    public override string EnglishName => "CreateSharedBox";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        using var brep = MyRhinoTool.Geometry.BoxFactory.CreateBox();
        if (brep is null)
            return Result.Failure;

        doc.Objects.AddBrep(brep, new ObjectAttributes());
        doc.Views.Redraw();
        return Result.Success;
    }
}
```

Replace `BoxFactory.CreateBox()` with your own library call and add normal Rhino command error handling. The command should not know whether the shared geometry library was also called from a CLI.

Use Rhino's cross-platform UI APIs for controls that need to work in all supported desktop Rhino environments. Keep Windows-only controls in an isolated Windows project/assembly and do not reference that assembly from the shared geometry library.

## 6. Keep the CLI host separate

The CLI host may call the same library but owns its own process setup. A typical Linux host project has a plain `net10.0` target, x64 process architecture for an x64 Rhino runtime, a Rhino.Inside package, and a compile-only RhinoCommon reference. For example:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <PlatformTarget>x64</PlatformTarget>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Rhino.Inside" Version="9.0.26084.13070-beta" />
  <PackageReference Include="RhinoCommon"
                    Version="9.0.25350.305-wip"
                    ExcludeAssets="runtime"
                    PrivateAssets="all" />
  <ProjectReference Include="..\MyRhinoTool.Geometry\MyRhinoTool.Geometry.csproj" />
</ItemGroup>
```

These are repository example versions; update and validate them against your installed runtime before adopting them. The Linux CLI's essential startup order is:

1. Parse arguments and validate configuration without touching Rhino geometry.
2. Initialize `RhinoInside.Resolver` with the configured Linux Rhino system directory.
3. Create `RhinoCore` in a separate method.
4. Call the shared library while the core is alive.
5. Dispose geometry and then `RhinoCore` before exiting.

Minimal lifecycle sketch:

```csharp
using System.Runtime.CompilerServices;
using Rhino.Runtime.InProcess;

internal static class Program
{
    public static int Main(string[] args)
    {
        InitializeResolver();
        return RunWithRhino();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializeResolver()
    {
        var systemDirectory =
            Environment.GetEnvironmentVariable("RHINO_SYSTEM_DIR")
            ?? "/usr/lib/rhino3d";
        RhinoInside.Resolver.Initialize(systemDirectory);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RunWithRhino()
    {
        using var core = new RhinoCore(null, WindowStyle.NoWindow);
        return MyRhinoTool.Geometry.Job.Run();
    }
}
```

Keep RhinoCommon-heavy calls out of `Main` and static initializers until the resolver has been set up. This host pattern is not the way a normal Rhino plug-in starts: when Rhino loads the plug-in, Rhino already owns the runtime and core.

## 7. Prepare an Ubuntu test machine using the provided installer

Use the repository's `script/install-rhino-compute-ubuntu.sh` as the starting point rather than writing a second package-install script from scratch. It currently checks for **Ubuntu 24.04 amd64**, installs .NET SDKs 8 and 10 if missing, adds McNeel's package source, and installs `rhino-compute` and its runtime dependencies. It deliberately does not set billing credentials or start the service automatically.

From the repository root on the Ubuntu test machine:

```bash
./script/install-rhino-compute-ubuntu.sh
```

A Linux CLI may require a valid Core-Hour billing token; follow the installation script's protected-secret instructions, do not put token values in source control or logs, and remember that use may incur charges.


## 8. Build and test both paths

### Windows plug-in test

1. Install the matching Rhino version and developer tools.
2. Build the plug-in using the target framework for that Rhino version (`net8.0` for Rhino 8 .NET Core, `net10.0` for Rhino 9).
3. Load the `.rhp` using Rhino's plug-in manager or the plugin template's debug launch configuration.
4. Run the command and verify the document result.
5. Test a Release build and a clean machine with only the intended packaged dependencies.

### Linux CLI test

1. Use Ubuntu 24.04 amd64 (or another environment explicitly supported by the current Rhino Linux runtime guidance).
2. Build/run the CLI for Linux with the Linux symbol explicitly selected if the code uses `#if LINUX`.
3. Set `RHINO_SYSTEM_DIR` only when the runtime is not at the default `/usr/lib/rhino3d`.
4. Provide credentials only through your approved secret-management mechanism if required by your Rhino.Inside workflow.
5. Call the same geometry/business operation as the plug-in and verify output files/geometry deterministically.

Example build/run (adjust paths and CLI arguments):

```bash
dotnet build src/MyRhinoTool.Cli/MyRhinoTool.Cli.csproj \
  -c Release -f net10.0 -p:RhinoTargetOS=Linux

dotnet run --project src/MyRhinoTool.Cli/MyRhinoTool.Cli.csproj \
  -c Release -- --help
```

A successful CLI test proves that the shared operation worked in that Linux host/runtime. It does not prove the `.rhp` wrapper loads under Linux Compute. Test the actual plug-in in each Rhino host where McNeel supports plug-in loading.

## Official references

- McNeel, [Moving to .NET Core](https://developer.rhino3d.com/guides/rhinocommon/moving-to-dotnet-core/) — Rhino 8/9 runtime and target framework guidance; last updated July 7, 2026.
- McNeel, [What is RhinoCommon?](https://developer.rhino3d.com/guides/rhinocommon/what-is-rhinocommon/) — RhinoCommon SDK scope and supported Rhino platforms.
- McNeel, [Your First Plugin (Windows)](https://developer.rhino3d.com/guides/rhinocommon/your-first-plugin-windows/) — plug-in template, plug-in class and command anatomy.
- McNeel, [Getting Started with Rhino.Compute on Linux](https://developer.rhino3d.com/guides/compute/compute-linux-getting-started/) — current Linux Compute status, setup, billing-token warning, and third-party plug-in limitation; last updated September 15, 2026.
- Microsoft, [.NET and .NET Core lifecycle](https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core) — .NET 8 and .NET 10 support dates.
