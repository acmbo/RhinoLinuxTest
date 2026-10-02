# Rhino standalone and Compute compatibility retest on Ubuntu VM

**Date:** September 14, 2026  
**Host:** Ubuntu 24.04.5 LTS, Oracle virtual machine, Linux x86-64  
**Purpose:** Establish a controlled non-WSL baseline for the standalone Rhino.Inside and Rhino.Compute tests.

## Result

Both hosting paths completed native geometry successfully on this Ubuntu VM:

| Test | Result |
| --- | --- |
| Token visibility in the standalone .NET process | Pass, exit `0` |
| `RhinoInside.MinimalHost` / `Brep.CreateFromBox` | Pass, exit `0` |
| `RhinoComputeLinuxRepro.Runner --mode standalone` | Pass, exit `0` |
| Full box creation, validation, `.3dm` write, and read-back | Pass, exit `0` |
| Installed Compute `/healthcheck` | `Healthy` |
| Installed Compute `/version` | Rhino `9.0.26257.1000`, Compute `9.0.0.0` |
| Compute `Mesh.CreateFromBox` HTTP request | `200 OK`, serialized mesh returned |

The full standalone workflow created a valid `10 × 20 × 30` solid with volume
`6000`, wrote a 23,580-byte `.3dm`, reopened it, retrieved the expected object,
and validated the persisted Brep as valid and solid.

This is a stronger VM baseline than the earlier partial report: the complete
write/read-back workflow now passes, not merely initial geometry creation.

## Environment

```text
OS                    Ubuntu 24.04.5 LTS
Kernel                6.17.0-35-generic
Virtualization        oracle
rhino3d               9.0.26257.7309
rhino-compute         9.0.26159.12510-wip
.NET SDK              10.0.111
.NET runtime          10.0.11
Rhino.Inside package  9.0.26084.13070-beta
RhinoCommon package   9.0.25350.305-wip (compile-only)
```

Relevant installed hashes:

```text
b220fcc562b0db4107b96193a00f7a6028177d8e9b204b23565a34ca172b2d7d  /usr/lib/rhino3d/RhinoCommon.dll
36e564a59ea5c0f68041e9717178b36c1e2d1025eb4a1eccd7bae57f29fa4def  /usr/lib/rhino3d/libRhinoLibrary.so
ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

The `rhino3d` and `rhino-compute` package versions match those recorded for the
failing WSL2 installation. The Rhino.Inside hash also matches the earlier WSL2
and installed-Compute hash. This removes package version and Rhino.Inside binary
version as explanations for the observed VM-versus-WSL licensing difference.
The September 14 WSL retest subsequently captured identical SHA-256 values for
`RhinoCommon.dll` and `libRhinoLibrary.so`; the three compared installed runtime
files are now known to be byte-identical across the two tested environments.

DNS and HTTPS to `accounts.rhino3d.com` succeeded with certificate verification,
and the standalone process loaded `RHINO_TOKEN` from the local Compute service
environment without displaying its value.

## Important .NET Snap finding

The first VM run failed before RhinoCore startup with:

```text
/snap/core22/current/lib/x86_64-linux-gnu/libstdc++.so.6:
version `GLIBCXX_3.4.32' not found
```

This was not a Rhino licensing failure. The `dotnet` command resolved to the
Snap-managed .NET 10 host. That executable uses the Core 22 dynamic loader and
RPATH, whose bundled `libstdc++.so.6` only provides symbols through
`GLIBCXX_3.4.30`. The installed Rhino native library requires
`GLIBCXX_3.4.32`, which the Ubuntu VM's system `libstdc++.so.6` provides.

For the controlled retest, a temporary host under `/tmp` combined:

- the system-linked `/usr/share/dotnet/dotnet` muxer;
- the existing .NET 10 SDK/runtime files;
- a copied `libhostfxr.so` with its Snap-only RPATH removed.

No installed files were changed. With Ubuntu's system C++ runtime selected, all
standalone tests passed. The permanent fix should be to install .NET 10 through
a normal Ubuntu/Microsoft package or a local `dotnet-install` deployment rather
than use this temporary host construction.

This runtime-loader issue must be kept separate from the WSL licensing issue:

- Snap-host failure boundary: `RhinoInside.Resolver.Initialize`,
  `DllNotFoundException` / missing `GLIBCXX_3.4.32`.
- WSL2 failure boundary: resolver and RhinoCore succeed, first licensed native
  geometry call throws `Rhino.Runtime.NotLicensedException`.
- Ubuntu VM with system libraries: resolver, RhinoCore, native geometry, and
  `.3dm` round trip all succeed.

## Additional observations

- The VM clock was close to current UTC, but `timedatectl` reported NTP inactive
  and `System clock synchronized: no`. Licensing still succeeded, so an active
  NTP service is not itself required; materially incorrect time remains a valid
  failure hypothesis.
- `/etc/machine-id` was present. A corrected read-based probe on September 15
  found that `/proc/sys/kernel/random/boot_id` is readable and non-empty even
  though procfs reports a zero file size; DMI product name and vendor are also
  readable, while the DMI UUID is present but unreadable to this user. The
  earlier `test -s` boot-ID check was not valid for procfs. The WSL boot-ID must
  be retested with the corrected probe before it is treated as empty.
- The installed Compute service unit was inactive, so the parent was launched
  temporarily on `127.0.0.1:5059` and shut down after the API test.

## Successful syscall baseline

A narrow `strace -ff` of the successful minimal host captured
`connect`, `openat`, `access`, `statx`, and `readlink` across 21 process/thread
trace files. The process still exited `0`. The trace contained four `connect`
calls, all to local Unix-domain tracing/name-service sockets; it contained no
`AF_INET` or `AF_INET6` connection. It also contained zero `EACCES` and zero
`EPERM` results. The 189 `ENOENT` results are normal candidate-path probing until
a WSL diff identifies specific missing paths that occur only in the failing run.

This means the successful VM execution did not require a newly observed outbound
TCP connection during this syscall window. The exact September 15 comparison
subsequently confirmed that reusable local licensing state did matter to the
interpretation: this VM process read an existing per-user `.lic` and keypair and
updated Cloud Zoo state. The failing WSL user's `<HOME>/.config` directory was
absent. The VM trace is therefore not a clean token-only licensing baseline, and
an equivalent clean-user or isolated-configuration retest is required. See
`vm-wsl-strace-exact-comparison-2026-09-15.md`.

## Retained local artifacts

Ignored test artifacts are under:

```text
artifacts/ubuntu-vm-retest-2026-09-14/
```

They include exit codes and stdout/stderr for both failed Snap-loader runs and
successful system-library runs, the generated `.3dm`, and sanitized Compute
health/version/API results. No token value was written to these files.

## Next WSL comparison

Run the same exact binaries and package-hash collection from a normal,
unrestricted WSL terminal:

1. Confirm `dotnet` is not using a runtime that injects an incompatible C++
   library.
2. Capture hashes for WSL2 `RhinoCommon.dll` and `libRhinoLibrary.so`.
3. Run `RhinoInside.MinimalHost` and the full box sample with the same token.
4. If `NotLicensedException` remains, capture a narrow `strace -ff` for
   `connect`, `openat`, `access`, `statx`, and `readlink` on both WSL2 and the VM.
5. Compare failed network calls and missing/denied files, reviewing traces for
   sensitive local data before sharing.
