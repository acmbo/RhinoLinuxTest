# WSL2 versus Ubuntu VM — Test Progress and Session Handoff

_Last updated: September 14, 2026_

## Purpose

This is the session handoff for the Rhino 9 Linux compatibility investigation.
Use it to resume the comparison between:

- **Failing environment:** Ubuntu 24.04 under WSL2.
- **Passing baseline:** Ubuntu 24.04.5 in an Oracle virtual machine.

The question is why a standalone Rhino.Inside process can obtain a usable
Core-Hour Billing license and run native geometry in the Ubuntu VM, but throws
`Rhino.Runtime.NotLicensedException` on its first native geometry operation in
WSL2. Installed Rhino.Compute can execute native geometry in both environments.

## Current conclusion

**The standalone failure is specific to the tested WSL2 environment. It is not a
general Rhino.Inside-on-Linux failure.**

The Ubuntu VM completed all currently implemented tests:

- token visible inside the .NET process;
- Rhino.Inside resolver initialization;
- headless `RhinoCore` construction;
- `Brep.CreateFromBox` native geometry;
- full `10 × 20 × 30` solid creation and validation;
- expected volume of `6000`;
- `.3dm` write and read-back validation;
- installed Rhino.Compute health/version checks;
- Compute `Mesh.CreateFromBox` HTTP request.

The WSL2 standalone process reaches resolver initialization and `RhinoCore`
construction, then aborts at the first native geometry operation with exit code
`134` and `Rhino.Runtime.NotLicensedException`.

## Checklist status

### Completed

- [x] Create a minimal standalone Rhino.Inside reproduction.
- [x] Verify `RHINO_TOKEN` reaches the standalone process without printing it.
- [x] Verify WSL2 resolver initialization succeeds.
- [x] Verify WSL2 `RhinoCore` construction succeeds.
- [x] Reproduce the WSL2 first-native-operation licensing failure.
- [x] Verify installed Rhino.Compute executes native geometry under WSL2.
- [x] Test framework-dependent and self-contained standalone WSL2 deployments.
- [x] Test Compute startup checkpoints and generic ASP.NET startup.
- [x] Establish a full Ubuntu VM passing baseline.
- [x] Verify the VM package versions match the recorded WSL2 versions.
- [x] Verify the Rhino.Inside binary hash matches.
- [x] Capture a narrow successful Ubuntu VM `strace` baseline.
- [x] Identify the unrelated Snap .NET `GLIBCXX_3.4.32` loader problem.

### Remaining technical steps: 5

- [ ] **1. Re-run the focused tests from an unrestricted WSL terminal.**
- [ ] **2. Capture the two remaining WSL installed-runtime hashes.**
- [ ] **3. Verify the WSL .NET host and native library resolution.**
- [ ] **4. If licensing still fails, capture the matching WSL `strace`.**
- [ ] **5. Compare and sanitize the VM/WSL results for a McNeel report.**

A sixth non-test checklist item is to keep the Snap loader error, WSL licensing
failure, and Compute API result separate in all reports.

## Repository layout

The repository-relative layout is portable between the VM and WSL:

```text
RhinoLinuxTest/
├── PROGRESS.md
├── README-rhino-compute-linux-repro.md
├── doc/
│   ├── wsl-ubuntu-test-progress.md              # this handoff
│   ├── ubuntu-vm-compatibility-retest-2026-09-14.md
│   ├── rhino-wsl-vs-ubuntu-vm-licensing-findings-2026-09-14.md
│   └── ...
└── RhinoLinxTest/
    ├── RhinoLinxTest.sln
    ├── RhinoInside.MinimalHost/
    ├── RhinoInside.BoxSample/
    ├── RhinoInside.BoxSolver/
    ├── RhinoComputeLinuxRepro/
    └── RhinoComputeLinuxRepro.Runner/
```

Known checkout paths:

```text
Ubuntu VM: /home/dev/RhinoLinuxTest
Earlier WSL: /mnt/c/Users/WegewitzSte/Desktop/temp/RhinoLinuxTest
```

Prefer a Linux filesystem path such as `~/RhinoLinuxTest` for the next WSL run
if possible, to avoid introducing Windows-mounted filesystem behavior.

## Compared software

| Item | WSL2 | Ubuntu VM |
| --- | --- | --- |
| OS | Ubuntu 24.04.5 LTS | Ubuntu 24.04.5 LTS |
| Kernel | `6.18.35.2-microsoft-standard-WSL2` | `6.17.0-35-generic` |
| Virtualization | WSL2 | Oracle VM |
| `rhino3d` | `9.0.26257.7309` | `9.0.26257.7309` |
| `rhino-compute` | `9.0.26159.12510-wip` | `9.0.26159.12510-wip` |
| .NET SDK | `10.0.111` | `10.0.111` |
| .NET runtime | `10.0.11` | `10.0.11` |
| Rhino.Inside package | `9.0.26084.13070-beta` | same |
| RhinoCommon compile package | `9.0.25350.305-wip` | same |
| Rhino.Inside SHA-256 | matching | `ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7` |
| Standalone geometry | `NotLicensedException` | pass |
| Full `.3dm` round trip | blocked | pass |
| Compute geometry API | pass | pass |

Ubuntu VM installed-runtime hashes:

```text
b220fcc562b0db4107b96193a00f7a6028177d8e9b204b23565a34ca172b2d7d  /usr/lib/rhino3d/RhinoCommon.dll
36e564a59ea5c0f68041e9717178b36c1e2d1025eb4a1eccd7bae57f29fa4def  /usr/lib/rhino3d/libRhinoLibrary.so
ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

## Ubuntu VM baseline results

The successful VM runs used the token configured in
`/etc/rhino-compute/environment`. Its value was never displayed or written to a
log.

```text
Token verification             exit 0
Minimal host                   exit 0
Standalone runner              exit 0
Full box sample                exit 0
Compute healthcheck            Healthy
Compute native geometry API    HTTP 200, serialized mesh returned
```

Full box result:

```text
Rhino version:  9.0.26257.1000
Bounding box:   10 × 20 × 30
Volume:         6000
3dm size:       23,580 bytes
Read-back:      one valid solid Brep
```

Ignored local VM artifacts:

```text
artifacts/ubuntu-vm-retest-2026-09-14/
```

The successful VM syscall trace contains:

```text
Trace files:                    21
connect calls:                  4, all local Unix-domain sockets
AF_INET/AF_INET6 connects:      0
EACCES:                         0
EPERM:                          0
ENOENT:                         189 candidate-path probes
```

Do not interpret the `ENOENT` count alone as a failure. Compare exact paths with
the future WSL trace.

## Important Snap .NET warning

On the VM, `/snap/bin/dotnet` and the underlying Snap .NET 10 host selected:

```text
/snap/core22/current/lib/x86_64-linux-gnu/libstdc++.so.6
```

That library provides symbols only through `GLIBCXX_3.4.30`, while the installed
Rhino native library requires `GLIBCXX_3.4.32`. The resulting error was:

```text
System.DllNotFoundException: Unable to load ... libRhinoLibrary.so
libstdc++.so.6: version `GLIBCXX_3.4.32' not found
```

This happens during `RhinoInside.Resolver.Initialize` and is unrelated to the
WSL licensing failure. Use a normal Microsoft/Ubuntu package or a local
`dotnet-install` .NET 10 deployment. Do not classify a `GLIBCXX` failure as a
licensing result.

## Resume procedure on WSL2

Run the following from a normal, unrestricted WSL terminal rather than an IDE,
agent sandbox, container, or restricted CI process.

### 1. Enter the checkout and load the token

Use the appropriate checkout path:

```bash
cd ~/RhinoLinuxTest
```

Load `RHINO_TOKEN` using the existing secure local method. For example, if an
untracked `.env.local` is used:

```bash
set -a
source /path/to/private/.env.local
set +a

test -n "$RHINO_TOKEN" \
  && echo "RHINO_TOKEN is present" \
  || echo "RHINO_TOKEN is absent"
```

Never print the token, put it on a command line, commit it, or include it in a
shared trace/report.

### 2. Capture platform and package information

```bash
uname -a
cat /etc/os-release
systemd-detect-virt || true

dpkg-query -W -f='${Package}\t${Version}\n' \
  rhino3d rhino-compute ca-certificates openssl

dotnet --info
readlink -f "$(command -v dotnet)"
date --utc --iso-8601=seconds
timedatectl status || true
```

Save the output after reviewing it for local identifiers.

### 3. Capture and compare runtime hashes

```bash
sha256sum \
  /usr/lib/rhino3d/RhinoCommon.dll \
  /usr/lib/rhino3d/libRhinoLibrary.so \
  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

Expected VM values are listed above. Record whether each WSL hash matches.

### 4. Check native library resolution

```bash
ldd /usr/lib/rhino3d/libRhinoLibrary.so

strings /usr/lib/x86_64-linux-gnu/libstdc++.so.6 \
  | grep '^GLIBCXX_' \
  | sort -V \
  | tail
```

The Rhino native library must resolve a `libstdc++.so.6` that provides
`GLIBCXX_3.4.32` or newer. If `dotnet` comes from Snap, record that fact and use
a non-Snap .NET 10 host before evaluating licensing.

### 5. Restore and build the focused projects

From `RhinoLinxTest/`:

```bash
cd RhinoLinxTest

dotnet restore RhinoInside.MinimalHost/RhinoInside.MinimalHost.csproj \
  --ignore-failed-sources

dotnet build RhinoInside.MinimalHost/RhinoInside.MinimalHost.csproj \
  --configuration Release \
  --no-restore

dotnet restore RhinoInside.BoxSample/RhinoInside.BoxSample.csproj \
  --ignore-failed-sources

dotnet build RhinoInside.BoxSample/RhinoInside.BoxSample.csproj \
  --configuration Release \
  --no-restore
```

If the SDK has the same workload-resolver issue observed with the VM Snap, add:

```bash
export MSBuildEnableWorkloadResolver=false
```

If only native apphost generation fails, build DLL outputs with:

```bash
-p:UseAppHost=false
```

### 6. Run the token and standalone tests

```bash
dotnet RhinoInside.BoxSample/bin/Release/net10.0/RhinoInside.BoxSample.dll \
  --verify-token

dotnet RhinoInside.MinimalHost/bin/Release/net10.0/RhinoInside.MinimalHost.dll
```

Expected VM result:

```text
CHECKPOINT: Resolver initialized from /usr/lib/rhino3d.
CHECKPOINT: RhinoCore constructed.
CHECKPOINT: RHINO_TOKEN cleared from this process environment.
CHECKPOINT: Starting deterministic native Brep.CreateFromBox operation.
SUCCESS: Deterministic native geometry operation completed.
```

Previously observed WSL result:

```text
CHECKPOINT: Resolver initialized from /usr/lib/rhino3d.
CHECKPOINT: RhinoCore constructed.
CHECKPOINT: Starting deterministic native Brep.CreateFromBox operation.
Unhandled exception. Rhino.Runtime.NotLicensedException
```

Record the exact exit code:

```bash
echo "$?"
```

### 7. Run the full box workflow

```bash
mkdir -p artifacts/wsl-retest

dotnet RhinoInside.BoxSample/bin/Release/net10.0/RhinoInside.BoxSample.dll \
  --output "$PWD/artifacts/wsl-retest/extruded-box.3dm" \
  --overwrite
```

If it succeeds, record the file size and hash:

```bash
stat artifacts/wsl-retest/extruded-box.3dm
sha256sum artifacts/wsl-retest/extruded-box.3dm
```

### 8. Capture WSL strace only if the failure remains

```bash
mkdir -p artifacts/wsl-retest/strace-minimal-host

strace -ff -s 256 \
  -o artifacts/wsl-retest/strace-minimal-host/trace \
  -e trace=connect,openat,access,statx,readlink \
  dotnet RhinoInside.MinimalHost/bin/Release/net10.0/RhinoInside.MinimalHost.dll \
  > artifacts/wsl-retest/strace-minimal-host/stdout.log \
  2> artifacts/wsl-retest/strace-minimal-host/stderr.log

echo "$?" \
  > artifacts/wsl-retest/strace-minimal-host/exit-code
```

Summarize without printing the token:

```bash
grep -hE 'connect\(|EACCES|EPERM|ENOENT' \
  artifacts/wsl-retest/strace-minimal-host/trace.* \
  | sort -u
```

Review the trace before sharing it. It can contain usernames, paths, IP
addresses, hostnames, cache locations, and other environment details.

## Comparison questions for the next session

Answer these in order:

1. Do WSL and VM have identical RhinoCommon and native Rhino library hashes?
2. Does WSL use a normal .NET 10 host and the correct system `libstdc++`?
3. Does unrestricted WSL still fail at the first native geometry call?
4. Does the WSL trace contain an `AF_INET`/`AF_INET6` connection absent from the
   passing VM trace, and does it fail?
5. Does WSL uniquely encounter `EACCES`, `EPERM`, or important `ENOENT` paths?
6. Are there material differences in `HOME`, XDG directories, licensing caches,
   certificate stores, proxy variables, time, or machine-identity interfaces?

## Reporting rules

Keep these three outcomes separate:

1. **Snap loader error:** resolver initialization fails with missing
   `GLIBCXX_3.4.32`; not a licensing result.
2. **WSL standalone error:** resolver and RhinoCore pass; first native geometry
   operation throws `NotLicensedException`.
3. **Compute result:** normal installed Compute native geometry request succeeds.

Do not include the token value in source, logs, traces, screenshots, issue
reports, or forum posts.

## Related documents

- `PROGRESS.md` — overall project status.
- `README-rhino-compute-linux-repro.md` — runner usage.
- `doc/ubuntu-vm-compatibility-retest-2026-09-14.md` — exact passing VM baseline.
- `doc/rhino-wsl-vs-ubuntu-vm-licensing-findings-2026-09-14.md` — detailed hypotheses and diagnostic sequence.
- `doc/rhino-linux-licensing-retest-2026-09-14.md` — earlier developer-facing reproduction report.
