# Recipe: migrate a Windows Rhino.Inside project to Linux

**Review date:** October 2, 2026
**Method:** read-only source inspection by two researcher agents, followed by documentation synthesis. No restore, build, publish, tests, or sample execution was performed.
**Target baseline:** a headless Linux x86-64 .NET application hosting Rhino 9 in-process. This is not a recipe for moving the Rhino desktop UI, Revit integration, or arbitrary Windows plug-ins to Linux.

## 1. Scope and source locations

The current solution directory does **not** contain a `src` directory. The original sample sources still exist at:

```text
/home/dev/RhinoOnLinux/src/
  RhinoInside.BoxSample/
  RhinoInside.BoxSolver/
  RhinoInside.MinimalHost/
  RhinoInside.AspNetHost/
```

I reviewed those sources, their copies in this solution, and the related server sources under `/home/dev/RhinoOnLinux/vendor/compute.rhino3d/src`. The Compute checkout reports commit `b49f16836f44d75b0dba583dede53076736f5359`; `src/compute.geometry/Startup.cs` also has an existing local diagnostic modification. Its checkpoint code must not be mistaken for a required upstream migration step.

Unless an absolute path is given, file references below are relative to the directory containing this document. The original `src` tree and the moved solution copies are **not identical**: see the `.3dm` workaround in section 7.

`RhinoWindowsTest` is only a `net8.0` “Hello, World!” application, with no Rhino dependencies. The meaningful Windows/Linux comparison is therefore the conditional configuration in `RhinoLinxTest` and `RhinoLib`, compared with the dedicated `RhinoInside.*` samples. [S1, S2]

## 2. Required differences at a glance

| Area | Existing Windows branch | Linux recipe supported by these samples |
| --- | --- | --- |
| Host framework | `net8.0-windows` in `RhinoLinxTest` | `net10.0`, without a Windows platform suffix |
| Rhino.Inside | `8.0.7-beta` | **`9.0.26084.13070-beta`** |
| RhinoCommon compile package | `8.25.25314.11001` in `RhinoLib` | **`9.0.25350.305-wip`** |
| RhinoCommon at runtime | Installed Windows Rhino runtime | Installed Linux Rhino runtime; do not deploy the NuGet compile copy as the runtime |
| Architecture | Existing worker sets x64 | Keep x64 for this baseline; the solver explicitly rejects other process architectures |
| Native/runtime location | Windows installation discovery; worker reads the registry | `/usr/lib/rhino3d`, optionally overridden by `RHINO_SYSTEM_DIR` |
| Native library checked by sample | Windows runtime is external to the project | Linux preflight requires `libRhinoLibrary.so` alongside `RhinoCommon.dll` |
| Startup | Existing worker already uses `WindowStyle.NoWindow` | Keep headless startup, resolver-first ordering, and scoped RhinoCore disposal |
| Windows registry / PATH | Windows registry call and semicolon-separated `path` mutation | Remove from Linux startup; use the resolver and selected Linux installation directory |
| Licensing preflight | No token check in the old worker | Require a nonempty `RHINO_TOKEN` for this sample workflow; presence is not proof of entitlement |
| UI dependencies | Windows-only references may exist in a normal application | Isolate or remove them from the headless Linux host; review plug-ins independently |
| Platform selection | Current projects inspect the OS running MSBuild | Prefer explicit target selection if cross-building/publishing is needed |

**These are pinned repository versions, not a claim about the newest available packages or a universal minimum version.** The migration baseline changes both OS and Rhino major version; it is not merely a Rhino 8 Windows build with a Linux RID. Sources: `RhinoLinxTest/RhinoLinxTest.csproj:12–29`, `RhinoLib/RhinoLib.csproj:9–30`, and `RhinoInside.BoxSolver/BoxSolver.cs:357–392`. [S1, S3]

## 3. Which RhinoCommon version, and why?

Use this pair to reproduce the reviewed samples:

```text
Rhino.Inside  9.0.26084.13070-beta
RhinoCommon   9.0.25350.305-wip   (compile reference only)
```

The cached Rhino.Inside package's own `.nuspec` declares a RhinoCommon dependency of `9.0.25350.305-wip` for its `net10.0` asset group, excluding runtime assets. It also provides non-Windows and Windows-specific .NET asset groups. The Windows-specific groups declare a WPF framework dependency, while the plain `net10.0` group does not. This is an additional reason to select the plain Linux TFM rather than retain a Windows TFM. [S9]

There are **three different versions to record**, not one:

1. The Rhino.Inside resolver NuGet version.
2. The RhinoCommon package used to compile the application.
3. The installed Rhino runtime used to execute native geometry.

The September 14, 2026 Ubuntu VM report records installed `rhino3d` package version `9.0.26257.7309` with the compile package above. The installed runtime version therefore need not numerically equal the compile package version. That report establishes a historical tested combination, not compatibility with every newer runtime. Pin and validate upgrades separately. [S11]

The cached RhinoCommon package has a `lib/net8.0` compile asset. Consuming that asset from a `net10.0` application does **not** mean the host should be retargeted to `net8.0`; the host's Rhino.Inside/runtime baseline is a separate choice. [S9]

### Compile-only references

Use:

```xml
<PackageReference Include="RhinoCommon"
                  Version="9.0.25350.305-wip"
                  ExcludeAssets="runtime"
                  PrivateAssets="all" />
```

- `ExcludeAssets="runtime"` excludes this package's runtime assets. The installed Rhino directory supplies the actual runtime through the resolver.
- `PrivateAssets="all"` prevents the package dependency from flowing transitively to consumers. It does **not**, by itself, mean “compile-only.”
- A consuming project that directly uses RhinoCommon types should declare its own compile dependency rather than assume a private library dependency will flow through.
- These settings are not blanket exclusions of every native/build/transitive asset in every dependency. Audit the dependency graph and output when eventually building or publishing.
- Do not “fix” resolution by copying a Windows `RhinoCommon.dll` or native DLLs into the Linux output directory.

The explicit `IncludeAssets` list in `RhinoLib.csproj` retains compile/build/native/content/analyzer/build-transitive assets and omits runtime. The dedicated BoxSolver project expresses the runtime exclusion without that extra list. [S1, S2, S9]

## 4. How to set up the `.csproj`

### Option A: one dedicated Linux host

For a small console application, this is a proposed baseline combining the package settings used by `RhinoInside.MinimalHost` with the explicit Linux compile symbol used by the old worker:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Only needed if the source contains #if LINUX. -->
    <DefineConstants>$(DefineConstants);LINUX</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Rhino.Inside"
                      Version="9.0.26084.13070-beta" />
    <PackageReference Include="RhinoCommon"
                      Version="9.0.25350.305-wip"
                      ExcludeAssets="runtime"
                      PrivateAssets="all" />
  </ItemGroup>
</Project>
```

This example intentionally describes a **Linux-specific project**. Its custom `LINUX` symbol remains defined even if the project is built on Windows; the symbol declares the project's intended target, not the machine currently running the compiler.

For this headless project, remove inherited `UseWPF`, `UseWindowsForms`, WindowsDesktop framework references, and Windows-specific assembly/native-file references unless they are deliberately isolated from the Linux build. The dedicated console samples do not require them. `RhinoInside.AspNetHost` uses `Microsoft.NET.Sdk.Web` because it hosts ASP.NET, not because Rhino.Inside requires a web host. [S2]

### Option B: host plus geometry library — the BoxSample pattern

This is the clearest separation for a larger existing application:

```text
Linux executable
  owns resolver initialization and the full RhinoCore lifetime
    -> geometry/solver library
       owns RhinoCommon geometry and file processing
```

The existing `RhinoInside.BoxSample.csproj` targets `net10.0`, sets x64, references Rhino.Inside, and references `../RhinoInside.BoxSolver/RhinoInside.BoxSolver.csproj`. The host receives its RhinoCommon compile API through its own Rhino.Inside dependency graph; the geometry library declares the private compile-only RhinoCommon reference. [S2, S9]

A corresponding geometry library project is:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <PlatformTarget>x64</PlatformTarget>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="RhinoCommon"
                      Version="9.0.25350.305-wip"
                      ExcludeAssets="runtime"
                      PrivateAssets="all" />
  </ItemGroup>
</Project>
```

The separate assembly is an organizational boundary, not a substitute for correct initialization: avoid touching Rhino types in startup/static initializers before the resolver is installed.

### Architecture, RID, SDK, and deployment

- `PlatformTarget=x64` and `RuntimeIdentifier=linux-x64` are different settings. The former selects process architecture; the latter identifies a platform-specific deployment target. [S1, S2]
- None of the solution's sample project files declares a RID or `SelfContained`. If a fixed Linux executable is wanted, explicitly choose a `linux-x64` RID and decide whether .NET itself is framework-dependent or self-contained. Do not infer those choices from `PlatformTarget`. [S2]
- A self-contained .NET publish does **not** install Rhino or license it. The samples still resolve an external Rhino installation. [S2, S3]
- Use a .NET 10 SDK for this baseline. The original `/home/dev/RhinoOnLinux/global.json` requests `10.0.102` with `rollForward: latestPatch`; the historical retest used SDK `10.0.111`. The moved solution has no corresponding `global.json` in its root. Decide whether to add a deliberate SDK pin to the migrated repository. [S10, S11]
- The ASP.NET diagnostic's `RuntimeFrameworkVersion=10.0.0` is a setting in that particular project, not a universal requirement for a Rhino.Inside console host. [S2]
- `RhinoLinxTest.sln` does not currently include the four `RhinoInside.*` sample projects. A solution build would therefore not validate those samples unless they were added or selected separately. [S1]

## 5. Resolver-first, headless startup

Preserve this order:

1. Parse settings and perform non-Rhino preflight.
2. Initialize `RhinoInside.Resolver` with the selected installation directory.
3. Enter a separate, non-inlined method that constructs RhinoCore.
4. Run geometry while RhinoCore is alive.
5. Dispose geometry and RhinoCore before exit.

`RhinoInside.MinimalHost/Program.cs:9–27` explicitly documents why `Main` avoids RhinoCommon references: the resolver must be installed before JIT compilation binds Rhino types. `RhinoInside.BoxSample/Program.cs:13–54` similarly separates resolver initialization and core creation using `NoInlining` methods. [S3, S4]

The following is a **proposed Linux bootstrap**, not code that was run in this review. It pairs with option A above:

```csharp
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rhino.Geometry;
using Rhino.Runtime.InProcess;

internal static class Program
{
    public static int Main()
    {
        if (!OperatingSystem.IsLinux() ||
            RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Linux x64 is required.");

        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("RHINO_TOKEN")))
            throw new InvalidOperationException("RHINO_TOKEN is required.");

        var systemDirectory =
            Environment.GetEnvironmentVariable("RHINO_SYSTEM_DIR");
        if (string.IsNullOrWhiteSpace(systemDirectory))
            systemDirectory = "/usr/lib/rhino3d";

        RhinoInside.Resolver.Initialize(systemDirectory);
        return StartRhinoAndRunGeometry();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartRhinoAndRunGeometry()
    {
        using var core = new RhinoCore(null, WindowStyle.NoWindow);
        using var brep = Brep.CreateFromBox(
            new BoundingBox(0, 0, 0, 10, 20, 30));

        return brep is not null && brep.IsValid && brep.IsSolid ? 0 : 4;
    }
}
```

For production-oriented preflight, also check the selected directory, `RhinoCommon.dll`, `libRhinoLibrary.so`, and the bootstrap assembly compatible with the chosen .NET runtime. The current BoxSolver only checks that **some** `dotnetstart.*.dll` exists; that is not proof the correct bootstrap is present. [S3]

Keep RhinoCore ownership in the executable. A geometry library should not silently create a second core or dispose the host's core. The synchronous BoxSample callback is a useful lifecycle pattern. Do not treat it as proof that arbitrary concurrent web requests, thread-pool tasks, or UI-dependent calls are safe; the inspected samples do not implement a general Rhino thread-dispatch strategy. [S3, S4]

There is no active `[STAThread]` attribute in these Linux hosts. The old worker contains only a commented hint. Adding STA is not the missing Linux migration step demonstrated by these samples. [S1, S4]

### Existing worker correction

`RhinoLinxTest/Program.cs:148` discovers a Windows installation path, but line 154 ignores it and initializes the resolver with `RHINO_SYSTEM_DIR` or `/usr/lib/rhino3d` **on every compiled platform**. Its Windows PATH adjustment occurs afterward.

For a Linux-only port, remove the registry/PATH code and consistently use the configured Linux path. For a dual-platform host, honor an explicit directory first; otherwise select Linux's default on Linux and Windows installation discovery on Windows. The related Compute `Program.cs:55–58` illustrates parameterless resolver discovery when no explicit system directory was supplied. Do not keep discovering a Windows directory that is never passed to the resolver. [S1, S5]

## 6. Are conditional-compilation measures already present?

**Yes in the old worker and Compute server. No platform `#if` measures in the four dedicated RhinoInside samples.** The dedicated BoxSolver instead enforces Linux/x64 at runtime. These mechanisms are different:

| Mechanism | Evaluated when | Meaning |
| --- | --- | --- |
| `<PropertyGroup Condition="...">` / conditional package group | MSBuild project evaluation | Selects TFMs, packages, properties, or items |
| `DefineConstants` plus `#if LINUX` | C# compilation | Includes/excludes source from the resulting assembly |
| `OperatingSystem.IsLinux()` / `RuntimeInformation.IsOSPlatform(...)` | Application runtime | Chooses behavior in already-compiled code |

### Where the measures are

| File / source location | Existing measure | Migration significance |
| --- | --- | --- |
| `RhinoLinxTest/RhinoLinxTest.csproj:12–29` | Host-OS conditional TFM/package groups; defines `LINUX` on a Linux build machine | Already distinguishes Windows Rhino 8 from Linux Rhino 9, but is host-selected |
| `RhinoLib/RhinoLib.csproj:9–30` | Same pattern for library TFM and RhinoCommon version | Host and library must select the same migration target |
| `RhinoLinxTest/Program.cs:145–163` | `#if LINUX` suppresses registry lookup and Windows PATH manipulation | Actual platform source exclusion exists, but the Linux branches are empty |
| `RhinoLinxTest/Program.cs:116–126` | `#if RELEASE` selects file/warning versus console/debug logging | Configuration behavior, not an OS measure |
| `RhinoInside.BoxSolver/BoxSolver.cs:357–392` | Runtime Linux/x64 checks, token and installation checks | These do not create or require a `LINUX` compile symbol |
| Original `/home/dev/RhinoOnLinux/src/RhinoInside.*` | No platform preprocessor branches; fixed `net10.0`/x64 projects | Dedicated Linux samples rather than full Windows/Linux shared hosts |
| Compute `src/compute.geometry/compute.geometry.csproj:17–28,60–66` | Windows desktop dependencies versus Linux TFM/RIDs/packages; defines `LINUX` | Useful server precedent, not a project file to copy wholesale |
| Compute `src/compute.geometry/Startup.cs:183–192,215–226` | `#if LINUX` explicitly loads RhinoCode and Grasshopper `.rhp` files from installation paths | Relevant only if the migrated application needs those plug-ins |
| Compute `src/rhino.compute/ComputeChildren.cs:272–275` | Runtime Windows check before adding `.exe` | Example of portable executable naming without a platform compile branch |

The Compute paths in this table are relative to `/home/dev/RhinoOnLinux/vendor/compute.rhino3d`. [S1, S3, S5, S6]

### Fix or account for these gotchas

**1. `LINUX` is custom, not automatically created by running the compiler on Linux.**

The checked-in worker explicitly defines it. A plain `net10.0` TFM does not supply that symbol, and selecting a Linux RID is not itself a C# `LINUX` definition. SDK-generated platform symbols such as `WINDOWS` follow platform-specific TFMs. This distinction is visible in the installed SDK's `GenerateTargetPlatformDefineConstants` implementation. [S10]

**2. Append your custom constants.**

The old worker/library use `<DefineConstants>LINUX</DefineConstants>`. Prefer:

```xml
<DefineConstants>$(DefineConstants);LINUX</DefineConstants>
```

This preserves existing explicitly configured constants instead of replacing them. SDK-generated framework constants are added separately; do not assume overwriting this property is harmless simply because some implicit constants still appear. [S1, S10]

**3. Host OS is not target OS.**

`$([MSBuild]::IsOSPlatform(linux))` asks about the system evaluating the project. Publishing with a Linux RID from Windows would still select the current Windows TFM/package groups.

For an intentional dual-platform project, use an explicit property, for example `RhinoTarget=Linux` or `RhinoTarget=Windows`, and condition TFMs, package versions, and symbols on it. Put common selection logic in a shared props file, or use separate Windows/Linux host projects. Propagate the choice to every referenced Rhino-dependent project. For example, the following **selection excerpt** replaces host-OS groups; matching package groups must use the same conditions:

```xml
<PropertyGroup Condition="'$(RhinoTarget)' == 'Linux'">
  <TargetFramework>net10.0</TargetFramework>
  <DefineConstants>$(DefineConstants);LINUX</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition="'$(RhinoTarget)' == 'Windows'">
  <TargetFramework>net8.0-windows</TargetFramework>
</PropertyGroup>
```

Require the property or define a deliberate default, and reject unsupported values. The Windows TFM above preserves this repository's Rhino 8 branch; it is not a prescription for all Windows Rhino applications. [S1]

**4. `#else` does not mean Windows.**

The old worker's `#else` branches also apply to any build without `LINUX`. For code that genuinely needs Windows APIs, use an explicit Windows branch, for example `#elif WINDOWS` when compiling against a Windows-specific TFM, with an unsupported-platform fallback. Runtime OS checks are often enough for directory selection and executable suffixes; compile-time guards remain useful for unavailable types/references. [S1, S6, S10]

**5. A `Release` configuration does not automatically define `RELEASE`.**

The checked-in worker lists a `Release` configuration but does not define that symbol. Unless supplied externally, the `#if RELEASE` logging branch is not selected. If that branch is intended, add it explicitly:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>$(DefineConstants);RELEASE</DefineConstants>
</PropertyGroup>
```

Alternatively, restructure logging around deliberately defined debug/configuration behavior. Do not treat the misspelled custom configuration `DebuLinux` as an OS target selector; no corresponding Linux-specific property group is tied to that configuration. [S1, S10]

## 7. Source changes beyond the project file

### Preserve the newer `.3dm` read-back workaround

The original source uses `readBack.Objects.First()` at `/home/dev/RhinoOnLinux/src/RhinoInside.BoxSolver/BoxSolver.cs:217`. The canonical solution copy instead:

1. Keeps the GUID returned when the Brep is added to the file.
2. Reopens the written file with a diagnostic read log.
3. Retrieves the object with `readBack.Objects.FindId(objectId)`.
4. Checks null geometry, type, validity, and solidity before publishing the output file.

Its comment at `RhinoInside.BoxSolver/BoxSolver.cs:255–257` documents a Linux object-table enumeration issue: a correct `Count` can coexist with failure to materialize an enumerated entry. Preserve the GUID-based lookup when consolidating the source trees. This is a **sample-specific workaround documented in code**, not a new reproduction or proof that every Linux Rhino version has this defect. [S3, S8]

The sample writes `.3dm` format version **8** while hosting Rhino **9** (`BoxSolver.cs:215`). File format version and runtime major version are separate choices; do not change the file format solely because the host moved to Rhino 9. [S3]

### Remove or isolate Windows-specific integration

Audit registry calls, drive-letter paths, hand-built backslashes, semicolon-separated PATH handling, Win32 P/Invoke, COM/desktop integrations, UI controls, `.exe` assumptions, and plug-ins with Windows-only native dependencies. Prefer `Path.Combine`/`Path.Join` and explicitly configured writable output directories, following the solver's file-handling approach. [S1, S3, S6, S7]

The Compute source contains Linux server adaptations, but its **Hops desktop plug-in is a separate boundary**: `src/hops/Hops.csproj` targets `net48` with WinForms, and its settings control imports `user32.dll`. `src/HopsNetCore/HopsNetCore.csproj` targets `net9.0-windows`. Do not interpret Linux support in the server as proof that these UI projects can be carried over unchanged. [S7]

The presence of `System.Drawing.Common` in a package graph or a Linux-conditioned Compute reference is likewise not proof that every rendering, printing, UI, or export operation is available. The reviewed box workflow demonstrates geometry/file calls only. Add such features to the migration checklist explicitly rather than infer support from compilation. [S3, S5, S9]

## 8. Runtime and licensing requirements are still external

The box workflow requires a nonempty `RHINO_TOKEN` before startup, never prints its value, and checks the installed Rhino files. Its token check only establishes that a value reaches the process; it does not activate or validate a license. [S3]

MinimalHost and AspNetHost clear `RHINO_TOKEN` from the process environment **after** constructing RhinoCore. BoxSample does not. Treat that as an explicit lifecycle/security choice demonstrated by particular diagnostics, not a universal fix for licensing. Never clear the token before startup, commit it, include it in command-line examples, or log it. [S3, S4]

Keep the following historical findings separate from the source migration recipe:

| Prior recorded finding | Consequence for migration |
| --- | --- |
| September 14, 2026: Ubuntu VM completed native geometry and the full box `.3dm` round trip | Evidence that the pinned workflow has worked on a Linux VM; not a new verification in this review |
| Same investigation: WSL2 reached RhinoCore but failed at the first licensed native geometry call | Compilation and core construction alone are insufficient acceptance criteria |
| September 15, 2026: successful VM run used existing per-user licensing state, unlike failing WSL run | The comparison does not establish a WSL-specific defect or clean token-only startup |
| VM's Snap-managed .NET host loaded an incompatible C++ runtime and failed with missing `GLIBCXX_3.4.32` | Native-loader failures need a compatible host/system-library setup; they are not licensing failures |

These findings are from existing reports, not tests run for this document. The earlier plan classifies the Linux work as prototype/WIP; this review does not certify a current production support policy. Do not copy license caches/keypairs between machines as a migration step. [S11]

## 9. Suggested change list for this repository

| Location | Suggested action |
| --- | --- |
| `RhinoLinxTest/RhinoLinxTest.csproj` | Keep Linux `net10.0`/Rhino.Inside 9; append constants; make target selection explicit if cross-publishing; intentionally define `RELEASE` if used |
| `RhinoLib/RhinoLib.csproj` | Keep Linux RhinoCommon 9 compile-only; apply the same target-selection policy as the host |
| `RhinoLinxTest/Program.cs` | Remove registry/PATH setup for a Linux-only port, or guard Windows explicitly; fix the unconditional Linux resolver fallback; isolate Rhino startup in a non-inlined method; use a scoped core lifecycle |
| Migrated host preflight | Check Linux/x64, installed runtime contents, writable output, and token presence without exposing it; do not equate file/token presence with compatibility/licensing |
| Migrated geometry layer | Keep headless geometry separate from UI and startup; preserve explicit disposal and newer GUID-based `.3dm` read-back |
| Solution organization | Include the migrated sample/host projects in the intended build entry point; the existing solution omits `RhinoInside.*` |
| Plug-ins/UI/native integration | Inventory individually; use the server's Linux `.rhp` loading paths only where those plug-ins are actually needed |

**No source or project edits in this table were applied.** This review adds only this Markdown recipe.

## 10. Acceptance checklist for a later execution phase

These checks were not performed; they are criteria for whoever implements and validates the port:

- [ ] Host and all Rhino-dependent libraries select consistent Linux TFMs, Rhino package versions, and target properties.
- [ ] Deployment has the intended Linux/x64 architecture and RID strategy.
- [ ] Output does not accidentally ship a NuGet/Windows RhinoCommon runtime copy in place of the installed Linux runtime.
- [ ] Resolver initialization precedes JIT binding of RhinoCore/geometry code.
- [ ] RhinoCore starts headlessly and is disposed after the workflow.
- [ ] A real licensed native geometry operation succeeds; token presence and startup logs alone do not count.
- [ ] Box is valid/solid, dimensions are 10 × 20 × 30, and volume is 6000.
- [ ] `.3dm` is written, reopened, and its GUID-selected geometry validates.
- [ ] Each required third-party plug-in, native dependency, exporter, and UI-dependent feature is evaluated separately.
- [ ] Windows and Linux branches are checked independently if retaining a dual-platform product.
- [ ] Licensing and native-loader failures are diagnosed separately, with secrets excluded from logs.

## Source index

All conclusions above are grounded in local project files, source, cached package metadata, or the explicitly dated existing reports. Live upstream resolver source and current package/support status were not independently retrieved; there is no “latest version” claim.

- **[S1] Existing worker/library and solution:** `RhinoLinxTest/RhinoLinxTest.csproj:3–41`; `RhinoLib/RhinoLib.csproj:3–30`; `RhinoLinxTest/Program.cs:20–55,92–105,112–174,185–199`; `RhinoLinxTest.sln:6–13`.
- **[S2] Dedicated sample project files and placeholder:** `RhinoInside.BoxSample/RhinoInside.BoxSample.csproj:1–14`; `RhinoInside.BoxSolver/RhinoInside.BoxSolver.csproj:1–13`; `RhinoInside.MinimalHost/RhinoInside.MinimalHost.csproj:1–15`; `RhinoInside.AspNetHost/RhinoInside.AspNetHost.csproj:1–16`; `RhinoWindowsTest/RhinoWindowsTest.csproj:1–10`; `RhinoWindowsTest/Program.cs:1–2`.
- **[S3] Canonical solver:** `RhinoInside.BoxSolver/BoxSolver.cs:18–85,98–165,182–310,357–419,450–484`.
- **[S4] Canonical host lifecycle:** `RhinoInside.BoxSample/Program.cs:7–54`; `RhinoInside.MinimalHost/Program.cs:7–62`; `RhinoInside.AspNetHost/Program.cs:9–30,56–117`.
- **[S5] Compute server:** `/home/dev/RhinoOnLinux/vendor/compute.rhino3d/src/compute.geometry/compute.geometry.csproj:1–66`; its `Program.cs:55–58`; its locally modified `Startup.cs:140–226`.
- **[S6] Compute executable discovery:** `/home/dev/RhinoOnLinux/vendor/compute.rhino3d/src/rhino.compute/ComputeChildren.cs:260–280`.
- **[S7] UI/plugin boundary:** `/home/dev/RhinoOnLinux/vendor/compute.rhino3d/src/hops/Hops.csproj:3–10`; its `HopsAppSettingsUserControl.cs:35–36,64–79`; `/home/dev/RhinoOnLinux/vendor/compute.rhino3d/src/HopsNetCore/HopsNetCore.csproj:3–7`.
- **[S8] Original sample source:** `/home/dev/RhinoOnLinux/src/RhinoInside.BoxSolver/BoxSolver.cs:210–224,249–284`; the four original sample `.csproj` files and host `Program.cs` files under that `src` tree.
- **[S9] Cached McNeel package metadata/build assets:** `/home/dev/.nuget/packages/rhino.inside/9.0.26084.13070-beta/rhino.inside.nuspec:16–46`; `/home/dev/.nuget/packages/rhinocommon/9.0.25350.305-wip/rhinocommon.nuspec`; its `lib/net8.0` and `build/net8.0/RhinoCommon.targets` / `buildTransitive/net8.0/RhinoCommon.props` / `.targets` assets. Rhino.Inside metadata identifies source commit `23865e47e652f2c24cdc81ef66b2fb96f5f376a2`.
- **[S10] Local SDK configuration and primary SDK implementation:** `/home/dev/RhinoOnLinux/global.json`; `/home/dev/RhinoOnLinux/.dotnet/sdk/10.0.111/Sdks/Microsoft.NET.Sdk/targets/Microsoft.NET.Sdk.BeforeCommon.targets:188–220,290–296`; `Microsoft.NET.TargetFrameworkInference.targets:65–84`; `Microsoft.NET.Sdk.CSharp.props:21–22` in the same targets directory.
- **[S11] Historical findings, not freshly executed checks:** `../doc/ubuntu-vm-compatibility-retest-2026-09-14.md:9–39,62–95,122–129`; `../doc/vm-wsl-strace-exact-comparison-2026-09-15.md:32–55,72–93`; `../doc/rhino-inside-linux-prototype-plan.md:3–10`; `../PROGRESS.md:195–209`.
