# Code Style Guide

Стиль backend-кода проекта (.NET 10, `Nullable=enable`, `ImplicitUsings=enable`).

## Member Order (обязательно)

```csharp
public class MyService
{
    // 1. Constructor (DI)
    public MyService(IOrleans orleans, ILogger<MyService> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    // 2. Private fields — сначала readonly, потом mutable
    private readonly IOrleans _orleans;
    private readonly ILogger<MyService> _logger;
    private readonly EventSource<int> _onChanged = new();
    private int _counter;

    // 3. Public API (в порядке: properties → методы интерфейсов → остальное)
    public IEventSource<int> OnChanged => _onChanged;

    public async Task<string> GetValue(Guid id) { ... }

    // 4. Private helpers
    private void Helper() { ... }

    // 5. Local functions — внутри методов
}
```

## Field Naming: `_camelCase`

```csharp
// CORRECT
private readonly IOrleans _orleans;
private readonly List<User> _users = new();
private readonly EventSource<Guid> _onUserAdded = new();

// WRONG
private readonly IOrleans _o;             // аббревиатура
private readonly List<User> _lst;         // не описательно
private int counter;                      // без подчёркивания
```

Правила:
- Описательные имена, без аббревиатур.
- `readonly` группой вперёд, `mutable` — после.
- Коллекции инициализируй inline (см. ниже).

## Структура логики метода

```csharp
public async Task<User> GetOrCreate(Guid id, string name)
{
    // 1. Fast path — кэш / ранний выход
    if (_users.TryGetValue(id, out var cached))
    {
        return cached;
    }

    // 2. Создание
    var user = new User { Id = id, Name = name };

    // 3. Подготовка зависимостей
    await _orleans.GetGrain<IUser>(id).Initialize(user);

    // 4. Side effects
    _users[id] = user;
    _onUserAdded.Invoke(id);

    // 5. Return
    return user;
}
```

## Local Functions

Используй для замыканий, валидаций, обработчиков ошибок, специфичных для метода.

```csharp
public void Process(IEnumerable<Entry> entries, IReadOnlyLifetime lifetime)
{
    var valid = entries.Where(IsValid).ToList();
    ApplyTransform(valid);

    bool IsValid(Entry entry) => entry != null && entry.IsReady;
}
```

## Collections: Inline init

```csharp
// CORRECT
private readonly List<Item> _items = new();
private readonly Dictionary<Guid, User> _users = new();

// WRONG
private readonly List<Item> _items;
public MyService()
{
    _items = new();
}
```

## Dictionary Lookup: `TryGetValue`

```csharp
// WRONG — два lookup'а
if (_map.ContainsKey(key))
{
    var value = _map[key]; // второй lookup
}

// CORRECT — один lookup
if (_map.TryGetValue(key, out var value))
{
    // используем value
}
```

## Exception Handling: Graceful Degradation

На границе (подписка, callback, external I/O) — ловим и логируем, не крашим процесс.

```csharp
try
{
    _messaging.ListenQueue<MyUpdate>(lifetime, queueId, OnUpdate);
}
catch (Exception ex)
{
    _logger.LogError(ex, "[{Type}] Subscribe failed", GetType().Name);
    // Возврат штатно — не ронять весь сервис из-за одной подписки
}
```

Внутри доменной логики — бросай исключения, Orleans логирует их сам.

## Braces: всегда на той же строке

```csharp
// CORRECT
public class MyService
{
    public void Method()
    {
        if (condition)
        {
            DoSomething();
        }
    }
}
```

(`.editorconfig` в репозитории предписывает именно такой стиль.)

## Async / Task

- Backend использует **BCL `Task<T>` / `ValueTask<T>`**, а не `UniTask` (`UniTask` — Unity-only).
- Используй суффикс `Async` только если это общепринятая .NET-конвенция в данной подсистеме; если проект явно избегает суффикса — следуй локальному стилю.
- Fire-and-forget: не игнорируй возвращаемый `Task` молча; как минимум `_ = Method();` с локальным try-catch внутри самого метода.
- Async void — только для event handler'ов.

## Nullable

`Nullable=enable` на уровне `Directory.Build.props`. Не глуши `?` и `!` без причины:

```csharp
// CORRECT
public User? FindUser(Guid id) => _users.TryGetValue(id, out var u) ? u : null;

// Use ! only when invariant is obvious from context
var card = player.Hand.First(c => c.Id == id)!;
```

## Быстрый чеклист

- [ ] Member order: Constructor → Fields → Public → Private → Locals
- [ ] Поля: `_camelCase`, описательные, readonly сверху
- [ ] Коллекции: inline init
- [ ] Dictionary: `TryGetValue`
- [ ] Методы: Fast-path → Create → Setup → Side-effects → Return
- [ ] Исключения на границе: log + graceful; внутри домена — пробрасывай
- [ ] Braces: same line
- [ ] Task/ValueTask (не UniTask)
- [ ] Nullable: не глуши `!` без причины

## Связано

- [CLAUDE.md](../CLAUDE.md) §Critical Rules
- [COMMON_ORLEANS.md](COMMON_ORLEANS.md) — grain-специфика
- [BLAZOR.md](BLAZOR.md) — стиль в .razor + @code
