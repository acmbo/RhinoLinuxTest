# Ubuntu VM versus WSL2 exact syscall comparison — September 15, 2026

## Scope

This report compares the protected raw `strace -ff` captures for the same
minimal standalone Rhino.Inside geometry test:

- Ubuntu VM: application exit `0`, native geometry passed.
- WSL2: application exit `134`, `Rhino.Runtime.NotLicensedException`.

Both captures selected `connect`, `openat`, `access`, `statx`, and `readlink`.
The comparison normalized the different checkout, application-output, .NET,
home, process-ID, CLR shared-memory, and GUID-shaped paths. The imported VM
trace is Git-ignored; the user-requested `tmp/` WSL copy is untracked and must
not be committed or shared without a separate privacy review.

## Aggregate result

| Observation | Ubuntu VM pass | WSL2 failure |
| --- | ---: | ---: |
| Trace files | 21 | 25 |
| Selected failure/connect events | 189 | 135 |
| Distinct selected events | 186 | 125 |
| `AF_INET` / `AF_INET6` connects | 0 | 0 |
| `EACCES` / `EPERM` | 0 / 0 | 0 / 0 |
| Selected `ENOENT` outcomes | 189 | 135 |

The VM's four `connect` calls were failed local Unix-domain LTTng/NSCD probes.
Neither capture shows an internet connection or an access-denied result in the
selected syscall set.

## Material finding: per-user licensing state differs

The successful Ubuntu VM process opened an existing per-user licensing cache:

```text
<HOME>/.config/McNeel/Rhinoceros/6.0/License Manager/Licenses/
```

It then:

- read an existing `<GUID>.lic` file twice;
- opened/created `cloudzoo-9.json`;
- read a per-user Rhino keypair XML file; and
- later created a `<GUID>.tmp` file.

The failing WSL process loaded `/usr/lib/rhino3d/CloudZooClient.dll`, but
`<HOME>/.config` itself returned `ENOENT`. It never accessed the license-cache
directory, a `.lic` file, `cloudzoo-9.json`, or the keypair file in this
capture.

This is a material uncontrolled variable. The successful VM trace is not a
clean token-only licensing baseline: it demonstrates reuse of pre-existing
per-user licensing state. The current VM-pass/WSL-fail result therefore does
not, by itself, prove a WSL2-specific standalone licensing defect.

## Other reviewed differences

- WSL lacks optional LTTng libraries present in the VM. This explains the
  LTTng library/socket divergence but is not direct evidence of a licensing
  cause.
- Both processes successfully read `/etc/machine-id`.
- Neither selected trace references `/proc/sys/kernel/random/boot_id` or the DMI
  paths. The DMI availability difference remains real, but these captures do
  not show Rhino licensing consuming those interfaces.
- VM-only `USER32`, ICU, and Rhino RDK candidate probes occur in the longer
  successful execution path. They should not be treated as causes of the
  earlier WSL licensing abort without a phase-aligned trace.
- WSL's missing `user-dirs.dirs` and `Desktop` are ordinary user-directory
  differences; the licensing-cache difference is the higher-value finding.

## Required controlled retest

Run the minimal host under equivalent clean per-user licensing state on both
systems. The copyable procedure is in
`directTests/clean-state-rhino-licensing-test.md`, with automation at
`../tools/run-rhino-clean-state-test.sh`. Prefer either:

1. a fresh unprivileged user on each system; or
2. a temporarily isolated home/configuration directory that is backed up and
   restored after the test.

Supply the same token through the existing protected method and capture the same
syscalls. Do not delete or copy live `.lic`, keypair, or `cloudzoo-9.json` files
between machines.

Interpretation:

- **VM clean-state pass; WSL clean-state failure:** strong WSL-specific evidence.
- **Both clean-state runs fail:** the earlier VM pass depended on cached
  licensing state rather than establishing the license from the token alone.
- **Both clean-state runs pass:** the WSL user's missing or stale per-user
  licensing initialization was the likely cause rather than WSL2 itself.

## Reviewed temporary outputs

The normalized comparison and a concise findings report were saved under the
user-requested temporary review bundle:

```text
tmp/wsl-review-2026-09-15/
```

They contain no local home path, Windows username, token/authorization marker,
IPv4-like string, or unnormalized GUID. The temporary directory and imported
raw artifacts must not be committed and can be removed after review.
