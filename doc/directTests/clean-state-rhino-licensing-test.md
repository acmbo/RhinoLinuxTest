# Direct clean-state Rhino licensing test

**Date prepared:** September 15, 2026
**Applies to:** Ubuntu VM and Ubuntu under WSL2

## Why this test is required

The exact syscall comparison found that the previous environments did not have
equivalent per-user licensing state:

- the passing Ubuntu VM read an existing `.lic` file and Rhino keypair;
- the failing WSL process found the user's `.config` directory absent and never
  reached equivalent cached licensing state.

The VM result is therefore not a clean token-only baseline. This test runs the
same minimal host with an empty temporary `HOME` and identical empty XDG roots
on each platform. The normal user configuration and existing license files are
left untouched.

The automation script is:

```text
tools/run-rhino-clean-state-test.sh
```

## Safety requirements

1. Run as the normal unprivileged user, never as `root`.
2. Use a non-Snap .NET host. The script rejects a resolved `/snap/...` host.
3. Load `RHINO_TOKEN` only from an existing private environment file. Never put
   the token on the command line or enable `set -x`.
4. Do not copy, delete, rename, or inspect existing `.lic`, keypair, or
   `cloudzoo-9.json` files.
5. Do not set the temporary `HOME` to a repository path. The script creates it
   beneath `/tmp` and removes it automatically.
6. Raw traces can contain local paths and identifiers. Keep output in an ignored
   artifact directory, review it locally, and do not commit it.
7. A successful test can use the configured Core-Hour Billing account in the
   same way as the existing Rhino tests.

## Prerequisites

The minimal host must already be built. The script deliberately does not restore
or build so the test does not depend on NuGet, SDK caches, or the isolated home.

The following must be available:

- a working non-Snap .NET executable;
- the built `RhinoInside.MinimalHost.dll`;
- a readable private token environment file;
- `strace`; and
- the installed Rhino runtime at `/usr/lib/rhino3d`.

Check `strace` with:

```bash
command -v strace
```

If WSL has no system `strace`, install it through the normal Ubuntu package
method or repeat the previously documented rootless extraction and pass its
absolute path with `--strace`.

## Step 1: run the Ubuntu VM control first

The VM test is the highest-value first result because its earlier successful run
used existing cached licensing state.

From the repository root on the Ubuntu VM:

```bash
set +e
./tools/run-rhino-clean-state-test.sh \
  --label ubuntu-vm \
  --dotnet /path/to/non-snap/dotnet \
  --app /home/dev/RhinoLinuxTest/RhinoLinxTest/RhinoInside.MinimalHost/bin/Release/net10.0/RhinoInside.MinimalHost.dll \
  --token-env /path/to/private/token-environment \
  --strace /usr/bin/strace \
  --output /home/dev/RhinoLinuxTest/artifacts/clean-state-ubuntu-vm-2026-09-15
status=$?
set -e
printf 'wrapper/application exit: %s\n' "$status"
```

If the temporary system-library .NET host still exists, the VM `--dotnet` value
may be:

```text
/tmp/dotnet10-ubuntu-host/dotnet
```

Verify that path before use. Do not fall back to `/snap/bin/dotnet`.

The wrapper intentionally exits with the minimal host's exit code. The command
above captures that value even when it is the expected WSL exit `134`. The value
is also retained in the output `exit-code` file.

## Step 2: inspect only the safe summary

```bash
VM_RUN=/home/dev/RhinoLinuxTest/artifacts/clean-state-ubuntu-vm-2026-09-15

cat "$VM_RUN/exit-code"
cat "$VM_RUN/licensing-state-summary.txt"
grep -aE \
  'CHECKPOINT:|SUCCESS:|NotLicensedException|Unhandled exception' \
  "$VM_RUN/stdout.log" "$VM_RUN/stderr.log" || true
```

Expected files are:

```text
run-metadata.txt
package-versions.txt
runtime-hashes.txt
exit-code
stdout.log
stderr.log
licensing-state-summary.txt
strace/trace.*
```

The temporary home and any license material created inside it are removed when
the script exits.

## Step 3: run the equivalent WSL test

From the repository containing the automation script in WSL:

```bash
./tools/run-rhino-clean-state-test.sh \
  --label wsl2 \
  --dotnet /home/dev/RhinoOnLinux/.dotnet/dotnet \
  --app /home/dev/RhinoOnLinux/src/RhinoInside.MinimalHost/bin/Release/net10.0/RhinoInside.MinimalHost.dll \
  --token-env /home/dev/RhinoOnLinux/.env.local \
  --strace /absolute/path/to/strace \
  --output /home/dev/RhinoOnLinux/artifacts/clean-state-wsl2-2026-09-15
```

Use the same script revision and syscall selection on both systems.

## Step 4: transfer only diagnostic output

Copy only the output files listed above into temporary review directories. Do
not copy a clean home or any licensing files. For example:

```text
tmp/clean-state-ubuntu-vm-2026-09-15/
tmp/clean-state-wsl2-2026-09-15/
```

Keep `artifacts/` and `tmp/` out of Git.

## Step 5: compare the traces

From this repository root, set the imported directory paths:

```bash
VM_RUN=tmp/clean-state-ubuntu-vm-2026-09-15
WSL_RUN=tmp/clean-state-wsl2-2026-09-15

VM_HOME="$(sed -n 's/^clean_home=//p' "$VM_RUN/run-metadata.txt")"
WSL_HOME="$(sed -n 's/^clean_home=//p' "$WSL_RUN/run-metadata.txt")"
```

Run the normalized failure/connect comparison:

```bash
./tools/compare-rhino-straces.py \
  "$VM_RUN/strace" \
  "$WSL_RUN/strace" \
  --left-label 'Ubuntu VM clean state' \
  --right-label 'WSL2 clean state' \
  --map "$VM_HOME=<CLEAN_HOME>" \
  --map "$WSL_HOME=<CLEAN_HOME>" \
  --map /home/dev/RhinoLinuxTest='<CHECKOUT>' \
  --map /home/dev/RhinoOnLinux='<CHECKOUT>' \
  --map /tmp/dotnet10-ubuntu-host='<DOTNET>' \
  --map /home/dev/RhinoOnLinux/.dotnet='<DOTNET>' \
  --map /home/dev='<HOME>' \
  > tmp/clean-state-ubuntu-vm-vs-wsl2.md
```

The comparer intentionally focuses on failed file operations and `connect`
calls. Review successful licensing paths separately, replacing GUIDs and clean
home paths before sharing:

```bash
for trace_dir in "$VM_RUN/strace" "$WSL_RUN/strace"; do
  echo "=== $trace_dir"
  grep -hEi \
    'License Manager/Licenses|cloudzoo-9\.json|/keypairs/' \
    "$trace_dir"/trace.* \
    | sed -E \
        -e 's#[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}#<GUID>#g' \
        -e "s#$VM_HOME#<CLEAN_HOME>#g" \
        -e "s#$WSL_HOME#<CLEAN_HOME>#g"
done
```

Review the normalized output for remaining local paths, names, addresses, or
identifiers before sharing it.

## Interpretation

| Ubuntu VM clean state | WSL2 clean state | Interpretation |
| --- | --- | --- |
| Pass | Fail | Strong evidence of a WSL2-specific standalone licensing problem. |
| Fail | Fail | The earlier VM pass depended on cached licensing state; the token alone did not establish the tested standalone session in either clean environment. |
| Pass | Pass | The normal WSL user's missing or stale per-user licensing initialization was the likely cause rather than WSL2 itself. |
| Loader failure | Any result | The VM is still using an incompatible Snap C++ runtime; fix the host before interpreting licensing. |

Do not send a WSL-defect report to McNeel until the VM and WSL clean-state
results have been captured and compared.
