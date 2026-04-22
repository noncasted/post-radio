---
name: start-cluster
description: Start the backend Aspire cluster. Use when any task requires a running cluster — benchmarks, tests, API calls, telemetry analysis. Also trigger on "start cluster", "start backend", "start server", "run cluster".
---

# Start Cluster

Start the post-radio Aspire dev cluster and wait for it to be ready.

## Launch Command

```bash
dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http
```

IMPORTANT: `aspire run` does NOT support `--launch-profile` (known Aspire issue). Always use `dotnet run`.

The `http` profile is the first entry in `launchSettings.json` (`applicationUrl: http://localhost:7100`). All service-to-service transport in dev is plain HTTP — no pfx/dev-cert is needed.

In prod Aspire is not used — see `.codex/docs/DEPLOY.md` (Coolify + `backend/Tools/deploy/docker-compose.yaml`).

## Procedure

1. Check if AppHost is already running:
```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:7100 2>/dev/null
```
If `200` — cluster is already up.

2. Start in background:
```bash
dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http 2>&1 &
```
Use `run_in_background=true`.

3. Poll until AppHost responds (up to 2 minutes):
```bash
for i in $(seq 1 24); do
  code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:7100 2>/dev/null)
  if [ "$code" = "200" ]; then echo "READY"; break; fi
  sleep 5
done
```

4. If startup fails — check the background process output and report the error.

## Stopping the Cluster

```bash
pkill -f "Aspire.dll"
```

## Ports

| Service | Port |
|---------|------|
| Aspire AppHost | 7100 (from `launchSettings.json`) |
| Aspire Dashboard OTLP | 19070 (dev env var) |
| Aspire Resource Service | 20269 (dev env var) |

Non-AppHost services (ConsoleGateway, MetaGateway, Coordinator, Silo, PostgreSQL, PgBouncer) have their ports assigned dynamically by Aspire — discover via the Dashboard at `http://localhost:7100` or via Aspire Resource Service.

## Requirements

- Docker available (Aspire creates PostgreSQL and PgBouncer containers).
- `appsettings.json` / env vars configured per `.codex/docs/DEPLOY.md`.
