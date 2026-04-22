# Codex Agent Instructions

## Overview

Backend-only project on .NET 10, Orleans, Aspire for dev, Docker Compose and Coolify for prod, PostgreSQL, and Blazor console.

Repository structure:
- `backend/Cluster/` - deploy epoch, coordination, service discovery, diagnostics, monitoring, state.
- `backend/Common/` - shared utilities: `Reactive/`, `Storage/`, `Extensions/`, `Lookups/`.
- `backend/Infrastructure/` - Orleans infrastructure, metrics, logging, data collections.
- `backend/Meta/` - domain grains: users, bots, online, images, and related services.
- `backend/Orchestration/` - Aspire AppHost, silo, console gateway, orchestration extensions.
- `backend/Frontend/` - Blazor frontend: `Client/`, `Server/`, `Shared/`.
- `backend/Console/` - Blazor admin console pages and components.
- `backend/Tools/` - tests, benchmarks, deploy assets.
- `tools/` - helper scripts.
- `.codex/docs/` - primary project documentation. Treat these files as the project knowledge base for Codex too.

## How To Use The Docs

Before making changes, read only the docs relevant to the task. Use `.codex/docs/TRIGGERS.md` as the broad keyword index when unsure.

Core docs:
- `.codex/docs/COMMON_ORLEANS.md` - grains, `State<T>`, state collections, `IOrleans`, messaging, jsonb storage.
- `.codex/docs/COMMON_LIFETIMES.md` - `Lifetime`, subscriptions, reactive primitives, deploy-scoped lifetimes.
- `.codex/docs/BLAZOR.md` - Blazor console UI rules and component patterns.
- `.codex/docs/DEPLOY.md` - dev/prod deploy architecture.
- `.codex/docs/DEPLOY_TROUBLESHOOTING.md` - deploy failure diagnosis.
- `.codex/docs/TESTING.md` - xUnit v3 and Microsoft Testing Platform workflow.
- `.codex/docs/TELEMETRY.md` - telemetry directory, metrics, logs, session logs.
- `.codex/docs/CODE_STYLE_FULL.md` - code style, member order, naming, braces, nullable, `Task` vs `ValueTask`.

Reference docs:
- `.codex/docs/ERRORS.md` - known errors, causes, and fixes.
- `.codex/docs/VOCABULARY.md` - project terminology.
- `.codex/docs/CLAUDE_MISTAKES.md` - accumulated lessons and anti-patterns. Use it as a Codex checklist too.
- `.codex/docs/TRIGGERS.md` - keyword-to-documentation index.

Claude Code artifacts:
- `.codex/AGENTS.md` - Codex high-level instructions and entrypoint.
- `.codex/agents/*` - review checklists. Do not treat them as autonomous agents unless the user explicitly asks for parallel agent work.
- `.codex/skills/*` - workflow references. Use as documentation/checklists when the user names one or the task clearly matches it.

## Keyword Lookup

| Keywords | Read |
| --- | --- |
| grain, `IGrainWithGuidKey`, `[Transaction]`, `State<T>`, `IStateValue`, `[GenerateSerializer]`, `[Id(N)]`, `StatesLookup`, `AddStates`, `StateCollection`, `IOrleans`, `GetGrain`, `InTransaction`, `OnActivateAsync`, `OnDeactivateAsync`, `[Reentrant]` | `.codex/docs/COMMON_ORLEANS.md` |
| messaging, `IMessaging`, `PushTransactionalQueue`, `ListenQueue`, queue subscription | `.codex/docs/COMMON_ORLEANS.md` |
| jsonb, `GrainStateStorage`, `PostgresJsonbConverter`, `OrleansStorage`, PostgreSQL payload | `.codex/docs/COMMON_ORLEANS.md` |
| `Lifetime`, `IReadOnlyLifetime`, `Advise`, `View`, `Terminate`, `Child`, `Intersect`, `TerminatedLifetime`, `DeployLifetime`, memory leak | `.codex/docs/COMMON_LIFETIMES.md` |
| `EventSource`, `LifetimedValue`, `ViewableProperty`, `ViewableList`, `ViewableDictionary`, reactive, observable | `.codex/docs/COMMON_LIFETIMES.md` |
| `DeployId`, `IDeployManagement`, `IDeployContext`, `IDeployAware`, `DeployIdPipe`, `DeployIdentity`, `LiveState`, `IServiceDiscoveryStorage`, `DeployHealthChecker`, cluster restart, coordinator ready, epoch | `.codex/docs/DEPLOY.md` |
| Aspire, AppHost, dcp, docker-compose, Coolify, Traefik, PgBouncer, migrator, Dockerfile, `DeploySetup`, `PostResourcesSetup` | `.codex/docs/DEPLOY.md` |
| deploy troubleshooting, prod broken, compose logs, pgbouncer error, migration failed | `.codex/docs/DEPLOY_TROUBLESHOOTING.md` |
| Blazor, razor, `@inject`, `[Inject]`, `UiComponent`, `ComponentBase`, `InvokeAsync`, `StateHasChanged`, `[Parameter]`, `ToastService`, `LucideIcon`, `BbAlert`, early return | `.codex/docs/BLAZOR.md` |
| xUnit v3, Microsoft Testing Platform, `UseMicrosoftTestingPlatformRunner`, `xunit.runner.json`, filter-class, filter-method, `ITestOutputHelper`, `TestResults`, UTF-16LE, `get-test-log`, `ClusterTestRoot`, `handle.Lifetime` | `.codex/docs/TESTING.md` |
| telemetry, metrics, logs, `.telemetry`, `FileLoggerProvider`, `SessionFileLogger`, `MetricsSnapshotService`, `TelemetryPaths`, logs-games, OpenTelemetry | `.codex/docs/TELEMETRY.md` |
| member order, `_camelCase`, `TryGetValue`, braces, inline init, code style | `.codex/docs/CODE_STYLE_FULL.md` |
| error lookup, memory leak, build error, test error, deploy error | `.codex/docs/ERRORS.md` |
| terminology, naming, `Advise` vs `View`, primary terms | `.codex/docs/VOCABULARY.md` |
| common mistakes, past lessons, anti-patterns | `.codex/docs/CLAUDE_MISTAKES.md` |

## Critical Rules

1. Do not inspect or edit `/bin` and `/obj`; use source files only.
2. Prose and comments are in Russian unless the surrounding file clearly uses English. Identifiers and code are in English.
3. Do not use emoji in code or docs. Use `OK:` / `WRONG:` or `[+]` / `[-]` instead.
4. Use UTF-8. xUnit v3 logs can be UTF-16LE; read them through `tools/scripts/get-test-log.sh`.
5. `.csproj` files are SDK-style. New `.cs` files are included by glob automatically; do not add manual `<Compile Include>` entries unless the project has an explicit exception.
6. Do not mix dev and prod deploy paths. `backend/Orchestration/Aspire/Program.cs` affects dev; `backend/Tools/deploy/docker-compose.yaml` affects prod.
7. Never create `new Lifetime()` in tests. Use `handle.Lifetime` or `handle.Lifetime.Child()`.
8. Observer grains must be `[Reentrant]`, use a timeout, and discard dead observers on failure.
9. If a new project anti-pattern is found, update `.codex/docs/CLAUDE_MISTAKES.md` and `.codex/docs/ERRORS.md`.
10. Protect unrelated working tree changes. This repository often has large uncommitted migrations; do not revert files unless explicitly asked.

## Verification

Prefer targeted checks first, then broader checks when risk justifies it.

Common commands:
- `dotnet build backend/post-radio.slnx`
- relevant xUnit v3 tests under `backend/Tools/Tests/`
- `tools/scripts/get-test-log.sh` when reading test logs

If a command needs network, external services, Docker, or writes outside the workspace, ask for permission through the normal Codex approval flow.
