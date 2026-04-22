---
name: benchmark
description: Write new benchmarks for post-radio backend features using the cluster benchmark framework. Use whenever the user asks to benchmark, performance-test, or measure throughput of any backend feature — grains, state, transactions, messaging, task scheduling, meta services, infrastructure. Also trigger on "benchmark this", "add benchmark coverage", "measure performance of".
---

# Write Benchmarks

Этот скилл пишет бенчмарки через cluster benchmark framework post-radio backend.

## Prerequisite

Benchmark project (`Benchmarks.csproj`) пока отсутствует. Первый прогон должен:
1. Создать `backend/Tools/Benchmarks/Benchmarks.csproj`.
2. Подтянуть cluster-benchmark фреймворк (`ClusterBenchmarkRoot`, `BenchmarkGroups` / аналоги) из `backend/Cluster/` / `backend/Infrastructure/` или написать минимальный скелет, если ещё нет.
3. Добавить в `backend/post-radio.slnx`.

Дальнейшие запуски пишут отдельные бенчмарки внутри него.

## Orchestration

Скилл оркестрирует 3 агента по очереди:
1. **Case collector** — анализирует фичу и предлагает кейсы.
2. **Writer** — пишет benchmark .cs файлы.
3. **Docs updater** — обновляет документацию в `backend/Tools/Benchmarks/docs/`.

## Execution Steps

### Step 1 — Collect benchmark cases

Launch `benchmarks-case-collector` agent с описанием фичи/области.

```
Read agents/benchmarks-case-collector.md for the full agent prompt.

Input: user description (e.g. "messaging", "state read/write", "DeployIdPipe")
Output: structured list of benchmark cases
```

### Step 2 — Confirm with user

Покажи список в таблице:

```
| # | Title | Group | Metric | Distributed | Description |
```

Ask: "Это бенчмарки, которые я напишу. Добавить, убрать, изменить?"

Дождись подтверждения.

### Step 3 — Write benchmarks

Launch `benchmarks-writer` agent с утверждённым списком.

```
Read agents/benchmarks-writer.md for the full agent prompt.

Input: confirmed case list + feature code context
Output: new .cs files in correct group subdirectories under backend/Tools/Benchmarks/
```

### Step 4 — Verify build

```bash
dotnet build backend/Tools/Benchmarks/Benchmarks.csproj
```

Fix compilation errors.

### Step 5 — Update documentation

Launch `benchmarks-docs` agent.

```
Read agents/benchmarks-docs.md for the full agent prompt.

Input: paths to newly created benchmark files
Output: updated docs in backend/Tools/Benchmarks/docs/
```

### Step 6 — Report

Отчёт пользователю:
- Список новых benchmark-файлов с путями.
- Build status.
- Обновлённые doc-файлы.

## Conventions (post-radio)

- SDK-style csproj — новые `.cs` файлы auto-include.
- Метрики: ops/sec, latency (p50/p99), memory allocs — в зависимости от кейса.
- Pure-code и распределённые (multi-node) бенчмарки — разные шаблоны; case-collector выбирает тип.
- Группа (`Group`) — по сути подсистемы (`State`, `Messaging`, `Infrastructure`, `Deploy`, ...), не по расположению кода.
- Title — kebab-case.
