# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /validate, validate, валидируй, проверь правила, validate rules
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Validate Skill

When the user runs `/validate`, check all modified `.cs` files against post-radio backend rules and report every violation. In-place checker (без делегации агентам — для коротких быстрых проверок).

---

## Execution Steps

### Step 1 — Get changed files

```
git diff --name-only HEAD
git diff --name-only --cached
git ls-files --others --exclude-standard
```
Merge, deduplicate. Keep only `.cs`. Skip `.csproj`, `.DotSettings`, `bin/`, `obj/`.

If no `.cs` files changed — report "Нет изменённых .cs файлов" and stop.

### Step 2 — Load rules

Always read:
- `.claude/docs/CODE_STYLE_FULL.md`
- `.claude/docs/CLAUDE_MISTAKES.md`

Read conditionally:
- `.claude/docs/COMMON_LIFETIMES.md` — if any file uses `Advise`, `View`, `ListenQueue`, `Lifetime`, `Updated.Advise`.
- `.claude/docs/COMMON_ORLEANS.md` — if any file contains `Grain`, `State<`, `[State]`, `[Transaction]`, `StateCollection`, `IOrleans`.
- `.claude/docs/BLAZOR.md` — if changeset contains `.razor`.

### Step 3 — Validate each file

Read each `.cs` in full. Apply every applicable rule:

**Orleans grains** (file contains `: Grain,` or `: Grain ` or inherits grain base):
- [ ] State parameters annotated with `[State]`.
- [ ] State wrapper is `State<T>` (not raw state class).
- [ ] Constructor-only injection — no `[Inject]` fields.
- [ ] Observer-forwarding methods use `[Reentrant]` + `WaitAsync(timeout)` + observer discard on failure (CLAUDE_MISTAKES lesson 2).
- [ ] No polling of own grain after local `await grain.MarkXxx()` (CLAUDE_MISTAKES lesson 3).

**State classes** (implements `IStateValue`):
- [ ] `[GenerateSerializer]` present.
- [ ] Every serialized property has `[Id(N)]`, sequential, no gaps.
- [ ] `int Version => 0;` present.
- [ ] `Id` type matches grain key.
- [ ] Reference properties have default values.

**Lifetime / Reactive**:
- [ ] Every `Advise(`, `View(`, `ListenQueue(`, `Listen(` has non-null Lifetime.
- [ ] UI bindings in Blazor `UiComponent.OnSetup` use `View` (when current state needed).
- [ ] Item subscriptions inside collection `View` use `item.Lifetime` (not outer).
- [ ] `new Lifetime()` has a matching `Terminate()`.
- [ ] In tests (`*Test.cs`): `handle.Lifetime`, никогда `new Lifetime()`.

**Transactions**:
- [ ] Methods called inside `InTransaction` have `[Transaction]` on their interface.
- [ ] Multi-grain mutation → wrapped in `InTransaction`.
- [ ] Inside `InTransaction` → `OnUpdatedTransactional`; outside → `OnUpdated`.

**Code Style**:
- [ ] Member order: Constructor → readonly fields → mutable fields → public → private → locals.
- [ ] Field names: `_camelCase`, no abbreviations.
- [ ] Collections initialized inline.
- [ ] Dictionary: `TryGetValue`, not `ContainsKey` + indexer.
- [ ] Braces same line.
- [ ] `UniTask` anywhere в backend — CRITICAL (Unity-only).

**Blazor** (file is `.razor`):
- [ ] No `@inject` in markup — only `[Inject]` in `@code`.
- [ ] Pages subscribing to reactive state inherit `UiComponent`.
- [ ] Early return pattern for loading/null.
- [ ] Reactive callbacks call `InvokeAsync(StateHasChanged)`.

### Step 4 — Output

---

## Output Format

```
## Validation Report

### `backend/Meta/Users/UserGrain.cs`
[FAIL] Lifetime leak — `_events.Advise(null, ...)` (строка ~42): передать Lifetime
[FAIL] Missing [Reentrant] — observer-forwarding grain, см. CLAUDE_MISTAKES lesson 2
[WARN] Naming — поле `_o` → `_orleans`
[PASS] Member order — OK
[PASS] State registration — OK

---

### `backend/Infrastructure/Foo.cs`
[PASS] Все проверки пройдены

---

## Итог
Файлов проверено: N
[FAIL]: X
[WARN]: Y
```

---

## Severity

- `[FAIL]` — rule violation, must be fixed (memory leak, missing registration, wrong transaction scope).
- `[WARN]` — style issue, should be fixed (naming, ordering, minor style).
- `[PASS]` — check satisfied.

Include approximate line numbers where possible. For each `[FAIL]`, write a one-line fix hint.
