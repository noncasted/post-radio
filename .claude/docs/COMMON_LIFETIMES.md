# Lifetimes: Resource Management

**TL;DR:** Lifetime — область видимости подписки. Когда `Terminate()` — все дети и подписки чистятся автоматически. **Правило: каждый `Advise()` / `View()` / `ListenQueue()` обязан получить Lifetime, иначе утечка.**

Реализация: `backend/Common/Reactive/Lifetimes/` (Lifetime.cs, LifetimeExtensions.cs, TerminatedLifetime.cs).
Используется в backend повсеместно: IMessaging, StateCollection.Updated, LiveState, DeployLifetime, Blazor `UiComponent`.

## Когда использовать

- Любая подписка на EventSource / ViewableProperty / ViewableList / ViewableDictionary / LifetimedValue.
- Подписки на очереди: `IMessaging.ListenQueue(lifetime, ...)`.
- Подписки на deploy-epoch: `IDeployAware.OnDeployChanged(id, lifetime)` — lifetime живёт до смены epoch.
- Orleans-grain: если grain регистрирует внешние подписки, они должны привязываться к времени жизни grain (через отдельный Lifetime, терминируемый в `OnDeactivateAsync`).
- Blazor: в `UiComponent.OnSetup(IReadOnlyLifetime lifetime)` — подписка живёт пока страница смонтирована.

## Создание

```csharp
// Standalone — ручная очистка
var lifetime = new Lifetime();
// ... работа ...
lifetime.Terminate();

// Child — автоматическая очистка вместе с родителем
var child = parent.Child();

// Из CancellationToken
var lifetime = token.ToLifetime();

// Пересечение — живёт пока живы оба
var intersect = a.Intersect(b);

// Пре-терминированный синглтон (no-op подписки)
TerminatedLifetime.Instance
```

## Ключевые правила

| Правило | Последствия |
|---------|-------------|
| Все подписки требуют Lifetime | Без него — утечка памяти |
| Child авто-терминируется с родителем | Каскадная очистка |
| `Terminate()` идемпотентен | Безопасно звать дважды |
| Подписки, добавленные на терминированный lifetime, срабатывают немедленно | `lifetime.Listen` на мёртвом lifetime = synchronous call |
| Слушатели удаляются во время `Terminate()` | После смерти callback уже не стреляет |

## Паттерн в тестах

**Никогда не создавай `new Lifetime()` в тесте** — используй `handle.Lifetime` от `ClusterTestRoot`:

```csharp
// WRONG — orphan lifetime, если тест упадёт до Terminate() — подписки живут вечно
var lifetime = new Lifetime();
service.Run(lifetime);
// ...
lifetime.Terminate();

// CORRECT — handle.Lifetime автоматически терминируется фреймворком
service.Run(handle.Lifetime);
// Нужен дочерний? → handle.Lifetime.Child()
```

## Паттерн: DeployLifetime

В `IDeployContext` хранится дочерний lifetime, который терминируется при смене DeployId. Все подписки, которые должны жить ровно в пределах текущей эпохи кластера, надо привязывать к нему:

```csharp
public async Task OnDeployChanged(Guid deployId, IReadOnlyLifetime deployLifetime) {
    // Эта подписка умрёт автоматически при следующем рестарте кластера
    await _messaging.ListenQueue<MyUpdate>(deployLifetime, queueId, OnUpdate);
}
```

## Паттерн: StateCollection.Updated

```csharp
public class MyService(IMyCollection collection) {
    public async Task OnLocalSetupCompleted(IReadOnlyLifetime lifetime) {
        collection.Updated.Advise(lifetime, diff => {
            // реагируем на добавления / удаления / изменения
        });
    }
}
```

## Распространённые ошибки

- `source.Advise(null, handler)` — `ArgumentNullException`, никогда не допускается.
- Создание нового `Lifetime` на каждой итерации цикла — вместо этого переиспользуй родительский.
- Подписка на элемент коллекции с родительским lifetime: когда элемент удалится, подписка останется висеть. Используй `item.Lifetime`, если у элемента он есть, иначе — дочерний lifetime элемента.
- Циркулярный ref: callback захватывает `this`, а `this` держит lifetime — GC не соберёт. Если реально нужно слабое сцепление — `WeakReference` или перенос подписки выше по стеку.

## Быстрый справочник

```
new Lifetime()              // создать корневой
parentLifetime.Child()      // дочерний
token.ToLifetime()          // из CancellationToken
TerminatedLifetime.Instance // пре-терминированный

lifetime.IsTerminated       // проверка состояния
lifetime.Terminate()        // завершить (идемпотентно)
lifetime.Listen(() => {})   // hook на терминацию

eventSource.Advise(lifetime, handler)
property.View(lifetime, handler)
messaging.ListenQueue(lifetime, queueId, handler)
```
