#!/bin/bash
# Runs the .NET test suite on Linux from a Windows checkout, through WSL - the ubuntu-latest half of CI,
# locally. From Git Bash:   MSYS_NO_PATHCONV=1 wsl.exe -d Ubuntu -- bash tools/wsl-test.sh [TestNameFilter...]
#
# Needs, inside WSL and without root: the .NET 10 SDK in ~/.dotnet
#   (curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet)
# and, when the distribution has no libicu, one unpacked next to it:
#   mkdir -p ~/icu && cd ~/icu && apt-get download libicu78 && dpkg -x libicu78_*.deb ~/icu/root
# The checkout is copied to ~/nf first: building on /mnt/c is slow and its bin/obj are the Windows ones.
set -u
export PATH="$HOME/.dotnet:/usr/local/bin:/usr/bin:/bin"
export LD_LIBRARY_PATH="$HOME/icu/root/usr/lib/x86_64-linux-gnu"
export DOTNET_ROOT="$HOME/.dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

REPO="$(cd "$(dirname "$0")/.." && pwd)"
rsync -a --delete --exclude bin --exclude obj --exclude node_modules --exclude .vs --exclude designer/host "$REPO/" ~/nf/
cd ~/nf || exit 1

FILTER=""
for t in "$@"; do FILTER="${FILTER:+$FILTER|}FullyQualifiedName~$t"; done

dotnet build NetForms.slnx -c Release -v quiet -nologo || exit 1
dotnet test tests/NetForms.Tests -c Release --no-build ${FILTER:+--filter "$FILTER"}
