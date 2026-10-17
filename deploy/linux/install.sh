#!/usr/bin/env bash
# Installs or updates Managey on Ubuntu/Debian with HTTPS via Caddy. Run from the extracted linux-x64 package:
#   sudo bash install.sh hr.yourcompany.com
set -euo pipefail
domain="${1:?Usage: sudo bash install.sh your.domain.com}"
if [[ ! "$domain" =~ ^[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]*[A-Za-z0-9])?)+$ ]]; then
  echo "'$domain' is not a domain name. Give just the name, e.g. hr.yourcompany.com (no https:// or slashes)." >&2
  exit 1
fi
here="$(cd "$(dirname "$0")" && pwd)"

need=()
command -v rsync &>/dev/null || need+=(rsync)
command -v curl &>/dev/null || need+=(curl)
if ((${#need[@]})); then apt-get update && apt-get install -y "${need[@]}"; fi

id managey &>/dev/null || useradd --system --home /opt/managey --shell /usr/sbin/nologin managey
mkdir -p /opt/managey
# Keep data and settings on update.
rsync -a --exclude data --exclude appsettings.json "$here/" /opt/managey/
[ -f /opt/managey/appsettings.json ] || cp "$here/appsettings.json" /opt/managey/
chmod +x /opt/managey/Managey.Api
chown -R managey:managey /opt/managey

cp "$here/managey.service" /etc/systemd/system/managey.service
systemctl daemon-reload
systemctl enable managey
systemctl restart managey

if ! command -v caddy &>/dev/null; then
  apt-get update && apt-get install -y debian-keyring debian-archive-keyring apt-transport-https gpg
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' > /etc/apt/sources.list.d/caddy-stable.list
  apt-get update && apt-get install -y caddy
fi

# Managey's site lives in its own file so other sites already in the Caddyfile are left alone.
cat > /etc/caddy/managey.caddy <<CADDY
$domain {
	reverse_proxy 127.0.0.1:5080
}
CADDY
main=/etc/caddy/Caddyfile
if [ ! -f "$main" ] || grep -q '/usr/share/caddy' "$main"; then
  # Fresh Caddy install: replace the placeholder welcome page.
  echo "import /etc/caddy/managey.caddy" > "$main"
elif ! grep -q 'import /etc/caddy/managey.caddy' "$main"; then
  printf '\nimport /etc/caddy/managey.caddy\n' >> "$main"
fi
caddy validate --config "$main" --adapter caddyfile
systemctl reload caddy

echo "Managey is running at https://$domain (first sign-in: admin / ChangeMe123!)"
