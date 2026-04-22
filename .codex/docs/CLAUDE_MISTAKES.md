# AI Self-Learning Log

Исторический лог ошибок и уроков — читать перед правками в deploy/Orleans/tests.

---

## Lesson 1: `new Lifetime()` в тестах (CRITICAL)

### Ошибка
```csharp
// WRONG — orphan lifetime, не будет терминирован если тест бросит до Terminate()
var lifetime = new Lifetime();
balancer.Run(lifetime);
// ... логика теста ...
lifetime.Terminate(); // не выполнится если выше брошено исключение
```

### Правильно
```csharp
// CORRECT — handle.Lifetime авто-терминируется ClusterTestRoot
balancer.Run(handle.Lifetime);
// Нужен дочерний? handle.Lifetime.Child()
```

### Почему важно
`ClusterTestRoot` создаёт Lifetime на каждый тест и терминирует после `Run()` (успех или падение). `new Lifetime()` обходит это: при исключении подписки и фоновые циклы остаются висеть, текут ресурсы и влияют на следующие тесты.

### Правило
Никогда не `new Lifetime()` в тестах. Только `handle.Lifetime` или `handle.Lifetime.Child()`.

---

## Lesson 2: Observer-grain обязан быть `[Reentrant]`

### Ошибка
```csharp
// WRONG — non-reentrant grain держит observer reference
public class RuntimePipe : Grain, IRuntimePipe
{
    private IRuntimePipeObserver? _observer;

    public Task BindObserver(IRuntimePipeObserver observer) { _observer = observer; return Task.CompletedTask; }

    public async Task<TResponse> Send<TResponse>(object message)
    {
        // Вызов _observer.Send() блокирует 30 сек когда клиент мёртв.
        // Non-reentrant grain сериализует все вызовы → новый BindObserver ждёт
        // за каждым зависшим Send. Рестарт координатора = 50+ сек заморозки.
    }
}
```

### Правильно
```csharp
using Orleans.Concurrency;

[Reentrant]
public class RuntimePipe : Grain, IRuntimePipe
{
    public async Task<TResponse> Send<TResponse>(object message)
    {
        var observer = _observer; // snapshot
        try { return await observer!.Send<TResponse>(message).WaitAsync(timeout); }
        catch (Exception ex)
        {
            if (ReferenceEquals(_observer, observer)) _observer = null; // discard dead
            throw;
        }
    }
}
```

### Правило
- Любой grain, форвардящий вызовы на observer, у которого процесс-владелец может умереть, должен быть `[Reentrant]` + **сбрасывать** observer при ошибке Send.
- Один застрявший `_observer.Send(...)` на non-reentrant grain блокирует всю очередь — свежий `BindObserver` ждёт, пока Orleans по одному выгребет очередь через outer response timeout (50 сек).
- Orleans даёт отключённым клиентам ~65 сек grace — в течение этого окна grain всё ещё маршрутизирует callback'и на мёртвого клиента.

---

## Lesson 3: Не жди свою же state-запись

### Ошибка
```csharp
// Процесс координатора:
await _loop.OnLocalSetupCompleted(lifetime);    // grain.MarkCoordinatorReady() → true
// Потом симметрично с остальными сервисами:
await WaitCoordinatorReady();                    // опрашивает grain.GetState() 55 секунд
                                                 // пока не увидит CoordinatorReady=true
```

Orleans-клиент координатора только что рестартанул; силос маршрутизирует опрос через свежее подключение. Initialize/MarkCoordinatorReady прошёл (первый бёрст), но последующие чтения висят в транзиентном роутинге пока силос не выкинет предыдущего клиента (~65 сек grace). Остальные сервисы (со своими клиентами) видят `CoordinatorReady=true` мгновенно.

### Правильно
```csharp
if (_discovery.Self.Tag == ServiceTag.Coordinator)
{
    // Координатор только что сделал await grain.MarkCoordinatorReady() — уже true.
}
else
{
    await WaitCoordinatorReady();
}
```

### Правило
Процесс, только что сделавший await собственной записи в grain, не должен опрашивать его. Локальный `await` — уже подтверждение. Симметричный startup-код для координатора и участников провоцирует «координатор ждёт самого себя» и может маскировать роутинг-задержки Orleans на минуты.

---

## Lesson 4: Запомни файловую структуру .telemetry

См. [TELEMETRY.md](TELEMETRY.md). Частая ошибка — писать логи в произвольную папку; всё должно идти через `TelemetryPaths.GetTelemetryDir(subfolder)`.

---

## Lesson 5: xUnit v3 filter syntax

См. [TESTING.md](TESTING.md). Частая ошибка — использовать `--filter "FullyQualifiedName~..."` (v2-синтаксис, тихо игнорируется в v3). Правильно — `-- --filter-class "*ClassName"` с обязательным `--` разделителем.

---

## Accumulation Log

| # | Дата | Файл | Ошибка | Урок | Статус |
|---|------|------|--------|------|--------|
| 1 | 2026-03 | Tests/* | `new Lifetime()` вместо `handle.Lifetime` | Lifetime в тестах | Fixed |
| 2 | 2026-03 | Cluster/Coordination/RuntimePipe.cs | Non-reentrant observer-grain | `[Reentrant]` + discard | Fixed |
| 3 | 2026-04 | Cluster/Coordination/* | Координатор ждёт свою же запись | Локальный `await` = подтверждение | Fixed |

---

## Процесс обновления

При обнаружении нового паттерна:
1. Добавь раздел «Lesson N» выше.
2. Обнови таблицу Accumulation Log.
3. Если ошибка периодическая — добавь строку в [ERRORS.md](ERRORS.md).
