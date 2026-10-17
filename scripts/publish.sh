#!/usr/bin/env bash
# Same as publish.ps1, for building on Linux or macOS.
#   ./scripts/publish.sh              -> publish/Managey-win-x64.zip
#   ./scripts/publish.sh linux-x64    -> publish/Managey-linux-x64.zip
set -euo pipefail
runtime="${1:-win-x64}"
root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/publish/Managey-$runtime"

# CI builds the client once and sets MANAGEY_SKIP_CLIENT=1 for each runtime.
if [[ "${MANAGEY_SKIP_CLIENT:-}" != "1" ]]; then
  (cd "$root/client" && npm ci && npm run build)
fi
rm -rf "$out"
dotnet publish "$root/server/Managey.Api/Managey.Api.csproj" -c Release -r "$runtime" --self-contained true -o "$out"

if [[ "$runtime" == win-* ]]; then
  cp "$root"/scripts/{install,uninstall,backup}.ps1 "$out/"
else
  cp "$root"/deploy/linux/* "$out/"
fi

rm -f "$out.zip"
(cd "$out" && zip -qr "$out.zip" .)
echo "Package ready: $out.zip"
