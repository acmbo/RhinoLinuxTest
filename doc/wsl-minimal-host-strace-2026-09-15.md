# WSL2 minimal-host syscall trace — September 15, 2026

## Scope

This follow-up completes the clean WSL2 trace capture left outstanding by
`wsl-live-retest-2026-09-14.md`. It traces only the minimal standalone
Rhino.Inside reproduction, not the installed Compute service.

The trace was run from the native-ext4 checkout at `/home/dev/RhinoOnLinux`
with its local non-Snap .NET `10.0.111` host. Before execution, the current
repository's `RhinoInside.MinimalHost/Program.cs` and
`RhinoInside.MinimalHost.csproj` were verified byte-for-byte identical to the
files in that checkout. The protected local environment file was loaded before
`strace` started; the token was checked for presence only and was never printed
or passed as a command-line argument.

A temporary rootless `strace` package was downloaded and extracted beneath
`/tmp`; no system package or installed runtime file was modified. The tracer
was invoked with:

```text
-ff -s 256 -e trace=connect,openat,access,statx,readlink
```

The raw local capture has been preserved in the Git-ignored native-ext4
checkout directory
`/home/dev/RhinoOnLinux/artifacts/wsl-minimal-host-strace-2026-09-15/`. It
contains local paths and other machine-specific details and must be reviewed and
sanitized before being shared. It was deliberately not retained on this
Windows-mounted repository checkout, where restrictive Unix file modes are not
effective.

## Result

The traced application reproduced the established standalone failure:

```text
Resolver initialized                 pass
RhinoCore constructed                pass
Brep.CreateFromBox                   Rhino.Runtime.NotLicensedException
Application exit code                134
```

The capture contains 25 per-process/thread trace files totaling 101,397 bytes.
The selected-syscall summary is:

| Observation | WSL2 result |
| --- | ---: |
| `connect` calls | 0 |
| `AF_INET` / `AF_INET6` connects | 0 |
| `AF_UNIX` connects | 0 |
| `EACCES` results | 0 |
| `EPERM` results | 0 |
| `ENOENT` results | 135 |
| `ENOENT` from `openat` | 117 |
| `ENOENT` from `access` | 16 |
| `ENOENT` from `readlink` | 2 |
| Distinct string paths in `ENOENT` calls | 125 |

The only other observed error class was `EINVAL` from `readlink` (552 calls).
It is distinct from an access-denied result and is not evidence of a permission
failure.

The raw trace was checked for path strings matching token/credential markers;
none were found. This is a limited hygiene check, not a substitute for a manual
review before disclosure.

## High-level comparison with the Ubuntu VM baseline

The September 14 Ubuntu VM trace used the same syscall filter and completed the
same minimal-host workflow successfully. Its retained summary can therefore be
compared with this WSL2 failure:

| Observation | Ubuntu VM pass | WSL2 failure |
| --- | ---: | ---: |
| Application exit code | 0 | 134 |
| Trace files | 21 | 25 |
| `connect` calls | 4 | 0 |
| `AF_INET` / `AF_INET6` connects | 0 | 0 |
| `AF_UNIX` connects | 4 | 0 |
| `EACCES` / `EPERM` results | 0 / 0 | 0 / 0 |
| `ENOENT` results | 189 | 135 |

The VM's four connects were local Unix-domain tracing/name-service sockets, not
internet connections. Consequently, this limited trace does **not** expose a
failed outbound TCP connection or an access-denied syscall that explains the
WSL licensing failure. The different number of Unix-domain connects and
candidate-path probes alone is not causal evidence.

## Corrected WSL identity follow-up

On September 15, the read-based compatibility collector was run in WSL from a
token-bearing process with the native local .NET `10.0.111` host on `PATH`. It
confirmed that `/proc/sys/kernel/random/boot_id` is present, readable, and
non-empty; `/sys/class/dmi/id` and the tested DMI product/vendor paths are
absent. This corrects the earlier invalid inference that the WSL boot ID was
empty from a procfs `test -s` check. The collector did not print any identifier
or token value.

## Exact comparison result

The protected VM trace was imported into the Git-ignored `artifacts/` directory
and compared with the WSL trace using additional mappings for both application
outputs and .NET roots. The normalized output contains no local home path,
Windows username, token/authorization marker, IPv4-like string, or unnormalized
GUID.

The comparison found a material licensing-state mismatch. The passing VM
successfully opened an existing per-user `.lic` file and Rhino keypair beneath
`<HOME>/.config/McNeel/Rhinoceros/6.0/License Manager/Licenses/`, then updated
Cloud Zoo state. The failing WSL process loaded `CloudZooClient.dll`, but
`<HOME>/.config` returned `ENOENT`; it never reached equivalent cached license
state.

This means the VM trace is not a clean token-only baseline. The next controlled
test must use fresh users or isolated per-user configuration on both platforms.
See `vm-wsl-strace-exact-comparison-2026-09-15.md` for the reviewed findings.
Keep both raw captures out of Git and never include or copy licensing files.
