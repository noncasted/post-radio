# Deploy architecture

## Overview

Two parallel deployment models that **do not share a runtime**:

| | Dev (`aspire run`) | Prod (Coolify Docker Compose) |
|---|---|---|
| Entry point | `backend/Orchestration/Aspire/Program.cs` | `backend/Tools/deploy/docker-compose.yaml` |
| Orchestrator | Aspire AppHost + dcp | Docker Compose + Coolify Traefik |
| Postgres | Aspire-managed container | External Coolify-managed resource |
| PgBouncer | Aspire `AddContainer` sidecar | First-class compose service |
| Dashboard | AppHost-spawned dashboard | Standalone `mcr.microsoft.com/dotnet/aspire-dashboard` container |
| Resources tab | Native AppHost resource service | Third-party `kiapanahi/Aspire.ResourceServer.Standalone` (forked) |
| TLS / routing | `localhost:<port>` | Coolify Traefik + Let's Encrypt |
| Migrations | `PostResourcesSetup.Run` called from AppHost after resources come up | Dedicated `migrator` init-container (DeploySetup) |

Production **never** boots `Aspire/Program.cs`. AppHost is dev-only. Anything in that file guarded by `IsDevelopment()` or env vars like `COOLIFY_URL` / `DB_CONNECTION_STRING` was dead in prod and has already been pruned.

Swap direction of truth depending on what you are touching:
- changing how something runs in prod → edit `docker-compose.yaml` or `Dockerfile`
- changing how `aspire run` feels → edit `Program.cs` / `DbUpstreamFactory` / `PgBouncerFactory`

## Why the split exists

`aspire run` is a fantastic dev experience — hot reload, integrated dashboard, dcp that orchestrates containers and dotnet processes side-by-side. In production it is wrong tool:

1. `aspire run` internally invokes `dotnet run` per service. Services end up running in **Debug** regardless of `--configuration Release` (Aspire bug #13659). We measured ~870 MB of Debug assemblies vs ~470 MiB in Release.
2. AppHost + dcp + dashboard + 6× `dotnet run` launchers = ~1.6 GB of pure orchestration overhead, on top of the app itself.
3. To run dockerd inside a deploy container you need `--privileged` (the old DinD model). Privileged containers are a big no.
4. `aspire publish` generates a compose scaffold but it is really just a starting point — no Dockerfiles, hardcoded dev values, embedded Postgres, no Traefik labels. It is useful once to see how the tool imagines the mapping, then you write your own compose by hand. We kept the compose-environment registration for a while and later dropped it because it is not needed when prod does not actually call `aspire publish`.

## File layout

```
backend/
  Orchestration/
    Aspire/
      Program.cs              # dev composition (local postgres + pgbouncer + 5 services)
      Startup/
        DbUpstreamFactory.cs  # dev-only: spins up postgres container
        PgBouncerFactory.cs   # dev-only: writes pgbouncer.ini / databases.ini
        ProcessCleanup.cs     # dev convenience
    Dockerfile                # shared multi-stage image for prod services
    Dockerfile.prebuilt       # runtime-only image for local compose smoke tests
    Extensions/
      ServiceDefaultsExtensions.cs   # OTel + health endpoints
      ProjectsSetupExtensions.cs     # per-service DI composition
      OrleansReadyHealthCheck.cs     # /ready reports Orleans silo started
      CoordinatorReadyHealthCheck.cs # /ready additionally requires DeployId
      OrleansSetupExtensions.cs      # dev uses localhost clustering, prod uses AdoNet
  Tools/
    DeploySetup/              # init-container: runs DB migrations before cluster starts
      Program.cs
      PostResourcesSetup.cs   # orchestrates the migration steps
      StatesSetup.cs          # creates state_* tables per GeneratedStatesRegistration
      SideEffectsSetup.cs
      AuditLogSetup.cs
      BenchmarkSetup.cs
      StatesCleanup.cs / StatesDrop.cs
      OrleansClusteringSetup.cs      # bootstraps Orleans AdoNet schema (see below)
      Sql/
        PostgreSQL-Main.sql            # OrleansQuery table (dotnet/orleans v10.1.0)
        PostgreSQL-Clustering.sql      # membership tables + 8 stored queries
        PostgreSQL-Supplemental.sql    # our addition: CleanupDefunctSiloEntriesKey (missing upstream)
    deploy/
      docker-compose.yaml      # Coolify-targeted prod compose
      docker-compose.local.yaml # overlay for local smoke tests using prebuilt binaries
      .env.example              # env template (tracked)
      .env.local                # local creds (gitignored)
      COOLIFY.md                # Coolify-specific runbook

tools/scripts/
  publish-local.sh            # dotnet publish into ./publish/<Service>/ for local overlay
```

## Dockerfile layering (production)

One Dockerfile serves six .NET containers: `silo`, `coordinator`, `meta`, `console`, `frontend`, `migrator`. The image a container ends up as is selected through the `ASSEMBLY_NAME` build-arg passed from compose.

```dockerfile
# syntax=docker/dockerfile:1.7-labs      # labs flag — enables COPY --parents

FROM sdk AS restore                       # narrow cache key:
COPY --parents backend/post-radio.slnx       #   slnx + csproj + Directory.*.props only.
              **/Directory.*.props        #   Source edits do not touch this layer.
              **/*.csproj
RUN dotnet restore <each .csproj>

FROM restore AS publish-all               # single build stage for all six services.
COPY . .                                  # Source goes in here. Layer invalidated on any edit.
RUN for pair in "Silo.csproj:Silo" ...; do
      dotnet publish <csproj> -o /app/out/<name> --no-restore
    done                                  # msbuild incremental reuses compiled shared refs.

FROM aspnet AS runtime                    # slim runtime stage per service.
ARG ASSEMBLY_NAME                          # Selected at compose time.
RUN apt-get install curl                   # needed by docker healthcheck CMD-SHELL curl ...
COPY --from=publish-all /app/out/${ASSEMBLY_NAME}/ .
ENTRYPOINT ["sh", "-c", "exec dotnet ${ASSEMBLY_NAME}.dll"]
```

Why this shape beats the naive “one build stage per service”:

- `restore` layer depends only on project-graph metadata → NuGet restore happens **once per package change**, not per code edit.
- `publish-all` compiles every service in a single RUN → msbuild reuses `Shared/*/bin/` across services. The old model launched six parallel docker builds and each recompiled shared projects from scratch, burning ~120s even though the CPU budget existed only once.
- Final runtime stages are just `COPY --from=publish-all` → BuildKit trivially parallelizes them.

## Build-time caches

1. **BuildKit layer cache** on the Coolify VPS is preserved across deploys. First layer that changes cascades downstream; anything above stays `CACHED`. Source edits typically invalidate only `publish-all` — shaves 60-90s vs a cold build.
2. **NuGet restore cache** lives inside the `restore` stage as a layer. Edits to package versions or `.csproj` re-run restore; code edits do not.
3. `.dockerignore` excludes `backend/Tools/Tests/`, `client/`, docs, `bin/`, `obj/`, `publish/` etc. The build context sent to BuildKit is ~15 MB per service.

## docker-compose.yaml structure

### Anchors

```yaml
x-service-build: &service-build      # shared build spec: context = repo root, one Dockerfile.
x-service-env:   &service-env        # ASPNETCORE_*, OTLP endpoint + API key, pgbouncer conn-string.
```

### Networks

```yaml
networks:
  post-radio-production:
    external: true                    # Coolify Destination network, not managed by this compose.
```

Every service attaches to the same external Coolify Destination network (`post-radio-production`). That network must also contain Coolify Traefik and the managed Postgres resource. Keeping a single explicit network avoids Docker/Traefik choosing an unreachable backend IP from an auto-created compose default network.

### Service graph

```
pgbouncer  (edoburu/pgbouncer, sidecar)
  │ healthcheck: pg_isready -h 127.0.0.1 -p 6432
  ▼
migrator   (ASSEMBLY_NAME=DeploySetup, restart: "no")
  │ waits for pgbouncer healthy, runs PostResourcesSetup.Run, exits 0
  ▼
silo       (ASSEMBLY_NAME=Silo)
  │ waits for migrator completed, healthcheck ← OrleansReadyHealthCheck
  ▼
coordinator (ASSEMBLY_NAME=Coordinator)
  │ healthcheck ← OrleansReadyHealthCheck AND CoordinatorReadyHealthCheck (DeployId != empty)
  ▼
meta / console (parallel Orleans clients)
  │
  ▼
frontend (static WASM frontend + /api proxy to meta)

minio (object storage sidecar for audio/images)

aspire-dashboard (mcr.microsoft.com/dotnet/aspire-dashboard:9.0)
  - publishes OTLP endpoint on :18889 for every service
  - reads resources from resource-service over gRPC
resource-service (noncasted fork, pinned SHA)
  - mounts /var/run/docker.sock read-only
  - filters containers by COMPOSE_PROJECT_FILTER=post-radio
```

Dependencies use `depends_on.condition: service_healthy` — matches the old AppHost `WaitFor(...)` semantics without needing AppHost.

### Ports / exposure

All services declare `expose:` (internal) — no `ports:` mapped to host. TLS / routing is Coolify Traefik's job.

| Service | Internal port(s) | Why exposed |
|---|---|---|
| pgbouncer | 6432 | consumed by migrator + services |
| silo | 8080 | /alive /ready /health for compose healthcheck |
| coordinator | 8080 | same |
| meta / console | 8080 | same, plus user traffic via Traefik |
| frontend | 8080 | public static frontend + `/api` proxy to meta |
| minio | 9000 / 9001 | internal object storage API / optional local console |
| aspire-dashboard | 18888 (frontend), 18889 (OTLP gRPC), 18890 (OTLP HTTP) | 18888 user, 18889 app→dashboard |
| resource-service | 80 (gRPC) | dashboard→resource-service |

When a container exposes **more than one** port, Coolify cannot guess which one Traefik should forward to. The UI Domain field must include `:port` in those cases (currently only `aspire-dashboard` — `https://aspire.<domain>:18888`).

For services with one port (8080) the Coolify magic variable `SERVICE_FQDN_<NAME>_<PORT>: /` in compose + a matching Domain entry in the UI together generate the right Traefik labels.

### Env vars passed in by Coolify

```
DB_HOST DB_PORT DB_NAME DB_USER DB_PASSWORD    # consumed by pgbouncer + connection-string interpolation
CONSOLE_TOKEN                                  # blazor admin login
MINIO_ROOT_USER MINIO_ROOT_PASSWORD           # object-storage root credentials
ASPIRE_TOKEN                                   # aspire-dashboard browser token
OTEL_API_KEY                                   # services → dashboard OTLP header
```

Everything else is literal or derived inside compose (`x-service-env`). Secrets must be tagged Secret in Coolify UI so they do not get shipped to container logs.

### Persistent volumes

Three named volumes are persistent across redeploys:

| Volume | Mount | Why |
|---|---|---|
| `aspire-dashboard-keys` | `aspire-dashboard:/root/.aspnet/DataProtection-Keys` | Without it, every redeploy invalidates dashboard browser cookies — every page throws `CryptographicException`. |
| `console-keys` | `console:/root/.aspnet/DataProtection-Keys` | Same mechanism: Blazor antiforgery + auth cookies become undecryptable, all admin buttons silently fail (`AntiforgeryValidationException`). |
| `minio-data` | `minio:/data` | Stores uploaded/cached audio and images used by the radio metadata services. |

Both services also set `user: root` because the runtime image drops privileges by default and the `/root/...` directory is root-owned, so the volume mount would be read-only otherwise.

If you add another Blazor-rendered service with cookie state (auth, antiforgery, ProtectedBrowserStorage), give it the same treatment: named volume + `user: root`. See `DEPLOY_TROUBLESHOOTING.md` "Console: AntiforgeryValidationException" for the full failure mode.

### Blazor framework JS in publish output

`ConsoleGateway.csproj` carries an explicit `<PackageReference Include="Microsoft.AspNetCore.App.Internal.Assets" />` (version pinned in `backend/Directory.Packages.props`). The package contains `wwwroot/_framework/blazor.web.js`, `dotnet.js`, etc.

The `mcr.microsoft.com/dotnet/sdk` image does **not** auto-restore this private package via the implicit framework reference — `MapStaticAssets` ends up with a manifest pointing at files that were never copied, the browser 404s on `_framework/blazor.web.js`, and the Blazor circuit fails to bootstrap. Local builds happen to work because the package is already in the global NuGet cache from earlier projects.

When you bump the .NET SDK image tag, also bump this package version in `Directory.Packages.props` to match `Microsoft.AspNetCore.App.Ref/<ver>` shipped with the image. Verify after build:
```bash
docker run --rm <image> ls /app/wwwroot/_framework/
# expect: blazor.server.js  blazor.web.js  blazor.server.js.{br,gz}  blazor.web.js.{br,gz}
```

### Profiles (or lack of)

Earlier we used `profiles: [with-dashboard]` on dashboard + resource-service so they were optional. That felt wrong because the dashboard is the primary observability surface and we always want it up. The profiles were dropped; both containers boot on every deploy.

If you ever want to make a service optional again:
1. Add `profiles: [some-name]` back to the service block.
2. In Coolify set env `COMPOSE_PROFILES=some-name` (Coolify respects it) — or add `--profile some-name` to the build/up command override.

## docker-compose.local.yaml overlay

Used with `backend/Tools/deploy/docker-compose.local.yaml` together with the base compose for local smoke tests:

```bash
./tools/scripts/publish-local.sh     # dotnet publish all services → publish/<Service>/
docker compose \
  --env-file backend/Tools/deploy/.env.local \
  -f backend/Tools/deploy/docker-compose.yaml \
  -f backend/Tools/deploy/docker-compose.local.yaml \
  up --build -d
```

The overlay:

- Swaps `Dockerfile` → `Dockerfile.prebuilt` (runtime-only; copies pre-published binaries from host). Docker build finishes in seconds.
- Overrides the prod external network with a local bridge (`post-radio-local`) because the Coolify destination network does not exist outside the Coolify host.
- Adds host-port maps on `meta:7100`, `console:7102`, `frontend:7103`, `minio:7900/7901`, `aspire-dashboard:7200/7201` so you can open them in a browser from the host.

## Coolify application configuration

See `backend/Tools/deploy/COOLIFY.md` for the full runbook. One-liner summary:

- Build Pack: `Docker Compose`
- Base Directory: `/`
- Docker Compose Location: `/backend/Tools/deploy/docker-compose.yaml`
- Custom Docker Options: empty (no `--privileged`)
- Domains: per-service in UI, **include `:port` for multi-port containers**
- Env vars: the set above, secrets marked
- Resource Postgres: same Project/Environment as the App, Coolify-managed

## Health endpoints

`ServiceDefaultsExtensions.MapDefaultEndpoints` exposes three routes on every service, available in every environment (not just Development):

- `/health` — summary of all checks
- `/alive` — tag `live`, Kestrel heartbeat
- `/ready` — tag `ready`, includes Orleans startup checks

Checks registered:

- `OrleansReadyHealthCheck` (all services) — pass when `IServiceLoopObserver.IsOrleansStarted`
- `CoordinatorReadyHealthCheck` (coordinator only) — pass when `IDeployContext.DeployId != Guid.Empty`

Console has an auth middleware that redirects everything except `/login` and `/_/css/*` to the login screen. Explicit allowlist was added for `/health`, `/alive`, `/ready` — without it Docker healthcheck saw 302 redirects and marked the container unhealthy.

## Resource service (Aspire Dashboard Resources tab)

Aspire's standalone dashboard only shows telemetry (Structured logs / Traces / Metrics) by default — the Resources tab is empty unless `DASHBOARD__RESOURCESERVICECLIENT__URL` points at a gRPC server implementing the Aspire resource service contract.

We ship a third-party implementation pinned by commit SHA: `https://github.com/noncasted/Aspire.ResourceServer.Standalone` (fork of `kiapanahi/Aspire.ResourceServer.Standalone`, MIT-licensed). Our fork adds three patches on top of upstream:

1. `COMPOSE_PROJECT_FILTER` env — filter listed containers by `com.docker.compose.project` label so we only see `post-radio-*`, not every container on the host.
2. Docker container state (`running`, `exited`, …) mapped to Aspire `KnownResourceStates` (`Running`, `Exited`, …) so the icon colour is correct.
3. Display name comes from `com.docker.compose.service` label; exit code parsed from `Status` string so `migrator` (exit 0) shows as `Finished` not `Failed`.

`resource-service` container mounts `/var/run/docker.sock:ro`. Coolify allows read-only socket mounts by default. Without the mount the Resources tab shows no data but the dashboard itself still works.

## Memory footprint (apples to apples)

| | AppHost DinD era | Coolify Compose (idle) | Coolify Compose (silo benchmarks running) |
|---|---|---|---|
| 5 application services | 870 MB Debug | 576 MiB Release | 940 MiB Release |
| └── of which silo | — | 113 MiB | **531 MiB** ← benchmark-driven grain caches |
| Aspire host + dashboard + dcp + 6× dotnet run | ~1.6 GB | — | — |
| aspire-dashboard + resource-service | — | 156 MiB | 197 MiB |
| pgbouncer | 3 MB | 1.7 MiB | 1.9 MiB |
| migrator (runs then exits) | N/A | 0 MiB | 0 MiB |
| **Total project footprint** | ~2.47 GB | **~734 MiB** | ~1.14 GiB |

Measured on the production VPS via `docker stats --no-stream` filtering by `zufqewcez1k024uw3hnzspzp` prefix. Full methodology in `docs/tasks/current/aspire_coolify_compose_deploy/memory_baseline.md`.

The "benchmarks running" column is taken with the silo bench harness actively driving grains; expect silo to fall back to ~120 MiB and total to ~750 MiB once benchmarks stop. If silo stays above ~200 MiB at idle, that is a real leak worth investigating, not a benchmark artefact.

Coolify itself adds ~660 MiB (orchestrator + db + redis + proxy + sentinel + realtime) on top of any project. Sized for the 11 GiB VPS, it leaves ~9 GiB for application workloads.

## Deploy pipeline timing

| Stage | Warm | Cold |
|---|---|---|
| git clone | 5-10s | 5-10s |
| resource-service build (pinned SHA) | 0s (CACHED) | ~30s |
| app build (restore + publish-all + 6 runtime stages) | ~30-60s | ~2m |
| Stop old + start new containers | ~10s | ~10s |
| Healthchecks (interval 2s) until all healthy | ~10-15s | ~15-20s |
| **Total** | **~1m-1m30s** | **~2m30s-3m** |

Floors are diminishing-returns territory; below ~1 min you would have to cut the apt-get curl install by baking curl into a shared base image, or switch the healthcheck to a dotnet-only probe. Not worth doing unless deploys become multi-per-hour.
