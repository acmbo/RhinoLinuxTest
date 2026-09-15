#!/usr/bin/env bash
set -Eeuo pipefail

usage() {
  cat <<'USAGE'
Run RhinoInside.MinimalHost with an isolated, empty per-user configuration.

Usage:
  run-rhino-clean-state-test.sh \
    --label LABEL \
    --dotnet /path/to/non-snap/dotnet \
    --app /path/to/RhinoInside.MinimalHost.dll \
    --token-env /path/to/private/environment \
    --output /path/to/new/output-directory \
    [--strace /path/to/strace] \
    [--rhino-system-dir /usr/lib/rhino3d]

The same values can be supplied through LABEL, DOTNET_BIN, APP_DLL, TOKEN_ENV,
RUN_DIR, STRACE_BIN, and RHINO_SYSTEM_DIR. Command-line options take precedence.

The token environment file is sourced without shell tracing. The script reports
only whether RHINO_TOKEN is present, never its value. It creates an isolated
HOME under /tmp, removes that HOME on exit, and retains only trace/log/summary
files in the output directory. The output directory must be empty or absent.

The script exits with the tested application's exit code. Exit 134 is the known
WSL NotLicensedException result.
USAGE
}

die() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 2
}

need_value() {
  [[ $# -ge 2 && -n "$2" ]] || die "option $1 requires a value"
}

label="${LABEL:-}"
dotnet_bin="${DOTNET_BIN:-}"
app_dll="${APP_DLL:-}"
token_env="${TOKEN_ENV:-}"
run_dir="${RUN_DIR:-}"
strace_bin="${STRACE_BIN:-}"
rhino_system_dir="${RHINO_SYSTEM_DIR:-/usr/lib/rhino3d}"

while (($#)); do
  case "$1" in
    --label)
      need_value "$@"; label="$2"; shift 2 ;;
    --dotnet)
      need_value "$@"; dotnet_bin="$2"; shift 2 ;;
    --app)
      need_value "$@"; app_dll="$2"; shift 2 ;;
    --token-env)
      need_value "$@"; token_env="$2"; shift 2 ;;
    --output)
      need_value "$@"; run_dir="$2"; shift 2 ;;
    --strace)
      need_value "$@"; strace_bin="$2"; shift 2 ;;
    --rhino-system-dir)
      need_value "$@"; rhino_system_dir="$2"; shift 2 ;;
    -h|--help)
      usage
      exit 0 ;;
    --)
      shift
      break ;;
    *)
      die "unknown option: $1" ;;
  esac
done

(($# == 0)) || die "unexpected positional arguments: $*"

[[ -n "$label" ]] || die '--label is required'
[[ "$label" =~ ^[A-Za-z0-9._-]+$ ]] || die '--label may contain only letters, numbers, dot, underscore, and dash'
[[ -n "$dotnet_bin" ]] || die '--dotnet is required'
[[ -n "$app_dll" ]] || die '--app is required'
[[ -n "$token_env" ]] || die '--token-env is required'
[[ -n "$run_dir" ]] || die '--output is required'

if [[ -z "$strace_bin" ]]; then
  strace_bin="$(command -v strace || true)"
fi
[[ -n "$strace_bin" ]] || die 'strace was not found; pass --strace /path/to/strace'

((EUID != 0)) || die 'run this test as the normal unprivileged user, not root'
[[ -x "$dotnet_bin" ]] || die "dotnet is not executable: $dotnet_bin"
[[ -r "$app_dll" ]] || die "minimal-host DLL is not readable: $app_dll"
[[ -r "$token_env" ]] || die "token environment file is not readable: $token_env"
[[ -x "$strace_bin" ]] || die "strace is not executable: $strace_bin"
[[ -d "$rhino_system_dir" ]] || die "Rhino system directory is missing: $rhino_system_dir"

resolved_dotnet="$(readlink -f "$dotnet_bin")"
case "$resolved_dotnet" in
  /snap/*)
    die "Snap-managed dotnet is not valid for this comparison: $resolved_dotnet" ;;
esac

if [[ -e "$run_dir" && ! -d "$run_dir" ]]; then
  die "output path exists and is not a directory: $run_dir"
fi
if [[ -d "$run_dir" && -n "$(find "$run_dir" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
  die "output directory is not empty: $run_dir"
fi

umask 077
mkdir -p "$run_dir/strace"
chmod 700 "$run_dir" "$run_dir/strace"

# Source the known private environment before HOME is isolated. Never enable
# `set -x` around this operation.
set -a
# shellcheck source=/dev/null
source "$token_env"
set +a
[[ -n "${RHINO_TOKEN:-}" ]] || die 'RHINO_TOKEN is absent after sourcing the private environment'

original_tmpdir="${TMPDIR:-/tmp}"
clean_home="$(mktemp -d "$original_tmpdir/rhino-clean-home-${label}.XXXXXX")"
cleanup() {
  unset RHINO_TOKEN
  if [[ -n "${clean_home:-}" && -d "$clean_home" ]]; then
    rm -rf -- "$clean_home"
  fi
}
trap cleanup EXIT INT TERM

mkdir -p \
  "$clean_home/.config" \
  "$clean_home/.local/share" \
  "$clean_home/.cache" \
  "$clean_home/.dotnet" \
  "$clean_home/Desktop" \
  "$clean_home/tmp"
chmod 700 \
  "$clean_home" \
  "$clean_home/.config" \
  "$clean_home/.local" \
  "$clean_home/.local/share" \
  "$clean_home/.cache" \
  "$clean_home/.dotnet" \
  "$clean_home/Desktop" \
  "$clean_home/tmp"

cat > "$clean_home/.config/user-dirs.dirs" <<'USER_DIRS'
XDG_DESKTOP_DIR="$HOME/Desktop"
USER_DIRS

{
  printf 'label=%s\n' "$label"
  printf 'captured_utc=%s\n' "$(date --utc --iso-8601=seconds)"
  printf 'clean_home=%s\n' "$clean_home"
  printf 'dotnet=%s\n' "$resolved_dotnet"
  printf 'application=%s\n' "$(readlink -f "$app_dll")"
  printf 'strace=%s\n' "$(readlink -f "$strace_bin")"
  printf 'rhino_system_dir=%s\n' "$(readlink -f "$rhino_system_dir")"
  printf 'kernel=%s\n' "$(uname -srm)"
  printf 'virtualization='
  systemd-detect-virt 2>/dev/null || printf 'unknown\n'
  printf 'dotnet_version='
  "$dotnet_bin" --version
  echo 'token_present=true'
} > "$run_dir/run-metadata.txt"

if command -v dpkg-query >/dev/null 2>&1; then
  dpkg-query -W -f='${Package}\t${Version}\n' rhino3d rhino-compute \
    > "$run_dir/package-versions.txt" 2>/dev/null || true
fi

{
  for path in \
    /usr/lib/rhino3d/RhinoCommon.dll \
    /usr/lib/rhino3d/libRhinoLibrary.so \
    /usr/lib/rhino-compute/compute.geometry/Rhino.Inside.dll; do
    if [[ -r "$path" ]]; then
      sha256sum "$path"
    else
      printf 'unreadable: %s\n' "$path"
    fi
  done
} > "$run_dir/runtime-hashes.txt"

export HOME="$clean_home"
export XDG_CONFIG_HOME="$clean_home/.config"
export XDG_DATA_HOME="$clean_home/.local/share"
export XDG_CACHE_HOME="$clean_home/.cache"
export DOTNET_CLI_HOME="$clean_home/.dotnet"
export TMPDIR="$clean_home/tmp"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export RHINO_SYSTEM_DIR="$rhino_system_dir"

set +e
"$strace_bin" -ff -s 256 \
  -o "$run_dir/strace/trace" \
  -e trace=connect,openat,access,statx,readlink \
  "$dotnet_bin" "$app_dll" \
  > "$run_dir/stdout.log" \
  2> "$run_dir/stderr.log"
application_status=$?
set -e

printf '%s\n' "$application_status" > "$run_dir/exit-code"

license_root="$XDG_CONFIG_HOME/McNeel/Rhinoceros/6.0/License Manager/Licenses"
keypair_root="$XDG_DATA_HOME/mcneel/rhinoceros/9.0/keypairs"

count_named_files() {
  local directory="$1" pattern="$2"
  if [[ -d "$directory" ]]; then
    find "$directory" -maxdepth 1 -type f -name "$pattern" -printf '.\n' | wc -l
  else
    echo 0
  fi
}

count_all_files() {
  local directory="$1"
  if [[ -d "$directory" ]]; then
    find "$directory" -type f -printf '.\n' | wc -l
  else
    echo 0
  fi
}

{
  printf 'application_exit=%s\n' "$application_status"
  if [[ -d "$license_root" ]]; then
    echo 'license_directory=created'
  else
    echo 'license_directory=absent'
  fi
  printf 'license_file_count=%s\n' "$(count_named_files "$license_root" '*.lic')"
  if [[ -f "$license_root/cloudzoo-9.json" ]]; then
    echo 'cloudzoo_state=created'
  else
    echo 'cloudzoo_state=absent'
  fi
  printf 'keypair_file_count=%s\n' "$(count_all_files "$keypair_root")"
  printf 'trace_file_count=%s\n' "$(find "$run_dir/strace" -maxdepth 1 -type f -name 'trace*' -printf '.\n' | wc -l)"
} > "$run_dir/licensing-state-summary.txt"

if grep -IlaEr \
    'RHINO_TOKEN=|Authorization:|Bearer[[:space:]]+[A-Za-z0-9._-]+' \
    "$run_dir" > "$run_dir/privacy-marker-files.txt"; then
  echo 'privacy_marker_scan=review-required' >> "$run_dir/licensing-state-summary.txt"
  echo 'WARNING: token/authorization marker text was detected; review locally before copying.' >&2
else
  rm -f "$run_dir/privacy-marker-files.txt"
  echo 'privacy_marker_scan=passed' >> "$run_dir/licensing-state-summary.txt"
fi

printf 'Clean-state test complete for %s.\n' "$label"
printf 'Application exit code: %s\n' "$application_status"
cat "$run_dir/licensing-state-summary.txt"
grep -aE \
  'CHECKPOINT:|SUCCESS:|NotLicensedException|Unhandled exception' \
  "$run_dir/stdout.log" "$run_dir/stderr.log" || true
printf 'Artifacts: %s\n' "$run_dir"
printf 'The isolated HOME will now be removed; no license files are retained.\n'

exit "$application_status"
