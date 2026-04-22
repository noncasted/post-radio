# Testing — How to Run Tests & Read Their Logs

**TL;DR:** Backend tests use xUnit v3 on Microsoft Testing Platform. Logs are written in UTF-16LE — convert them via `tools/scripts/get-test-log.sh` before reading with Read tool.

---

## Test Project Layout

Backend tests live in `backend/Tools/Tests/`:

```text
backend/Tools/Tests/
├── Tests.csproj           # test project (Microsoft Testing Platform runner)
├── xunit.runner.json      # single-threaded runner config
├── Fixtures/              # Orleans test cluster fixtures
├── Execution/             # test execution helpers
├── Game/                  # game-system tests
├── Meta/                  # meta-system tests
├── Grains/                # grain-level tests
├── Messaging/             # messaging tests
└── State/                 # state tests
```

Benchmarks are in `backend/Tools/Benchmarks/` (separate project).

---

## Tests.csproj — required properties

For `dotnet test` to write log files Claude can read, the project must declare:

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

Without `UseMicrosoftTestingPlatformRunner`, logs are NOT created and Claude cannot read failures.

### xunit.runner.json

Single-threaded execution keeps Orleans test cluster deterministic:

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "diagnosticMessages": false,
  "maxParallelThreads": 1,
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false
}
```

---

## Running Tests

```bash
# All tests (background recommended — tests are slow):
dotnet test backend/Tools/Tests/Tests.csproj

# One class (xUnit v3 syntax — note the `--` separator):
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-class "*BoardTests"

# One method:
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-method "*BoardTests.RevealMine_ReturnsLoss"

# By namespace:
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-namespace "Tests.Game.Board"

# Exclude class:
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-not-class "*SlowTests"
```

xUnit v3 DROPPED the old `--filter "FullyQualifiedName~..."` syntax — it is silently ignored.

---

## Reading Test Logs (xUnit v3 UTF-16LE workflow)

xUnit v3 emits UTF-16LE logs into `**/TestResults/*.log`. The Read tool cannot parse UTF-16LE. Use this workflow:

```bash
# 1. After tests finish, convert every UTF-16LE log into UTF-8:
tools/scripts/get-test-log.sh

# 2. Find converted logs:
# Glob: **/TestResults/*.utf8.log

# 3. Open with Read tool:
# Read ./backend/Tools/Tests/bin/Debug/net10.0/TestResults/*.utf8.log
```

| File | Encoding | Readable by Claude |
|------|----------|--------------------|
| `TestResults/*.log` | UTF-16LE | no (original) |
| `TestResults/*.utf8.log` | UTF-8 | yes (converted) |

Useful bash searches on converted logs:

```bash
grep "^failed" TestResults/*.utf8.log
grep -A 10 "^failed" TestResults/*.utf8.log     # 10 lines of error detail
file -bi TestResults/*.utf8.log                 # verify encoding
```

---

## Capturing Output Inside a Test

Use `ITestOutputHelper` to emit debugging data that ends up in the log file:

```csharp
public class BoardTests {
    private readonly ITestOutputHelper _output;

    public BoardTests(ITestOutputHelper output) {
        _output = output;
    }

    [Fact]
    public void RevealMine_Loses() {
        _output.WriteLine($"board state: {board}");
        // ...
    }
}
```

---

## Debugging Hanging / Timing-Out Tests

1. If a test hangs — the problem is in code (deadlock, infinite loop, bad SQL), NOT in the test framework. Do not increase timeouts — find the root cause.
2. Never delete a failing test to "make it pass". Find the root cause.
3. Never use `Task.Delay` as a synchronization primitive — use proper waits / pumps.
4. Check Orleans application logs in `/tmp/orleans-test-*.log` (already UTF-8) if tests time out.

---

## Reference

- `backend/Tools/Tests/Tests.csproj` — canonical test project layout
- `tools/scripts/get-test-log.sh` — UTF-16LE → UTF-8 conversion
- `.codex/docs/ERRORS.md` — error lookup table (includes test-log errors)
