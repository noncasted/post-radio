# Coolify deployment

Production deploy is driven by `backend/Tools/deploy/docker-compose.yaml`. Coolify builds Release Docker images directly from the repository and runs the stack through Docker Compose — no `aspire run`, no nested Docker daemon and no privileged deploy container.

## Runtime topology

```text
Coolify Docker host + Traefik (TLS)
├── pgbouncer          (DB sidecar, DB_HOST=<managed Postgres>)
├── media-data volume  (persistent filesystem storage for audio/images)
├── migrator           (one-shot init: Orleans + state/audit/benchmark tables)
├── silo               (Orleans silo, Postgres clustering)
├── coordinator        (DeployIdentity + cluster coordination)
├── meta               (public API: meta/api domain)
├── console            (admin Blazor console)
├── frontend           (public WebAssembly frontend + /api proxy to meta)
├── aspire-dashboard   (browser + OTLP endpoint)
└── resource-service   (Docker resources feed for the dashboard Resources tab)
```

All containers join the external Coolify Destination network `post-radio-production`. Keep that name aligned with the Destination network configured in Coolify, or update the compose file and `traefik.docker.network` labels together.

## Build model

Build context is the repository root. `backend/Orchestration/Dockerfile` restores the project graph once, publishes all runtime projects in one stage, then each service image copies only its own publish output selected by `ASSEMBLY_NAME`.

Published .NET entry points:

- `Silo`
- `Coordinator`
- `MetaGateway`
- `ConsoleGateway`
- `Frontend.Server`
- `DeploySetup` (migrator)

For fast local smoke tests, run `./tools/scripts/publish-local.sh` and include `docker-compose.local.yaml`; it switches every .NET image to `Dockerfile.prebuilt` and copies `./publish/<Name>/` instead of rebuilding inside Docker.

## Coolify application settings

- **Build Pack:** Docker Compose
- **Base Directory:** `/`
- **Docker Compose Location:** `/backend/Tools/deploy/docker-compose.yaml`
- **Custom Docker Options:** empty (do not use `--privileged`)
- **Destination Network:** `post-radio-production`

## Domains

Configure these in the Coolify UI or by overriding the matching `SERVICE_FQDN_*` env vars:

- `https://<frontend-domain>` → service `frontend`, port `8080`
- `https://<meta-domain>` → service `meta`, port `8080`
- `https://<console-domain>` → service `console`, port `8080`
- `https://<aspire-domain>` → service `aspire-dashboard`, port `18888`

The Aspire dashboard container exposes more than one port, so include `:18888` in the Coolify domain field when the UI requires an explicit target port.

## Environment variables

Set these in Coolify; mark secrets as secret values.

| Name | Purpose |
| --- | --- |
| `DB_HOST` | Coolify-internal hostname of the managed Postgres resource |
| `DB_PORT` | Postgres port, usually `5432` |
| `DB_NAME` | Database name |
| `DB_USER` | Database user |
| `DB_PASSWORD` | Database password |
| `CONSOLE_TOKEN` | Admin login token for `/login?token=...` |
| `ASPIRE_TOKEN` | Browser token for Aspire Dashboard |
| `OTEL_API_KEY` | OTLP API key shared by services and dashboard |

The compose passes both `postgres` and `ConnectionStrings__postgres`; keep both because the migrator and application helpers read different key shapes.

## Readiness ordering

```text
pgbouncer ─┐
migrator ──┴─> silo ─> coordinator ─┬─> meta ─> frontend
                                   └─> console
```

`media-data` is mounted at `/app/media` for `silo`, `coordinator`, `meta` and `console`. The app creates `/app/media/audio` and `/app/media/images` automatically.

- `pgbouncer` uses `pg_isready`.
- `migrator` exits after `PostResourcesSetup.Run` completes.
- `silo`, `coordinator`, `meta` and `console` use `/ready` from `MapDefaultEndpoints`.
- `frontend` uses `/` because it is a lightweight static frontend/proxy and does not host the Orleans health endpoints.

## Local smoke test

Create `backend/Tools/deploy/.env.local` from `.env.example`, set `DB_HOST=host.docker.internal` if Postgres runs on the host, then run:

```bash
./tools/scripts/publish-local.sh

docker compose \
  --env-file backend/Tools/deploy/.env.local \
  -f backend/Tools/deploy/docker-compose.yaml \
  -f backend/Tools/deploy/docker-compose.local.yaml \
  up --build -d

docker compose \
  --env-file backend/Tools/deploy/.env.local \
  -f backend/Tools/deploy/docker-compose.yaml \
  -f backend/Tools/deploy/docker-compose.local.yaml \
  ps
```

Local port map:

- `meta`: <http://localhost:7100>
- `console`: <http://localhost:7102>
- `frontend`: <http://localhost:7103>
- `aspire-dashboard`: <http://localhost:7200>

## Troubleshooting notes

- If containers cannot resolve the managed Postgres hostname, they are not on the same Coolify Destination network as Postgres. Verify the network name and the per-service `networks` entries.
- If Traefik routes to a dead or unreachable backend IP, keep `traefik.docker.network` pinned to the same external network used by all public services.
- If Blazor console buttons fail after redeploy, check that `console-keys` is mounted at `/root/.aspnet/DataProtection-Keys` and the service runs as `root`.
- If the Aspire Dashboard opens but Resources is empty, check that `resource-service` can read `/var/run/docker.sock` and that `COMPOSE_PROJECT_FILTER` matches the compose project name (`post-radio`).
