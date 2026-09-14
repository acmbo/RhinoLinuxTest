# Rhino Compute Linux reproduction helper

This solution now contains a **new class library**, `RhinoComputeLinuxRepro`, plus a small console runner, `RhinoComputeLinuxRepro.Runner`.

It intentionally distinguishes two tests with platform-dependent results verified on September 14, 2026:

- **`compute-api`** sends a real native-geometry request to a running Linux Rhino.Compute service. It succeeds with HTTP 200 and a serialized mesh in both tested environments.
- **`standalone`** starts Rhino through Rhino.Inside and calls `Brep.CreateFromBox`. It succeeds in the full Ubuntu VM but reaches `RhinoCore` and then terminates with `Rhino.Runtime.NotLicensedException` (exit 134) under WSL2.

Do not describe the working Compute HTTP request as an API failure, and do not describe standalone Rhino.Inside as generally broken on Linux. The current contrast is Ubuntu VM pass versus WSL2 failure for the standalone host.

## Build

Run from this solution directory on Linux:

```bash
dotnet build RhinoLinxTest.sln --configuration Release
```

The runner targets .NET 10 and references the same Rhino.Inside/RhinoCommon package versions as the standalone reproduction. RhinoCommon is compile-only; the standalone mode resolves the installed Rhino runtime through Rhino.Inside.

Use a normal system or local .NET 10 installation. On the tested Ubuntu VM, the Snap-managed `dotnet` host injected Core 22's older `libstdc++.so.6`, causing a pre-start `GLIBCXX_3.4.32` error. That native-loader failure is unrelated to Rhino licensing.

## 1. Test a running Rhino.Compute service

Start your installed Compute parent separately, for example on loopback port 5058, with a valid `RHINO_TOKEN` inherited by the service process. Then run:

```bash
dotnet run --project RhinoComputeLinuxRepro.Runner -- \
  --mode compute-api \
  --url http://127.0.0.1:5058/
```

The library sends exactly this API request:

```text
POST /rhino/geometry/mesh/createfrombox-boundingbox_int_int_int
Content-Type: application/json

[{"Min":{"X":0,"Y":0,"Z":0},"Max":{"X":10,"Y":20,"Z":30}},1,1,1]
```

A successful result reports HTTP 200 and `Serialized mesh returned: True`.

## 2. Compare standalone Rhino.Inside behavior

On Linux, set `RHINO_TOKEN` without echoing it and point `RHINO_SYSTEM_DIR` to the installed Rhino directory if it is not `/usr/lib/rhino3d`:

```bash
export RHINO_TOKEN='...redacted...'
export RHINO_SYSTEM_DIR=/usr/lib/rhino3d

dotnet run --project RhinoComputeLinuxRepro.Runner -- --mode standalone
```

Expected WSL2 failure output:

```text
CHECKPOINT: Resolver initialized from /usr/lib/rhino3d.
CHECKPOINT: RhinoCore constructed.
CHECKPOINT: Calling deterministic Brep.CreateFromBox operation.
Unhandled exception. Rhino.Runtime.NotLicensedException: ...
```

The exception is raised by the native licensing callback and may terminate the process with exit code 134 before managed exception handling can continue. On the verified Ubuntu VM, the same command exits `0` and reports successful standalone geometry. The token is never printed by this sample.
