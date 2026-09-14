# Prototype Progress

_Last updated: August 28, 2026_

## Objective

Create a minimal Linux console prototype that starts Rhino 9 through Rhino.Inside, extrudes a capped box from a closed polyline, validates the result, and writes a `.3dm` artifact.

## Current status

**Technical groundwork, build validation, direct Rhino.Inside attempts (framework-dependent and self-contained), and a Rhino.Compute control test are complete. Direct geometry validation remains blocked because the standalone Rhino.Inside process throws `Rhino.Runtime.NotLicensedException` on its first native/licensed RhinoCommon geometry operation.**

The project has been restored and built successfully with a workspace-local .NET SDK. The runner securely loads `RHINO_TOKEN` from ignored `.env.local`, and Rhino is installed. The direct console app still stops with `Rhino.Runtime.NotLicensedException` before geometry or `.3dm` output. In contrast, a loopback-only Rhino.Compute control successfully started one `compute.geometry` child, and both the parent and child served health and version endpoints before a clean shutdown.

On August 27, 2026, the direct sample was deliberately split at the Rhino boundary to test assembly-load isolation. `RhinoInside.BoxSample` now owns `RhinoInside.Resolver.Initialize(...)`, `RhinoCore` construction, and `RhinoCore` disposal. The new `RhinoInside.BoxSolver` class library owns command handling, preflight, RhinoCommon geometry, `.3dm` validation, and reporting. The solver compiles against RhinoCommon with runtime assets excluded, so the installed Rhino runtime remains the only runtime RhinoCommon source. The split build and non-starting paths pass. A subsequent self-contained `linux-x64` publish started through its generated ELF apphost but failed with the same `NotLicensedException` before output, so self-contained hosting alone did not resolve the issue. The isolated Step 2 minimal host then confirmed the same failure in both framework-dependent and self-contained forms after resolver initialization and `RhinoCore` construction, even after clearing `RHINO_TOKEN` in-process. On August 28, 2026, Step 3 checkpoints A through F were run in a separate local checkout of Compute `9.x` through a self-contained local `rhino.compute` parent with one local `compute.geometry` child. Checkpoint A, immediately after `RhinoCore` construction; checkpoint B, immediately after Compute clears `RHINO_TOKEN`; checkpoint C, immediately after Compute registers `HostUtils.OnExceptionReport`; checkpoint D, immediately after the Rhino commands plug-in load block; checkpoint E, immediately after the RhinoCode plug-in load block; and checkpoint F, after the Grasshopper-disabled branch and Compute extension plug-in loader reflection block, all threw `Rhino.Runtime.NotLicensedException` at the same `Brep.CreateFromBox` call. The commands plug-in load reported an error in D, so D proves that completing its attempted-load block does not enable geometry; it does not prove a successfully loaded commands plug-in is irrelevant. In E, RhinoCode loaded successfully and its controller was configured before geometry still failed. F completes every direct `RhinoCoreStartup()` stage used by the Grasshopper-disabled working control; it did not reach ASP.NET host startup. A new independent self-contained ASP.NET Core host at `src/RhinoInside.AspNetHost` then started Kestrel on loopback and ran the same geometry checkpoint from `ApplicationStarted`; it also ended with `Rhino.Runtime.NotLicensedException` (exit `134`). This proves generic ASP.NET host startup is not sufficient, but it does not replace the remaining exact local Compute-child post-host test.

## Completed

- [x] Reviewed the Rhino.Inside, Rhino.Compute, developer-sample, and Linux setup references.
- [x] Documented the feasibility assessment and implementation plan in `doc/rhino-inside-linux-prototype-plan.md`.
- [x] Added `global.json` pinning .NET SDK `10.0.102`.
- [x] Added the `net10.0` x64 project at `src/RhinoInside.BoxSample/`.
- [x] Pinned `Rhino.Inside` to `9.0.26084.13070-beta`.
- [x] Implemented safe initialization ordering: preflight, resolver initialization, then headless `RhinoCore`.
- [x] Implemented the box geometry workflow:
  - closed `10 × 20` rectangular polyline;
  - capped extrusion with height `30`;
  - valid-solid, bounding-box, and expected-volume (`6000`) checks.
- [x] Implemented `.3dm` write and read-back validation.
- [x] Added `scripts/check-rhino-linux.sh` for non-starting prerequisite checks.
- [x] Added `scripts/run-box-sample.sh` for the future deliberate execution path.
- [x] Added `.env.example` and `.gitignore` so tokens and generated artifacts are not committed.
- [x] Ran static checks:
  - `bash -n` passed for both shell scripts;
  - `global.json` parses correctly;
  - source checks confirm required startup, geometry, and output-validation paths are present.
- [x] Installed workspace-local .NET SDK `10.0.111` in `.dotnet/`; it satisfies the pinned `10.0.102` SDK through patch roll-forward.
- [x] Restored the pinned NuGet dependencies.
- [x] Built `RhinoInside.BoxSample` in Release configuration with 0 warnings and 0 errors.
- [x] Ran the application `--help` path; it did not initialize Rhino.
- [x] Ran `scripts/check-rhino-linux.sh`; it correctly failed with exit code `2` without starting Rhino because the runtime prerequisite is unavailable.
- [x] Verified `.env.local` has mode `0600` and contains a non-empty token assignment without inspecting or printing its value.
- [x] Verified `scripts/run-box-sample.sh` loads `.env.local` and reaches the Rhino-directory preflight check.
- [x] Installed `rhino3d` `9.0.26238.7039` and `rhino-compute` `9.0.26159.12510-wip`.
- [x] Confirmed `/usr/lib/rhino3d` contains `libRhinoLibrary.so`, `RhinoCommon.dll`, `dotnetstart.9.dll`, and `dotnetstart.10.dll`.
- [x] Ran the direct headless Rhino.Inside prototype once. Preflight and resolver initialization passed.
- [x] Captured the startup result: the process exited `134` with `Rhino.Runtime.NotLicensedException` before RhinoCore completed startup; no geometry or `.3dm` artifact was produced.
- [x] Retried the direct headless startup on August 27, 2026 after the token update. The result was unchanged: preflight and resolver initialization passed, then RhinoCore exited `134` with `Rhino.Runtime.NotLicensedException`.
- [x] Retried again on August 27, 2026 after a further token update. The result remained unchanged: RhinoCore exited `134` with `Rhino.Runtime.NotLicensedException` before geometry execution.
- [x] Retried once more on August 27, 2026 after another token update. The result was again unchanged: preflight and resolver initialization passed, then RhinoCore exited `134` with `Rhino.Runtime.NotLicensedException` before geometry execution.
- [x] Added and ran a non-secret `--verify-token` mode on August 27, 2026. It proved that `RHINO_TOKEN` is present inside the actual .NET application process after the runner loads `.env.local`; the value was not displayed and Rhino was not initialized.
- [x] Ran the full sample again on August 27, 2026 after verifying in-process token propagation. The result was unchanged: resolver initialization passed and RhinoCore exited `134` with `Rhino.Runtime.NotLicensedException`; no geometry or `.3dm` artifact was produced.
- [x] Split the direct sample on August 27, 2026 into `RhinoInside.BoxSample` and `RhinoInside.BoxSolver` to isolate RhinoCommon geometry from the executable while retaining Rhino.Inside resolver and complete RhinoCore lifetime ownership in `RhinoInside.BoxSample`.
- [x] Configured `RhinoInside.BoxSolver` with a compile-only RhinoCommon reference (`ExcludeAssets="runtime"`, `PrivateAssets="all"`); neither Release output directory contains a NuGet-supplied `RhinoCommon.dll`.
- [x] Built the split project in Release configuration with 0 errors. The only two warnings were `NU1900` vulnerability-metadata lookup failures caused by the sandbox denying access to `api.nuget.org`; they do not affect restore or compilation.
- [x] Verified the split executable `--help` and runner `--verify-token` paths without initializing Rhino; the latter still proves that `RHINO_TOKEN` reaches the .NET process without printing its value.
- [x] Read McNeel’s official **Running and Debugging Compute Locally** guide on August 27, 2026. It documents the diagnostic endpoints `GET /version`, `GET /healthcheck`, `GET /activechildren`, and `GET /sdk` (shown at port `6500` in the guide; the local test used configured port `5055`).
- [x] Ran a loopback-only Rhino.Compute control test on August 27, 2026 with one requested child and Grasshopper disabled. The parent served `GET /healthcheck` as `Healthy` and `GET /version` as `{"rhino":"9.0.26238.1000","compute":"9.0.0.0","git_sha":null}`.
- [x] Verified the child state during that control: `GET /activechildren?initialize=false` returned `1`; the observed process tree contained one `rhino.compute` parent and one `compute.geometry` child on port `6001`; the child’s own `/healthcheck` returned `Healthy` and `/version` reported the same Rhino and Compute versions.
- [x] Confirmed from the child log that it reached `Now listening on: http://localhost:6001`; there was no licensing exception or server crash. Rhino command-plugin loading logged an error, but the scripting and Compute plugins loaded and the HTTP service stayed healthy. This is a successful Rhino.Compute/Rhino runtime control, not yet a successful direct Rhino.Inside run.
- [x] Stopped the temporary test cleanly using `POST /shutdown-children`, then interrupted the parent. Both parent and child endpoints became unreachable and no `rhino.compute` or `compute.geometry` process remained.
- [x] Published `RhinoInside.BoxSample` as a self-contained `linux-x64` executable on August 27, 2026. The publish used its generated ELF apphost and included `System.Private.CoreLib.dll`, `libcoreclr.so`, `libhostfxr.so`, `libhostpolicy.so`, and `Rhino.Inside.dll`; it did not include a copied `RhinoCommon.dll`. The runtime pack was downloaded during publish.
- [x] Verified the published `Rhino.Inside.dll` SHA-256 is identical to the installed Compute child (`ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7`).
- [x] Ran the self-contained ELF apphost directly from the token-bearing environment on August 27, 2026. Resolver initialization reported `/usr/lib/rhino3d`, then the process exited `134` with `Rhino.Runtime.NotLicensedException`; no `.3dm` artifact was produced. This rules out the broad self-contained-versus-framework-dependent deployment difference as a sufficient fix, although the published host uses .NET `10.0.11` while the installed Compute child bundles .NET `10.0.0`.
- [x] Created the isolated Step 2 project at `src/RhinoInside.MinimalHost/` and its runner at `scripts/run-minimal-host.sh`. The host has no project reference to the existing box sample or solver, uses the same pinned Rhino.Inside/RhinoCommon package versions, and performs only resolver initialization, headless `RhinoCore` construction, process-level token clearing, and one `Brep.CreateFromBox` validation.
- [x] Kept `Main` free of RhinoCommon usage and put resolver initialization, RhinoCore construction, and the geometry call in non-inlined methods. This prevents the framework-dependent loader from trying to bind RhinoCommon before `RhinoInside.Resolver` installs its resolution hooks.
- [x] Ran the Step 2 minimal host framework-dependent on August 27, 2026. It reached every checkpoint through `RhinoCore` construction and process-level `RHINO_TOKEN` clearing, then exited `134` with `Rhino.Runtime.NotLicensedException` at `Brep.CreateFromBox`.
- [x] Published and ran the same Step 2 minimal host as a self-contained `linux-x64` ELF apphost on August 27, 2026. It reached the same checkpoints and failed at the same `Brep.CreateFromBox` call with exit `134`. The output has the required self-contained runtime files, uses a `Rhino.Inside.dll` identical to Compute's, and does not copy `RhinoCommon.dll`.
- [x] Completed Step 3 checkpoint A on August 28, 2026 in a separate checkout of the official Compute `9.x` source at commit `b49f16836f44d75b0dba583dede53076736f5359`. The local self-contained `rhino.compute` parent started one local `compute.geometry` child, which reached the deterministic post-`RhinoCore` `Brep.CreateFromBox` checkpoint and threw `Rhino.Runtime.NotLicensedException` before token clearing or later Compute initialization. The local child used a `Rhino.Inside.dll` whose SHA-256 exactly matches the installed Compute child; both are `ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7`.
- [x] Added `scripts/run-compute-step3.sh` to rebuild and run this isolated local parent/child experiment without modifying `/usr/lib/rhino-compute`. It sources ignored `.env.local`, performs the existing non-starting preflight, keeps the parent loopback-only, starts one child, disables Grasshopper, and never prints `RHINO_TOKEN`.
- [x] Completed Step 3 checkpoint B on August 28, 2026 by moving only the local Compute checkpoint to immediately after `Environment.SetEnvironmentVariable("RHINO_TOKEN", null, EnvironmentVariableTarget.Process)`. The same local self-contained parent/child test reached `CHECKPOINT B` and again threw `Rhino.Runtime.NotLicensedException` at `Brep.CreateFromBox`, before exception-handler registration or plug-in/server initialization.
- [x] Completed Step 3 checkpoint C on August 28, 2026 by moving only the local Compute checkpoint to immediately after `HostUtils.OnExceptionReport` registration. The same local self-contained parent/child test reached `CHECKPOINT C` and again threw `Rhino.Runtime.NotLicensedException` at `Brep.CreateFromBox`, before Rhino command plug-in loading. The exception-report callback registration therefore does not enable the first native geometry call.
- [x] Completed Step 3 checkpoint D on August 28, 2026 by moving only the local Compute checkpoint to after the complete Rhino commands plug-in load block. That plug-in reported `Error loading rhino commands plugin.`, and the test then reached `CHECKPOINT D` and again threw `Rhino.Runtime.NotLicensedException` at `Brep.CreateFromBox`, before RhinoCode loading. This rules out completion of the commands plug-in attempted-load block as the missing transition, but not a hypothetical successful commands plug-in load.
- [x] Completed Step 3 checkpoint E on August 28, 2026 by moving only the local Compute checkpoint to after the complete RhinoCode plug-in load block. RhinoCode reported `Successfully loaded scripting plugin` and `Configured scripting plugin for compute`; the test then reached `CHECKPOINT E` and again threw `Rhino.Runtime.NotLicensedException` at `Brep.CreateFromBox`. Successful RhinoCode loading and controller configuration therefore do not enable the first native geometry call.
- [x] Completed Step 3 checkpoint F on August 28, 2026 by moving only the local Compute checkpoint to after the Grasshopper-disabled branch and `LoadComputeExtensionPlugins` reflection block. The child logged both `(3/4) Skipping grasshopper` and `(4/4) Loading compute plug-ins`, then reached `CHECKPOINT F` and again threw `Rhino.Runtime.NotLicensedException` at `Brep.CreateFromBox`. The extension-loader reflection block is therefore not sufficient; because upstream code does not log whether the reflection lookup returns a method, this does not independently confirm that an extension method existed or loaded a plug-in.
- [x] Added and ran the independent `src/RhinoInside.AspNetHost` diagnostic on August 28, 2026. It uses `Microsoft.NET.Sdk.Web`, the same compile-only RhinoCommon/Rhino.Inside package strategy, self-contained Linux x64 publishing, a loopback-only Kestrel endpoint, and a one-shot `ApplicationStarted` `Brep.CreateFromBox` checkpoint. Kestrel reported `Now listening on: http://127.0.0.1:5057`, then the checkpoint ended with `Rhino.Runtime.NotLicensedException` and exit `134`. The published host has .NET `10.0.0` framework configuration, no copied `RhinoCommon.dll`, and a `Rhino.Inside.dll` SHA-256 identical to the installed Compute child.

## Pending prerequisites

- [x] Install .NET 10 SDK: workspace-local SDK `10.0.111` is available to the provided scripts.
- [x] Install the Rhino 9 Linux runtime at `/usr/lib/rhino3d`.
- [x] Make a Core-Hour Billing token visible to the runner through ignored `.env.local`.
- [x] Confirm that the installed Rhino.Compute package can start a healthy Rhino 9 child while launched from the token-bearing environment.
- [ ] Identify the later Compute initialization stage that changes native-geometry licensing behavior. Step 3 checkpoints A through F all throw `Rhino.Runtime.NotLicensedException`, including F after every direct `RhinoCoreStartup()` stage used in the Grasshopper-disabled control. A separate standalone ASP.NET `ApplicationStarted` host also fails after Kestrel binds, so the remaining controlled position is the same post-ASP.NET-host checkpoint inside the local `compute.geometry` child itself.

Current host observations on August 27, 2026:

- Workspace-local `dotnet` SDK `10.0.111` is installed at `.dotnet/dotnet`; the scripts add it to `PATH` automatically.
- `rhino3d` `9.0.26238.7039` and `rhino-compute` `9.0.26159.12510-wip` are installed.
- `/usr/lib/rhino3d` contains the required native library, RhinoCommon assembly, and .NET 9/10 bootstrap assemblies.
- The token is stored in ignored `.env.local` with mode `0600`; `scripts/run-box-sample.sh` loads and exports it successfully without printing its value.
- The original standalone sample and the isolated Step 2 minimal host both fail in framework-dependent and self-contained forms with `Rhino.Runtime.NotLicensedException` at the first native/licensed RhinoCommon geometry operation. The minimal host proves that application-level dependencies and cleanup logic are not necessary to reproduce the failure.
- The separate loopback-only Rhino.Compute control started and served one healthy `compute.geometry` child using Rhino `9.0.26238.1000`, so the host installation and inherited token-bearing launch environment can support a running Rhino Compute child.
- The executable starts and disposes RhinoCore itself before invoking the solver library's post-start workflow. The self-contained ELF-host test has now exercised this layout and did not resolve the licensing failure.
- The local Step 3 Compute checkout is at official `9.x` commit `b49f16836f44d75b0dba583dede53076736f5359`; it is kept under `vendor/compute.rhino3d` and has one intentional diagnostic-only source change. Its self-contained local child failed at checkpoints A through F. In D, the commands plug-in reported a load error before geometry failed, so only the attempted-load block—not a successful command-plugin load—is ruled out. In E, the RhinoCode plug-in loaded successfully and its controller was configured before geometry still failed. In F, the child reached the logs for skipping Grasshopper and loading Compute plug-ins before geometry still failed. Thus every direct `RhinoCoreStartup()` stage in the Grasshopper-disabled topology has now been crossed; the next meaningful boundary is the child ASP.NET host reaching `ApplicationStarted`. The checkout commit is not asserted to be source-identical to the installed package build, so later checkpoint tests should retain the same checkout and launch arrangement.
- `src/RhinoInside.AspNetHost` is an independent diagnostic host, not a copy of or dependency on the local Compute checkout. Its self-contained runtime configuration uses .NET `10.0.0` and its `Rhino.Inside.dll` hash matches Compute's. It reached Kestrel `ApplicationStarted` on loopback port `5057`, then hit the same native `NotLicensedException`. Generic ASP.NET host startup alone is therefore not sufficient; applying the same lifecycle checkpoint to the local Compute child remains necessary.

## Next execution steps

Steps 1 and 2 are complete. Step 3 checkpoints A through F are also complete: the local self-contained Compute child throws the same `Rhino.Runtime.NotLicensedException` at every direct `RhinoCoreStartup()` position in the Grasshopper-disabled topology. The new independent ASP.NET host reaches its own `ApplicationStarted` callback after Kestrel has bound and still fails, so generic ASP.NET host startup is not sufficient. The next controlled comparison must apply that same post-host checkpoint **inside the local Compute child**, not change the geometry algorithm.

1. In `vendor/compute.rhino3d/src/compute.geometry/Startup.cs`, retain the existing Step 3F startup sequence but remove the in-startup geometry call. Register a one-shot non-secret geometry checkpoint with the child host's `IHostApplicationLifetime.ApplicationStarted` event in `Startup.Configure`, so it runs only after the child Kestrel server has bound. Log start/result markers; do not log token or raw environment data.
2. If the exact local Compute post-host checkpoint still fails while packaged Compute HTTP geometry continues to succeed, capture the safe process/runtime/assembly comparison between working packaged Compute, the local child, and both standalone hosts. Prioritize package/source provenance and executable/working-directory differences.
3. Test a Grasshopper-enabled path only as a separate experiment if its behavior is relevant; it is not required for the previously working packaged, Grasshopper-disabled control.
4. If no controlled difference remains, prepare the minimal reproduction and licensing question for McNeel.

## Expected first-run result

A successful run should:

- initialize Rhino from `/usr/lib/rhino3d` (unless `RHINO_SYSTEM_DIR` overrides it);
- run without a graphical display;
- create one valid solid Brep with dimensions `10 × 20 × 30`;
- report volume `6000` within tolerance;
- write and successfully read back `artifacts/extruded-box.3dm`;
- exit with code `0`.

## Known limitations

- Rhino on Linux is being treated as WIP/prototype software, not a production target.
- This initial implementation supports Linux x86-64 only.
- Grasshopper, Rhino.Compute HTTP use, third-party plugins, and non-`.3dm` export are intentionally out of scope.
- A successful source build and resolver initialization are insufficient; RhinoCore must accept the Core-Hour Billing token before geometry can be tested.
