---
name: test
description: Write xUnit integration and unit tests for post-radio backend features. Use this skill whenever the user asks to write, create, add, or cover tests for any backend functionality — grains, state, transactions, messaging, meta services, infrastructure. Also trigger on "cover with tests", "add test coverage", "test this".
---

# Write Tests

## Codex Adaptation

If this workflow mentions launching agents, use Codex subagents only when the user explicitly asks for parallel agents or delegation. Otherwise, perform the case collection, writing, and docs update locally while following the referenced prompts as checklists.


This skill writes xUnit tests for post-radio backend features.

## Prerequisite

The repo does not yet contain a test project. Before the first test:

1. Decide on a location (typical: `backend/Tools/Tests/Tests.csproj`).
2. Create the csproj with Microsoft Testing Platform configured — see `.codex/docs/TESTING.md` for the required properties. Minimal requirement for readable logs:
```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <IsTestProject>true</IsTestProject>
  <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
  <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
</PropertyGroup>
<ItemGroup>
  <Content Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest"/>
</ItemGroup>
```
3. Reference Orleans test cluster fixtures from `backend/Cluster/` / `backend/Infrastructure/`.
4. Add to `backend/post-radio.slnx`.

Once the test project exists, this skill writes individual test files inside it.

## Invocation

```
/test <description of what to test>
```

Examples:
- `/test UserRating grain`
- `/test DeployCoordinator bootstrap`
- `/test StateCollection sync`

## Execution Flow

Run three phases sequentially. Each phase uses a subagent.

### Phase 1: Collect Test Cases

Launch Agent with `subagent_type: "general-purpose"` and the prompt from `agents/tests-case-collector.md`, passing:
- User description of what to test.
- Instruction to output a structured list: { title, type (unit/integration), scenario, expected, payload-style }.

Wait for the result. Present the list and ask for confirmation before Phase 2.

### Phase 2: Write Tests

Launch Agent with `subagent_type: "general-purpose"` and the prompt from `agents/tests-writer.md`, passing:
- Confirmed case list.
- Feature name and source file paths from Phase 1.
- Target test project path.

Wait for result. Build: `dotnet build <path-to-Tests.csproj>`. Fix compile errors.

### Phase 3: Update Documentation

Launch Agent with `subagent_type: "general-purpose"` and the prompt from `agents/tests-docs.md`:
- Test file path from Phase 2.
- Case list from Phase 1.

Report updated docs.

## Test conventions (post-radio)

### Lifetime — use the test handle lifetime, never `new Lifetime()`

See `.codex/docs/CLAUDE_MISTAKES.md` lesson 1. Orphan `new Lifetime()` in tests leaks background loops on test failure. Always use `handle.Lifetime` or `handle.Lifetime.Child()`.

### Outer class + inner `Root`

Pattern:
```csharp
public class {Feature}Test
{
    [GenerateSerializer]
    public class Payload { }

    public class Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions)
        : ClusterTestRoot<Payload>(utils)
    {
        private readonly IOrleans _orleans = orleans;
        private readonly ITransactions _transactions = transactions;

        public override string Group => TestGroups.Meta; // Meta | State | Messaging | ...
        public override string Title => "feature-name";  // kebab-case

        protected override async Task Run(ClusterTestNodeHandle handle, Payload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            // Arrange
            // Act
            // Assert (TestAssert.* or direct throws)

            handle.Progress.SetProgress(1f);
        }
    }
}
```

### Atomic tests — one behavior per test class

Each class verifies ONE scenario. Multiple aspects → multiple test classes.

### Constructor injection (not `[Inject]`)

`ClusterTestUtils` is always first, then `IOrleans`, `ITransactions`, or feature-specific services.

### Transactions

Wrap grain calls requiring transaction:
```csharp
var result = await _transactions.Run(() => grain.DoSomething());
```

### Cleanup

Track created state for post-test deletion:
```csharp
Cleanup.TrackUser(userId);
Cleanup.Track<MyState>(grainId);
```

### Payload types

Default: empty `Payload` inside the test class.

For concurrent iterations:
```csharp
[GenerateSerializer]
[method: SetsRequiredMembers]
public class StartPayload() : IConcurrentIterationTestPayload
{
    [Id(0)] public int Iterations { get; set; } = 10;
    [Id(1)] public int Concurrent { get; set; } = 3;
}

await handle.RunConcurrentIterations(payload, Process);
```

### Assertions

`TestAssert.Equal(expected, actual, "message")`, `TestAssert.NotNull`, `TestAssert.True`, `TestAssert.GreaterThan`, `TestAssert.Contains`. Or direct `throw new Exception("...")` for simple checks.

### Progress tracking

```csharp
handle.Progress.SetStatus(OperationStatus.InProgress);
handle.Progress.SetProgress(0.3f);
handle.Progress.Log("loaded user");
handle.Progress.SetProgress(1f);
```

## File placement

By `Group` — matching subfolder under the test project root, e.g.:
- `TestGroups.Meta` → `.../Tests/Meta/`
- `TestGroups.State` → `.../Tests/State/`
- `TestGroups.Messaging` → `.../Tests/Messaging/`

SDK-style csproj auto-includes new `.cs` files — no manual `<Compile Include>`.

## Good test checklist

- Tests actual behavior, not implementation details.
- Happy path first, then edge cases.
- Verifies persistence (write → read back → verify).
- Uses realistic data (real grain keys, plausible sizes).
- Cleans up all created state.
- Reports progress at meaningful checkpoints.
