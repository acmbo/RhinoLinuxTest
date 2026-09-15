#!/usr/bin/env bash
set -u

# Collects comparison-friendly facts without printing machine identifiers or RHINO_TOKEN.
content_state() {
  local label="$1" path="$2" value=''
  if [[ ! -e "$path" ]]; then
    printf '%-34s absent\n' "$label"
  elif [[ ! -r "$path" ]]; then
    printf '%-34s present, unreadable\n' "$label"
  else
    IFS= read -r value < "$path" || true
    if [[ -n "$value" ]]; then
      printf '%-34s present, readable, non-empty\n' "$label"
    else
      printf '%-34s present, readable, empty\n' "$label"
    fi
  fi
}

printf '%s\n' '== platform =='
uname -srm
printf 'virtualization: '
systemd-detect-virt 2>/dev/null || printf '%s\n' unknown
[[ -n "${WSL_INTEROP:-}" ]] && echo 'WSL_INTEROP: present' || echo 'WSL_INTEROP: absent'
[[ -d /run/WSL ]] && echo '/run/WSL: present' || echo '/run/WSL: absent'

printf '%s\n' '== identity surfaces (values withheld) =='
# Read content without printing it. `test -s` is unreliable for procfs/sysfs
# pseudo-files because stat(2) may report size zero even when reads return data.
content_state '/etc/machine-id' /etc/machine-id
content_state '/proc boot_id' /proc/sys/kernel/random/boot_id
content_state 'DMI product_uuid' /sys/class/dmi/id/product_uuid
content_state 'DMI product_name' /sys/class/dmi/id/product_name
content_state 'DMI sys_vendor' /sys/class/dmi/id/sys_vendor

printf '%s\n' '== time =='
date --utc --iso-8601=seconds
if command -v timedatectl >/dev/null 2>&1; then
  timedatectl show -p NTPSynchronized -p TimeUSec 2>/dev/null || true
fi

printf '%s\n' '== runtime packages =='
dpkg-query -W -f='${Package}\t${Version}\n' rhino3d rhino-compute ca-certificates openssl 2>/dev/null || true

printf '%s\n' '== dotnet =='
if command -v dotnet >/dev/null 2>&1; then
  dotnet --version
  readlink -f "$(command -v dotnet)"
else
  echo 'dotnet: not found on PATH'
fi

printf '%s\n' '== Rhino runtime hashes =='
for path in \
  /usr/lib/rhino3d/RhinoCommon.dll \
  /usr/lib/rhino3d/libRhinoLibrary.so \
  /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll; do
  if [[ -r "$path" ]]; then sha256sum "$path"; else echo "unreadable: $path"; fi
done

printf '%s\n' '== token =='
if [[ -n "${RHINO_TOKEN:-}" ]]; then
  echo 'RHINO_TOKEN: present, value withheld'
else
  echo 'RHINO_TOKEN: absent'
fi
