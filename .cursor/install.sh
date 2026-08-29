#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for SubtitleCompare.
#
# The solution has three projects:
#   - SubtitleCompare.Core  (net10.0)          -> cross-platform library
#   - SubtitleCompare.Tests (net10.0)          -> xUnit test suite
#   - SubtitleCompare.App   (net10.0-windows)  -> WPF desktop app, Windows-only
#
# On this Linux VM only the Core library and its test suite build and run; the
# WPF app requires Windows and is built in CI (see .github/workflows). This
# script installs the pinned .NET SDK and restores/builds the cross-platform
# projects so tests can run immediately.
set -euo pipefail

DOTNET_CHANNEL="10.0"
DOTNET_DIR="$HOME/.dotnet"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

if ! "$DOTNET_DIR/dotnet" --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "Installing .NET SDK (channel $DOTNET_CHANNEL) into $DOTNET_DIR ..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_DIR"
else
  echo ".NET SDK already present:"
  "$DOTNET_DIR/dotnet" --list-sdks
fi

# Expose dotnet to every future agent shell without mutating shell profiles.
# The muxer resolves the real path via the symlink and finds the SDK/runtime.
sudo ln -sf "$DOTNET_DIR/dotnet" /usr/local/bin/dotnet

# Build the cross-platform projects (Tests pulls in Core). Skip the WPF app.
"$DOTNET_DIR/dotnet" build tests/SubtitleCompare.Tests/SubtitleCompare.Tests.csproj -c Release
