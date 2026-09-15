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

Run the same collector from a normal WSL terminal before treating boot ID as a
platform difference:

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

## Remaining action

1. Run the corrected context collector in the unrestricted WSL distribution.
2. Make the protected WSL and VM trace directories available on one trusted
   machine; do not commit either raw trace.
3. Run and manually review the normalized diff.
4. If no material path, access, or socket difference appears, attach only the
   reviewed summary and minimal reproducer to the McNeel report.
