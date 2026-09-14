# Rhino 9 Linux licensing retest — standalone Rhino.Inside fails; Rhino.Compute API succeeds

**Test date:** September 14, 2026  
**Platform:** Linux x64 (Ubuntu 24.04 under WSL2)  
**Purpose:** Provide a concise, reproducible report for Rhino developers without exposing the Core-Hour token.

## Summary

There are two different outcomes on this machine:

1. A standalone .NET 10 process that hosts Rhino through `Rhino.Inside` can construct `RhinoCore`, but its first native RhinoCommon geometry operation throws `Rhino.Runtime.NotLicensedException` and terminates the process with exit code `134`.
2. The installed `rhino.compute` service starts successfully with one `compute.geometry` child. A real HTTP geometry request returns `200 OK` and a serialized mesh.

Therefore, this retest **does not reproduce the licensing error through the normal Rhino.Compute HTTP API**. The error is currently reproducible in the standalone Rhino.Inside host path. The discrepancy is the issue to investigate.

## Components tested

| Component | Version / configuration |
| --- | --- |
| Local .NET SDK | 10.0.111 |
| Rhino.Inside NuGet package used by standalone test | 9.0.26084.13070-beta |
| RhinoCommon compile-time package used by standalone test | 9.0.25350.305-wip; runtime assets excluded |
| Installed Rhino reported by Compute | 9.0.26257.1000 |
| Installed Compute reported by service | 9.0.0.0 |
| Rhino system directory | `/usr/lib/rhino3d` |
| Compute configuration | one child, Grasshopper disabled, loopback-only parent on `127.0.0.1:5058` |

A non-empty `RHINO_TOKEN` was loaded from a local, mode-`0600` file for the Rhino processes. Its value was neither printed nor saved in the test artifacts.

## Reproduction A — failing standalone Rhino.Inside process

Command:

```bash
./scripts/run-minimal-host.sh --framework-dependent
```

Observed checkpoints:

```text
CHECKPOINT: Resolver initialized from /usr/lib/rhino3d.
CHECKPOINT: RhinoCore constructed.
CHECKPOINT: RHINO_TOKEN cleared from this process environment.
CHECKPOINT: Starting deterministic native Brep.CreateFromBox operation.
Unhandled exception. Rhino.Runtime.NotLicensedException: Exception of type 'Rhino.Runtime.NotLicensedException' was thrown.
    at Rhino.Runtime.NotLicensedException.ThrowNotLicensedException(...)
    at Rhino.Runtime.HostUtils.ExecuteNamedCallbackHelper(...)
```

Result: exit code `134`.

The full project was also run again:

```bash
./scripts/run-box-sample.sh artifacts/retest-extruded-box.3dm
```

It also exited `134` with the same exception. No `artifacts/retest-extruded-box.3dm` was created.

## Reproduction B — working Rhino.Compute API request

The installed Compute parent and one child were started on the loopback interface. The following endpoints succeeded:

```text
GET  /healthcheck                         -> Healthy
GET  /version                             -> {"rhino":"9.0.26257.1000","compute":"9.0.0.0","git_sha":null}
GET  /activechildren?initialize=false     -> 1
```

The native-geometry API request below also succeeded:

```bash
curl --silent --show-error --max-time 60 \
  -H 'Content-Type: application/json' \
  --data '[{"Min":{"X":0,"Y":0,"Z":0},"Max":{"X":10,"Y":20,"Z":30}},1,1,1]' \
  http://127.0.0.1:5058/rhino/geometry/mesh/createfrombox-boundingbox_int_int_int
```

Result: HTTP `200`; response was a non-empty serialized `Rhino.Geometry.Mesh` object. The parent log recorded the request as `200` in approximately 97 ms.

The service and child were then stopped through `POST /shutdown-children`; the response reported `{"shutdown":[6001],"active":0}`.

## Important interpretation

The Compute request performs native geometry (`Mesh.CreateFromBox`) in a running `compute.geometry` child and succeeds. It is therefore not evidence that Compute's public API has the same licensing failure.

The minimal standalone test is intentionally smaller and more useful for diagnosing the failure:

1. initialize `RhinoInside.Resolver` with `/usr/lib/rhino3d`;
2. create a headless `RhinoCore`;
3. invoke `Brep.CreateFromBox(new BoundingBox(0, 0, 0, 10, 20, 30))`.

`RhinoCore` construction succeeds, but the first native geometry call invokes the licensing callback and terminates with `Rhino.Runtime.NotLicensedException`.

## Question for Rhino developers

What initialization, licensing, or process-hosting step performed by the normal Linux `compute.geometry` startup makes native RhinoCommon geometry licensed/usable, while a standalone Rhino.Inside .NET 10 host with the same installed Rhino system directory and inherited Core-Hour token fails at its first `Brep.CreateFromBox` call?

The previous investigation already tested direct startup checkpoints before and after token clearing, exception-handler registration, command-plugin loading, RhinoCode loading, Compute extension-plugin loading, and generic ASP.NET `ApplicationStarted`. Those standalone/checkpoint tests still failed. The normal installed Compute child, by contrast, starts and serves native geometry successfully.

## Artifacts retained locally

- Standalone minimal-host output: `artifacts/retest-minimal-host.stdout.log` and `artifacts/retest-minimal-host.stderr.log`
- Full sample output: `artifacts/retest-box-sample.stdout.log` and `artifacts/retest-box-sample.stderr.log`
- Installed Compute health/version/request/shutdown logs: `artifacts/retest-installed-compute/`

These artifacts contain no token value.

## C# forum demonstrator

A copyable C# demonstrator was added to the requested solution location:

```text
/mnt/c/Users/WegewitzSte/Desktop/temp/RhinoLinuxTest/RhinoLinxTest/
├── RhinoComputeLinuxRepro/             # reusable API-call class library
└── RhinoComputeLinuxRepro.Runner/      # console runner for both test modes
```

The runner was built successfully and re-tested on September 14, 2026:

```text
--mode compute-api --url http://127.0.0.1:5059/
HTTP 200 (OK)
Serialized mesh returned: True

--mode standalone
CHECKPOINT: Resolver initialized from /usr/lib/rhino3d.
CHECKPOINT: RhinoCore constructed.
CHECKPOINT: Calling deterministic Brep.CreateFromBox operation.
Unhandled exception. Rhino.Runtime.NotLicensedException: ...
```

See `README-rhino-compute-linux-repro.md` in that solution for build and run commands. The console runner deliberately has two modes so that a forum post can demonstrate both the successful Compute API request and the failing standalone Rhino.Inside call without conflating them.
