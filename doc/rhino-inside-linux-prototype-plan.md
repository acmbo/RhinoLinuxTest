# Rhino.Inside on Linux: working prototype plan

## Status

- Research snapshot: **2026-08-27**
- Target host: Ubuntu 24.04 on WSL2, x86-64
- Goal: prove that a standalone .NET process can start Rhino 9 in-process on Linux, create a capped box extrusion from a closed polyline, validate it, and write it to a `.3dm` file.
- Production status: **prototype/WIP only**. McNeel's Linux guide explicitly describes Rhino.Compute on Linux as Rhino WIP software and does not recommend it for production.
- Implementation status: technical groundwork was added on 2026-08-27. The project was restored and built successfully with a workspace-local .NET SDK 10.0.111. The standalone RhinoCore smoke test still stops with `Rhino.Runtime.NotLicensedException` before geometry execution, despite four retries and a non-secret in-process check proving that `RHINO_TOKEN` reaches the compiled direct application. However, a separate loopback-only Rhino.Compute control launched from the same token-bearing environment successfully started one `compute.geometry` child; parent and child health/version endpoints responded, and the child logged that it was listening on port 6001. The direct sample’s startup discrepancy is therefore unresolved; do not treat the Compute control as proof that the direct sample is fixed. A class-library boundary experiment was added later on August 27, 2026: `RhinoInside.BoxSample` retains resolver initialization plus the entire RhinoCore lifetime, and `RhinoInside.BoxSolver` contains the RhinoCommon workflow. It builds and passes non-starting checks, but the refactored direct RhinoCore startup has not yet been executed.

## Feasibility conclusion

Proceed with the prototype.

The current Rhino 9 code has an intentional Linux path:

1. `Rhino.Inside` detects Linux, defaults the Rhino system directory to `/usr/lib/rhino3d`, and loads `/usr/lib/rhino3d/libRhinoLibrary.so`.
2. The current `Rhino.Inside` NuGet package contains both `net9.0` and `net10.0` assemblies.
3. `compute.geometry` on branch `9.x` targets `net10.0` on Linux, calls `RhinoInside.Resolver.Initialize()`, and then creates a headless `RhinoCore` with `WindowStyle.NoWindow`.
4. The Compute packaging workflow publishes self-contained Linux x64 and arm64 builds and creates DEB/RPM packages.
5. McNeel's guide says Rhino.Compute has been tested on Ubuntu Server 24.04 and explicitly includes WSL2 as a possible host environment.

This is strong evidence that the prototype should work. It is not yet a runtime proof for this machine:

- The public Compute workflow builds and packages Linux artifacts, but does not install Rhino, activate a license, and execute a geometry smoke test.
- The `Rhino.Inside` workflow runs its Rhino tests on Windows, not Linux.
- The local host now has the workspace-local .NET SDK, Rhino runtime, and Rhino.Compute packages installed. A Rhino.Compute child completed the endpoint-level startup control, but the standalone application is still not a runtime proof because it fails during its own RhinoCore licensing/startup path.

The prototype below closes the remaining gap with the smallest possible licensed, headless, in-process test.

## Source snapshot used for this plan

| Source | Branch/version inspected | Snapshot detail |
| --- | --- | --- |
| `mcneel/rhino.inside` | `rhino-9.x` | commit `23865e47e652f2c24cdc81ef66b2fb96f5f376a2`, dated 2026-03-25 |
| `mcneel/compute.rhino3d` | `9.x` | commit `b49f16836f44d75b0dba583dede53076736f5359`, dated 2026-06-08 |
| `mcneel/rhino-developer-samples` | `9` | commit `b1b3f5e4e432619109c15be6e5304b7e81ab980e`, dated 2026-07-17 |
| Linux getting-started guide | page updated 2026-03-27 | Ubuntu 24.04/Amazon Linux 2023 instructions and WIP warning |
| `Rhino.Inside` NuGet | `9.0.26084.13070-beta` | newest version visible in the NuGet package index on 2026-08-27 |

The exact package version should be pinned for the first successful run. Upgrades should be tested separately rather than silently changing the baseline.

## Compute endpoint control and documented endpoints

McNeel’s official **Running and Debugging Compute Locally** guide (last updated May 13, 2024) documents these parent-service diagnostics:

| Endpoint | Purpose | Local 2026-08-27 result |
| --- | --- | --- |
| `GET /version` | Report Rhino and Compute version data. | `{"rhino":"9.0.26238.1000","compute":"9.0.0.0","git_sha":null}` |
| `GET /healthcheck` | Basic service health. | `Healthy` |
| `GET /activechildren` | Report active `compute.geometry` child count. | One active child once it was fully initialized. |
| `GET /sdk` | List the available Compute API/SDK routes. | Returned the generated API list. |

The guide’s examples use `http://localhost:6500`; the local package was deliberately bound only to `http://127.0.0.1:5055`. The guide is explicitly Windows-oriented and predates the current Linux WIP guidance, but these endpoint paths were present in the installed Rhino 9 Compute package.

For the control, `rhino-compute-start` was run with `--childcount 1`, `--spawn-on-startup`, `--load-grasshopper false`, and a 60-second idle span. The parent spawned one `compute.geometry` child on port 6001. The child served its own `GET /healthcheck` (`Healthy`) and `GET /version` response, and its log recorded `Now listening on: http://localhost:6001`. The parent/child process tree and healthy endpoints show that this Compute child did not crash during Rhino startup. The test was then terminated via the local `POST /shutdown-children` control route followed by a parent interrupt; both endpoints were unavailable afterward and no Compute process remained.

This is useful evidence that the installed Rhino 9 Compute runtime can launch successfully with the token-bearing environment. It does **not** establish why the direct `Rhino.Inside` application continues to throw `Rhino.Runtime.NotLicensedException`, nor does it validate the box-extrusion artifact.

## Important difference from the existing HelloWorld sample

The linked HelloWorld source is useful for its initialization order, but its project file cannot be copied unchanged:

- It targets `net8.0-windows8.0`.
- It references `Rhino.Inside` `8.0.6-beta`.
- Its comments and `[STAThread]` setup assume a UI-capable Windows process.

For Linux, retain this sequence from the sample:

1. Initialize `RhinoInside.Resolver` before first use of RhinoCommon types.
2. Construct `RhinoCore` inside a `using` scope.
3. Perform geometry work only after Rhino has started.
4. Dispose `RhinoCore` before process exit.

Use the current Compute implementation for the Linux-specific choices:

- Target `net10.0`.
- Start `new RhinoCore(null, WindowStyle.NoWindow)`.
- Let the resolver use `/usr/lib/rhino3d`, with an explicit override available for diagnostics.

## Prototype scope

### In scope

- One Linux console executable plus one class library for the RhinoCommon workflow.
- Rhino 9 loaded in the same process through `Rhino.Inside`.
- No Rhino UI and no X server/display requirement.
- A closed rectangular `Polyline` in the World XY plane.
- A capped extrusion that forms a solid box.
- Deterministic geometry validation.
- A `.3dm` output artifact.
- Clear preflight and error messages.
- A repeatable shell-level smoke test.

### Out of scope

- Rhino.Compute HTTP endpoints.
- Grasshopper and third-party plug-ins.
- File formats other than `.3dm`.
- A long-running service.
- Docker image production hardening.
- Parallel RhinoCore instances.
- Production support claims.

## Proposed repository layout

```text
.
├── doc/
│   └── rhino-inside-linux-prototype-plan.md
├── .env.example
├── .gitignore
├── global.json
├── src/
│   ├── RhinoInside.BoxSample/
│   │   ├── RhinoInside.BoxSample.csproj
│   │   └── Program.cs
│   ├── RhinoInside.BoxSolver/
│   │   ├── RhinoInside.BoxSolver.csproj
│   │   └── BoxSolver.cs
│   └── RhinoInside.AspNetHost/
│       ├── RhinoInside.AspNetHost.csproj
│       └── Program.cs
├── scripts/
│   ├── check-rhino-linux.sh
│   ├── run-box-sample.sh
│   └── run-aspnet-host.sh
├── artifacts/
│   └── .gitkeep
└── .gitignore
```

Keep the first implementation deliberately small. The deliberate `BoxSample`/`BoxSolver` boundary is an assembly-load experiment: it isolates RhinoCommon workflow code from the executable without moving Rhino.Inside resolver setup or RhinoCore lifetime out of the executable.

## Version baseline

Use this baseline for the first implementation:

| Component | Baseline |
| --- | --- |
| OS | Ubuntu 24.04 x86-64 |
| .NET SDK/TFM | SDK `10.0.102`, target `net10.0` |
| Rhino.Inside package | `9.0.26084.13070-beta` |
| Rhino runtime | Rhino 9 WIP Linux package installed under `/usr/lib/rhino3d` |
| Output format | Rhino 8-compatible `.3dm` for easy interchange |

Why .NET 10: the current `compute.geometry` Linux project and packaging workflow use .NET 10. The Linux guide was last updated before that change and still installs .NET `9.0.102`. The selected `Rhino.Inside` package supports both .NET 9 and .NET 10, so `net9.0` remains a fallback if a package/runtime incompatibility is discovered.

A `global.json` should pin `10.0.102` with patch roll-forward enabled, so a developer does not accidentally build against an unrelated SDK major version.

## Environment provisioning plan

### 1. Install Rhino from the McNeel Linux repository

Follow the repository/key setup from McNeel's current Linux guide. For the prototype, install `rhino-compute`, because its package declares a dependency on `rhino3d (>= 9.0.0)` and therefore supplies a known Compute-compatible Rhino runtime.

Installing only `rhino3d` is acceptable later, after the direct sample works and the minimal package set has been verified.

Expected runtime files include:

```text
/usr/lib/rhino3d/libRhinoLibrary.so
/usr/lib/rhino3d/RhinoCommon.dll
/usr/lib/rhino3d/dotnetstart.9.dll
```

The preflight script must fail before `dotnet run` if the Rhino directory, native library, or managed bootstrap assembly is missing.

### 2. Install the .NET SDK

Install .NET SDK `10.0.102`, matching the current Compute packaging workflow. Make sure the resulting `dotnet` executable is on `PATH` for the user running the sample.

Do not rely on the framework bundled inside the self-contained `rhino-compute` package: the prototype needs an SDK to restore and build its own project.

### 3. Configure licensing securely

A Core-Hour Billing token is required. Supply it through `RHINO_TOKEN` in the process environment.

Rules:

- Never place the token in source code, project files, committed shell scripts, command output, or test snapshots.
- Support an untracked `.env.local` file with permissions `0600`, or export the variable interactively.
- The runner should check only whether `RHINO_TOKEN` is set; it must not echo the value.
- A missing token should produce a clear error and a non-zero exit code before starting Rhino.

`RHINO_COMPUTE_KEY` is not needed for the direct in-process sample. It is only needed if the optional Rhino.Compute control test is run.

### 4. WSL2 considerations

The direct console sample does not depend on systemd. This avoids differences between WSL2 installations with and without systemd enabled.

Recommended WSL resources for a reliable first run are the same order of magnitude as McNeel's lightweight VM guidance: 4 CPUs and 8 GB RAM. This is not a hard geometry requirement, but it reduces startup/resource ambiguity.

## Project design

### Project files and assembly boundary

The implementation has two framework-dependent `net10.0` prototype projects and one independent diagnostic host:

| Project | Responsibility | References |
| --- | --- | --- |
| `RhinoInside.BoxSample` | Console entry point, Rhino.Inside resolver initialization, RhinoCore construction, RhinoCore disposal | `Rhino.Inside` `9.0.26084.13070-beta`, project reference to `RhinoInside.BoxSolver` |
| `RhinoInside.BoxSolver` | Argument handling, preflight, RhinoCommon geometry, `.3dm` write/read-back, summary/error reporting | `RhinoCommon` `9.0.25350.305-wip` with `ExcludeAssets="runtime"` and `PrivateAssets="all"` |
| `RhinoInside.AspNetHost` | Independent ASP.NET Core lifecycle diagnostic; runs one `Brep.CreateFromBox` checkpoint from `ApplicationStarted` after a loopback Kestrel bind, then stops | `Microsoft.NET.Sdk.Web`, `Rhino.Inside` `9.0.26084.13070-beta`, compile-only `RhinoCommon` `9.0.25350.305-wip` |

`RhinoInside.AspNetHost` is not part of the box-extrusion deliverable and does not reference the local Compute source checkout. It exists solely to isolate the effect of generic ASP.NET Core host startup from Compute-specific initialization.

The explicit RhinoCommon reference in the solver library is compile-only. It must not copy the NuGet `RhinoCommon.dll` beside either output assembly; Rhino.Inside must resolve the installed copy from `/usr/lib/rhino3d` at runtime. This was checked after the split build.

The console calls `BoxSolver.Run(args, RunWithRhinoCore)`. The library validates the request, then calls the executable callback with a post-start workflow delegate. The executable performs this scope:

```csharp
InitializeResolver(configuration.RhinoSystemDirectory);
using var rhinoCore = StartRhinoCore();
return workflow(configuration);
```

This keeps every RhinoCore operation in `RhinoInside.BoxSample`, while ensuring the solver's geometry and file IO cannot execute until RhinoCore has started.

### Initialization sequence

Initialization order is a functional requirement, not a style preference.

```text
process starts
  -> BoxSolver parses arguments and validates Linux, token, Rhino files, and output path
  -> BoxSample callback calls RhinoInside.Resolver.Initialize(rhinoSystemDirectory)
  -> BoxSample creates RhinoCore(..., WindowStyle.NoWindow) in a using scope
  -> BoxSample invokes BoxSolver's post-start geometry workflow
  -> BoxSolver creates/validates geometry and writes/re-opens .3dm
  -> BoxSample disposes RhinoCore as its using scope exits
  -> exit 0
```

Keep resolver startup in a non-inlined method on `Program`, and avoid static fields whose types come from RhinoCommon. `BoxSample` must invoke the solver library only through the post-start callback after resolver initialization and RhinoCore construction; this makes the intended assembly-load order explicit.

A non-secret `--verify-token` mode is available for diagnostics. It runs inside the compiled .NET application after `.env.local` has been sourced by the runner, reports only whether `RHINO_TOKEN` is present, and exits before resolver or RhinoCore startup.

Support an optional `RHINO_SYSTEM_DIR` environment variable. Default it to `/usr/lib/rhino3d`. This gives a diagnostic escape hatch without changing code.

### Geometry operation

Use deterministic dimensions:

- Width: `10.0`
- Depth: `20.0`
- Height: `30.0`
- Expected volume: `6000.0`

Algorithm:

1. Create these points in World XY:
   - `(0, 0, 0)`
   - `(10, 0, 0)`
   - `(10, 20, 0)`
   - `(0, 20, 0)`
   - `(0, 0, 0)` to close the polyline
2. Construct a `Polyline` and a `PolylineCurve`.
3. Assert that the profile is valid, closed, planar, and has four non-zero edges.
4. Call `Extrusion.Create(profile, 30.0, cap: true)`.
5. Convert the extrusion to a `Brep` for solid and volume checks.
6. Assert:
   - the extrusion and Brep are valid;
   - the Brep is solid;
   - bounding-box extents are `10 x 20 x 30` within tolerance;
   - volume is `6000` within tolerance.
7. Add the Brep to a `File3dm` and write `artifacts/extruded-box.3dm`.
8. Re-open the file and verify that it contains one valid Brep object.

Writing and re-reading the file makes the prototype stronger than a console-only calculation. It exercises Rhino startup, RhinoCommon geometry, native calls, and supported `.3dm` file IO.

### Program output and exit codes

On success, emit a short machine-readable JSON summary, for example:

```json
{
  "rhinoSystemDirectory": "/usr/lib/rhino3d",
  "rhinoVersion": "9.x",
  "isSolid": true,
  "boundingBox": [10.0, 20.0, 30.0],
  "volume": 6000.0,
  "output": "artifacts/extruded-box.3dm"
}
```

Suggested exit codes:

| Code | Meaning |
| --- | --- |
| `0` | Startup, geometry validation, write, and re-read succeeded |
| `2` | Preflight/configuration failure |
| `3` | Rhino.Inside/RhinoCore startup failure |
| `4` | Geometry creation or validation failure |
| `5` | Output write/read-back failure |
| `1` | Unexpected exception |

Write full exception details to stderr for the prototype, but never print environment-variable values.

## Scripts

### `scripts/check-rhino-linux.sh`

Responsibilities:

- Confirm `uname` reports Linux.
- Confirm 64-bit x86-64 for the initial target.
- Confirm `dotnet --version` is available and major version 10.
- Confirm `/usr/lib/rhino3d` and `libRhinoLibrary.so` exist.
- Confirm at least one compatible `dotnetstart.*.dll` exists.
- Confirm `RHINO_TOKEN` is non-empty without printing it.
- Return non-zero with one actionable message per failed check.

### `scripts/run-box-sample.sh`

Responsibilities:

1. Run the preflight script.
2. Create `artifacts/` if necessary.
3. Run `dotnet restore`.
4. Run the sample in Release mode.
5. Confirm the `.3dm` file exists and is non-empty.
6. Preserve the application's exit code.

The script should not use `sudo`; provisioning and runtime execution should remain separate.

## Implementation phases

### Phase 0: provision and establish a control

1. Install the McNeel repository and `rhino-compute` package.
2. Install .NET SDK `10.0.102`.
3. Configure `RHINO_TOKEN` for the current user.
4. Verify the expected files under `/usr/lib/rhino3d`.
5. Optionally start Rhino.Compute and call its version endpoint once. This is a diagnostic control proving that the package and license can start Rhino before debugging custom code.

Exit gate: Rhino is installed, the token is accepted, and either the optional Compute control or the direct sample can begin Rhino startup.

### Phase 1: scaffold the direct sample

1. Add `global.json`.
2. Create the `net10.0` console project.
3. Pin `Rhino.Inside` `9.0.26084.13070-beta`.
4. Implement resolver initialization and a no-window `RhinoCore` lifetime in `RhinoInside.BoxSample`.
5. Place argument parsing, preflight, RhinoCommon geometry, and output validation in `RhinoInside.BoxSolver`; invoke its post-start workflow only while `RhinoCore` is alive.
6. Ensure neither output directory contains a NuGet `RhinoCommon.dll`.
7. Print the resolved Rhino system directory and assembly version.

Exit gate: the process starts Rhino and exits cleanly without geometry work.

### Phase 2: implement the box extrusion

1. Add the closed rectangle polyline.
2. Create the capped extrusion.
3. Convert it to a Brep.
4. Add validation for solidity, dimensions, and volume.
5. Produce the JSON success summary.

Exit gate: all in-memory assertions pass and the process exits `0`.

### Phase 3: persist and re-read the result

1. Write `artifacts/extruded-box.3dm`.
2. Re-open it in the same process.
3. Verify one valid Brep is present.
4. Ensure all disposable Rhino objects and `RhinoCore` are cleaned up.

Exit gate: a non-empty, readable `.3dm` is produced on every run.

### Phase 4: make the check repeatable

1. Add the preflight and runner scripts.
2. Run the sample twice as separate processes.
3. Test an unset token and an invalid Rhino path.
4. Record startup time and peak memory for reference, not as pass/fail limits.

Exit gate: two consecutive successful runs and clean, actionable failures for missing prerequisites.

### Phase 5: optional CI/manual validation

A licensed end-to-end job requires a protected `RHINO_TOKEN` secret and installation of McNeel's Linux package. Do not put this in untrusted pull-request CI. Prefer one of:

- a manually dispatched protected GitHub Actions job;
- a self-hosted Ubuntu 24.04 runner;
- a local release checklist.

The normal unlicensed CI job can still restore/build the project and run shell linting, but it cannot prove RhinoCore startup.

## Verification matrix

| Test | Expected result |
| --- | --- |
| Preflight on current unprovisioned host | Fails clearly because .NET/Rhino are absent |
| Valid token and default path | Resolver reports `/usr/lib/rhino3d`; RhinoCore starts |
| Valid geometry run | Solid Brep, dimensions `10 x 20 x 30`, volume `6000` |
| File output | `artifacts/extruded-box.3dm` exists and is non-empty |
| File read-back | One valid Brep is found |
| Second process run | Succeeds again without stale locks/state |
| `RHINO_TOKEN` unset | Exit `2`, no Rhino startup attempt |
| `RHINO_SYSTEM_DIR` invalid | Exit `2` or `3` with path in error message |
| No display/X server | Run still succeeds |
| Token secrecy | Token absent from stdout, stderr, files, and source control |
| Assembly boundary | `BoxSample` owns resolver/RhinoCore; `BoxSolver` owns RhinoCommon workflow; no NuGet RhinoCommon runtime file is copied to either output directory |
| Split-layout direct startup | Must be run separately; build and non-starting checks do not demonstrate that the `NotLicensedException` is resolved |

## Acceptance criteria

The prototype is considered working only when all of the following are true on Ubuntu 24.04 Linux:

1. `dotnet run -c Release` starts Rhino 9 through `Rhino.Inside` in the same process.
2. No Rhino.Compute server is required for the geometry operation.
3. No graphical display is required.
4. A closed polyline is extruded and capped into a valid solid.
5. The dimensions and volume match the expected values within a documented tolerance.
6. A `.3dm` is written and successfully re-opened.
7. The program exits `0` and can be run successfully a second time.
8. Missing license/runtime prerequisites produce non-zero, actionable errors.
9. No secret is committed or printed.
10. The exact Rhino.Inside package and SDK versions used for the successful run are recorded.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Rhino Linux support is WIP | Keep scope to core geometry and `.3dm`; label results with exact versions and date |
| Guide and repository SDK versions differ | Mirror current Compute with .NET 10; retain .NET 9 as an explicit fallback |
| NuGet compile assets do not match installed Rhino runtime | Pin package; record installed Rhino version; upgrade both deliberately |
| RhinoCommon assembly-load timing contributes to the direct-start failure | Keep resolver/RhinoCore in the executable; isolate geometry in the compile-only `BoxSolver` library; run the split layout as a separate licensed test |
| Resolver cannot find Rhino | Preflight `/usr/lib/rhino3d`; support `RHINO_SYSTEM_DIR` override |
| Native dependency is missing | Capture the full loader exception and inspect `ldd /usr/lib/rhino3d/libRhinoLibrary.so` |
| License activation fails | Verify `RHINO_TOKEN` separately; use optional Compute control test |
| WSL2/systemd differences | Run the direct executable; do not require the service manager |
| CI build is mistaken for runtime support | Require a licensed end-to-end smoke test before declaring success |
| Token leaks | Use protected environment injection, never command-line arguments or committed files |
| A later WIP package breaks the prototype | Keep the known-good pinned version and add upgrades as separate test changes |

## Troubleshooting order

Use this order to avoid debugging geometry when startup is the actual problem:

1. Confirm OS/architecture and `dotnet --info`.
2. Confirm Rhino package installation and `/usr/lib/rhino3d` contents.
3. Confirm `RHINO_TOKEN` is present.
4. Run the optional packaged Rhino.Compute control.
5. Run a program that only initializes the resolver and RhinoCore.
6. Add polyline creation.
7. Add extrusion and solid validation.
8. Add `.3dm` write/read-back.
9. For native loader errors, run `ldd` on `libRhinoLibrary.so` and inspect missing libraries.
10. Record exact package versions before trying an upgrade or downgrade.

## References

- Rhino.Inside, `rhino-9.x`: <https://github.com/mcneel/rhino.inside/tree/rhino-9.x>
- Rhino.Inside Linux resolver path: <https://github.com/mcneel/rhino.inside/blob/rhino-9.x/src/RhinoInside/RhinoFinder.cs>
- Rhino.Inside native/managed resolver: <https://github.com/mcneel/rhino.inside/blob/rhino-9.x/src/RhinoInside/Resolver_NetCore.cs>
- Compute, `9.x`: <https://github.com/mcneel/compute.rhino3d/tree/9.x>
- Compute Linux project settings: <https://github.com/mcneel/compute.rhino3d/blob/9.x/src/compute.geometry/compute.geometry.csproj>
- Compute startup sequence: <https://github.com/mcneel/compute.rhino3d/blob/9.x/src/compute.geometry/Program.cs>
- Compute headless RhinoCore startup: <https://github.com/mcneel/compute.rhino3d/blob/9.x/src/compute.geometry/Startup.cs>
- Compute Linux workflows: <https://github.com/mcneel/compute.rhino3d/tree/9.x/.github/workflows>
- Existing HelloWorld sample: <https://github.com/mcneel/rhino-developer-samples/tree/9/rhino.inside/dotnet-netcore/HelloWorld>
- Linux setup guide: <https://developer.rhino3d.com/guides/compute/compute-linux-getting-started/>
- Core-hour billing: <https://developer.rhino3d.com/guides/compute/core-hour-billing/>
