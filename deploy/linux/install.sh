#!/usr/bin/env bash
# Installs Managey on Ubuntu/Debian with HTTPS via Caddy. Run from the extracted linux-x64 package:
#   sudo ./install.sh hr.yourcompany.com
set -euo pipefail
domain="${1:?Usage: sudo ./install.sh your.domain.com}"
here="$(cd "$(dirname "$0")" && pwd)"

id managey &>/dev/null || useradd --system --home /opt/managey --shell /usr/sbin/nologin managey
mkdir -p /opt/managey
# Keep data and settings on update.
rsync -a --exclude data --exclude appsettings.json "$here/" /opt/managey/
[ -f /opt/managey/appsettings.json ] || cp "$here/appsettings.json" /opt/managey/
chmod +x /opt/managey/Managey.Api
chown -R managey:managey /opt/managey

cp "$here/managey.service" /etc/systemd/system/managey.service
systemctl daemon-reload
systemctl enable --now managey
systemctl restart managey

if ! command -v caddy &>/dev/null; then
  apt-get update && apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl gpg
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' > /etc/apt/sources.list.d/caddy-stable.list
  apt-get update && apt-get install -y caddy
fi
sed "s/hr.example.com/$domain/" "$here/Caddyfile" > /etc/caddy/Caddyfile
systemctl reload caddy

echo "Managey is running at https://$domain (first sign-in: admin / ChangeMe123!)"
