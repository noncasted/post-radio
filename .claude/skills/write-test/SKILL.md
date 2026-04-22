---
name: write-test
description: Write integration tests for post-radio backend features using the Orleans cluster test framework. Use this skill whenever the user asks to write, create, or add tests for any backend feature — grains, state, transactions, messaging, meta services, deploy-epoch, infrastructure. Also trigger on "cover this with tests", "add test coverage", or mentions of testing backend code.
---

# Write Integration Tests

Этот скилл пишет тесты для post-radio backend через custom cluster test framework.

## Prerequisite

Test project (`Tests.csproj`) пока не существует. Первый прогон должен:
1. Создать `backend/Tools/Tests/Tests.csproj` (Microsoft Testing Platform — см. `/test` skill за шаблоном csproj и `docs/CLAUDE_MISTAKES.md` lesson 1 за Lifetime-правилами).
2. Подтянуть Orleans cluster fixtures из `backend/Cluster/` / `backend/Infrastructure/`.
3. Добавить в `backend/post-radio.slnx`.

Дальнейшие запуски пишут отдельные тест-файлы в него.

## Before writing ANY test

1. **Understand what's being tested.** Прочитай код фичи — grain interfaces, implementations, state classes, relevant services.

2. **Find similar existing tests.** Search the test project for tests, покрывающие близкий домен. Read 1-2 closest matches. They are ground truth for conventions and available infrastructure.

3. **Decide the test type** based on what the feature does:

| Feature type | Test pattern | Payload | Notes |
|---|---|---|---|
| Pure logic (parsers, calculations) | Synchronous, no grains | `EmptyPayload` | `return Task.CompletedTask` |
| Single-grain CRUD / state | Grain calls + transactions | `EmptyPayload` | Use `_transactions.Run(...)` |
| Concurrent grain operations | `RunConcurrentIterations` | `IConcurrentIterationTestPayload` | Stress test |
| Cross-service messaging | Multi-node Root + Node | Custom payload | For real distributed tests |

## Test structure

```csharp
using Common.Extensions;
using Infrastructure;
// other imports

namespace Tests;

public class {Feature}Test
{
    // Всегда определяй СВОЙ payload — не ссылайся на payload другого теста
    [GenerateSerializer]
    public class Payload { }

    public class Root : ClusterTestRoot<Payload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions)
            : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;

        public override string Group => TestGroups.Meta; // Meta | State | Messaging | ...
        public override string Title => "feature-name";  // kebab-case

        protected override async Task Run(ClusterTestNodeHandle handle, Payload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            // --- Arrange ---
            // --- Act ---
            // --- Assert ---

            handle.Progress.SetProgress(1f);
        }
    }
}
```

## Atomic tests — one behavior per test class

Каждый тест класс проверяет ОДНО поведение. Нужно покрыть несколько аспектов → несколько классов.

**Wrong:** один жирный тест, покрывающий очередь + dedup + delay + priority + concurrency.

**Right:** отдельные классы `QueueCollectTest`, `QueueDeduplicationTest`, `QueueDelayTest`, `BalancerPriorityTest`, `BalancerConcurrencyTest`.

## Key conventions

### Lifetime — ВСЕГДА `handle.Lifetime`, НИКОГДА `new Lifetime()`

`handle.Lifetime` терминируется `ClusterTestRoot` автоматически по завершении (успех или падение). `new Lifetime()` = orphan при исключении → фоновые циклы висят.

```csharp
// WRONG — утечка при падении теста
var lifetime = new Lifetime();
service.Run(lifetime);

// CORRECT
service.Run(handle.Lifetime);

// Нужен child scope:
var child = handle.Lifetime.Child();
```

Это CLAUDE_MISTAKES.md lesson 1.

### Outer class + inner `Root`

Outer = `{Feature}Test`, inner всегда `Root : ClusterTestRoot<TPayload>`. Test discovery зависит от этого.

### Constructor injection (не `[Inject]`)

Orleans-style: `ClusterTestUtils` первым, потом `IOrleans`, `ITransactions`, feature services.

### `Group` + `Title`

- `Group` — константа из `TestGroups`. Выбирай по тому, ЧТО тестируется, не где лежит код.
- `Title` — kebab-case: `"user-rating"`, `"deploy-coordinator-bootstrap"`.

### Progress tracking

```csharp
handle.Progress.SetStatus(OperationStatus.InProgress);
handle.Progress.SetProgress(0.3f);
handle.Progress.Log("created user, testing state ops");
handle.Progress.SetProgress(0.7f);
handle.Progress.SetProgress(1f);
```

### Assertions

```csharp
TestAssert.Equal(expected, actual, "context");
TestAssert.NotNull(result, "grain response");
TestAssert.True(condition, "invariant");
TestAssert.GreaterThan(value, 0, "counter");
TestAssert.Contains(item, collection, "membership");
```

Или прямой throw:
```csharp
if (state.Count == 0)
    throw new Exception("No state entries after init");
```

Предпочитай `TestAssert` — контекст помогает диагностировать падение.

### State cleanup

Трекай всё созданное, чтобы удалить после теста:
```csharp
Cleanup.TrackUser(userId);
Cleanup.Track<MyState>(grainId);
```

Без трекинга — утечка в БД, влияет на другие тесты.

### Payload types

Empty (большинство):
```csharp
[GenerateSerializer]
public class Payload { }

public class Root : ClusterTestRoot<Payload>
```

Concurrent:
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

### Transactions

```csharp
var result = await _transactions.Run(() => grain.DoSomething());
```

### Creating test users

Если есть `IUserFactory`:
```csharp
var userId = await _userFactory.Create(new UserCreateOptions());
Cleanup.TrackUser(userId);
```

### Pure logic tests (без grains)

```csharp
protected override Task Run(ClusterTestNodeHandle handle, Payload payload)
{
    // test logic
    return Task.CompletedTask;
}
```

## File placement

Subfolder matching `Group` внутри тест-проекта. File name matches outer class: `{Feature}Test.cs`. SDK-style csproj auto-includes — без ручного `<Compile Include>`.

## Good test checklist

- Тестирует поведение, а не реализацию.
- Happy path → edge cases (empty, duplicate, invalid input).
- Проверяет persistence (write → read back).
- Реалистичные данные.
- Cleans up after itself.
- Reports progress на ключевых точках.
