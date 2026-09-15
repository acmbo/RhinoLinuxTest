# Live WSL2 compatibility retest — September 14, 2026

## Purpose and scope

This record captures the live WSL2 retest performed after the Ubuntu VM baseline
was established. It supplements, rather than replaces,
`wsl-ubuntu-test-progress.md` and
`ubuntu-vm-compatibility-retest-2026-09-14.md`.

The current repository worktree is on the Windows-mounted `/mnt/c` filesystem,
so the executable tests were deliberately run from the existing native-ext4 WSL
checkout at `/home/dev/RhinoOnLinux`. The focused `MinimalHost` and `BoxSample`
program/project source files in that checkout were byte-for-byte identical to
the corresponding files in this repository before execution. Existing Release
outputs were used; restore/build was not repeated in this retest.

`RHINO_TOKEN` was sourced from the existing protected local file only for the
processes that needed it. Its value was neither printed nor saved.

## Live results

| Check | Result |
| --- | --- |
| WSL kernel | `6.18.35.2-microsoft-standard-WSL2` on x86-64 |
| .NET host | `/home/dev/RhinoOnLinux/.dotnet/dotnet`, SDK `10.0.111`; not Snap |
| Token visibility check | Passed, exit `0`; token value not displayed |
| `RhinoInside.MinimalHost` | Resolver and `RhinoCore` passed; native `Brep.CreateFromBox` threw `Rhino.Runtime.NotLicensedException`; exit `134` |
| Full `RhinoInside.BoxSample` workflow | Threw `Rhino.Runtime.NotLicensedException`; exit `134`; no `.3dm` was created |
| Installed Rhino runtime hashes | All three match the recorded Ubuntu VM hashes exactly |
| Native C++ dependency | `libRhinoLibrary.so` resolves `libstdc++.so.6` from `/lib/x86_64-linux-gnu`; the system library provides `GLIBCXX_3.4.32` and `GLIBCXX_3.4.33` |

The minimal host reached these checkpoints in order:

```text
Resolver initialized from /usr/lib/rhino3d.
RhinoCore constructed.
RHINO_TOKEN cleared from this process environment.
Starting deterministic native Brep.CreateFromBox operation.
Rhino.Runtime.NotLicensedException
```

The full box workflow failed before it could write the requested temporary
artifact. These results reproduce the WSL-specific licensing boundary from the
existing reports using the local non-Snap .NET host and an ext4 checkout.

## Verified installed-runtime identity

The live WSL values below match the Ubuntu VM values in
`ubuntu-vm-compatibility-retest-2026-09-14.md`:

```text
b220fcc562b0db4107b96193a00f7a6028177d8e9b204b23565a34ca172b2d7d  /usr/lib/rhino3d/RhinoCommon.dll
36e564a59ea5c0f68041e9717178b36c1e2d1025eb4a1eccd7bae57f29fa4def  /usr/lib/rhino3d/libRhinoLibrary.so
ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

This removes a differing installed Rhino runtime binary, differing
`Rhino.Inside.dll`, a Snap-selected .NET host, and an insufficient system C++
runtime as explanations for this comparison.

## Trace status at stop point

A matching WSL `strace` is the next diagnostic. `strace` was not initially
installed. A non-system `strace` 6.8 executable was downloaded and extracted
under `/tmp/rhino-wsl-trace-tools/`; no system package was installed because
non-interactive `sudo` required a password.

The first trace command was intentionally interrupted before a result was
collected. The stop check found no running `strace`, minimal-host, or related
`dotnet` process, and no temporary `rhino-wsl-minimal-host-strace.*` directory.
There is therefore **no partial trace to interpret or share**.

The extracted tracer is temporary and may disappear when `/tmp` is cleaned. If
it is absent, either install `strace` through the normal WSL package-management
process or repeat the rootless download/extract procedure documented below.

## Exact next action

Run the following only from an unrestricted WSL shell and review the resulting
raw trace locally before sharing it. It does not print the token. The raw trace
may contain paths, hostnames, IP addresses, and other local identifiers.

```bash
cd /home/dev/RhinoOnLinux
export DOTNET_ROOT="$PWD/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$PWD/.dotnet-cli"
export NUGET_PACKAGES="$PWD/.nuget/packages"

set -a
source .env.local
set +a

tracer=/tmp/rhino-wsl-trace-tools/extracted/usr/bin/strace
if ! test -x "$tracer"; then
  # Rootless fallback: downloads and extracts only beneath /tmp; it does not
  # install a system package. Use ordinary package management instead if preferred.
  tools_dir=/tmp/rhino-wsl-trace-tools
  rm -rf "$tools_dir"
  mkdir -p "$tools_dir"
  (
    cd "$tools_dir"
    apt-get download strace
    dpkg-deb -x strace_*.deb extracted
  )
fi

test -x "$tracer" || {
  echo "strace is unavailable; install it or recreate the temporary tracer first." >&2
  exit 2
}

trace_dir=$(mktemp -d /tmp/rhino-wsl-minimal-host-strace.XXXXXX)
"$tracer" -ff -s 256 \
  -o "$trace_dir/trace" \
  -e trace=connect,openat,access,statx,readlink \
  dotnet src/RhinoInside.MinimalHost/bin/Release/net10.0/RhinoInside.MinimalHost.dll \
  >"$trace_dir/stdout.log" \
  2>"$trace_dir/stderr.log"
trace_exit=$?
printf '%s\n' "$trace_exit" >"$trace_dir/exit-code"
printf 'Trace directory: %s\nExit code: %s\n' "$trace_dir" "$trace_exit"
```

Then compare sanitized summaries and exact reviewed paths with the 21-file
passing VM trace. In particular, establish whether WSL has an `AF_INET` or
`AF_INET6` connection, `EACCES`, `EPERM`, or material `ENOENT` paths not present
in the VM trace. Do not treat an `ENOENT` count alone as evidence of a cause.

## Follow-up — September 15 trace capture

The clean trace requested above was completed from the ext4 checkout with the
local non-Snap .NET host. The minimal host reproduced
`Rhino.Runtime.NotLicensedException` and exit `134`. The 25 WSL trace files
recorded zero `connect` calls, zero `EACCES`, zero `EPERM`, and 135 `ENOENT`
results. This provides no selected-syscall evidence of a failed internet
connection or access denial. The raw capture is retained locally in the
Git-ignored native-ext4 directory
`/home/dev/RhinoOnLinux/artifacts/wsl-minimal-host-strace-2026-09-15/`; the
exact path-level diff against the protected VM trace remains outstanding.

See `wsl-minimal-host-strace-2026-09-15.md` for the collection details and the
high-level VM comparison.
