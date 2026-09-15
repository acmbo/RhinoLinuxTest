# WSL2 versus Ubuntu VM — Test Progress and Session Handoff

_Last updated: September 15, 2026_

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

**The observed pass/fail remains Ubuntu-VM-pass versus WSL2-fail, but the exact
trace comparison found that per-user licensing state was not equivalent. A
WSL2-specific defect is therefore not yet proven.**

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

### Remaining technical steps: 1

- [x] **1. Re-run the focused tests from an unrestricted WSL host context.**
- [x] **2. Capture and compare the WSL installed-runtime hashes.**
- [x] **3. Verify the WSL .NET host and native library resolution.**
- [x] **4. Capture the matching WSL `strace` because licensing still fails.**
- [x] **5. Re-run the machine-identity availability probe in WSL using the
  read-based collector.** The September 15 run confirmed that the boot ID is
  present, readable, and non-empty; the tested DMI paths are absent. This
  replaces the earlier inconclusive `test -s` result.
- [x] **6. Compare and sanitize the protected raw VM/WSL traces by exact path and
  syscall context.** The comparison found no internet or access-denied failure,
  but it did find an existing VM `.lic`/keypair cache versus an absent WSL
  `<HOME>/.config` directory.
- [ ] **7. Repeat the minimal-host test with equivalent clean per-user licensing
  state on both systems.** Follow
  `directTests/clean-state-rhino-licensing-test.md`; do not copy licensing files
  between machines.

Keep the Snap loader error, WSL licensing failure, and Compute API result
separate in all reports.

## Latest live WSL retest

A live retest on September 14, 2026 used the native-ext4 checkout at
`/home/dev/RhinoOnLinux`, whose focused `MinimalHost` and `BoxSample` source
files matched this repository byte-for-byte. The local non-Snap .NET SDK
`10.0.111` was used. The token-visibility check exited `0`; the minimal host
again reached the first native `Brep.CreateFromBox` call and exited `134` with
`Rhino.Runtime.NotLicensedException`; and the full box workflow also exited
`134` before producing a `.3dm`.

All three installed-runtime hashes exactly match the Ubuntu VM baseline. The
resolved C++ runtime is the normal `/lib/x86_64-linux-gnu/libstdc++.so.6`, which
provides `GLIBCXX_3.4.32` and `GLIBCXX_3.4.33`; this is not the VM Snap loader
failure. That interrupted attempt was superseded on September 15 by a clean
rootless `strace` capture of the same minimal host. The failure again exited
`134`; the selected syscalls recorded no `connect`, `EACCES`, or `EPERM` result
and 135 `ENOENT` candidate-path results across 25 trace files. The raw WSL
capture is locally retained in the Git-ignored native-ext4 directory
`/home/dev/RhinoOnLinux/artifacts/wsl-minimal-host-strace-2026-09-15/`. It is
not retained on this Windows-mounted repository checkout.

See `wsl-minimal-host-strace-2026-09-15.md` for the result and high-level VM
comparison. The repository now includes `tools/compare-rhino-straces.py` for the
exact reviewed path-level comparison and `tools/collect-rhino-compat-context.sh`
for the corrected boot-ID probe. That probe completed on September 15: it
confirmed a readable, non-empty WSL boot ID and absent tested DMI paths, without
printing any identifier. The protected VM trace was subsequently imported and
compared. The passing VM read an existing per-user `.lic` and keypair; the
failing WSL user's `<HOME>/.config` directory was absent. The remaining action
is a clean-state licensing comparison. Raw traces must still be reviewed before
sharing because they can include local paths, hostnames, and IP addresses.

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

Steps 1 through 7 below remain the clean full-reproduction procedure. They were
completed in the latest live retest using existing matching Release outputs;
restore/build was not repeated. Step 8 was completed on September 15 with the
safer temporary rootless tracer described in
`wsl-live-retest-2026-09-14.md`. The raw-trace comparison is complete; resume
with the equivalent clean-user or isolated-configuration test described in
`vm-wsl-strace-exact-comparison-2026-09-15.md`.

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

Answers now established:

1. **Yes.** WSL and VM have identical recorded SHA-256 values for `RhinoCommon.dll`,
   `libRhinoLibrary.so`, and Compute's `Rhino.Inside.dll`.
2. **Yes.** The retest used local non-Snap .NET SDK `10.0.111`, and the resolved
   system `libstdc++.so.6` provides `GLIBCXX_3.4.32` and newer.
3. **Yes.** The unrestricted WSL retest still failed at the first native geometry
   call with `NotLicensedException`, exit `134`.

Additional trace answers now established:

4. **No.** The WSL trace contains no `AF_INET`/`AF_INET6` connect and, in fact,
   no `connect` call. The VM trace also contains no internet connect; its four
   connects are local Unix-domain sockets.
5. **No at the aggregate level.** Both traces contain zero `EACCES` and zero
   `EPERM`. WSL has 135 `ENOENT` results versus the VM's 189, so the count alone
   is not a causal difference.

6. **Yes, a material difference was found.** The passing VM read an existing
   per-user `.lic` and keypair and updated Cloud Zoo state. The failing WSL
   process found `<HOME>/.config` absent and never accessed equivalent license
   state.

The remaining question is whether the VM can establish a license from equivalent
clean per-user state while WSL cannot. Until that control is run, the traces do
not prove a WSL2-specific defect.

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
- `doc/wsl-live-retest-2026-09-14.md` — latest WSL retest and original trace-capture handoff.
- `doc/wsl-minimal-host-strace-2026-09-15.md` — completed WSL trace and high-level comparison.
- `doc/wsl-ubuntu-comparison-tooling-2026-09-15.md` — corrected identity probe and exact trace-diff tooling.
