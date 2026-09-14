# Rhino.Inside Linux: New Insights and Recommended Next Steps

**Updated:** August 28, 2026

## New insights established

### 1. Rhino.Compute executes real geometry successfully

Rhino.Compute is not merely starting and serving health/version endpoints. Actual geometry requests succeed on this host.

This establishes that the following are functional in the Compute context:

- the current `RHINO_TOKEN`;
- the Core-Hour Billing account configuration;
- the installed Rhino 9 Linux runtime;
- the native Rhino geometry engine;
- RhinoCommon geometry execution on Linux;
- a .NET 10 Linux process hosting Rhino.

Therefore, token regeneration or general billing troubleshooting is no longer the recommended first action. The investigation should focus on differences between the working `compute.geometry` host and a standalone Rhino.Inside host.

### 2. The standalone sample gets past RhinoCore startup

After correcting its Linux project configuration, the separate sample:

1. restores successfully;
2. builds successfully for `net10.0`;
3. initializes `RhinoInside.Resolver` using `/usr/lib/rhino3d`;
4. constructs `new RhinoCore(null, WindowStyle.NoWindow)` successfully;
5. reaches the class-library geometry call.

The process fails when it performs the first native/licensed RhinoCommon geometry operation:

```text
Rhino.Runtime.NotLicensedException
Process exit code: 134
```

This narrows the failure boundary to:

```text
resolver initialization       PASS
RhinoCore construction        PASS
first native geometry call    FAIL
```

### 3. Two standalone layouts fail at the same boundary

Both tested standalone layouts fail with the same exception:

- `RhinoLinxTest` plus `RhinoLib`;
- `RhinoInside.BoxSample` plus `RhinoInside.BoxSolver`.

This makes the specific circle-intersection or box-extrusion algorithm an unlikely cause. It also shows that simply separating RhinoCommon geometry into a class library does not resolve the problem.

### 4. Compute and the standalone sample use the exact same Rhino.Inside DLL

The installed Compute child uses:

```text
/usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

The standalone Release build uses its package copy of the same assembly.

Both files have this SHA-256 hash:

```text
ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7
```

Both are Rhino.Inside:

```text
9.0.26084.13070-beta
```

A different Rhino.Inside package binary is therefore not the explanation.

### 5. The strongest known difference is the .NET deployment model

The working `compute.geometry` process is a self-contained `linux-x64` application. Its installation includes its own runtime files:

```text
System.Private.CoreLib.dll
libcoreclr.so
libhostfxr.so
libhostpolicy.so
```

Its bundled CoreCLR reports .NET `10.0.0`.

The standalone sample is currently framework-dependent and runs using the workspace-local shared runtime:

```text
Microsoft.NETCore.App 10.0.11
```

The direct output does not contain its own CoreCLR, hostfxr, hostpolicy, or System.Private.CoreLib.

This self-contained-versus-framework-dependent difference was the highest-value variable to test; the completed self-contained test below shows it is not a sufficient explanation on its own.

### 6. Runtime RhinoCommon isolation is correct

The standalone output does not contain copied runtime versions of:

```text
RhinoCommon.dll
Grasshopper.dll
GH_IO.dll
Eto.dll
```

Rhino.Inside therefore resolves the installed runtime assembly from:

```text
/usr/lib/rhino3d/RhinoCommon.dll
```

An accidentally copied RhinoCommon runtime is not causing the failure.

### 7. Compute was not using a separate `/etc` token configuration

`/etc/rhino-compute/environment` is not present on this machine.

The manually launched Compute control inherited its environment from the token-bearing shell. This reduces the likelihood that Compute succeeds only because it uses a different token configured in `/etc`.

The exact token source should still be recorded during the next controlled comparison, without printing the token.

### 8. A host-context licensing difference remains possible

Since the same token, Rhino runtime, Rhino.Inside assembly, and RhinoCommon runtime work under `compute.geometry` but fail under an arbitrary standalone executable, the remaining possibilities include:

- self-contained versus framework-dependent .NET hosting;
- executable/application identity used by the Linux licensing path;
- additional initialization performed by Compute;
- headless document creation or plug-in initialization;
- working directory, parent process, or launch context;
- a standalone Rhino.Inside licensing limitation or defect in the current Linux WIP build.

### 9. Compute checkpoint A fails before Compute-specific initialization

On August 28, 2026, the official Compute `9.x` branch was cloned into a separate local checkout at commit `b49f16836f44d75b0dba583dede53076736f5359`. A diagnostic-only `Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 20, 30))` checkpoint was added directly after `new RhinoCore(null, WindowStyle.NoWindow)` in `compute.geometry`.

The experiment published both the local `rhino.compute` parent and `compute.geometry` child as self-contained `linux-x64` applications. The parent started exactly one local child with the normal `-childof` relationship, loopback ports `5056`/`6001`, and Grasshopper disabled. The local child reported its Compute/Rhino versions, then logged the start of checkpoint A and exited with `Rhino.Runtime.NotLicensedException`. It did **not** reach the checkpoint-success log.

On the same day, checkpoint B was run by moving only the same geometry call to immediately after Compute clears `RHINO_TOKEN`. It again logged the start of the checkpoint and exited with the same `Rhino.Runtime.NotLicensedException`, without reaching the checkpoint-success log.

Checkpoint C then moved only that call to immediately after Compute registers `HostUtils.OnExceptionReport`. It likewise logged the start of the checkpoint and exited with the same `Rhino.Runtime.NotLicensedException`, without reaching the checkpoint-success log.

Checkpoint D then moved only that call to after the complete Rhino commands plug-in load block. That plug-in logged `Error loading rhino commands plugin.`, after which the child logged the start of checkpoint D and exited with the same `Rhino.Runtime.NotLicensedException`. This proves the failed/attempted commands plug-in load block is not sufficient; it does not prove a successfully loaded commands plug-in is irrelevant.

Checkpoint E then moved only that call to after the complete RhinoCode plug-in load block. RhinoCode logged `Successfully loaded scripting plugin` and `Configured scripting plugin for compute`; the child then logged the start of checkpoint E and exited with the same `Rhino.Runtime.NotLicensedException`. Successful RhinoCode loading and its Compute-specific controller configuration are therefore not sufficient.

Checkpoint F then moved only that call to after the Grasshopper-disabled branch and Compute extension plug-in loader reflection block. The child logged `(3/4) Skipping grasshopper` and `(4/4) Loading compute plug-ins`, then logged the start of checkpoint F and exited with the same `Rhino.Runtime.NotLicensedException`. This proves that completing the extension-loader block is not sufficient. Because current upstream code does not log whether `GetMethod("LoadComputeExtensionPlugins")` returned a method, the run does not independently prove that an extension-loader method existed or loaded a plug-in.

Together, A through F span every direct `RhinoCoreStartup()` stage in the Grasshopper-disabled topology. The next boundary is ASP.NET host startup. The local child `Rhino.Inside.dll` SHA-256 matches the installed Compute child's exactly:

```text
ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7
```

**Conclusion:** Compute's direct `RhinoCoreStartup()` stages in the Grasshopper-disabled topology—token clearing, exception callback registration, commands plug-in attempted loading, successful RhinoCode loading/configuration, and the Compute extension-loader block—are each insufficient to permit the first native geometry operation. The next test must cross the remaining startup boundary by invoking the same checkpoint from `IHostApplicationLifetime.ApplicationStarted`, after the child ASP.NET host has started. The local checkout's `b49f168` source revision is not asserted to be source-identical to the installed WIP package build, so retain this checkout and launch topology for the post-host test and compare package provenance before drawing a package-versus-source conclusion.

### 10. An independent ASP.NET ApplicationStarted host also fails

On August 28, 2026, a new independent source-local diagnostic was added at `src/RhinoInside.AspNetHost`; it does not reference or modify the local Compute checkout. It uses `Microsoft.NET.Sdk.Web`, the same Rhino.Inside package (`9.0.26084.13070-beta`), a compile-only RhinoCommon reference, and a self-contained `linux-x64` publish with .NET `10.0.0` framework configuration. The published `Rhino.Inside.dll` SHA-256 is identical to the installed Compute child, and the output contains no copied `RhinoCommon.dll`.

The host performs resolver initialization, headless `RhinoCore` construction, Compute-style process-level `RHINO_TOKEN` clearing, and exception callback registration. It starts a loopback-only Kestrel host and registers the deterministic `Brep.CreateFromBox` checkpoint with `IHostApplicationLifetime.ApplicationStarted`. The test reached all of these markers:

```text
RhinoCore constructed
RHINO_TOKEN cleared
Now listening on: http://127.0.0.1:5057
Application started
ASP.NET host started; beginning deterministic native Brep.CreateFromBox operation
```

It then ended with `Rhino.Runtime.NotLicensedException` and exit code `134`. The callback's managed `try`/`catch` did not emit its catch marker before the process terminated.

**Conclusion:** generic self-contained ASP.NET host startup and the `ApplicationStarted` lifecycle are not sufficient by themselves. This is an independent control only: it omits Compute's startup/plugins and therefore does not replace the remaining exact post-host checkpoint inside the local `compute.geometry` child.

## Recommended next steps

### Step 1 result: self-contained Linux x64 did not solve the failure

On August 27, 2026, the current `RhinoInside.BoxSample` host was published and tested as a self-contained `linux-x64` application:

```bash
/home/dev/RhinoOnLinux/.dotnet/dotnet publish \
  src/RhinoInside.BoxSample/RhinoInside.BoxSample.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:UseAppHost=true \
  -o artifacts/self-contained-check
```

The publish succeeded after obtaining the Linux x64 runtime pack. Its directory contained the generated `RhinoInside.BoxSample` ELF apphost plus `System.Private.CoreLib.dll`, `libcoreclr.so`, `libhostfxr.so`, `libhostpolicy.so`, and `Rhino.Inside.dll`. It contained no copied `RhinoCommon.dll`; the published `Rhino.Inside.dll` had the same SHA-256 as the installed `compute.geometry` copy.

The ELF apphost was run directly from the token-bearing environment. Resolver initialization succeeded and reported `/usr/lib/rhino3d`, but the process still exited `134` with `Rhino.Runtime.NotLicensedException` before it created a `.3dm` artifact.

**Conclusion:** moving from framework-dependent hosting to a self-contained deployment is not a sufficient fix. It remains a meaningful negative result because it removes the largest deployment-model difference as the sole explanation. The test did not exactly match Compute's bundled CoreCLR version: the published host uses .NET `10.0.11`, while the installed Compute child bundles .NET `10.0.0`.

### Step 2 result: the minimal direct host also fails at first native geometry

An isolated project now exists at `src/RhinoInside.MinimalHost/`, with `scripts/run-minimal-host.sh` providing framework-dependent and self-contained launch modes. It does not reference the existing box sample or solver and contains no Serilog, JSON, registry, file-output, or custom exception-handling logic.

The minimal sequence is:

1. `Main` calls a non-inlined resolver initializer using `/usr/lib/rhino3d`.
2. A separate non-inlined method constructs `new RhinoCore(null, WindowStyle.NoWindow)`.
3. The process clears `RHINO_TOKEN`, matching Compute's post-start behavior.
4. A third non-inlined method performs only `Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 20, 30))` and checks that the returned Brep is a valid solid.

Keeping `Main` free of RhinoCommon type usage is necessary in this project: when `Main` directly contained `RhinoCore` usage, the framework-dependent loader attempted to bind the compile-only `RhinoCommon` assembly before the resolver ran. Moving all RhinoCommon-dependent code behind non-inlined methods lets the resolver install its assembly-resolution hooks first.

On August 27, 2026, both forms reached all of these checkpoints:

```text
resolver initialized
RhinoCore constructed
RHINO_TOKEN cleared from this process environment
starting Brep.CreateFromBox
```

Both then exited `134` with `Rhino.Runtime.NotLicensedException` at `Brep.CreateFromBox`. The self-contained publish contained the expected CoreCLR/host files, no copied `RhinoCommon.dll`, and a `Rhino.Inside.dll` with the same SHA-256 as the installed Compute child.

**Conclusion:** neither framework-dependent deployment, self-contained deployment, application-level dependencies, nor retaining `RHINO_TOKEN` after RhinoCore construction explains the failure. The next experiment must compare direct execution against the initialization happening inside Compute.

### Step 3 result: checkpoints A through F all fail

Checkpoints A through F were implemented and run on August 28, 2026 as described in the new insight above. All six throw `Rhino.Runtime.NotLicensedException` at the deterministic `Brep.CreateFromBox` call. The results at B and C show that neither Compute clearing `RHINO_TOKEN` after startup nor registering `HostUtils.OnExceptionReport` enables the first native RhinoCommon geometry operation. At D, Rhino's commands plug-in load reported an error; the subsequent geometry failure means the failed/attempted commands plug-in load block does not enable geometry, but this does not test a successful commands plug-in load. At E, RhinoCode loaded successfully and its controller was configured for Compute, yet the same geometry call failed. At F, the child skipped Grasshopper (matching the working packaged control) and completed the Compute extension-loader reflection block before the same geometry failure.

A separate independent ASP.NET host has now shown that generic `ApplicationStarted` timing is insufficient, but it is not Compute. The next controlled run remains an **exact local Compute post-ASP.NET-host checkpoint**: register a one-shot non-secret geometry checkpoint with the local child's `IHostApplicationLifetime.ApplicationStarted` event so it executes only after the child Kestrel server has started. This must be a separate, minimal diagnostic change; do not alter the preceding Step 3F initialization sequence.

If that exact local Compute checkpoint fails while packaged Compute HTTP geometry continues to succeed, collect safe runtime/process/assembly diagnostics and establish exact installed-package/source provenance before trying unrelated initialization changes.

Interpretation:

- **The local Compute post-host checkpoint succeeds:** the interaction of Compute's own startup sequence with the host lifecycle is the first demonstrated required context.
- **The local Compute post-host checkpoint fails:** the difference is outside all directly tested local source startup positions and generic ASP.NET lifecycle timing; prioritize exact package provenance, executable/working-directory/parent context, and a safe working-versus-failing process comparison.

### Step 4: capture safe diagnostics from both processes

Add a diagnostic mode to both the standalone test and a local Compute build.

Record only non-secret values:

```text
Environment.Version
Environment.ProcessPath
Environment.CurrentDirectory
Environment.UserName
HOME
process architecture
entry assembly name and location
Rhino.Inside assembly path, version, and hash
RhinoCommon assembly path and version
resolved Rhino system directory
RHINO_TOKEN present: true/false
token length or short SHA-256 fingerprint only
```

Inspect `/proc/self/maps` for:

```text
libRhinoLibrary.so
libcoreclr.so
libhostfxr.so
RhinoCommon.dll
```

Do not print `/proc/self/environ` without redaction because it can expose `RHINO_TOKEN`.

Create a comparison table for the working and failing processes:

| Property | Working `compute.geometry` | Standalone sample |
| --- | --- | --- |
| Executable path | installed Compute apphost | sample apphost or shared `dotnet` |
| Deployment | self-contained | currently framework-dependent |
| CoreCLR | bundled 10.0.0 | shared 10.0.11 |
| Rhino.Inside hash | identical | identical |
| RhinoCommon path | `/usr/lib/rhino3d/RhinoCommon.dll` | same expected path |
| User | capture at runtime | currently `dev` |
| Working directory | capture at runtime | sample repository |
| Parent process | `rhino.compute` | shell/timeout/dotnet |
| Geometry result | succeeds | `NotLicensedException` |

### Step 5: test Compute-specific initialization differences individually

If a minimal self-contained host still fails, test these one at a time:

1. Clear `RHINO_TOKEN` immediately after RhinoCore startup.
2. Set:

   ```text
   RHINO_COMPUTE_CREATE_HEADLESS_DOC=true
   ```

3. Create a headless Rhino document before the first geometry operation.
4. Register `Rhino.Runtime.HostUtils.OnExceptionReport` before geometry.
5. Load the same RhinoCode plug-in used by Compute.
6. Add a short controlled delay after RhinoCore construction.
7. Run from `/usr/lib/rhino-compute` as the working directory.
8. Run under the same user and parent-launch pattern as the working child.

Do not apply all changes together; changing one variable at a time keeps the result interpretable.

### Step 6: eliminate compile/runtime RhinoCommon skew

The class library currently compiles against:

```text
RhinoCommon 9.0.25350.305-wip
```

At runtime it loads the installed assembly:

```text
RhinoCommon 9.0.26238.01000
```

This is not the leading theory because Compute was built against a similar package baseline, but it can be eliminated by temporarily compiling against the installed assembly:

```xml
<ItemGroup Condition="$([MSBuild]::IsOSPlatform(linux))">
  <Reference Include="RhinoCommon">
    <HintPath>/usr/lib/rhino3d/RhinoCommon.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

Treat this as a diagnostic experiment rather than the final package strategy.

### Step 7: ask McNeel about standalone Linux licensing behavior

If all of the following are true:

- Compute geometry succeeds;
- the minimal direct host is self-contained;
- it uses the same Rhino.Inside DLL;
- it loads the same installed RhinoCommon DLL;
- it follows the same immediate startup sequence;
- it still throws `NotLicensedException`;

then prepare a minimal reproduction for McNeel.

The central question should be:

> Does the current Rhino 9 Linux Core-Hour Billing implementation support arbitrary standalone Rhino.Inside hosts, or is there additional licensing initialization or host context supplied specifically by `compute.geometry`?

Include:

```text
Ubuntu 24.04 under WSL2
rhino3d 9.0.26238.7039
rhino-compute 9.0.26159.12510-wip
.NET runtime and deployment model
Rhino.Inside 9.0.26084.13070-beta
identical Rhino.Inside hashes
loaded RhinoCommon path/version
minimal source code
NotLicensedException stack trace
confirmation that Compute geometry succeeds
```

Never include the token or an unredacted environment dump.

## Practical fallback

If the objective is to deliver Linux geometry processing rather than specifically prove in-process Rhino.Inside, use the already-working Rhino.Compute server through loopback HTTP.

This is currently the lowest-risk path because actual Compute geometry is already confirmed to work. Continue the standalone investigation separately without blocking application development.

## Recommended order

```text
1. [complete] Self-contained linux-x64 standalone publish — still fails with NotLicensedException
2. [complete] Minimal exact Compute-style startup host — fails at the same native geometry boundary
3. [partially complete] Local Compute checkpoints A through F — all fail through the full direct Grasshopper-disabled RhinoCoreStartup sequence
4. [complete independent control] Source-local standalone ASP.NET ApplicationStarted host — also fails after Kestrel starts
5. [next] Exact local Compute post-ASP.NET-host checkpoint through ApplicationStarted
6. Safe process/runtime/assembly comparison
7. Individual Compute initialization experiments
8. Compile against installed RhinoCommon
9. Minimal reproduction and licensing question for McNeel
```

Do not spend additional time changing the geometry algorithm until these host and licensing-context differences have been tested.
