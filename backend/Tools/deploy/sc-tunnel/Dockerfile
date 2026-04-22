# AmneziaWG SOCKS5 tunnel sidecar for SoundCloud access from RF-hosted
# deployments. Brings up a userspace AmneziaWG interface (awg0) to an EU VPS,
# then runs microsocks as a SOCKS5 server bound to 0.0.0.0:1080 on the docker
# network. All outbound traffic except the VPS endpoint itself is routed via
# awg0, so siblings on the `internal` network get SoundCloud access through
# the tunnel at sc-tunnel:1080.
#
# AmneziaWG is used because plain SSH / shadowsocks / VLESS-Reality were all
# throttled or blocked by TSPU in RF. AmneziaWG obfuscates the WireGuard
# handshake (J/S/H/I parameters) to evade pattern-based DPI.
#
# Container requirements (set via docker-compose):
#   cap_add: NET_ADMIN
#   devices: /dev/net/tun:/dev/net/tun
#   sysctls: net.ipv4.conf.all.src_valid_mark=1

FROM golang:1.24-alpine AS builder
RUN apk add --no-cache git make build-base linux-headers
WORKDIR /src
# Pin amneziawg-go to a released tag — master has occasionally regressed the
# protocol against stable Amnezia server deployments. Keep amneziawg-tools on
# master because it needs to parse v2 config params (S3/S4/I1/H-ranges).
ARG AWG_GO_TAG=v0.2.12
RUN git clone --depth=1 --branch "$AWG_GO_TAG" https://github.com/amnezia-vpn/amneziawg-go.git awg-go \
 && cd awg-go \
 && go build -ldflags='-s -w' -trimpath -o /out/amneziawg-go .
RUN git clone --depth=1 https://github.com/amnezia-vpn/amneziawg-tools.git awg-tools \
 && cd awg-tools/src \
 && make \
 && cp wg /out/awg \
 && cp wg-quick/linux.bash /out/awg-quick \
 && chmod +x /out/awg-quick
# microsocks is not packaged in alpine 3.20 any more; build it from source here
# so the runtime stage stays small.
RUN git clone --depth=1 https://github.com/rofl0r/microsocks.git msocks \
 && cd msocks \
 && make \
 && cp microsocks /out/microsocks \
 && strip /out/microsocks

FROM alpine:3.20
RUN apk add --no-cache bash iproute2 iptables openresolv tini iputils wget
COPY --from=builder /out/amneziawg-go /usr/bin/amneziawg-go
COPY --from=builder /out/awg          /usr/bin/awg
COPY --from=builder /out/awg-quick    /usr/bin/awg-quick
COPY --from=builder /out/microsocks   /usr/bin/microsocks

COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD pgrep -x microsocks >/dev/null && pgrep -x amneziawg-go >/dev/null || exit 1

ENTRYPOINT ["/sbin/tini", "--", "/entrypoint.sh"]
