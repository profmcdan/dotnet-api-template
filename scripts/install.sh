#!/usr/bin/env bash
#
# Installs the `dotnet-api-template` CLI on macOS or Linux.
#
#   From a clone:   ./scripts/install.sh
#   Straight down:  curl -fsSL https://raw.githubusercontent.com/profmcdan/dotnet-api-template/main/scripts/install.sh | bash
#
# The tool carries the project template inside it, so this is the only thing you need to install.

set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/profmcdan/dotnet-api-template.git}"
REF="${REF:-main}"
INSTALL_ROOT="${INSTALL_ROOT:-$HOME/.dotnet-api-template}"
PACKAGE_ID="DotnetApiTemplate.Cli"
PROJECT_PATH="tools/DotnetApiTemplate.Cli/DotnetApiTemplate.Cli.csproj"

info()  { printf '\033[0;36m==>\033[0m %s\n' "$1"; }
warn()  { printf '\033[0;33m warn\033[0m %s\n' "$1"; }
die()   { printf '\033[0;31merror\033[0m %s\n' "$1" >&2; exit 1; }

# --- prerequisites ----------------------------------------------------------------------------
command -v dotnet >/dev/null 2>&1 || die "The .NET SDK is not installed or not on PATH. See https://dotnet.microsoft.com/download"

SDK_MAJOR="$(dotnet --version | cut -d. -f1)"
if [ "${SDK_MAJOR:-0}" -lt 10 ]; then
  die ".NET SDK 10.0 or later is required, but 'dotnet --version' reports $(dotnet --version)."
fi

# --- locate the source ------------------------------------------------------------------------
# Run from a checkout and we build that; piped from curl and we clone instead.
SOURCE_DIR=""
if [ -n "${BASH_SOURCE[0]:-}" ] && [ -f "${BASH_SOURCE[0]:-}" ]; then
  CANDIDATE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
  if [ -f "$CANDIDATE/$PROJECT_PATH" ]; then
    SOURCE_DIR="$CANDIDATE"
    info "Building from this checkout: $SOURCE_DIR"
  fi
fi

if [ -z "$SOURCE_DIR" ]; then
  command -v git >/dev/null 2>&1 || die "git is required to fetch the repository."
  SOURCE_DIR="$INSTALL_ROOT/src"

  if [ -d "$SOURCE_DIR/.git" ]; then
    info "Updating $SOURCE_DIR"
    git -C "$SOURCE_DIR" fetch --quiet origin "$REF"
    git -C "$SOURCE_DIR" checkout --quiet "$REF"
    git -C "$SOURCE_DIR" reset --hard --quiet "origin/$REF"
  else
    info "Cloning $REPO_URL"
    mkdir -p "$INSTALL_ROOT"
    git clone --quiet --depth 1 --branch "$REF" "$REPO_URL" "$SOURCE_DIR"
  fi
fi

[ -f "$SOURCE_DIR/$PROJECT_PATH" ] || die "Could not find $PROJECT_PATH under $SOURCE_DIR."

# --- build and install ------------------------------------------------------------------------
ARTIFACTS="$INSTALL_ROOT/artifacts"
rm -rf "$ARTIFACTS"
mkdir -p "$ARTIFACTS"

info "Packing the CLI"
dotnet pack "$SOURCE_DIR/$PROJECT_PATH" \
  --configuration Release \
  --output "$ARTIFACTS" \
  --nologo --verbosity quiet

# Uninstall first, then install. `dotnet tool update` is a no-op when the version number has
# not changed, which would silently keep an older build after a re-run.
info "Installing the global tool"
dotnet tool uninstall --global "$PACKAGE_ID" >/dev/null 2>&1 || true
dotnet tool install --global --add-source "$ARTIFACTS" "$PACKAGE_ID" >/dev/null

TOOLS_DIR="$HOME/.dotnet/tools"
export PATH="$PATH:$TOOLS_DIR"

info "Registering the bundled project template"
dotnet-api-template update >/dev/null

echo
printf '\033[0;32mInstalled.\033[0m %s\n' "$(dotnet-api-template version)"
echo
echo "  dotnet-api-template new --project-name Acme.Billing --allow-grpc"
echo

if ! echo ":$PATH:" | grep -q ":$TOOLS_DIR:"; then
  warn "$TOOLS_DIR is not on your PATH. Add this to your shell profile:"
  echo
  echo "    export PATH=\"\$PATH:$TOOLS_DIR\""
  echo
fi
