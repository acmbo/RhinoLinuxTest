# WSL2 Rhino licensing diagnostic report

**Collection date:** September 14, 2026  
**Collection time:** approximately 15:21–15:27 CEST  
**Environment:** Ubuntu 24.04.5 LTS under WSL2  
**Purpose:** Capture the current WSL2 state so that the same observations can later be collected and compared in a full Ubuntu VM.

## Executive summary

> **September 15 correction:** the boot-ID classification below used `test -s`.
> That test can report false "empty" results for procfs/sysfs pseudo-files whose
> stat size is zero even though a read returns content. The WSL boot-ID result is
> therefore inconclusive pending a read-based retest with
> `tools/collect-rhino-compat-context.sh`. The missing WSL DMI hierarchy remains
> a valid observed platform difference.

The WSL2 environment passes the basic prerequisites that can be checked without
successfully licensing Rhino:

- `RHINO_TOKEN` is present and non-empty in the compiled .NET application.
- The token file has restrictive `0600` permissions.
- DNS resolution works for Rhino, Cloud Zoo, and NuGet hosts.
- HTTPS and certificate validation succeed for `cloudzoo.rhino3d.com`,
  `accounts.rhino3d.com`, `www.rhino3d.com`, and `developer.rhino3d.com`.
- The system clock reports that it is synchronized.
- `/etc/machine-id` exists and is non-empty.
- The actual Rhino test repository is on the WSL ext4 filesystem, not `/mnt/c`.
- Rhino and Rhino.Compute are installed and their principal binaries are readable.

A fresh, unrestricted WSL execution of the minimal standalone Rhino.Inside host
still fails:

```text
RhinoCore constructed.
Starting deterministic Brep.CreateFromBox operation.
Rhino.Runtime.NotLicensedException
Exit code: 134
```

The most notable WSL-specific observations are:

1. `/proc/sys/kernel/random/boot_id` exists but is an empty zero-byte file.
2. `/sys/class/dmi/id` does not exist, so the usual DMI/SMBIOS machine identity
   values are unavailable.
3. WSL systemd is degraded because `systemd-binfmt.service` failed.
4. Installed and compile-time Rhino components are from different WIP/Beta
   build dates.
5. Basic Cloud Zoo connectivity succeeds, so a general DNS, outbound HTTPS, or
   CA-certificate failure is unlikely. This test does not prove that an
   authenticated Core-Hour Billing request succeeds.

The missing boot/DMI identity surfaces are important items to compare with the
Ubuntu VM. They are not proven Rhino licensing requirements.

## Test execution context

Host, network, package, and application tests were deliberately run outside the
Codex command sandbox so they reflect the actual WSL distribution rather than
Codex's restricted process namespace.

The licensing smoke test was run from:

```text
/home/dev/RhinoOnLinux
```

This location is on WSL's native ext4 filesystem.

No token value was printed or written to this report.

## 1. WSL and operating system

### Result

```text
Distribution name:      claudeDis
WSL generation:         2
WSL version:            2.9.3.0
WSL kernel version:     6.18.35.2-1
Linux kernel:           6.18.35.2-microsoft-standard-WSL2
Architecture:           x86_64
Ubuntu:                 24.04.5 LTS (Noble Numbat)
Windows version:        10.0.22631.7517
Init process:           systemd
systemd version:        255.4-1ubuntu8.17
```

WSL integration markers were present:

```text
WSL_INTEROP:            present
/run/WSL:               present
WSLInterop binfmt:      present
```

### Commands

```bash
uname -a
uname -m
cat /proc/version
cat /etc/os-release
wsl.exe --version
wsl.exe --list --verbose
ps -p 1 -o pid=,comm=,args=
systemctl --version
```

## 2. systemd state

### Result

```text
systemctl is-system-running: degraded
```

One failed unit was reported:

```text
systemd-binfmt.service loaded failed failed Set Up Additional Binary Formats
```

WSL interoperability nevertheless remained available. There is currently no
evidence that this failed unit causes the Rhino licensing failure, but the VM
result should record whether systemd is healthy.

### Commands

```bash
systemctl is-system-running
systemctl --failed --no-pager --plain
```

## 3. Installed Rhino packages

### Result

```text
rhino3d         9.0.26257.7309
rhino-compute   9.0.26159.12510-wip
```

The installed Rhino runtime reported by the existing packaged Compute control
was:

```text
Rhino           9.0.26257.1000
Compute         9.0.0.0
```

### Commands

```bash
dpkg-query -W -f='${Package}\t${Version}\n' \
  rhino3d rhino-compute
```

## 4. Installed binary fingerprints

These hashes should be collected in the Ubuntu VM. Equal hashes establish that
the exact same binaries are being compared.

```text
b220fcc562b0db4107b96193a00f7a6028177d8e9b204b23565a34ca172b2d7d  /usr/lib/rhino3d/RhinoCommon.dll
36e564a59ea5c0f68041e9717178b36c1e2d1025eb4a1eccd7bae57f29fa4def  /usr/lib/rhino3d/libRhinoLibrary.so
ebbf6fc8f215a76ebd20e9b419b42c2fa9f62aeff512289ab7738f4ada1206f7  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

File metadata:

```text
/usr/lib/rhino3d/RhinoCommon.dll
  size:   4,592,128 bytes
  mode:   0644
  owner:  root:root

/usr/lib/rhino3d/libRhinoLibrary.so
  size:   17,440 bytes
  mode:   0644
  owner:  root:root

/usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
  size:   17,920 bytes
  mode:   0755
  owner:  root:root
```

### Commands

```bash
stat -c '%n size=%s mode=%a owner=%U group=%G mtime=%y' \
  /usr/lib/rhino3d/RhinoCommon.dll \
  /usr/lib/rhino3d/libRhinoLibrary.so \
  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll

sha256sum \
  /usr/lib/rhino3d/RhinoCommon.dll \
  /usr/lib/rhino3d/libRhinoLibrary.so \
  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

## 5. .NET SDK and runtime

The system shell did not have a global `dotnet` command on `PATH`. The project
uses its workspace-local SDK.

### Result

```text
SDK:                    10.0.111
MSBuild:                18.0.11
Host/runtime:           10.0.11
Runtime identifier:     linux-x64
Architecture:           x64
Microsoft.NETCore.App:  10.0.11
Microsoft.AspNetCore:   10.0.11
```

SDK path:

```text
/home/dev/RhinoOnLinux/.dotnet
```

### Command

```bash
/home/dev/RhinoOnLinux/.dotnet/dotnet --info
```

## 6. Project package references

The standalone projects compile against:

```text
Rhino.Inside   9.0.26084.13070-beta
RhinoCommon    9.0.25350.305-wip
```

The `RhinoCommon` package excludes runtime assets, so the installed runtime copy
from `/usr/lib/rhino3d` is loaded at execution time.

This creates the following version spread:

```text
Compile-time RhinoCommon:  9.0.25350.305-wip
Rhino.Inside package:      9.0.26084.13070-beta
Installed rhino-compute:   9.0.26159.12510-wip
Installed rhino3d:         9.0.26257.7309
```

This spread may be binary-compatible, but it remains a variable that should be
eliminated or reproduced exactly in the Ubuntu VM.

### Command

```bash
grep -RIn --include='*.csproj' \
  -E 'PackageReference Include="Rhino(Common|\.Inside)"' \
  /home/dev/RhinoOnLinux/src
```

## 7. Token presence and permissions

### Result

```text
/home/dev/RhinoOnLinux/.env.local: present
Permissions:                         0600
Owner:                               dev:dev
RHINO_TOKEN:                         present and non-empty
RHINO_SYSTEM_DIR:                    unset
Effective default system directory: /usr/lib/rhino3d
```

The compiled application token-only check succeeded:

```text
RHINO_TOKEN is present in this .NET process environment.
Exit code: 0
```

This confirms that the failure is not explained by the shell script forgetting
to export the token.

### Commands

```bash
cd /home/dev/RhinoOnLinux
./scripts/run-box-sample.sh --verify-token
```

Do not print or commit `.env.local`.

## 8. Proxy environment

### Result

All checked proxy variables were unset:

```text
HTTP_PROXY   unset
HTTPS_PROXY  unset
ALL_PROXY    unset
NO_PROXY     unset
http_proxy   unset
https_proxy  unset
all_proxy    unset
no_proxy     unset
```

Direct HTTPS nevertheless succeeded, so this WSL environment does not currently
require an explicit proxy for the tested hosts.

## 9. DNS and HTTPS connectivity

All tests below were run outside the Codex network sandbox.

### Result summary

| Host | DNS | HTTPS status | TLS verification | Approx. total time |
| --- | --- | ---: | ---: | ---: |
| `cloudzoo.rhino3d.com` | Passed | 200 | Passed (`0`) | 0.49 s |
| `accounts.rhino3d.com` | Passed | 200 | Passed (`0`) | 0.26 s |
| `www.rhino3d.com` | Passed | 200 | Passed (`0`) | 0.38 s |
| `developer.rhino3d.com` | Passed | 200 | Passed (`0`) | 0.84 s |
| `api.nuget.org/v3/index.json` | Passed | 200 | Passed (`0`) | 0.12 s |

The Cloud Zoo hostname was identified from the installed
`CloudZooClient.dll`. Its DNS and anonymous HTTPS checks both succeeded.

### Interpretation

These results make the following broad explanations unlikely:

- broken WSL DNS;
- all outbound HTTPS being blocked;
- missing standard CA certificates;
- inability to reach the Cloud Zoo host at all.

They do not test an authenticated Core-Hour Billing operation. The licensing
client might use a different endpoint path, protocol exchange, request payload,
or machine/session identity after the initial TLS connection.

### Commands

```bash
getent ahosts cloudzoo.rhino3d.com
getent ahosts accounts.rhino3d.com
getent ahosts www.rhino3d.com
getent ahosts developer.rhino3d.com
getent ahosts api.nuget.org

curl --silent --show-error --location --max-time 20 \
  --output /dev/null \
  --write-out 'http_code=%{http_code} tls_verify=%{ssl_verify_result} total=%{time_total}s\n' \
  https://cloudzoo.rhino3d.com/

curl --silent --show-error --location --max-time 20 \
  --output /dev/null \
  --write-out 'http_code=%{http_code} tls_verify=%{ssl_verify_result} total=%{time_total}s\n' \
  https://api.nuget.org/v3/index.json
```

## 10. TLS and CA configuration

### Result

```text
OpenSSL library:       3.0.13
Ubuntu OpenSSL pkg:    3.0.13-0ubuntu3.15
ca-certificates pkg:  20260601~24.04.1
Local custom CA files: 0
```

All tested HTTPS certificates validated successfully.

The .NET restore still emitted `NU1900` while trying to obtain NuGet
vulnerability data. Because direct access to the exact NuGet service index
returned HTTP 200 with successful TLS verification, this warning is not evidence
of a general WSL network or certificate failure. It may involve a secondary
NuGet audit resource or NuGet/.NET behavior.

## 11. Clock state

### Result

```text
Local time:               2026-09-14 15:21:50 CEST
UTC:                      2026-09-14 13:21:50 UTC
Time zone:                Europe/Vienna
System clock synchronized: yes
NTP service:               active
RTC in local TZ:           no
```

No clock problem was visible during collection. Clock drift is therefore not a
leading explanation for this particular run.

### Commands

```bash
date --iso-8601=seconds
date -u --iso-8601=seconds
timedatectl status
```

## 12. Machine identity interfaces

Values were deliberately not recorded. Only availability was tested.

### Result

| Interface | WSL2 result |
| --- | --- |
| `/etc/machine-id` | Present, readable, non-empty (33 bytes) |
| `/var/lib/dbus/machine-id` | Present, readable, non-empty (33 bytes) |
| `/proc/sys/kernel/random/boot_id` | Present but empty (zero bytes) |
| `/sys/class/dmi/id` | Absent |
| DMI product UUID | Unavailable |
| DMI product name | Unavailable |
| DMI system vendor | Unavailable |
| DMI product/board serial | Unavailable |
| Hostname | Present; value not recorded in this report |

### Interpretation

The missing DMI hierarchy is a concrete WSL2/VM difference. The boot-ID result
was originally classified as empty, but the September 15 correction above makes
that result inconclusive until the file is read rather than checked with
`test -s`.

There is no current proof that Rhino licensing consumes either value. If the VM
has valid boot and DMI identities while the otherwise identical standalone host
licenses correctly, these interfaces become useful evidence for McNeel.

### Commands

```bash
for f in \
  /etc/machine-id \
  /var/lib/dbus/machine-id \
  /proc/sys/kernel/random/boot_id \
  /sys/class/dmi/id/product_uuid \
  /sys/class/dmi/id/product_name \
  /sys/class/dmi/id/sys_vendor; do
  if [ -r "$f" ] && [ -s "$f" ]; then
    echo "$f: present/readable/non-empty"
  elif [ -e "$f" ]; then
    echo "$f: present but empty or unreadable"
  else
    echo "$f: absent"
  fi
done
```

Do not include actual UUID, machine ID, serial, or hostname values in a public
report unless they have been reviewed and intentionally disclosed.

## 13. Filesystem locations

### Result

```text
/                         ext4 on /dev/sdd
/home/dev                 ext4 on /dev/sdd
/home/dev/RhinoOnLinux    ext4 on /dev/sdd
/tmp                      ext4 on /dev/sdd
/mnt/c                    9p/DrvFs Windows mount
Current Windows clone     9p/DrvFs Windows mount
```

The tested application and `.env.local` are under `/home/dev/RhinoOnLinux` on
ext4. Therefore, the standalone licensing failure is not explained by running
the application or secret file directly from the Windows `/mnt/c` filesystem.

### Command

```bash
findmnt -T /home/dev/RhinoOnLinux -no TARGET,SOURCE,FSTYPE,OPTIONS
```

## 14. Rhino/McNeel user-state paths

The following directories exist and are owned by the interactive user:

```text
/home/dev/.local/share/mcneel
/home/dev/.local/share/mcneel/rhinoceros
/home/dev/.local/share/mcneel/rhinoceros/9.0
/home/dev/.cache/mcneel
/home/dev/.cache/mcneel/rhinoceros
/home/dev/.cache/mcneel/rhinoceros/9.0
```

The cache hierarchy was updated during the current test. No obvious licensing
file or permission failure was identified from file names and metadata alone.

## 15. Fresh standalone licensing smoke test

### Command

The following was executed directly in WSL, outside the Codex command sandbox:

```bash
cd /home/dev/RhinoOnLinux
./scripts/run-minimal-host.sh --framework-dependent
```

The script sourced the protected `.env.local`, passed preflight, restored the
already-installed project dependencies, and started the minimal host.

### Result

```text
Preflight passed. Rhino has not been started.
CHECKPOINT: Resolver initialized from /usr/lib/rhino3d.
CHECKPOINT: RhinoCore constructed.
CHECKPOINT: RHINO_TOKEN cleared from this process environment.
CHECKPOINT: Starting deterministic native Brep.CreateFromBox operation.
Unhandled exception. Rhino.Runtime.NotLicensedException
Exit code: 134
```

Relevant stack frames:

```text
Rhino.Runtime.NotLicensedException.ThrowNotLicensedException(...)
Rhino.Runtime.HostUtils.ExecuteNamedCallbackHelper(...)
```

### Interpretation

The current failure is reproducible even when:

- the process runs outside the Codex command sandbox;
- the token reaches the compiled application;
- Cloud Zoo DNS and HTTPS work;
- the system clock is synchronized;
- the project is on WSL ext4;
- `RhinoCore` constructs successfully.

This substantially weakens the simple sandbox, token-absence, general network,
TLS, clock, and Windows-filesystem explanations.

The minimal host deliberately clears `RHINO_TOKEN` after constructing
`RhinoCore` to match Compute's behavior. Previous project tests also showed that
retaining the environment variable did not resolve the failure.

## 16. Packaged Compute control

The packaged Compute control was not started again during this collection.
Existing artifacts from the same WSL installation and the same date show that
it previously succeeded:

```text
GET  /healthcheck  -> Healthy
GET  /version      -> Rhino 9.0.26257.1000, Compute 9.0.0.0
POST Mesh.CreateFromBox endpoint -> HTTP 200
```

Existing artifact directory:

```text
/home/dev/RhinoOnLinux/artifacts/retest-installed-compute/
```

A future comparison may rerun this control immediately before or after the
standalone host. The report must distinguish a newly executed result from this
retained artifact.

## 17. Current conclusion

The current evidence supports this narrowed conclusion:

> WSL2 can reach Rhino's Cloud Zoo host and packaged Rhino.Compute has performed
> licensed geometry, but an arbitrary standalone Rhino.Inside host still fails
> its first native geometry operation with `NotLicensedException`.

The leading unresolved possibilities are:

1. a standalone Rhino.Inside licensing/host-initialization difference in the
   current Linux Beta;
2. a WSL-specific interaction with unavailable boot/DMI machine identity;
3. a package-build compatibility issue between installed Rhino, Compute,
   Rhino.Inside, and compile-time RhinoCommon;
4. an authenticated Cloud Zoo request failure not visible in anonymous HTTPS
   tests;
5. a process-user or licensing-session initialization difference between
   standalone Rhino.Inside and packaged `compute.geometry`.

General DNS failure, standard HTTPS failure, token absence, clock drift, and
running from `/mnt/c` are not supported by the current measurements.

## 18. Ubuntu VM comparison checklist

Run the following in the Ubuntu VM and create a second report using the same
headings.

### Platform and systemd

```bash
date --iso-8601=seconds
date -u --iso-8601=seconds
uname -a
uname -m
cat /etc/os-release
ps -p 1 -o pid=,comm=,args=
systemctl is-system-running
systemctl --failed --no-pager --plain
timedatectl status
```

### Rhino and .NET versions

```bash
dpkg-query -W -f='${Package}\t${Version}\n' \
  rhino3d rhino-compute

dotnet --info

sha256sum \
  /usr/lib/rhino3d/RhinoCommon.dll \
  /usr/lib/rhino3d/libRhinoLibrary.so \
  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll
```

### Token check

```bash
cd /path/to/RhinoOnLinux
./scripts/run-box-sample.sh --verify-token
```

Do not print the token.

### Network and TLS

```bash
for host in \
  cloudzoo.rhino3d.com \
  accounts.rhino3d.com \
  www.rhino3d.com \
  developer.rhino3d.com \
  api.nuget.org; do
  echo "--- $host"
  getent ahosts "$host"
  curl --silent --show-error --location --max-time 20 \
    --output /dev/null \
    --write-out 'http_code=%{http_code} tls_verify=%{ssl_verify_result} total=%{time_total}s\n' \
    "https://$host/"
done

curl --silent --show-error --location --max-time 20 \
  --output /dev/null \
  --write-out 'http_code=%{http_code} tls_verify=%{ssl_verify_result} total=%{time_total}s\n' \
  https://api.nuget.org/v3/index.json
```

### Machine identity availability

```bash
for f in \
  /etc/machine-id \
  /var/lib/dbus/machine-id \
  /proc/sys/kernel/random/boot_id \
  /sys/class/dmi/id/product_uuid \
  /sys/class/dmi/id/product_name \
  /sys/class/dmi/id/sys_vendor \
  /sys/class/dmi/id/product_serial \
  /sys/class/dmi/id/board_serial; do
  if [ -r "$f" ] && [ -s "$f" ]; then
    echo "$f: present/readable/non-empty"
  elif [ -e "$f" ]; then
    echo "$f: present but empty or unreadable"
  else
    echo "$f: absent"
  fi
done
```

### Filesystem

```bash
findmnt -T /path/to/RhinoOnLinux -no TARGET,SOURCE,FSTYPE,OPTIONS
findmnt -T /tmp -no TARGET,SOURCE,FSTYPE,OPTIONS
```

### Standalone licensing smoke test

```bash
cd /path/to/RhinoOnLinux
./scripts/run-minimal-host.sh --framework-dependent
```

Record the exit code immediately:

```bash
echo "$?"
```

## 19. Comparison priorities

When the Ubuntu VM report is available, compare these first:

| Priority | Item | WSL2 result |
| ---: | --- | --- |
| 1 | Standalone minimal-host exit | `134`, `NotLicensedException` |
| 2 | Exact Rhino binary hashes | Recorded above |
| 3 | `/proc/.../boot_id` | Present but empty |
| 4 | `/sys/class/dmi/id` | Absent |
| 5 | `rhino3d` package | `9.0.26257.7309` |
| 6 | `rhino-compute` package | `9.0.26159.12510-wip` |
| 7 | `Rhino.Inside` project package | `9.0.26084.13070-beta` |
| 8 | Cloud Zoo HTTPS | HTTP 200, TLS valid |
| 9 | Clock synchronization | Synchronized |
| 10 | Repository filesystem | ext4 |

If the Ubuntu VM uses identical binaries and succeeds while boot/DMI interfaces
are available there, that would be strong evidence for a WSL-specific platform
or machine-identity interaction. It would still require McNeel to confirm which
host interfaces the licensing client expects.

## Related project reports

- [`../rhino-wsl-vs-ubuntu-vm-licensing-findings-2026-09-14.md`](../rhino-wsl-vs-ubuntu-vm-licensing-findings-2026-09-14.md)
- [`../rhino-linux-licensing-retest-2026-09-14.md`](../rhino-linux-licensing-retest-2026-09-14.md)
- [`../rhino-inside-linux-next-steps.md`](../rhino-inside-linux-next-steps.md)
