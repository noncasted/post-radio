# Benchmarks Case Collector Agent

Analyze a backend feature or area and produce a structured list of benchmark cases to write.

## Input

You receive a feature name or area description (e.g. "messaging", "user progression", "card system", "task balancer").

## Process

### Step 1 — Understand existing coverage

Read these files to know what benchmarks already exist:

- `backend/Benchmarks/docs/INDEX.md` — framework overview and group summary
- `backend/Benchmarks/docs/STATE.md` — existing State benchmarks
- `backend/Benchmarks/docs/MESSAGING.md` — existing Messaging benchmarks
- `backend/Benchmarks/docs/GAME.md` — existing Game benchmarks
- `backend/Benchmarks/docs/META.md` — existing Meta benchmarks
- `backend/Benchmarks/docs/INFRASTRUCTURE.md` — existing Infrastructure benchmarks

Read the matching group directory to see actual patterns:
- `backend/Benchmarks/State/*.cs`
- `backend/Benchmarks/Messaging/*.cs`
- `backend/Benchmarks/Game/*.cs`
- `backend/Benchmarks/Meta/*.cs`
- `backend/Benchmarks/Infrastructure/*.cs`

Read `backend/Benchmarks/TestGroups.cs` for valid group names.

### Step 2 — Analyze the feature code

Find and read the feature's source code:
- Grain interfaces (look for `IGrainWith*Key`)
- Grain implementations
- State classes (`IStateValue`)
- Service interfaces and implementations
- Look in `backend/Game/`, `backend/Meta/`, `backend/Infrastructure/`, `backend/Common/`

Identify every meaningful operation: CRUD methods, transaction chains, async workflows, state transitions, message flows.

### Step 3 — Identify gaps

Compare existing benchmarks against the feature's operations. List what's NOT covered yet.

### Step 4 — Propose benchmark cases

For each gap, propose a benchmark case. Avoid duplicating existing benchmarks.

## Output Format

Return a structured list. Each case must include:

```
### [case number]. [proposed title]
- **Group**: State | Messaging | Game | Meta | Infrastructure
- **MetricName**: ops/s | msg/s | ms
- **What it measures**: 1-2 sentences
- **Distributed**: Yes | No
- **Payload fields**: field=type=default (or "EmptyPayload")
- **Template**: closest existing benchmark to follow (file path)
```

## Choosing the right metric

| Scenario | MetricName | How reported |
|----------|-----------|--------------|
| Concurrent iterations (throughput) | ops/s | Auto via `RunConcurrentIterations` |
| Message sending/receiving | msg/s | Manual `Stopwatch` + `handle.ReportMetric()` |
| Correctness / single operation | ms | Auto fallback (DurationMs) |

## Choosing the right template

| Benchmark type | Template file |
|---------------|--------------|
| Throughput with concurrent iterations | `backend/Benchmarks/State/StateTest.cs` |
| Messaging throughput (distributed) | `backend/Benchmarks/Messaging/MessagingDirectQueueStressTest.cs` |
| Correctness (single operation, ms) | `backend/Benchmarks/State/StatePersistenceTest.cs` |
| Game logic (pure, no grains) | `backend/Benchmarks/Game/BoardGenerationTest.cs` |
| Meta (user/match lifecycle) | `backend/Benchmarks/Meta/MatchRecordingTest.cs` |
| Infrastructure (task scheduling) | `backend/Benchmarks/Infrastructure/TaskBalancerPriorityTest.cs` |
