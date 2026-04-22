# Telemetry & Logging

All telemetry data is stored in `.telemetry/` at the project root (gitignored).

## Directory Structure

```
.telemetry/
  metrics/          — service metrics snapshots (JSON)
  logs/             — backend file logs (all levels including Trace)
  logs-games/       — game session logs (per-session files)
```

## Metrics (`.telemetry/metrics/`)

**Writer:** `backend/Infrastructure/Metrics/MetricsSnapshotService.cs`

- Hosted service, writes every 10 seconds
- Listens to `MeterListener` for "Backend" meter
- Output: `metrics_{serviceName}.json` (e.g. `metrics_silo.json`, `metrics_metagateway.json`)
- Contains counters, histograms with min/max/avg/sum

## Backend File Logs (`.telemetry/logs/`)

**Writer:** `backend/Infrastructure/Logging/FileLoggerProvider.cs`

- Registered in `ServiceDefaultsExtensions.ConfigureOpenTelemetry()`
- Minimum level: **Trace** (all levels written to file)
- Output: `{serviceName}.log` (e.g. `silo.log`, `metagateway.log`)
- Format: `[timestamp] [TRC/DBG/INF/WRN/ERR/CRT] [category] message`
- Logs go to both Aspire (OpenTelemetry) and file simultaneously

## Game Session Logs (`.telemetry/logs-games/`)

**Writer:** `backend/Game/Session/Logging/SessionFileLogger.cs`

- Created per game session, disposed on session end
- Output: `{date}/{sessionId}.log` (e.g. `2026-04-13/{guid}.log`)
- Logs game events: cell opens, flags, card usage, health/mana changes, bot actions, round start/end, game over

## Shared Utility

`backend/Infrastructure/TelemetryPaths.cs` — shared helper:
- `FindProjectRoot()` — walks up from `AppContext.BaseDirectory` looking for `.git`
- `GetTelemetryDir(subfolder)` — returns `.telemetry/{subfolder}`, creates directory if needed
