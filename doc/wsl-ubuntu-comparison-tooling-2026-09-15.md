# WSL2/Ubuntu compatibility comparison tooling — September 15, 2026

## Purpose

Two repository tools now make the remaining comparison repeatable without
printing `RHINO_TOKEN` or machine identifier values:

- `tools/collect-rhino-compat-context.sh` records platform, package, runtime,
  clock, identity-interface availability, and installed-runtime hashes.
- `tools/compare-rhino-straces.py` compares two `strace -ff` directories by
  normalized syscall, result, and target. It includes `ENOENT`, `EACCES`, and
  `EPERM` file operations plus every captured `connect` call.

Both outputs still require manual privacy review before they are shared.

## Corrected boot-ID probe

The earlier WSL report classified `/proc/sys/kernel/random/boot_id` with
`test -s`. That is not a reliable content test for procfs/sysfs pseudo-files:
their `stat(2)` size can be zero even when reading the file returns data.
Consequently, the recorded WSL statement that `boot_id` was empty is now
**inconclusive**, not established.

The new context collector reads one line without printing it and reports only
whether content was returned. On the Ubuntu VM it established:

```text
/etc/machine-id                    present, readable, non-empty
/proc boot_id                      present, readable, non-empty
DMI product_uuid                   present, unreadable
DMI product_name                   present, readable, non-empty
DMI sys_vendor                     present, readable, non-empty
```

The corrected collector was run in WSL on September 15 from a token-bearing
process with `/home/dev/RhinoOnLinux/.dotnet/dotnet` (`10.0.111`) on `PATH`. It
reported the WSL boot ID as present, readable, and non-empty, and reported the
tested DMI paths (`product_uuid`, `product_name`, and `sys_vendor`) as absent.
The three installed runtime hashes matched the recorded Ubuntu VM values. This
replaces the earlier inconclusive WSL boot-ID observation; no identifier or
token value was printed.

The collector is still repeatable from a normal WSL terminal:

```bash
cd ~/RhinoOnLinux
/path/to/RhinoLinuxTest/tools/collect-rhino-compat-context.sh \
  > artifacts/wsl-compat-context-2026-09-15.txt
```

Review the output before copying it into a report. The tool prints only token
presence, never the token value.

## Exact raw-trace comparison

The successful VM raw trace is locally available at:

```text
/home/dev/RhinoLinuxTest/artifacts/ubuntu-vm-retest-2026-09-14/strace-minimal-host/
```

The failing WSL raw trace remains in the WSL-native checkout at:

```text
/home/dev/RhinoOnLinux/artifacts/wsl-minimal-host-strace-2026-09-15/
```

When both protected directories are available on one machine, run:

```bash
./tools/compare-rhino-straces.py \
  /path/to/ubuntu-vm/strace-minimal-host \
  /path/to/wsl-minimal-host-strace-2026-09-15 \
  --left-label 'Ubuntu VM pass' \
  --right-label 'WSL2 failure' \
  --map /home/dev/RhinoLinuxTest='<CHECKOUT>' \
  --map /home/dev/RhinoOnLinux='<CHECKOUT>' \
  --map /tmp/dotnet10-ubuntu-host='<DOTNET>' \
  --map /home/dev='<HOME>' \
  > /tmp/rhino-vm-vs-wsl-strace.md
```

Add mappings for any other private checkout, home, token-file, or temporary
paths before sharing output. The script normalizes process IDs, CLR shared-memory
names, and GUID-shaped values automatically. It deliberately does not broadly
redact all paths because that would make the exact path comparison ineffective.

## Verification completed

The comparison script was syntax-checked and run with the VM trace as both
inputs. It found 21 trace files, 189 selected event instances, 186 distinct
selected events, and zero differences. The context collector passed `bash -n`
and produced the non-secret VM baseline above.

## Exact comparison completed

The protected VM trace was added to the Git-ignored `artifacts/` directory and
the VM-versus-WSL comparison completed on September 15. Additional mappings
normalized both application-output directories and both .NET roots. The saved
normalized report passed checks for local home paths, the Windows username,
token/authorization markers, IPv4-like strings, and unnormalized GUIDs.

The failure/connect diff still showed no internet or access-denied explanation.
Manual review of successful as well as failed path operations found the material
difference: the passing VM read an existing per-user `.lic` and keypair, while
the failing WSL process found `<HOME>/.config` absent. The comparer intentionally
focuses on failures/connects, so this successful VM cache access required the
additional raw-path review.

The remaining action is an equivalent clean-state run on both platforms. See
`vm-wsl-strace-exact-comparison-2026-09-15.md`. Do not commit either raw trace or
copy licensing files between systems.
