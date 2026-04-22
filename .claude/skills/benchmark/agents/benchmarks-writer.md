# Benchmarks Writer Agent

Write benchmark .cs files for confirmed benchmark cases, following existing patterns exactly.

## Input

You receive:
1. A list of confirmed benchmark cases (title, group, metric, payload fields, template reference)
2. Context about the feature being benchmarked (grain interfaces, state classes, etc.)

## Process

### Step 1 — Read the template

For each benchmark case, read the template file specified in the case definition. Understand its exact structure: imports, class nesting, constructor signature, payload class, Run() method.

### Step 2 — Read framework base classes

Read these to understand the API:
- `backend/Benchmarks/Common/ClusterTestRoot.cs` — base class, constructor signature, ReportMetric()
- `backend/Benchmarks/Common/ClusterTestNodeHandle.cs` — handle passed to Run()
- `backend/Benchmarks/Common/ClusterTestNode.cs` — base for distributed nodes (only if distributed)
- `backend/Benchmarks/TestsExtensions.cs` — RunConcurrentIterations extension
- `backend/Benchmarks/Common/TestCleanup.cs` — cleanup patterns
- `backend/Benchmarks/Common/TestAssert.cs` — assertion helpers

### Step 3 — Write each benchmark file

For each case, create a .cs file in the correct group directory.

#### Mandatory structure

Every benchmark file must follow this exact pattern:

```csharp
using System.Diagnostics.CodeAnalysis;
using Common.Extensions;
using Infrastructure;
// ... other imports as needed

namespace Benchmarks;

public class {Feature}{Type}Test
{
    // Payload class (or use StateMigrationTest.EmptyPayload for no-config benchmarks)
    [GenerateSerializer]
    [method: SetsRequiredMembers]
    public class StartPayload() : IConcurrentIterationTestPayload  // only for throughput
    {
        [Id(0)] public int Iterations { get; set; } = 100;
        [Id(1)] public int Concurrent { get; set; } = 10;
    }

    public class Root : ClusterTestRoot<StartPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage, IOrleans orleans)
            : base(utils, benchmarkStorage)
        {
            _orleans = orleans;
        }

        private readonly IOrleans _orleans;

        public override string Group => TestGroups.State;  // use correct group
        public override string Title => "feature-type";
        public override string MetricName => "ops/s";  // ops/s, msg/s, or ms

        protected override async Task Run(ClusterTestNodeHandle handle, StartPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);
            // benchmark logic here
        }
    }
}
```

#### Constructor rules

- First two params ALWAYS: `ClusterTestUtils utils, BenchmarkStorage benchmarkStorage`
- Additional params after: `IOrleans orleans`, `ITransactions transactions`, etc.
- Pass to base: `base(utils, benchmarkStorage)`

#### Metric reporting

| MetricName | How to report |
|-----------|--------------|
| ops/s | Use `handle.RunConcurrentIterations(payload, action)` — auto-reports |
| msg/s | Add `Stopwatch`, call `handle.ReportMetric(totalMessages / elapsed.TotalSeconds)` |
| ms | Don't call ReportMetric — auto-fallback to DurationMs |

#### Payload rules

- `[GenerateSerializer]` on every payload class
- `[Id(N)]` on every field, sequential starting from 0
- For throughput: implement `IConcurrentIterationTestPayload` (requires `Iterations` and `Concurrent`)
- For no-config: use `StateMigrationTest.EmptyPayload`

#### Cleanup rules

- Call `Cleanup.Track<TState>(key)` for every grain state created
- Call `Cleanup.TrackUser(userId)` for user grains
- Call `Cleanup.TrackMatch(matchId)` for match grains
- Cleanup executes automatically after Run() completes

#### File naming

- Pattern: `{Feature}{Type}Test.cs` (e.g. `UserDeckTest.cs`, `TransactionLargeBatchTest.cs`)
- Place in correct directory: `backend/Benchmarks/{Group}/`

### Step 4 — Verify build

Run `dotnet build backend/Benchmarks/Benchmarks.csproj` and fix any errors.

## Output

Report created files with their full paths.
