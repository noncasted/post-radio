#!/bin/bash
# sc-tunnel entrypoint.
#
# 1. Materialise the AmneziaWG client config from /run/secrets/awg_config.
# 2. Carve out a direct route to the VPS endpoint (so awg0 traffic itself
#    doesn't try to ride through awg0 — chicken-and-egg).
# 3. Bring awg0 up via awg-quick (uses userspace amneziawg-go in background).
# 4. Start microsocks on 0.0.0.0:1080. All outbound traffic from it defaults
#    through awg0 (AllowedIPs = 0.0.0.0/0 in the WG config).

set -euo pipefail

CONFIG_SRC="/run/secrets/awg_config"
CONFIG_DIR="/etc/amnezia/amneziawg"
CONFIG_DST="${CONFIG_DIR}/awg0.conf"

if [ ! -f "$CONFIG_SRC" ]; then
    echo "[sc-tunnel] missing /run/secrets/awg_config — mount the AmneziaWG client config as a file secret." >&2
    exit 1
fi

mkdir -p "$CONFIG_DIR"
cp "$CONFIG_SRC" "$CONFIG_DST"
chmod 600 "$CONFIG_DST"

# amneziawg-tools rejects empty `In = ` lines (it tries to parse the value and
# chokes on an empty one). Some AmneziaVPN exports include blank I2..I5
# placeholders — strip them before `awg-quick` touches the file.
sed -i -E '/^[[:space:]]*I[1-5][[:space:]]*=[[:space:]]*$/d' "$CONFIG_DST"

# Drop the IPv6 leg from AllowedIPs — we only need IPv4 reachability to SoundCloud
# and the container runs with IPv6 disabled by default, which would make
# awg-quick fail on the ip6tables step.
sed -i -E 's|AllowedIPs[[:space:]]*=.*|AllowedIPs = 0.0.0.0/0|' "$CONFIG_DST"

# awg-quick expects the tool binary to be named `awg` and lives at /usr/bin/awg (we installed it there).
# It looks for /etc/amnezia/amneziawg/<iface>.conf by default.

# Derive endpoint IP so we can preserve a direct route to it through the container's
# original default gateway. Everything else will be redirected through awg0 once up.
ENDPOINT_LINE="$(grep -E '^[[:space:]]*Endpoint[[:space:]]*=' "$CONFIG_DST" | head -1 | sed 's/.*=[[:space:]]*//' | tr -d '[:space:]')"
ENDPOINT_HOST="${ENDPOINT_LINE%%:*}"
ENDPOINT_IP="$ENDPOINT_HOST"

# If the endpoint is a hostname, resolve it now — before we hijack the default route.
if ! printf '%s' "$ENDPOINT_IP" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$'; then
    ENDPOINT_IP="$(getent hosts "$ENDPOINT_HOST" 2>/dev/null | awk '{print $1; exit}')"
fi

DEFAULT_LINE="$(ip route show default | head -1)"
DEFAULT_GW="$(echo "$DEFAULT_LINE" | awk '{for (i=1;i<=NF;i++) if ($i=="via") {print $(i+1); exit}}')"
DEFAULT_DEV="$(echo "$DEFAULT_LINE" | awk '{for (i=1;i<=NF;i++) if ($i=="dev") {print $(i+1); exit}}')"

echo "[sc-tunnel] parsed endpoint_line='${ENDPOINT_LINE}' endpoint_host='${ENDPOINT_HOST}' endpoint_ip='${ENDPOINT_IP}'"
echo "[sc-tunnel] default_route='${DEFAULT_LINE}' gw='${DEFAULT_GW}' dev='${DEFAULT_DEV}'"
echo "[sc-tunnel] ip addr:"
ip addr
echo "[sc-tunnel] ip route (all):"
ip route show table all
echo "[sc-tunnel] /proc/net/route:"
cat /proc/net/route
echo "[sc-tunnel] --- end diag ---"

if [ -z "$DEFAULT_GW" ] || [ -z "$ENDPOINT_IP" ]; then
    echo "[sc-tunnel] failed to determine default gateway or endpoint IP" >&2
    exit 1
fi

echo "[sc-tunnel] endpoint=${ENDPOINT_IP}, default gw=${DEFAULT_GW} via ${DEFAULT_DEV}"

# Pin the WG endpoint to the original gateway so the tunnel has somewhere to send
# its encrypted packets even after AllowedIPs=0.0.0.0/0 hijacks the default route.
ip route replace "${ENDPOINT_IP}/32" via "${DEFAULT_GW}" dev "${DEFAULT_DEV}"

# Bring the interface up. awg-quick will:
#   - Start amneziawg-go (userspace)
#   - Configure awg0 with keys, address, DNS
#   - Install AllowedIPs routes (0.0.0.0/0 becomes the default via awg0)
awg-quick up awg0

echo "[sc-tunnel] awg0 up, routing table:"
ip route
echo "[sc-tunnel] awg0 interface status:"
awg show awg0
echo "[sc-tunnel] probing tunnel (ping 10.8.1.1 via awg0, 3 attempts):"
ping -c 3 -W 3 10.8.1.1 || echo "[sc-tunnel] ping via awg0 FAILED — handshake probably stuck"
echo "[sc-tunnel] HTTP probe via awg0 (curl --interface awg0):"
wget -q -O - --timeout=8 https://ifconfig.me 2>&1 || echo "[sc-tunnel] http via tunnel FAILED"
echo

echo "[sc-tunnel] starting microsocks on 0.0.0.0:1080"

# microsocks is a tiny SOCKS5 server — no auth, listens on 0.0.0.0:1080.
# Traffic from its accepted connections inherits the container's routing table,
# so outbound goes through awg0 by default.
exec microsocks -i 0.0.0.0 -p 1080
