# deploy/

Coolify-driven production deploy artefacts.

## sc-tunnel/

Sidecar container that brings up a userspace **AmneziaWG** tunnel to an EU
VPS and exposes a SOCKS5 proxy on `sc-tunnel:1080` (docker-internal network).
The silo uses it to reach `api-v2.soundcloud.com` and `*.sndcdn.com` from
RF-hosted deployments where SoundCloud is blocked and classic tunnel
protocols (SSH/WG/SS/VLESS-Reality) are throttled by TSPU.

### One-time setup (prod)

1. Spin up an AmneziaWG server on an EU VPS (e.g., via the AmneziaVPN
   provisioning workflow — it gives you a ready-to-use client `.conf`).
2. Place the client `.conf` as a Coolify file-secret at
   `backend/Tools/deploy/secrets/awg_config` (gitignored).
   The file should start with `[Interface]` and contain the usual WG fields
   plus AmneziaWG obfuscation parameters (`Jc`, `Jmin/max`, `S1-S4`,
   `H1-H4`, `I1-I5`).
3. In the silo service enable:
   ```
   Audio__Socks5Proxy=socks5://sc-tunnel:1080
   ```
4. Container requirements are already in `docker-compose.yaml`
   (`cap_add: NET_ADMIN`, `devices: /dev/net/tun:/dev/net/tun`,
   `sysctls: net.ipv4.conf.all.src_valid_mark=1`).

### Dev (your machine)

No sidecar needed. The dev loop is:

1. Connect via the AmneziaVPN desktop/mobile app on your workstation.
2. Leave `Audio.Socks5Proxy` **unset** in
   `backend/Orchestration/Aspire/appsettings.local.json` (the code falls
   back to direct HTTP — which reaches SoundCloud cleanly while your
   system-level AmneziaVPN is active).
3. `aspire run` as usual.

If you ever need to test the SOCKS path locally, spin the sidecar up
standalone:
```
cd backend/Tools/deploy
docker compose build sc-tunnel
docker compose up sc-tunnel
# Expose locally for curl: add `ports: ["1080:1080"]` to the service
# temporarily or exec curl from inside the container.
```
