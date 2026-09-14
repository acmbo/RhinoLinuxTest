# Rhino 9 Linux licensing: WSL2 versus an Ubuntu VM

**Date:** September 14, 2026  
**Observed platforms:** Ubuntu 24.04 under WSL2 and a full Ubuntu virtual machine  
**Scope:** Standalone Rhino.Inside host using Core-Hour Billing through `RHINO_TOKEN`

## Summary

The standalone Rhino.Inside test fails under WSL2 with
`Rhino.Runtime.NotLicensedException` on its first native RhinoCommon geometry
operation. The same application runs substantially further in a full Ubuntu VM:
it creates geometry, writes a non-empty `.3dm`, and reaches the file read-back
validation.

This does not look like a missing environment variable or invalid geometry
algorithm. Existing tests show that:

1. `RHINO_TOKEN` reaches the standalone .NET process.
2. `RhinoInside.Resolver` initializes from `/usr/lib/rhino3d`.
3. `RhinoCore` is constructed successfully.
4. The failure occurs when the first licensed native geometry operation runs.
5. Framework-dependent and self-contained standalone deployments both fail in
   WSL2.
6. The installed Rhino.Compute process can perform native geometry successfully
   in the same WSL2 distribution.
7. The standalone application can perform licensed geometry in a full Ubuntu VM.

The most likely remaining explanations are an execution sandbox or network
restriction, a Rhino package/version difference, or a WSL-specific limitation or
bug in the current Linux licensing path. Machine identity, user/cache state, TLS
configuration, proxy behavior, and clock drift remain secondary possibilities.

## Verified Ubuntu VM baseline

A controlled retest on September 14, 2026 completed the entire standalone
workflow on the Ubuntu VM. `RhinoInside.MinimalHost`, the standalone runner, and
the box sample all exited `0`. The box sample created and read back a valid solid
`10 × 20 × 30` Brep with volume `6000` in a 23,580-byte `.3dm`. The installed
Compute API also returned `200 OK` with a serialized mesh.

The VM uses the same recorded `rhino3d` (`9.0.26257.7309`) and `rhino-compute`
(`9.0.26159.12510-wip`) package versions as WSL2, and its Rhino.Inside binary
has the already-matching SHA-256. Package version and Rhino.Inside provenance
are therefore no longer leading explanations. WSL2 hashes for RhinoCommon and
the native Rhino library are still needed for a complete binary comparison.

The VM's Snap-managed `dotnet` initially caused a separate pre-start
`GLIBCXX_3.4.32` loader failure. Bypassing Snap's Core 22 C++ runtime made every
standalone test pass. This loader failure is distinct from the WSL2
`NotLicensedException`, which occurs only after resolver initialization and
RhinoCore construction. See
[`ubuntu-vm-compatibility-retest-2026-09-14.md`](ubuntu-vm-compatibility-retest-2026-09-14.md)
for exact results and hashes.

A narrow successful VM `strace` contained no internet-socket `connect` call and
no `EACCES` or `EPERM`. This weakens the claim that every successful standalone
startup must establish a new outbound TCP connection, although cached licensing
state or untraced behavior remains possible. A matching WSL2 trace is required
before drawing a licensing-network conclusion.

## What `NotLicensedException` means here

The error does not necessarily mean that Rhino cannot read `RHINO_TOKEN`.
Preflight and in-process checks have already shown that the variable is present.

A more precise interpretation is:

> RhinoCore starts, but Rhino's licensing subsystem does not establish a valid
> Core-Hour Billing session before the first licensed native operation is
> executed.

On Linux, the current Rhino WIP/Beta uses Core-Hour Billing. If the licensing
service cannot be reached or initialization fails, Rhino may defer the visible
failure until a native geometry operation requests a valid license.

The successful Ubuntu VM run is important evidence: it shows that the token,
application structure, resolver initialization, and core geometry workflow can
work outside WSL2.

## Current WSL2 software observed

The WSL2 environment inspected on September 14, 2026 reported:

```text
Ubuntu              24.04.5 LTS
Kernel               6.18.35.2-microsoft-standard-WSL2
rhino3d              9.0.26257.7309
rhino-compute        9.0.26159.12510-wip
```

The projects currently reference:

```text
Rhino.Inside         9.0.26084.13070-beta
RhinoCommon          9.0.25350.305-wip
```

`RhinoCommon` runtime assets are excluded from the project package, so the
application loads the installed runtime copy from `/usr/lib/rhino3d`.

Before attributing the behavior to WSL2, the WSL2 distribution and Ubuntu VM
must be confirmed to use the same package versions and binaries.

## Most likely causes

### 1. Restricted execution or outbound network access

This is the first condition to exclude.

Core-Hour Billing depends on online licensing. A process can contain a valid
`RHINO_TOKEN` and still fail if it cannot establish the required outbound
connection.

This is particularly relevant when the WSL2 test is started through:

- an automated coding agent;
- an IDE sandbox;
- a container;
- a restricted CI runner;
- a shell with filtered proxy variables;
- a process subject to seccomp or network namespace restrictions.

The Codex execution context used during part of the investigation is itself a
sandboxed process with restricted network access. This does not prove that all
previous WSL2 runs were sandboxed, but it means a result produced inside that
context must be repeated manually from a normal WSL terminal.

Run the minimal host directly from an ordinary WSL terminal:

```bash
cd ~/RhinoOnLinux
source .env.local

test -n "$RHINO_TOKEN" && echo "RHINO_TOKEN is present"

./scripts/run-minimal-host.sh --framework-dependent
```

Do not print the token.

Check basic DNS and HTTPS independently:

```bash
getent ahosts accounts.rhino3d.com

curl --fail --silent --show-error \
  --output /dev/null \
  https://accounts.rhino3d.com/ \
  && echo "Rhino HTTPS connectivity succeeded"
```

This URL is a general Rhino HTTPS test and is not asserted to be the exact
Core-Hour Billing endpoint.

If the standalone host works in a normal WSL terminal but fails when launched
through an agent or IDE, the execution sandbox is the probable cause.

- USER: Tested getent ahosts. This works in WSL and  you get Rhino HTTPS connectivity

### 2. Proxy, VPN, firewall, DNS, or TLS differences

WSL2 networking is not identical to networking in a conventional Ubuntu VM.
Typical differences include:

- WSL2 normally uses a Microsoft-managed utility VM with NAT networking.
- A Windows proxy configuration might not be inherited by Linux processes.
- A corporate VPN might route VM and WSL2 traffic differently.
- Windows Firewall or endpoint security might treat WSL processes differently.
- A corporate HTTPS interception certificate might be installed in Windows but
  not in Ubuntu's CA store.
- DNS might resolve successfully while outbound TLS is blocked or intercepted.

Compare proxy and TLS configuration on WSL2 and the VM:

```bash
env | grep -iE '^(http|https|all|no)_proxy='

dpkg-query -W ca-certificates openssl
openssl version
```

If the organization uses HTTPS interception, install the organization root CA
in Ubuntu's certificate store rather than relying only on the Windows
certificate store.

Recent WSL releases support mirrored networking and improved DNS/proxy
integration. If network diagnostics demonstrate a WSL networking problem,
consider the following Windows `%UserProfile%\.wslconfig` configuration:

```ini
[wsl2]
networkingMode=mirrored
dnsTunneling=true
autoProxy=true
```

After changing it, run from PowerShell:

```powershell
wsl --update
wsl --shutdown
```

Do not change WSL networking merely as a licensing workaround without first
confirming a connectivity difference.

### 3. Different Rhino, Rhino.Compute, or .NET versions

Linux support is currently WIP/Beta and the packages change frequently. The VM
may contain a bug fix that is absent from the WSL installation, or the WSL
installation may have a mismatched Rhino and Rhino.Compute package pair.

Run this on both machines:

```bash
dpkg-query -W -f='${Package}\t${Version}\n' \
  rhino3d rhino-compute

dotnet --info

sha256sum \
  /usr/lib/rhino3d/RhinoCommon.dll \
  /usr/lib/rhino3d/libRhinoLibrary.so \
  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

The package versions and hashes should be compared before treating the operating
environment as the only variable.

The project package references should also be kept in a compatible release
family with the installed Rhino runtime. All Rhino-related projects in the
solution should be updated consistently rather than updating only one project.

### 4. A WSL-specific standalone Rhino.Inside licensing defect

McNeel's Linux documentation describes the feature as WIP/Beta. Ubuntu Server
24.04 and Amazon Linux 2023 are the primary tested platforms. McNeel staff have
reported successful WSL2 use, but WSL2 is not an officially supported target.

The existing evidence is compatible with a WSL-specific defect because:

- packaged Rhino.Compute can obtain a usable license in WSL2;
- an arbitrary standalone Rhino.Inside host cannot;
- the standalone host can obtain a usable license in a full Ubuntu VM;
- token presence and RhinoCore construction have already been verified.

If unrestricted execution, connectivity, exact package versions, clock, and
permissions are equivalent, this becomes the leading explanation and should be
reported to McNeel with the minimal reproduction.

### 5. Machine identity and virtualization differences

A conventional Ubuntu VM normally exposes ordinary virtual hardware identity,
including DMI/SMBIOS information, a product UUID, virtual adapters, and a stable
boot environment.

WSL2 distributions run inside a Microsoft-managed utility VM and may expose a
different or reduced machine identity. A licensing implementation could
potentially inspect one or more of:

- `/etc/machine-id`;
- `/proc/sys/kernel/random/boot_id`;
- hostname;
- DMI product UUID;
- network adapter identity;
- virtualization/platform classification.

There is currently no evidence that Rhino Core-Hour Billing specifically relies
on these files. This remains a hypothesis rather than a confirmed requirement.
Check availability without publishing the identifier values:

```bash
test -s /etc/machine-id \
  && echo "machine-id present" \
  || echo "machine-id missing"

test -r /sys/class/dmi/id/product_uuid \
  && echo "DMI UUID available" \
  || echo "DMI UUID unavailable"

test -s /proc/sys/kernel/random/boot_id \
  && echo "boot ID available" \
  || echo "boot ID unavailable"

hostname
```

### 6. Clock drift

Token-based authentication can fail when the local UTC time is substantially
incorrect. WSL2 clock drift can occur after the Windows host sleeps or
hibernates.

Check:

```bash
date --utc --iso-8601=seconds
timedatectl status
```

Compare the result with Windows. If it is wrong, run:

```powershell
wsl --shutdown
```

Then restart WSL and repeat the test. Clock drift is less likely if packaged
Rhino.Compute and the standalone host were tested at approximately the same time.

### 7. User, home directory, cache, and permissions

The installed Rhino.Compute service normally runs as the dedicated
`rhino-compute` user, whereas the standalone application runs as the interactive
user. This changes:

- `HOME`;
- XDG configuration and data paths;
- cache ownership;
- temporary directories;
- working directory;
- filesystem permissions.

Look for relevant files and permission errors without changing ownership
indiscriminately:

```bash
find "$HOME/.config" "$HOME/.local/share" "$HOME/.cache" \
  -maxdepth 4 \
  \( -iname '*rhino*' -o -iname '*mcneel*' \) \
  -print 2>/dev/null
```

Do not use `sudo` as a permanent workaround. Running once as root can create
root-owned cache files and make the original problem harder to understand.

### 8. systemd is a lower-probability explanation

A full Ubuntu VM normally uses systemd, while WSL2 can run with or without it.
The packaged Rhino.Compute installation uses a systemd service, but Rhino's
Linux deployment also supports containerized environments where systemd is not
present.

Therefore, systemd itself is unlikely to be a licensing requirement. It may
still indirectly affect the process through its service user, environment file,
network readiness, working directory, or permissions.

## Explanations already weakened by existing tests

The existing WSL2 investigation makes the following explanations less likely:

- invalid box geometry;
- missing Rhino installation;
- missing resolver initialization;
- missing .NET runtime;
- `RHINO_TOKEN` simply absent from the process;
- framework-dependent versus self-contained deployment;
- clearing `RHINO_TOKEN` after constructing RhinoCore;
- generic ASP.NET host startup;
- exception callback registration;
- RhinoCode initialization;
- Compute extension plug-in loading by itself;
- WSL2 being completely unable to run Rhino.

See also:

- [`rhino-linux-licensing-retest-2026-09-14.md`](rhino-linux-licensing-retest-2026-09-14.md)
- [`rhino-inside-linux-next-steps.md`](rhino-inside-linux-next-steps.md)
- [`rhino-inside-linux-prototype-plan.md`](rhino-inside-linux-prototype-plan.md)

## Recommended diagnostic sequence

Perform the following steps in order:

1. Run the minimal standalone host manually in an unrestricted WSL terminal.
2. Verify token presence without printing its value.
3. Verify DNS and TLS connectivity from that exact terminal.
4. Compare `rhino3d`, `rhino-compute`, .NET versions, and binary hashes with the
   working Ubuntu VM.
5. Confirm UTC time on both systems.
6. Compare proxy variables and CA certificates.
7. Compare machine-identity interface availability.
8. Compare process user, `HOME`, XDG paths, and relevant file permissions.
9. Capture sanitized syscall traces on WSL2 and the VM.
10. If no meaningful difference is found, report the minimal reproduction to
    McNeel as a probable WSL2 standalone Rhino.Inside licensing defect.

## Syscall comparison

A narrow `strace` can reveal network failures, missing files, or denied access:

```bash
strace -ff \
  -o /tmp/rhino-license-trace \
  -e trace=connect,openat,access,statx,readlink \
  ./artifacts/minimal-host-self-contained/RhinoInside.MinimalHost
```

Extract likely failures:

```bash
grep -hE 'connect\(|EACCES|EPERM|ENOENT' \
  /tmp/rhino-license-trace* \
  | sort -u
```

Review traces before sharing them. They can contain usernames, filesystem paths,
IP addresses, and details about the local environment. They should not contain
the token value with this restricted syscall selection, but they must still be
reviewed before publication.

## Information to include in a McNeel report

Provide:

- exact test date;
- WSL and VM `uname -a` output;
- `/etc/os-release` from both systems;
- exact `rhino3d` and `rhino-compute` package versions;
- installed binary hashes;
- .NET SDK/runtime information;
- confirmation that `RHINO_TOKEN` is present, without its value;
- the successful Ubuntu VM result;
- the failing WSL2 `NotLicensedException` stack trace;
- whether the WSL test was run from an unrestricted terminal;
- whether packaged Rhino.Compute succeeds in the same WSL environment;
- sanitized network/file syscall differences;
- the minimal standalone project.

The central support question is:

> Does the current Rhino 9 Linux Core-Hour Billing implementation support an
> arbitrary standalone Rhino.Inside host under WSL2, and if so, which licensing
> initialization or host/environment prerequisite differs from a conventional
> Ubuntu VM?

## References

- [Rhino.Compute Linux getting started](https://developer.rhino3d.com/guides/compute/compute-linux-getting-started/)
- [Rhino.Compute development guides](https://developer.rhino3d.com/guides/compute/development/)
- [Rhino.Compute licensing and billing](https://developer.rhino3d.com/guides/compute/compute-licensing-and-billing/)
- [McNeel forum: Rhino/Compute on Linux beta discussion](https://discourse.mcneel.com/t/rhino-beta-feature-rhino-compute-on-linux/217111)
- [Microsoft: Comparing WSL versions](https://learn.microsoft.com/windows/wsl/compare-versions)
- [Microsoft: WSL networking](https://learn.microsoft.com/windows/wsl/networking)
