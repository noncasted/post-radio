# Deploy troubleshooting

Concrete failure modes hit during the Coolify Compose migration and how to recognise / fix each one. Read after `DEPLOY.md` — that file explains the architecture, this one lists the landmines.

## Coolify shared network not joined

**Symptom.** Migrator (or any service) crashes at startup with Npgsql `Name or service not known` / `Failed to resolve host name` for the managed Postgres host (e.g. `qxeff0tbfccmhj4i8qn404o6`). Local `docker compose up` works fine, only Coolify deploys break.

**Cause.** Coolify-managed Postgres lives on the shared bridge network named `coolify`. Our compose, by default, gets its own per-project bridge (`zufqewcez1k024uw3hnzspzp_default`). DNS for the managed resource is only resolvable inside `coolify`, so containers on the project bridge cannot reach it.

**Fix.** Two pieces both required in `docker-compose.yaml`:

```yaml
networks:
  coolify:
    external: true            # tell compose this network already exists

services:
  silo:
    networks: [default, coolify]   # MUST be set per-service
```

Setting it only at the top level (`networks: default: external: name: coolify`) does **not** stick — Coolify's compose pre-processor overrides the top-level definition. Per-service `networks: [default, coolify]` is the only form that survives.

Verify: `sudo docker network inspect coolify --format '{{range .Containers}}{{.Name}} {{end}}'` should list every service plus the managed Postgres container.

## Orleans clustering schema missing

**Symptom.** Silo loops forever logging `relation "orleansquery" does not exist` or `function membership_read_all(...) does not exist`. Healthcheck `/ready` never goes green, deploy times out.

**Cause.** Orleans `UseAdoNetClustering` expects the schema (`OrleansQuery` table + a set of stored queries) to already exist. Locally Aspire's dev provider auto-bootstraps it; in prod nothing does. Worse, Orleans 10.1.0's bundled `PostgreSQL-Clustering.sql` is missing the `CleanupDefunctSiloEntriesKey` row, so even after running upstream SQL the cluster fails on cleanup paths.

**Fix.** `OrleansClusteringSetup` in `DeploySetup` runs three embedded SQL files in order on every migrator boot:
1. `Sql/PostgreSQL-Main.sql` — creates `OrleansQuery` if absent.
2. `Sql/PostgreSQL-Clustering.sql` — membership tables + 8 stored queries from upstream.
3. `Sql/PostgreSQL-Supplemental.sql` — our patch with `CleanupDefunctSiloEntriesKey`, applied via `ON CONFLICT DO NOTHING` so it is safe to re-run.

The setup is gated on `OrleansQuery` existence, so first deploy bootstraps everything and subsequent deploys are essentially no-ops. If you upgrade Orleans, re-check upstream `PostgreSQL-Clustering.sql` — the supplemental row may have been merged and the patch can be removed.

## Console returns 502 / Gateway Timeout while game and meta work

**Symptom.** `https://console.minesleader.xyz/login` hangs and returns 504, but `meta` / `game` on identical Traefik labels are fine. Container itself is `Up (healthy)`, internal `curl http://localhost:8080/login` works, `coolify-proxy` access logs do not show the request.

**Cause.** Traefik's dynamic router store can hold a stale entry for the host pointing at a dead container after a redeploy or a rapid label change. The router exists — Traefik resolves the host — but the upstream IP is gone, hence the timeout (request enters Traefik, never leaves).

**Diagnosis.**
```bash
# enumerate routers Traefik thinks exist for this host
sudo docker exec coolify-proxy wget -qO- http://127.0.0.1:8080/api/http/routers \
  | python3 -m json.tool | grep -iC2 console.minesleader

# scan host for label collisions on the same hostname
sudo docker ps --format "{{.Names}}" | while read c; do
  sudo docker inspect "$c" --format '{{range $k,$v := .Config.Labels}}{{$k}}={{$v}} {{end}}' \
    | grep -q "console.minesleader.xyz" && echo "=== $c ==="
done
```

If you see two `===` lines, two containers are competing for the host — kill the orphan. If only one and the router still misroutes:

**Fix.** Restart the proxy (briefly drops every site for 5-10s):
```bash
sudo docker restart coolify-proxy
```

We hit this once after the rapid healthcheck-tuning redeploy cycle. A clean Redeploy from the Coolify UI fixed it without needing the proxy restart, presumably because Coolify reissues all dynamic config on Redeploy.

If the symptom returns, the nuclear option is to rename the offending hostname temporarily (e.g. `admin.minesleader.xyz`) — that bypasses any cached router and confirms whether the issue is host-name-bound or container-bound.

## `host.docker.internal` does not resolve in pgbouncer (local only)

**Symptom.** Local smoke test: `pgbouncer` exits with `getaddrinfo failed for host.docker.internal`. Production is unaffected.

**Cause.** Linux Docker does not auto-inject `host.docker.internal` into containers (macOS / Windows do).

**Fix.** Already in compose:
```yaml
pgbouncer:
  extra_hosts:
    - "host.docker.internal:host-gateway"
```
If you copy the pgbouncer block to a new compose file, do not drop this line.

## Migrator runs but `state_*` tables are empty

**Symptom.** Migrator container exits 0, but Silo logs `relation "state_player" does not exist`.

**Cause.** `DbExtensions.GetConnection` reads the **plain** key `postgres` from configuration, **not** `ConnectionStrings:postgres`. If only the latter is set, the migrator opens a connection to a wrong / empty database (or to localhost), creates tables there, and exits successfully.

**Fix.** Set both env vars on the `migrator` service:
```yaml
environment:
  postgres: "Host=pgbouncer;Port=6432;Database=...;Username=...;Password=..."
  ConnectionStrings__postgres: "${postgres}"
```

Double-check with `psql` against the actual managed Postgres after a deploy: `\dt state_*` should list all GeneratedStatesRegistration entries.

## Deploy build hits NuGet restore on every code edit

**Symptom.** Build time stays at ~3+ minutes per deploy even after warm cache. Restore step never shows `CACHED`.

**Cause.** Either (a) the `restore` stage `COPY` is too wide and pulls in source files (any source edit invalidates restore), or (b) `Directory.Build.props` / `Directory.Packages.props` were not copied into the restore stage, so the implicit project graph keeps changing.

**Fix.** Two requirements:
1. The Dockerfile must use `# syntax=docker/dockerfile:1.7-labs` and `COPY --parents <patterns>` to copy **only** `*.csproj`, `Directory.*.props`, `*.slnx`. Anything else in the restore stage breaks layer caching.
2. `.dockerignore` must not exclude `Directory.*.props` (it doesn't — but if you ever rewrite it, double-check).

Verify: edit a `.cs` file, rerun `docker build` locally, confirm the `restore` step shows `CACHED`. If not, the `COPY --parents` glob is wrong.

## Aspire Dashboard Resources tab is empty

**Symptom.** Telemetry tabs (Logs, Traces, Metrics) work; Resources tab shows no entries.

**Cause.** The standalone `mcr.microsoft.com/dotnet/aspire-dashboard` image only renders Resources when `DASHBOARD__RESOURCESERVICECLIENT__URL` points at a gRPC server implementing the resource contract. Stock dashboard ships none.

**Fix.** `resource-service` container (forked `noncasted/Aspire.ResourceServer.Standalone`) provides this. Required pieces:
- `DASHBOARD__RESOURCESERVICECLIENT__URL=http://resource-service:80` on `aspire-dashboard`.
- `resource-service` mounts `/var/run/docker.sock:ro`.
- `COMPOSE_PROJECT_FILTER=mines-leader` env on `resource-service` so it filters our containers out of the entire Docker host.

If the tab is still empty, check `docker logs <resource-service>` — most failures are permission-related on the socket mount or label-filter typos.

## Console healthcheck fails with 302 redirects

**Symptom.** `console` container marked unhealthy. `docker exec console curl -v http://localhost:8080/ready` returns `302 → /login`.

**Cause.** Console gateway has an auth middleware that redirects every unauthenticated request to `/login`. Health endpoints were not on its allowlist.

**Fix.** `ConsoleGateway/Program.cs` registers an explicit allowlist for `/health`, `/alive`, `/ready` before the auth middleware. If you add a new health route, add it to the allowlist too.

## Aspire Dashboard browser nags about insecure OTLP

**Symptom.** Dashboard console / browser warning about OTLP API key over plain HTTP.

**Cause.** OTLP traffic between services and the dashboard is intra-compose plain HTTP — by design, never leaves the host network. Dashboard cannot tell.

**Fix.** Not a bug, do not switch to TLS for intra-cluster OTLP. The browser warning is about the dashboard UI itself, which already has TLS via Coolify Traefik. Ignore.

## Coolify build fails with `lstat /backend: no such file or directory`

**Symptom.** Build container logs `Error: resolve : lstat /backend: no such file or directory` immediately after pulling the repo. `docker compose config` locally works fine. Coolify also prints earlier warnings like `Dockerfile not found for service migrator at ../../.././backend/Orchestration/Dockerfile, skipping ARG injection`.

**Cause.** Coolify invokes `docker compose` with an explicit `--project-directory`, which overrides the default (the compose file's directory). `build.context: ../../..` in our compose was written relative to the compose file location; relative to Coolify's project directory it resolves to the filesystem root, so `./backend/...` becomes `/backend/...` which does not exist.

**Fix.** Coolify UI → App → General:
- **Base Directory:** `/backend/Tools/deploy`
- **Docker Compose Location:** `/docker-compose.yaml`

That puts project-directory at `/backend/Tools/deploy/`, so `context: ../../..` resolves to the repo root. Any time you move the compose file, update both fields together.

## Dockerfile parse error `unknown flag: parents`

**Symptom.** Build fails on the first `COPY --parents ...` line with `dockerfile parse error on line N: unknown flag: parents`.

**Cause.** `--parents` is an experimental BuildKit flag only available on the **labs** variant of the Dockerfile frontend. Plain `# syntax=docker/dockerfile:1.7` rejects it.

**Fix.** First line of Dockerfile must be:
```dockerfile
# syntax=docker/dockerfile:1.7-labs
```
If you ever bump to a newer syntax version (e.g. `1.8`), check that it is a `-labs` tag or switch to the upstream-master-labs channel.

## Restore fails with `NETSDK1013: TargetFramework value ''`

**Symptom.** `dotnet restore <path.csproj>` during build fails with `NETSDK1013: The TargetFramework value '' was not recognized` — but only in Docker builds, never locally.

**Cause.** Two combined mistakes:
1. Our `.csproj` files rely on `Directory.Build.props` to set `TargetFramework`.
2. If the restore stage uses `dotnet restore backend/backend.slnx` and the solution includes `Tests.csproj` while `.dockerignore` excludes `backend/Tools/Tests/`, the solution file references a project that is not in the build context → restore fails on the missing csproj.

**Fix.** Two guardrails:
1. Restore stage must copy all `Directory.*.props` files (we use `COPY --parents backend/backend.slnx **/Directory.*.props **/*.csproj`).
2. Do not `dotnet restore` a solution that references excluded projects. List the six production `.csproj` files explicitly in the Dockerfile restore step. When you add a new production service, add a `dotnet restore` line for it alongside the existing five.

## BuildKit cache-mount race across parallel publishes

**Symptom.** Concurrent `dotnet publish` on 6 services intermittently fails with `Could not find a part of the path '/root/.nuget/packages/<pkg>/<ver>/lib/.../X.dll'`. Different services fail on different packages between runs — a classic race signature.

**Cause.** We used `RUN --mount=type=cache,id=nuget,...` to share the NuGet cache across build stages, then ran six parallel `publish` stages that all wrote to it. BuildKit does not lock shared cache mounts by default — two writers racing on the same file yield a half-written file for a third reader.

**Fix (historical, superseded).** The current Dockerfile avoids the problem entirely by publishing all six services **sequentially in a single RUN** inside one `publish-all` stage. No cache-mount needed. If you ever fork the Dockerfile back to per-service publish stages, add `sharing=locked` to the cache mount: `--mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked`.

## Coolify routes to wrong port on multi-port container

**Symptom.** Service with multiple exposed ports returns 502 / Gateway Timeout even though the container is healthy and traffik labels exist. Rotating Redeploys may intermittently fix it.

**Cause.** Coolify generates Traefik labels from the **Domains** UI field. When the Domain is just `https://x.example.com` (no port) and the service exposes more than one port, Traefik has no deterministic way to pick a backend. It may pick the OTLP port (18889) instead of the frontend (18888) and hang.

**Fix.** In Coolify UI → Domains, always include the port for multi-port services:
```
https://aspire.minesleader.xyz:18888
```
Single-port services (meta/game/console on 8080) can omit the port — Traefik picks the only option. Affects `aspire-dashboard` (18888 / 18889 / 18890). If you expose a second port on any other service, add `:port` to its Domain entry.

## Resource-service image tag out of sync with fork SHA

**Symptom.** After pushing a fix to the fork, rebuild pulls new code but the container keeps running the old behaviour. `docker inspect` shows the old short-SHA in the image tag.

**Cause.** Compose pins the fork by `build.context: https://github.com/noncasted/Aspire.ResourceServer.Standalone.git#<sha>` and tags the image `mines-leader/resource-service:<short-sha>`. BuildKit caches the git clone by URL+ref. If we bump only the `context:` ref but leave the `image:` tag unchanged, docker may still use the cached image layer under the old tag.

**Fix.** When you update the fork:
1. Bump the full SHA in `build.context`.
2. Bump the short-SHA in `image:` to match.
3. If still stale: `docker compose build --no-cache resource-service` on the host (Coolify Redeploy sometimes does this automatically, sometimes not).

Keep both fields in lock-step — grep the compose for the old SHA and replace both occurrences when updating.

## Dev: Aspire persistent container stuck on wrong network

**Symptom.** `aspire run` locally boots OK, but pgbouncer logs `DNS lookup failed: postgres: result=-2` forever. Services cannot reach Postgres. Happens after a config or Aspire version change.

**Cause.** `DbUpstreamFactory` uses `WithLifetime(ContainerLifetime.Persistent)` on the Postgres container so dcp reuses it between `aspire run` invocations. When the internal dcp network model changes (e.g. an Aspire upgrade added the `aspire-persistent-network` bridge), persistent containers keep their old `NetworkMode=bridge` and never join the new network. Other services (pgbouncer) spawn fresh on the new network and cannot see the stranded Postgres.

**Fix.** Kill the persistent containers so the next `aspire run` recreates them on the current network:
```bash
docker rm -f postgres-<dcp-id> pgbouncer-<dcp-id>
```
`docker ps | grep -E "postgres|pgbouncer"` to find the IDs. This has to be re-done after Aspire upgrades that change internal networking.

## Dev: port collision with standalone local Postgres

**Symptom.** `aspire run` fails to start Postgres: `Ports=map[] ExposedPorts=...` — container is alive but no host-port binding. All downstream services time out.

**Cause.** Someone left `ml-pg` (a standalone `docker run postgres -p 9432:5432`) running on the host. Aspire's `DbUpstreamFactory` also tries to bind host port 9432 (from `ConnectionStrings:db` in `appsettings.json`). The second bind silently fails with `isProxied: false`, leaving the container unrouted, and dcp then also does not attach it to the aspire bridge.

**Fix.** Before `aspire run`:
```bash
docker stop ml-pg
```
Symmetric hygiene before the reverse: when switching from local compose testing to `aspire run`, stop whatever container was bound to 9432.

Long-term option: change `ConnectionStrings:db` port to something unlikely to collide (e.g. 15432) in `appsettings.json`, so dev never fights other Postgres-like containers.

## Aspire Dashboard: every page throws `CryptographicException: key ... was not found in the key ring`

**Symptom.** Opening any tab in the Aspire Dashboard (StructuredLogs, Traces, Metrics, …) immediately fails with a Blazor unhandled-exception circuit error. Server logs show `The key {<guid>} was not found in the key ring` from `Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore`.

**Cause.** The `mcr.microsoft.com/dotnet/aspire-dashboard` image stores ASP.NET Core DataProtection keys under `/root/.aspnet/DataProtection-Keys` by default — an in-container path that does not survive a container restart. Every redeploy generates a fresh keyring. The browser still sends the previous instance's `ProtectedBrowserStorage` cookie, the new container cannot decrypt it, every page that calls `BrowserStorageBase.GetAsync` throws on first parameter set.

**Fix.** Persist the keyring on a named volume:

```yaml
services:
  aspire-dashboard:
    volumes:
      - aspire-dashboard-keys:/root/.aspnet/DataProtection-Keys

volumes:
  aspire-dashboard-keys:
```

After the next deploy the keyring is stable across restarts. Existing browser cookies stay invalid forever (their key is gone) — clear cookies for the dashboard host once and the loop ends.

## Aspire Dashboard Console Logs tab throws `Status(StatusCode="Unknown", Detail="Exception was thrown by handler.")`

**Symptom.** Any Console Logs view in the dashboard immediately fails. Server logs show `Grpc.Core.RpcException: Status(StatusCode="Unknown", Detail="Exception was thrown by handler.")` from `Aspire.Dashboard.Model.DashboardClient.SubscribeConsoleLogs`.

**Cause.** Our forked `resource-service` (`noncasted/Aspire.ResourceServer.Standalone`) was looking up the target container in `GetResourceLogs` by raw Docker container name (`<project>-<service>-<N>`). After fork patch #3 (display name from `com.docker.compose.service` label), `Resource.Name` sent by the dashboard is the service label (`silo`, `migrator`, …) — they never match, `Single(...)` throws `InvalidOperationException`, gRPC wraps it as Unknown.

**Fix.** Patched in fork commit `31272a1`: `GetResourceLogs` mirrors the same precedence as `Resource.FromDockerContainer` (compose-service label first, container name fallback) and degrades gracefully (`SingleOrDefault` + warn log + empty stream) when no container matches.

If you bump the fork further, keep this invariant: the lookup key in `GetResourceLogs` must match whatever `FromDockerContainer` writes into `Resource.Name`.

## Console: `Failed to load resource: 404` for `_framework/blazor.web.js` (Blazor admin dead)

**Symptom.** `https://console.minesleader.xyz/login` loads, but every page is dead — no buttons clickable, top-bar links return 404, browser console shows `blazor.web.js:1 Failed to load resource: the server responded with a status of 404`. `/_content/...` and `/css/...` work fine. `/_framework/blazor.web.js` returns 404 from prod, even though `MapStaticAssets()` is wired and the manifest (`<svc>.staticwebassets.endpoints.json`) lists the route.

**Cause (after a long detour).** The `mcr.microsoft.com/dotnet/sdk` image — both `:10.0`, `:10.0.201`, `:10.0.202` — does **not** auto-restore the private `Microsoft.AspNetCore.App.Internal.Assets` package, even for Web SDK projects that need it. That package is the actual carrier of `blazor.web.js`, `dotnet.js`, etc; without it, `dotnet publish` writes the manifest pointing at files that do not exist on disk (because they were never copied — never even fetched). Local hosts have the package because earlier projects pulled it; the SDK image starts clean and the implicit framework reference for Web SDK is not enough to drag it in.

The investigation that led here, in case the symptom returns and the fix is not obvious:
1. Cookies cleared, persistent keyring added — still 404.
2. `app.UseStaticFiles()` removed (it duplicates `MapStaticAssets`) — still 404.
3. `dotnet publish` repro on host: ConsoleGateway gets `wwwroot/_framework/` with 6 files. The same loop in Docker: empty.
4. SDK pinned 10.0.201 (matches host) — still 404.
5. Per-service `BaseIntermediateOutputPath` isolation tried — broke `ProjectReference` resolution (CS0246 everywhere) and reverted.
6. SWA tracking files (`staticwebassets*.json`, `*.StaticWebAssets.Up2Date`) deleted between iterations — still 404.
7. Repro built locally with our exact Dockerfile, `find / -name 'blazor.web.js'` → only `/src/publish/...` (a stale dev artifact from `tools/scripts/publish-local.sh` that slipped past `.dockerignore`). Nothing under `/usr/share/dotnet/`, nothing under `/root/.nuget/packages/`. The package that should ship it was never restored.
8. On host, the same `find / -name 'blazor.web.js'` finds it under `/home/<user>/.nuget/packages/microsoft.aspnetcore.app.internal.assets/10.0.5/_framework/blazor.web.js`. That package was nowhere in the Docker image's restored set.

**Fix.** Add an explicit reference in `ConsoleGateway.csproj` (or any other Web SDK project that interactively renders Blazor) plus a Central Package Management entry:

```xml
<!-- ConsoleGateway.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.App.Internal.Assets" />
  ...
</ItemGroup>
```

```xml
<!-- backend/Directory.Packages.props -->
<PackageVersion Include="Microsoft.AspNetCore.App.Internal.Assets" Version="10.0.5" />
```

The version must match (or be ≤) the `Microsoft.AspNetCore.App.Ref` pack version installed in the SDK image (`/usr/share/dotnet/packs/Microsoft.AspNetCore.App.Ref/<ver>/`) — currently `10.0.5`. When you bump the .NET SDK image, also bump this package version, otherwise restore will silently downgrade the framework reference.

**Verification.**

```bash
docker build -f backend/Orchestration/Dockerfile --target publish-all -t debug:latest .
docker run --rm debug:latest ls /app/out/ConsoleGateway/wwwroot/_framework/
# expect: blazor.server.js  blazor.web.js  blazor.server.js.{br,gz}  blazor.web.js.{br,gz}
```

If the output is empty after this fix, search the SDK image for the package — `docker run --rm debug:latest ls /root/.nuget/packages/ | grep aspnetcore.app.internal` — and bump the version in Directory.Packages.props if missing.

## Console (Blazor admin): `AntiforgeryValidationException: token could not be decrypted` after every redeploy

**Symptom.** Identical mechanism to the Aspire dashboard DataProtection bug, but for our own admin console. Server logs show `Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException` wrapping `CryptographicException: The key {<guid>} was not found in the key ring`. Every form POST (every button, every save) fails silently after a redeploy.

**Cause.** Same as the dashboard: ASP.NET Core DataProtection auto-generates an in-container keyring at `/root/.aspnet/DataProtection-Keys`. Container restart → fresh keyring → cookies set by the previous instance (auth cookie, antiforgery cookie) become undecryptable. Auth cookie failures redirect users to `/login`; antiforgery failures look like silent button no-ops.

**Fix.** Persist the keyring on a named volume, same shape as the dashboard:

```yaml
services:
  console:
    user: root        # the runtime image drops privileges; without root the volume is RO
    volumes:
      - console-keys:/root/.aspnet/DataProtection-Keys

volumes:
  console-keys:
```

Apply this to **every** Blazor Server-rendered service that uses cookies / antiforgery. After the next redeploy, ask users to clear cookies for the console host once — old cookies are encrypted with the now-discarded ephemeral key and will never decrypt again.

**Why `user: root`.** The aspnet base image runs the entrypoint as a non-root user by default in some variants. The `/root/...` directory is owned by root, so the writable volume mount needs root. Same one-line fix on `aspire-dashboard` for the same reason.

## Compose `git+sha` references must use the FULL commit SHA

**Symptom.** Coolify build aborts immediately on the resource-service stage:
```
ERROR: failed to read dockerfile: failed to load cache key:
       repository does not contain ref b2f751c, output: ""
```
Even though the SHA exists on `origin` and `git rev-parse b2f751c` resolves locally.

**Cause.** BuildKit's `git://...#<ref>` source resolver does **not** accept abbreviated SHAs. Branch names and tags work; full 40-char SHAs work. Anything between (8/12-char shorthand) fails, because BuildKit calls `git fetch --depth=1 <ref>` and the git protocol only accepts full SHAs there.

**Fix.** Use the full SHA in compose, keep the short-SHA only for the local `image:` tag (cosmetic):
```yaml
build:
  context: https://github.com/<owner>/<repo>.git#ba56b364bfb7d56262e984c476071a811af07c5a
image: mines-leader/resource-service:ba56b36
```

When you bump the fork, copy the full SHA into `context:` (`git rev-parse HEAD` in the fork worktree), then truncate it for the image tag.

## Resource-service fork: `CS0718 'static type cannot be used as type argument'` after .NET 10 bump

**Symptom.** Building the standalone resource-service fork on net10 fails on `Services/DashboardService.cs`:
```
CS1520: Method must have a return type
CS0718: 'DashboardService': static types cannot be used as type arguments
```
on every `ILogger<DashboardService>` and on the constructor.

**Cause.** `Grpc.Tools` generates a static container class `DashboardService` in the proto's namespace (`Aspire.ResourceService.Proto.V1`). Our wrapper class is also called `DashboardService` and inherits from `Proto.V1.DashboardService.DashboardServiceBase`. C# 14 / .NET 10 name resolution started preferring the inherited (proto-generated) static type when an unqualified `DashboardService` is referenced inside the class body — the wrapper class becomes inaccessible to itself.

**Fix.** Rename the wrapper to something that does not collide. Our fork uses `ContainerDashboardService` (commit `b2f751c`). Inheritance line stays fully qualified:
```csharp
internal sealed class ContainerDashboardService : Proto.V1.DashboardService.DashboardServiceBase
```
`Program.cs` uses `app.MapGrpcService<ContainerDashboardService>()`. Test assemblies have to be updated in lock-step (CS0234) — our fork commit `ba56b36` did this.

If you regenerate the gRPC bindings or rename the proto service, re-evaluate the collision.

## Resource-service fork: `NU1902` for OpenTelemetry packages

**Symptom.** Restore fails (treated as error) with:
```
NU1902: Package 'OpenTelemetry.Api' 1.10.0 has a known moderate severity vulnerability
```

**Cause.** `Directory.Build.props` in the fork sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Upstream pinned OpenTelemetry to 1.10.0; later advisories upgraded that to NU1902.

**Fix.** Bump every OpenTelemetry.* package in `src/Aspire.ResourceService.Standalone.ServiceDefaults/*.csproj` to ≥ 1.15.0 (we use 1.15.2 stable + 1.15.0-beta.1 for the GrpcNetClient/Process instrumentations). Fork commit `ca5558e`.

Future advisories will need the same kind of bump; do not turn off `TreatWarningsAsErrors` to dodge it.

## `BaseIntermediateOutputPath` per service breaks ProjectReference resolution

**Symptom.** During the publish-all loop, the second iteration crashes with hundreds of `CS0246: type or namespace 'Grain' / 'IGrainFactory' / 'GenerateSerializerAttribute' could not be found` errors in transitive Infrastructure projects.

**Cause.** A previous attempt at fixing the missing-`_framework` issue passed `/p:BaseIntermediateOutputPath=/tmp/obj/<svc>/` and `/p:BaseOutputPath=/tmp/bin/<svc>/` to isolate per-service intermediate dirs. That property only applies to the **entry** project — every transitive `<ProjectReference>` keeps using the default `obj/` in its own source folder. Restore goes to `/tmp/obj/<svc>/`, transitives have no `project.assets.json` in their default `obj/`, and the C# compiler resolves none of the Orleans types.

**Fix.** Do **not** use `BaseIntermediateOutputPath` to isolate per-service publishes. Either (a) accept that obj/ is shared across the loop and clean SWA tracking between iterations, or (b) publish each service in its own copy of the source tree. The current Dockerfile uses (a) — sequential publish, shared obj/, and the `Microsoft.AspNetCore.App.Internal.Assets` package reference makes the static-asset issue moot.

## Console: `app.UseStaticFiles()` together with `app.MapStaticAssets()`

**Symptom.** Some Blazor static-asset routes serve correctly, others 404 — inconsistent depending on which middleware reaches the request first.

**Cause.** In ASP.NET Core 9+/Blazor Web App, `MapStaticAssets()` replaces `UseStaticFiles()` for wwwroot files **and** virtual framework assets. Calling both causes `UseStaticFiles` to short-circuit some paths (own-wwwroot files) before they hit the endpoint dispatch where `MapStaticAssets` would have served them. The two systems are mutually exclusive.

**Fix.** Remove `app.UseStaticFiles()`; keep only `app.MapStaticAssets()`:
```csharp
// before
app.UseHttpsRedirection();
app.UseStaticFiles();        // ❌ delete
...
app.MapStaticAssets();

// after
app.UseHttpsRedirection();
...
app.MapStaticAssets();
```
Done in `ConsoleGateway/Program.cs`. Apply the same change to any other Web SDK service that mixes the two.

## Aspire Dashboard takes ~2 minutes to become reachable after deploy

**Symptom.** Right after a successful redeploy, `https://aspire.minesleader.xyz/` hangs / 504s for 1-3 minutes, then suddenly works. Console and game services are responsive immediately.

**Cause.** `aspire-dashboard` is wired with `depends_on: resource-service condition: service_started`. `service_started` waits only until Docker has booted the container, **not** until its gRPC port is actually accepting connections. The dashboard kicks off `WatchResources` immediately, the call fails (port not yet listening), and the gRPC channel goes into exponential back-off. Each retry doubles the delay; after a few rounds the back-off window happens to land just past the moment resource-service is ready, and the next call succeeds.

**Fix.** Replace `service_started` with `service_healthy` and add a TCP healthcheck to `resource-service`:

```yaml
resource-service:
  healthcheck:
    test: ["CMD-SHELL", "exec 3<>/dev/tcp/127.0.0.1/80 && echo ok >&3"]
    interval: 1s
    retries: 30
    start_period: 2s

aspire-dashboard:
  depends_on:
    resource-service:
      condition: service_healthy
```

Without `start_period`, the first failed checks during the ~3-second cold start mark the container unhealthy and Coolify may flap it. With it, the dashboard waits the real ~5s instead of the back-off ~120s.

## "Login to the dashboard at http://localhost:18888" startup log line

**Symptom.** Aspire dashboard logs `Login to the dashboard at http://localhost:1...` (truncated). Looks alarming.

**Cause.** Cosmetic. The dashboard prints its bind address (always `0.0.0.0:18888` inside the container) and assumes the user opens it as `localhost`. Coolify Traefik terminates TLS externally and proxies to that bind address.

**Fix.** Ignore. The real entry point is `https://aspire.<your-domain>/` and the browser-token URL is shown inside the dashboard UI after first login — not in this log line.

## Quick reference: where to look when a deploy goes sideways

| Symptom | First check | Second |
|---|---|---|
| Build never finishes | BuildKit log on Coolify — is `restore` `CACHED`? | `.dockerignore` not over-excluding |
| `lstat /backend` at build start | Coolify Base Directory / Compose Location fields | `context:` relative path in compose |
| `unknown flag: parents` | first line of Dockerfile uses `1.7-labs` | — |
| `NETSDK1013` during restore | `Directory.Build.props` copied into restore stage | no excluded `.csproj` in the solution being restored |
| Random missing NuGet file | publish still parallel on shared cache? | switch to sequential `publish-all` or add `sharing=locked` |
| 502 on multi-port service | Coolify Domain field has explicit `:port`? | other containers also claim the host |
| Migrator fails | `docker logs <migrator>` for Npgsql error | both `postgres` and `ConnectionStrings__postgres` set |
| Silo never goes healthy | `docker logs <silo>` for `OrleansQuery` errors | `OrleansClusteringSetup` ran (migrator logs) |
| Service unreachable via HTTPS | `coolify-proxy` access logs for the host | Traefik routers API for stale entries |
| Resources tab empty | `docker logs <resource-service>` | socket mount + project filter env |
| Resource-service stuck on old SHA | `image:` tag matches `context:` SHA? | `docker compose build --no-cache resource-service` |
| Dev: pgbouncer `DNS lookup failed: postgres` | `docker ps` persistent postgres/pgbouncer still around? | `docker rm -f` them, rerun `aspire run` |
| Dev: Aspire postgres `Ports=map[]` | other Postgres on 9432? | `docker stop ml-pg` before `aspire run` |
| RAM blew up after deploy | `docker stats --no-stream` filtered by project prefix | a service stuck in restart loop |
| Blazor `_framework/blazor.web.js` 404 | `Microsoft.AspNetCore.App.Internal.Assets` in csproj? | version matches SDK pack `Microsoft.AspNetCore.App.Ref/<ver>` |
| Blazor buttons silently dead | server logs for `AntiforgeryValidationException` | persistent DataProtection volume + `user: root` |
| `repository does not contain ref <sha>` | `context:` uses **full** 40-char SHA, not abbreviated | `git rev-parse <ref>` to get full SHA |
| Resource-service `CS0718 / CS0234` after rename | static class collision with proto-generated `DashboardService` | wrapper renamed everywhere incl. tests |
| `NU1902` advisory for OTel | bump OpenTelemetry.* to ≥ 1.15 in fork's ServiceDefaults csproj | do not disable TreatWarningsAsErrors |
| `CS0246` flood after Dockerfile tweak | per-service `BaseIntermediateOutputPath` was added? | revert — it breaks transitive ProjectReferences |
| Aspire dashboard 504 for first ~2 min | `depends_on: resource-service` is still `service_started`? | switch to `service_healthy` + TCP healthcheck on resource-service |
| `_blazor/negotiate` 502/503/504 then container is `Up healthy` | Traefik pool holding the previous container ID | `docker restart coolify-proxy` (kicks all sites for ~10 s) |
