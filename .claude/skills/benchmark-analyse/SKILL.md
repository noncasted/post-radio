---
name: benchmark-analyse
description: Run post-radio backend benchmarks via API and analyse results — trends, regressions, anomalies. Use when user asks to run benchmarks, check performance, compare benchmark results, or analyse benchmark history. Also trigger on "run benchmarks", "check performance", "benchmark results", "performance regression".
---
# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /benchmark-analyse, benchmark analyse, analyze benchmarks, проанализируй бенчмарки
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.


# Benchmark Analyse

Запускает бенчмарки через `ConsoleGateway` API и интерпретирует результаты.

## Prerequisites

1. Aspire-кластер должен быть запущен. Используй `/start-cluster` если нужно.
2. Benchmark-проект должен существовать (см. `/benchmark`), ConsoleGateway должен экспонировать `/api/benchmarks/*` endpoints.

Если бенчмарк-API ещё не реализован — сообщи пользователю и предложи: (1) создать benchmark-проект через `/benchmark`, (2) добавить API-эндпоинты в `ConsoleGateway`. Скилл бесполезен без обоих компонентов.

## API

Base URL — порт ConsoleGateway, назначается динамически Aspire. Получи его через Aspire Dashboard (`http://localhost:7100`) или Resource Service.

Ожидаемые эндпоинты (конвенция):

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/benchmarks` | List all benchmarks with last result |
| GET | `/api/benchmarks/group/{group}` | List benchmarks in group |
| POST | `/api/benchmarks/{title}/run` | Run single benchmark |
| POST | `/api/benchmarks/group/{group}/run` | Run all benchmarks in group sequentially |
| GET | `/api/benchmarks/{title}/history` | Historical results |
| GET | `/api/benchmarks/group/{group}/history` | Group history |

Актуальный список эндпоинтов — через `grep "/api/benchmarks" backend/Orchestration/ConsoleGateway/`.

## Execution Steps

### Step 1 — Determine what to do

Parse the user's request:
- **"run all"** / **"run benchmarks"** → все группы последовательно.
- **"run {group}"** → конкретная группа.
- **"run {title}"** → один бенчмарк.
- **"analyse"** / **"check"** → fetch history, без нового прогона.
- **"compare"** → fetch history, смотрим тренды.

### Step 2 — Check cluster availability and start if needed

Сначала найди ConsoleGateway URL (из Dashboard или заведомо известной переменной). Затем:
```bash
curl -s -o /dev/null -w "%{http_code}" <console-gateway-url>/api/benchmarks
```

Not 200/503 → запусти кластер сам:
1. Background: `dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http` (`run_in_background=true`).
2. Poll: `curl` каждые 5s до 200 (до 2 минут).
3. Если не поднялся за 2 мин — собери stdout/stderr фоновой задачи и покажи пользователю.

503 (cluster initializing) → poll до 200.

### Step 3 — Run benchmarks (если нужно)

```bash
# Single benchmark
curl -s -X POST <url>/api/benchmarks/{title}/run --connect-timeout 5 --max-time 120

# Group
curl -s -X POST <url>/api/benchmarks/group/{group}/run --connect-timeout 5 --max-time 600
```

Report each result as it completes. Summary table after all finish.

### Step 4 — Fetch history

```bash
curl -s <url>/api/benchmarks/{title}/history
curl -s <url>/api/benchmarks/group/{group}/history
```

### Step 5 — Analyse and report

```
## Результаты бенчмарков — {Group}

| Benchmark | Result | Metric | Duration | Status |
|-----------|--------|--------|----------|--------|
| state     | 12345  | ops/s  | 3200ms   | OK     |

## Анализ трендов (последние N запусков)

| Benchmark | Current | Previous | Delta | Trend |
|-----------|---------|----------|-------|-------|
| state     | 12345   | 11800    | +4.6% | stable |
```

### Analysis criteria

- **Regression**: current < previous на >10% → flag.
- **Improvement**: current > previous на >10% → flag.
- **Stable**: ±10% → stable.
- **Anomaly**: один запуск отклоняется от среднего последних 5 на >25%.
- **Trend**: 3+ подряд монотонного уменьшения → "degrading trend".

### Step 6 — Recommendations

- Какие бенчмарки регрессировали и на сколько.
- Возможные причины (если пользователь делал изменения — `git log`).
- Статистическая значимость (сравни с дисперсией истории).
- Suggested next steps (re-run для подтверждения, исследовать конкретную область).

## Output Language

Prose — на русском. Имена бенчмарков и метрики — English.
