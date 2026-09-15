# Rhino on Linux — Project Progress

_Last updated: September 15, 2026_

## Canonical project layout

Use repository-relative paths so the same checkout works under WSL2 and a full
Ubuntu VM:

```text
RhinoLinuxTest/
├── PROGRESS.md
├── README-rhino-compute-linux-repro.md
├── doc/
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

The current Ubuntu VM checkout is `/home/dev/RhinoLinuxTest`. The earlier WSL2
checkout was under `/mnt/c/Users/WegewitzSte/Desktop/temp/RhinoLinuxTest`.

## Objective

Determine why the same standalone Rhino.Inside workflow succeeds in a full
Ubuntu VM but fails at its first licensed native RhinoCommon geometry operation
under WSL2, while installed Rhino.Compute succeeds in both environments.

The direct test case creates a `10 × 20 × 30` solid, validates dimensions and
volume, writes a `.3dm`, reopens it, and validates the persisted Brep.

## Current status

**The issue is now WSL2-specific in the tested environments. Standalone
Rhino.Inside and installed Rhino.Compute both pass on the Ubuntu VM.**

Controlled Ubuntu VM results from September 14, 2026:

- Token visibility check passed.
- `RhinoInside.MinimalHost` constructed `RhinoCore`, cleared `RHINO_TOKEN` after
  startup, and completed `Brep.CreateFromBox`; exit `0`.
- The standalone runner completed the same native operation; exit `0`.
- The full box sample created a valid solid with bounding box `10 × 20 × 30` and
  volume `6000`, wrote a 23,580-byte `.3dm`, reopened it, and validated the
  persisted Brep; exit `0`.
- Installed Compute returned `Healthy`, reported Rhino `9.0.26257.1000` and
  Compute `9.0.0.0`, and returned HTTP `200` with a serialized mesh.
- A narrow successful minimal-host `strace` recorded no `AF_INET`/`AF_INET6`
  connects and no `EACCES` or `EPERM`; it is retained as the VM diff baseline.

The Ubuntu VM and WSL2 now have matching `rhino3d` and `rhino-compute`
package versions and identical SHA-256 values for all three compared installed
runtime files: `RhinoCommon.dll`, `libRhinoLibrary.so`, and the Compute child
`Rhino.Inside.dll`. A live WSL retest also used the local non-Snap .NET 10.0.111
host and found the system `libstdc++.so.6` provides `GLIBCXX_3.4.32` and newer.

A separate VM setup issue was found: Snap's .NET 10 host injects the Core 22
`libstdc++.so.6`, which lacks `GLIBCXX_3.4.32` required by the installed Rhino
native library. That failure occurs in resolver initialization and is not a
licensing result. Bypassing the Snap C++ runtime made all standalone VM tests
pass.

The earlier WSL2 result remains:

```text
resolver initialization       PASS
RhinoCore construction        PASS
first native geometry call    FAIL: Rhino.Runtime.NotLicensedException, exit 134
```

A live WSL retest on September 14 reconfirmed that result from an ext4 checkout:
the token visibility check exited `0`; `RhinoInside.MinimalHost` reached the
native `Brep.CreateFromBox` call and exited `134`; and the full box workflow
also exited `134` before it wrote a `.3dm`.

On September 15, a clean matching WSL `strace` was captured from the same ext4
checkout with a local non-Snap .NET host and a temporary rootless tracer. The
minimal host again exited `134`. Its 25 trace files recorded zero `connect`
calls (including `AF_INET`/`AF_INET6`), zero `EACCES`, zero `EPERM`, and 135
`ENOENT` candidate-path results. The raw WSL trace is retained only in the
native-ext4 checkout's Git-ignored artifact directory, not this Windows-mounted
repository checkout. At a high level, neither this trace nor the passing VM
trace shows an internet connect or a permission denial in the chosen syscall
set. An exact reviewed path-level comparison with the protected VM raw trace is
still required. A reusable normalizer/diff tool and a non-secret compatibility
context collector were added on September 15. The corrected collector also
showed that `test -s` is unsuitable for the procfs boot-ID check, so the earlier
claim that WSL's `boot_id` is empty must be retested by reading it without
printing its value. See `doc/wsl-minimal-host-strace-2026-09-15.md` and
`doc/wsl-ubuntu-comparison-tooling-2026-09-15.md`.

See `doc/ubuntu-vm-compatibility-retest-2026-09-14.md` for exact VM results and
`doc/rhino-wsl-vs-ubuntu-vm-licensing-findings-2026-09-14.md` for the comparison.

## Project descriptions

| Project | Canonical path | Purpose and current result |
| --- | --- | --- |
| `RhinoInside.BoxSample` | `RhinoLinxTest/RhinoInside.BoxSample/` | Main standalone box workflow executable. It initializes Rhino.Inside/RhinoCore; delegates geometry and `.3dm` validation to `RhinoInside.BoxSolver`; passes on the Ubuntu VM and remains the full-workflow WSL2 reproducer. |
| `RhinoInside.BoxSolver` | `RhinoLinxTest/RhinoInside.BoxSolver/` | Class library for preflight, closed-polyline extrusion, solid/volume validation, and `.3dm` write/read-back validation. It references RhinoCommon for compilation only; runtime RhinoCommon must be resolved from the installed Rhino runtime. |
| `RhinoInside.MinimalHost` | `RhinoLinxTest/RhinoInside.MinimalHost/` | Minimal standalone licensing reproduction. It initializes the resolver, constructs headless `RhinoCore`, and calls `Brep.CreateFromBox`. It passes on the Ubuntu VM and is the smallest direct WSL2 reproducer of exit `134` / `Rhino.Runtime.NotLicensedException`. |
| `RhinoInside.AspNetHost` | `RhinoLinxTest/RhinoInside.AspNetHost/` | Independent standalone ASP.NET Core/Kestrel experiment. Its `ApplicationStarted` geometry checkpoint also failed with `NotLicensedException`, showing that generic Kestrel startup alone is insufficient. |
| `RhinoComputeLinuxRepro` | `RhinoLinxTest/RhinoComputeLinuxRepro/` | Reusable class library that calls the Rhino.Compute `Mesh.CreateFromBox` REST endpoint without loading Rhino locally. |
| `RhinoComputeLinuxRepro.Runner` | `RhinoLinxTest/RhinoComputeLinuxRepro.Runner/` | Console sample with two modes: `compute-api` verifies the Compute HTTP call; `standalone` passes on the Ubuntu VM and reproduces the WSL2 Rhino.Inside failure. |
| `RhinoLib` | `RhinoLinxTest/RhinoLib/` | Existing general RhinoCommon test library retained in the solution. It is not the minimal licensing reproducer. |
| `RhinoLinxTest` | `RhinoLinxTest/RhinoLinxTest/` | Existing console application that references `RhinoLib`. It is retained as a legacy/general test application. |
| `RhinoWindowsTest` | `RhinoLinxTest/RhinoWindowsTest/` | Existing Windows-targeted placeholder console application; it is not part of the Linux licensing investigation. |

## Build status

The focused projects restored and built successfully on the Ubuntu VM with .NET
SDK `10.0.111` when `MSBuildEnableWorkloadResolver=false` and `UseAppHost=false`
were used. The flags bypass two Snap SDK packaging issues: an inconsistent legacy
workload manifest and a missing generated apphost. The resulting DLLs are valid.

The solution's legacy projects still emit six existing nullable/unused-member
warnings. A normal non-Snap .NET 10 installation is recommended before treating
a whole-solution Snap build as the canonical build result.

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

### B. Standalone Rhino.Inside comparison

On Linux, set a valid `RHINO_TOKEN` in the process environment. If necessary, set `RHINO_SYSTEM_DIR` to the installed Rhino system directory (normally `/usr/lib/rhino3d`). Then run:

```bash
/home/dev/RhinoOnLinux/.dotnet/dotnet run \
  --project RhinoComputeLinuxRepro.Runner/RhinoComputeLinuxRepro.Runner.csproj \
  --configuration Release \
  --no-build \
  -- \
  --mode standalone
```

Observed result under WSL2:

```text
CHECKPOINT: Resolver initialized from /usr/lib/rhino3d.
CHECKPOINT: RhinoCore constructed.
CHECKPOINT: Calling deterministic Brep.CreateFromBox operation.
Unhandled exception. Rhino.Runtime.NotLicensedException: ...
```

Expected WSL2 exit code: `134`. On the Ubuntu VM, the same runner exits `0` and
prints `SUCCESS: Standalone Rhino.Inside geometry completed.`

## Compared environments

| Item | WSL2 | Ubuntu VM |
| --- | --- | --- |
| OS | Ubuntu 24.04.5 LTS under WSL2 | Ubuntu 24.04.5 LTS, Oracle VM |
| Kernel | `6.18.35.2-microsoft-standard-WSL2` | `6.17.0-35-generic` |
| `rhino3d` | `9.0.26257.7309` | `9.0.26257.7309` |
| `rhino-compute` | `9.0.26159.12510-wip` | `9.0.26159.12510-wip` |
| .NET SDK/runtime | 10.0.111 / 10.0.11 | 10.0.111 / 10.0.11 |
| Rhino.Inside package | `9.0.26084.13070-beta` | same |
| RhinoCommon compile package | `9.0.25350.305-wip`, runtime excluded | same |
| Standalone native geometry | `NotLicensedException` | pass |
| Full `.3dm` round trip | blocked before output | pass |
| Compute native geometry API | pass | pass |

`RHINO_TOKEN` is required but must never be committed, echoed, or included in
shared logs or reports.

## Documentation

- `README-rhino-compute-linux-repro.md` — build and run instructions for the new class library and runner.
- `doc/rhino-linux-licensing-retest-2026-09-14.md` — concise developer/forum report, including the successful Compute request and failed standalone test.
- `doc/rhino-inside-linux-prototype-plan.md` — original feasibility and implementation plan.
- `doc/rhino-inside-linux-next-steps.md` — earlier investigation notes.
- `doc/rhino-wsl-vs-ubuntu-vm-licensing-findings-2026-09-14.md` — platform comparison and diagnostic sequence.
- `doc/ubuntu-vm-compatibility-retest-2026-09-14.md` — verified full Ubuntu VM pass, hashes, and Snap runtime caveat.
- `doc/wsl-ubuntu-test-progress.md` — focused WSL2/Ubuntu VM session handoff and copyable resume commands.
- `doc/wsl-live-retest-2026-09-14.md` — live non-Snap WSL retest results and the original trace-capture handoff.
- `doc/wsl-minimal-host-strace-2026-09-15.md` — completed WSL trace, high-level VM comparison, and remaining exact-diff action.
- `doc/wsl-ubuntu-comparison-tooling-2026-09-15.md` — corrected identity probe and reusable protected-trace comparison commands.

## Next steps

1. Run `tools/collect-rhino-compat-context.sh` in unrestricted WSL to replace
   the earlier unreliable `test -s` boot-ID result with a read-based presence
   check that does not print the identifier.
2. Compare the reviewed WSL trace with the protected successful Ubuntu VM raw
   trace using `tools/compare-rhino-straces.py`. Sanitize selected evidence
   before sharing it; neither raw trace should be committed.
3. If that exact comparison identifies no material network, access, or path
   difference, send the minimal reproducer and VM-pass/WSL-fail comparison to
   McNeel as evidence of a probable WSL2 standalone licensing defect.
4. Keep the Snap `GLIBCXX` loader error, WSL2 licensing error, and Compute API
   results separate in all reporting.

## Known limitations

- Rhino on Linux is being evaluated as WIP/prototype software, not a production target.
- The standalone tests target Linux x86-64.
- The token is deliberately excluded from source, documentation, and saved command output.
- The old workspace helper scripts and local Compute source checkout are not under this moved project root. This progress file documents the moved C# test cases and uses direct `dotnet` commands against the canonical solution paths above.
