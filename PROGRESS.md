# Rhino on Linux — Project Progress

_Last updated: September 14, 2026_

## Canonical project layout

The C# test cases were moved to this location:

```text
/mnt/c/Users/WegewitzSte/Desktop/temp/RhinoLinuxTest/
├── PROGRESS.md
├── README-rhino-compute-linux-repro.md
├── doc/
│   ├── rhino-inside-linux-next-steps.md
│   ├── rhino-inside-linux-prototype-plan.md
│   └── rhino-linux-licensing-retest-2026-09-14.md
└── RhinoLinxTest/
    ├── RhinoLinxTest.sln
    ├── RhinoInside.BoxSample/
    ├── RhinoInside.BoxSolver/
    ├── RhinoInside.MinimalHost/
    ├── RhinoInside.AspNetHost/
    ├── RhinoComputeLinuxRepro/
    ├── RhinoComputeLinuxRepro.Runner/
    ├── RhinoLib/
    ├── RhinoLinxTest/
    └── RhinoWindowsTest/
```

`RhinoLinxTest/RhinoLinxTest.sln` is the solution containing the test projects. The source paths formerly documented as `src/...` are no longer the canonical paths; use the corresponding directories under `RhinoLinxTest/...` above.

## Objective

Determine why a standalone Linux .NET host using Rhino.Inside cannot perform its first native RhinoCommon geometry operation, while the installed Rhino.Compute Linux service can start and process a native geometry API request.

The original box prototype remains the direct test case: create a `10 × 20 × 30` solid, validate it, and write a `.3dm` file. It is currently blocked by the licensing exception described below.

## Current status

**Standalone Rhino.Inside geometry is still blocked. Installed Rhino.Compute is working for the tested HTTP geometry request.**

On September 14, 2026:

- The standalone reproducer created `RhinoCore` successfully, then its first `Brep.CreateFromBox(...)` call terminated with `Rhino.Runtime.NotLicensedException` and exit code `134`.
- The full box sample has the same failure and does not create its `.3dm` output.
- The installed `rhino.compute` parent started one `compute.geometry` child on loopback successfully.
- `GET /healthcheck` returned `Healthy`.
- `GET /version` reported Rhino `9.0.26257.1000` and Compute `9.0.0.0`.
- A `POST` to the native geometry endpoint `Mesh.CreateFromBox` returned HTTP `200 OK` with a non-empty serialized mesh.

This is an important distinction: **the normal Rhino.Compute HTTP API request did not reproduce `NotLicensedException`.** The reproducible error is in the standalone Rhino.Inside hosting path.

See `doc/rhino-linux-licensing-retest-2026-09-14.md` for the developer-facing report and exact observed output.

## Project descriptions

| Project | Canonical path | Purpose and current result |
| --- | --- | --- |
| `RhinoInside.BoxSample` | `RhinoLinxTest/RhinoInside.BoxSample/` | Main standalone box workflow executable. It initializes Rhino.Inside/RhinoCore; delegates geometry and `.3dm` validation to `RhinoInside.BoxSolver`; currently terminates with `NotLicensedException` before output. |
| `RhinoInside.BoxSolver` | `RhinoLinxTest/RhinoInside.BoxSolver/` | Class library for preflight, closed-polyline extrusion, solid/volume validation, and `.3dm` write/read-back validation. It references RhinoCommon for compilation only; runtime RhinoCommon must be resolved from the installed Rhino runtime. |
| `RhinoInside.MinimalHost` | `RhinoLinxTest/RhinoInside.MinimalHost/` | Minimal standalone licensing reproduction. It initializes the resolver, constructs headless `RhinoCore`, and calls `Brep.CreateFromBox`. This is the smallest direct reproducer of exit `134` / `Rhino.Runtime.NotLicensedException`. |
| `RhinoInside.AspNetHost` | `RhinoLinxTest/RhinoInside.AspNetHost/` | Independent standalone ASP.NET Core/Kestrel experiment. Its `ApplicationStarted` geometry checkpoint also failed with `NotLicensedException`, showing that generic Kestrel startup alone is insufficient. |
| `RhinoComputeLinuxRepro` | `RhinoLinxTest/RhinoComputeLinuxRepro/` | Reusable class library that calls the Rhino.Compute `Mesh.CreateFromBox` REST endpoint without loading Rhino locally. |
| `RhinoComputeLinuxRepro.Runner` | `RhinoLinxTest/RhinoComputeLinuxRepro.Runner/` | Console sample with two modes: `compute-api` verifies the working Compute HTTP call; `standalone` reproduces the failing Rhino.Inside call. |
| `RhinoLib` | `RhinoLinxTest/RhinoLib/` | Existing general RhinoCommon test library retained in the solution. It is not the minimal licensing reproducer. |
| `RhinoLinxTest` | `RhinoLinxTest/RhinoLinxTest/` | Existing console application that references `RhinoLib`. It is retained as a legacy/general test application. |
| `RhinoWindowsTest` | `RhinoLinxTest/RhinoWindowsTest/` | Existing Windows-targeted placeholder console application; it is not part of the Linux licensing investigation. |

## Build status

The moved solution was built on September 14, 2026 with the workspace-local .NET SDK `10.0.111`:

```bash
/home/dev/RhinoOnLinux/.dotnet/dotnet build \
  /mnt/c/Users/WegewitzSte/Desktop/temp/RhinoLinuxTest/RhinoLinxTest/RhinoLinxTest.sln \
  --configuration Release \
  --ignore-failed-sources
```

Result: **0 errors**.

The new `RhinoComputeLinuxRepro` library and `RhinoComputeLinuxRepro.Runner` built without warnings. The solution emitted six pre-existing warnings from `RhinoLinxTest/Program.cs`; they are unrelated to the new Compute reproducer and did not block the build.

## Reproduction commands

### A. Working Rhino.Compute API request

Start the installed Compute parent on a loopback port with one child and a valid `RHINO_TOKEN` inherited by the service process. Then, from the solution directory:

```bash
/home/dev/RhinoOnLinux/.dotnet/dotnet run \
  --project RhinoComputeLinuxRepro.Runner/RhinoComputeLinuxRepro.Runner.csproj \
  --configuration Release \
  --no-build \
  -- \
  --mode compute-api \
  --url http://127.0.0.1:5059/
```

The demonstrator makes this request:

```text
POST /rhino/geometry/mesh/createfrombox-boundingbox_int_int_int
Content-Type: application/json

[{"Min":{"X":0,"Y":0,"Z":0},"Max":{"X":10,"Y":20,"Z":30}},1,1,1]
```

Verified result:

```text
HTTP 200 (OK)
Serialized mesh returned: True
```

### B. Failing standalone Rhino.Inside call

On Linux, set a valid `RHINO_TOKEN` in the process environment. If necessary, set `RHINO_SYSTEM_DIR` to the installed Rhino system directory (normally `/usr/lib/rhino3d`). Then run:

```bash
/home/dev/RhinoOnLinux/.dotnet/dotnet run \
  --project RhinoComputeLinuxRepro.Runner/RhinoComputeLinuxRepro.Runner.csproj \
  --configuration Release \
  --no-build \
  -- \
  --mode standalone
```

Observed result on the affected system:

```text
CHECKPOINT: Resolver initialized from /usr/lib/rhino3d.
CHECKPOINT: RhinoCore constructed.
CHECKPOINT: Calling deterministic Brep.CreateFromBox operation.
Unhandled exception. Rhino.Runtime.NotLicensedException: ...
```

Expected affected-system exit code: `134`.

## Environment known to reproduce the contrast

| Item | Value |
| --- | --- |
| OS | Ubuntu 24.04 under WSL2, Linux x64 |
| .NET SDK | 10.0.111 |
| Rhino system directory | `/usr/lib/rhino3d` |
| Standalone Rhino.Inside package | `9.0.26084.13070-beta` |
| Standalone RhinoCommon compile package | `9.0.25350.305-wip`, with runtime assets excluded |
| Compute Rhino version reported by `/version` | `9.0.26257.1000` |
| Compute version reported by `/version` | `9.0.0.0` |
| Token handling | `RHINO_TOKEN` is required but must never be committed, echoed, or included in forum posts. |

## Documentation

- `README-rhino-compute-linux-repro.md` — build and run instructions for the new class library and runner.
- `doc/rhino-linux-licensing-retest-2026-09-14.md` — concise developer/forum report, including the successful Compute request and failed standalone test.
- `doc/rhino-inside-linux-prototype-plan.md` — original feasibility and implementation plan.
- `doc/rhino-inside-linux-next-steps.md` — earlier investigation notes; consult the September 14 retest report for the latest verified behavior.

## Next steps

1. Send `doc/rhino-linux-licensing-retest-2026-09-14.md` and the `RhinoComputeLinuxRepro` sample to Rhino/McNeel developers.
2. Ask which normal `compute.geometry` initialization, licensing, executable, working-directory, or package-provenance difference permits native geometry in Compute while the standalone Rhino.Inside host fails.
3. If requested, compare the installed Compute child with the local diagnostic Compute checkout only after keeping the source locations and runtime assets clearly separated.
4. Keep the `compute-api` and `standalone` outcomes separate in all reports: the former is currently successful; the latter is the reproducible failure.

## Known limitations

- Rhino on Linux is being evaluated as WIP/prototype software, not a production target.
- The standalone tests target Linux x86-64.
- The token is deliberately excluded from source, documentation, and saved command output.
- The old workspace helper scripts and local Compute source checkout are not under this moved project root. This progress file documents the moved C# test cases and uses direct `dotnet` commands against the canonical solution paths above.
