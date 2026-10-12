#!/usr/bin/env bash
set -Eeuo pipefail

# Install Rhino.Compute and .NET 10 console-host prerequisites on Ubuntu 24.04
# (amd64), including WSL2.
# This follows McNeel's Ubuntu setup guide. It deliberately does not write a
# billing token, generate an API key, or start the service before credentials
# have been reviewed and configured by the developer. Rhino.Compute on Linux
# is currently documented as WIP, not recommended for production.

readonly MCNEEL_KEY_URL='https://mcneel-packages.s3.amazonaws.com/mcneel-packages.gpg.key'
readonly MCNEEL_REPO='deb [signed-by=/usr/share/keyrings/mcneel-archive-keyring.gpg] https://mcneel-packages.s3.amazonaws.com/deb stable main'
readonly KEYRING_PATH='/usr/share/keyrings/mcneel-archive-keyring.gpg'
readonly REPO_PATH='/etc/apt/sources.list.d/mcneel.list'
readonly RHINO_ENV_EXAMPLE='/etc/rhino-compute/environment.example'
readonly RHINO_ENV='/etc/rhino-compute/environment'

say() {
  printf '\n==> %s\n' "$*"
}

die() {
  printf 'ERROR: %s\n' "$*" >&2
  exit 1
}

if [[ -r /etc/os-release ]]; then
  # shellcheck source=/etc/os-release
  source /etc/os-release
else
  die '/etc/os-release is missing; cannot identify this distribution.'
fi

[[ "${ID:-}" == ubuntu && "${VERSION_ID:-}" == '24.04' ]] || \
  die "This installer targets Ubuntu 24.04 only (detected ${PRETTY_NAME:-unknown})."
[[ "$(dpkg --print-architecture)" == amd64 ]] || \
  die "Ubuntu amd64 is required (detected $(dpkg --print-architecture))."

if ((EUID == 0)); then
  SUDO=()
else
  command -v sudo >/dev/null 2>&1 || die 'sudo is required; run this as a normal user with sudo access.'
  sudo -v
  SUDO=(sudo)
fi

run_privileged() {
  "${SUDO[@]}" "$@"
}

# Select the config root for the user who will invoke Rhino, not the elevated
# installer. A distinct SUDO_USER is resolved from passwd because sudo's HOME
# and XDG_CONFIG_HOME may describe root rather than the original user.
if ((EUID == 0)) && [[ -n "${SUDO_USER:-}" && "$SUDO_USER" != root ]]; then
  passwd_entry="$(getent passwd "$SUDO_USER")" || die "Cannot resolve SUDO_USER '$SUDO_USER' with getent."
  IFS=: read -r config_user _ config_uid config_gid _ config_home _ <<< "$passwd_entry"
  [[ -n "$config_user" && "$config_uid" =~ ^[0-9]+$ && "$config_gid" =~ ^[0-9]+$ && "$config_home" == /* ]] || \
    die "Invalid passwd entry for SUDO_USER '$SUDO_USER'."
  config_root="$config_home/.config"
  say "Using $config_root for SUDO_USER=$config_user; ignoring XDG_CONFIG_HOME under sudo (it may be root's)."
elif ((EUID == 0)); then
  passwd_entry="$(getent passwd root)" || die 'Cannot resolve root with getent.'
  IFS=: read -r config_user _ config_uid config_gid _ config_home _ <<< "$passwd_entry"
  [[ -n "$config_uid" && "$config_uid" =~ ^[0-9]+$ && "$config_gid" =~ ^[0-9]+$ && "$config_home" == /* ]] || \
    die 'Invalid root passwd entry.'
  if [[ -n "${XDG_CONFIG_HOME:-}" ]]; then
    [[ "$XDG_CONFIG_HOME" == /* ]] || die 'XDG_CONFIG_HOME must be an absolute path.'
    config_root="$XDG_CONFIG_HOME"
  else
    config_root="$config_home/.config"
  fi
  say "Root-only installation: normal users still need their own config directory."
else
  config_uid="$(id -u)"
  config_gid="$(id -g)"
  config_home="${HOME:-}"
  [[ "$config_home" == /* ]] || die 'HOME must be set to an absolute path for the invoking user.'
  if [[ -n "${XDG_CONFIG_HOME:-}" ]]; then
    [[ "$XDG_CONFIG_HOME" == /* ]] || die 'XDG_CONFIG_HOME must be an absolute path.'
    config_root="$XDG_CONFIG_HOME"
  else
    config_root="$config_home/.config"
  fi
fi

# Verified behavior: without .config, ApplicationData resolves empty and Rhino
# licensing fails; an empty config directory is sufficient, with no license-cache copying.
if [[ -L "$config_root" || -e "$config_root" ]]; then
  [[ -d "$config_root" ]] || die "Config path exists but is not a directory: $config_root"
  printf 'Keeping existing config directory and contents unchanged: %s\n' "$config_root"
else
  if ((EUID == 0)) && [[ -n "${SUDO_USER:-}" && "$SUDO_USER" != root ]]; then
    run_privileged runuser -u "$config_user" -- mkdir -p -m 0700 -- "$config_root"
  elif ((EUID == 0)); then
    run_privileged mkdir -p -m 0700 -- "$config_root"
  else
    (umask 077; mkdir -p -m 0700 -- "$config_root")
  fi
  [[ -d "$config_root" ]] || die "Could not create config directory: $config_root"
  printf 'Created config directory for uid %s with restrictive permissions: %s\n' "$config_uid" "$config_root"
fi

work_dir="$(mktemp -d)"
cleanup() {
  rm -rf -- "$work_dir"
}
trap cleanup EXIT

say 'Installing repository prerequisites'
run_privileged apt-get update
run_privileged env DEBIAN_FRONTEND=noninteractive apt-get install -y \
  ca-certificates gnupg wget

say 'Adding the Microsoft package repository for .NET'
if [[ ! -f /etc/apt/sources.list.d/microsoft-prod.list ]]; then
  wget -q \
    "https://packages.microsoft.com/config/ubuntu/${VERSION_ID}/packages-microsoft-prod.deb" \
    -O "$work_dir/packages-microsoft-prod.deb"
  run_privileged dpkg -i "$work_dir/packages-microsoft-prod.deb"
else
  printf 'Microsoft repository already configured.\n'
fi
run_privileged apt-get update

# Check SDKs by major version so an SDK installed outside APT is also detected.
dotnet_sdk_list() {
  if command -v dotnet >/dev/null 2>&1; then
    dotnet --list-sdks
  elif [[ -x /usr/share/dotnet/dotnet ]]; then
    /usr/share/dotnet/dotnet --list-sdks
  else
    return 0
  fi
}

has_dotnet_sdk() {
  dotnet_sdk_list | awk -v major="$1" '$1 ~ ("^" major "\\.") { found = 1 } END { exit !found }'
}

say 'Installing missing .NET SDKs (8 and 10)'
missing_dotnet_packages=()
for sdk_major in 8 10; do
  if has_dotnet_sdk "$sdk_major"; then
    printf '.NET SDK %s is already installed.\n' "$sdk_major"
  else
    missing_dotnet_packages+=("dotnet-sdk-${sdk_major}.0")
  fi
done
if ((${#missing_dotnet_packages[@]})); then
  run_privileged env DEBIAN_FRONTEND=noninteractive apt-get install -y \
    "${missing_dotnet_packages[@]}"
fi

for sdk_major in 8 10; do
  has_dotnet_sdk "$sdk_major" || die ".NET SDK ${sdk_major} was not installed successfully."
done

say 'Adding the McNeel APT signing key and package source'
wget -q "$MCNEEL_KEY_URL" -O "$work_dir/mcneel-packages.gpg.key"
gpg --batch --yes --dearmor \
  --output "$work_dir/mcneel-archive-keyring.gpg" \
  "$work_dir/mcneel-packages.gpg.key"
run_privileged install -D -o root -g root -m 0644 \
  "$work_dir/mcneel-archive-keyring.gpg" "$KEYRING_PATH"
printf '%s\n' "$MCNEEL_REPO" > "$work_dir/mcneel.list"
run_privileged install -D -o root -g root -m 0644 \
  "$work_dir/mcneel.list" "$REPO_PATH"

say 'Installing Rhino.Compute and its Rhino runtime dependencies'
run_privileged apt-get update
run_privileged env DEBIAN_FRONTEND=noninteractive apt-get install -y rhino-compute

say 'Preparing the private Rhino.Compute environment file'
if [[ -e "$RHINO_ENV" || -L "$RHINO_ENV" ]]; then
  printf 'Keeping existing configuration: %s\n' "$RHINO_ENV"
else
  [[ -r "$RHINO_ENV_EXAMPLE" ]] || \
    die "Package installed, but the expected template is missing: $RHINO_ENV_EXAMPLE"
  run_privileged install -o root -g root -m 0600 "$RHINO_ENV_EXAMPLE" "$RHINO_ENV"
  printf 'Created %s with root-only permissions. Add RHINO_TOKEN and RHINO_COMPUTE_KEY.\n' "$RHINO_ENV"
fi

printf '\nInstallation complete. Installed package versions:\n'
dpkg-query -W -f='  ${Package} ${Version}\n' rhino3d rhino-compute 2>/dev/null || true
printf '  .NET SDKs:\n'
dotnet --list-sdks

cat <<'NEXT_STEPS'

WSL2 note: the service commands require systemd to be enabled and running
inside the distro. If systemctl reports that systemd is not running, enable it
in /etc/wsl.conf with:
  [boot]
  systemd=true
Then, from Windows, run `wsl --shutdown`, reopen Ubuntu, configure credentials
if not already done, and start the service. This installer does not alter
/etc/wsl.conf or WSL networking, and does not start the service automatically.

Important: RHINO_TOKEN is a Core-Hour billing token. Keep it secret; its use
can incur charges. Rhino.Compute for Linux is WIP software and is not
recommended for production by the current guide.

Next steps:
  1. To run Rhino workloads set the Rhino_token via export. This set is temporarily for
     a console session:
	     `export RHINO_TOKEN='YourTOKEN'`
	 Never expose the token to an agent. You can set the token via plain text a permanent
	 variable in Linux, but beware that it will be stored on you machine permantly
  2. Running dotnet projects:
      - To run dotnet projects set RHINO_TOKEN with export in the same console
	  - Then start a project with an RhinoInside solver with "dotnet run"
	  - WARNING: Projects without RhinoSolver  wont run currently. Rhino cant start with UI in Linux!
  2. Running RhinoCompute:
      - use command: rhino-compute-start 
        
NEXT_STEPS
