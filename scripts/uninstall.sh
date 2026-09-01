#!/usr/bin/env bash
# Removes the dotnet-api-template CLI and its registered project template.

set -euo pipefail

export PATH="$PATH:$HOME/.dotnet/tools"

if command -v dotnet-api-template >/dev/null 2>&1; then
  dotnet-api-template uninstall-template >/dev/null 2>&1 || true
fi

dotnet tool uninstall --global ProfmcdanDotnetApiTemplate.Cli 2>/dev/null || true
rm -rf "${INSTALL_ROOT:-$HOME/.dotnet-api-template}"

echo "Removed the dotnet-api-template CLI, its template registration and its cached checkout."
