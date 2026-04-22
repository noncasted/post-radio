# Check Skill

When the user runs `/check`, launch validation agents in parallel to audit post-radio backend code for correctness.

---

## Arguments

- `/check` — check all git-modified files (staged + unstaged + untracked `.cs`/`.razor`)
- `/check path/to/dir` — check all `.cs`/`.razor` files in the specified directory (recursively)
- `/check path/to/File.cs` — check a single file

---

## Execution Steps

### Step 1 — Determine target files

**No argument:** get git-modified files:
```bash
git ls-files --others --exclude-standard
git diff --name-only HEAD
git diff --name-only --cached
```
Merge, deduplicate. Keep only `.cs` and `.razor`. Classify each as **new** (from `ls-files --others`) or **modified** (from `diff`).

**Directory argument:** use Glob to find all `.cs` and `.razor` files recursively. Classify all as modified.

**File argument:** use the single file. Classify via `git ls-files --error-unmatch <file>`.

If no matching files — report "Нет файлов для проверки" and stop.

### Step 2 — Categorize files

Split files into domains:

| Domain | Path pattern | Typical contents |
|--------|-------------|------------------|
| `backend` | `backend/**/*.cs` (excluding razor) | Orleans grains, services, infrastructure |
| `blazor` | `backend/**/*.razor` and `backend/Console/**`, `backend/Frontend/**` | Blazor pages, components |
| `tools` | `backend/Tools/**/*.cs` | Deploy setup, generators, test/benchmark projects if present |

Test files — `*Tests.cs` / `*Test.cs` — special handling (see below).

### Step 3 — Select agents per domain

Launch ONLY relevant agents.

**Always launch** (if any `.cs` files present):
- `code-style-checker` — member order, naming, braces, nullable, SDK-style csproj, `UniTask` leak check.
- `public-interface-prettifier` — return types, naming, vocabulary, `Task` vs `UniTask`.
- `logging-inspector` — ILogger injection, silent failures, structured templates.
- `error-handling-checker` — try/catch coverage, rethrow rules, boundary vs propagate.
- `lifetimes-inspector` — every `Advise`/`View`/`ListenQueue` has a Lifetime; `item.Lifetime` inside collection views; `DeployLifetime` usage.

**If any `backend` files present:**
- `state-checker` — `[GenerateSerializer]`, `[Id(N)]`, StatesLookup, AddStates, StateCollection.
- `transaction-checker` — `[Transaction]` attribute, `InTransaction` usage, `OnUpdated` variants.
- `race-condition-checker` — `[Reentrant]` check, interleaving after await, timer reentrancy.

**If `blazor` files present:**
- `blazor-inspector` — `@inject` rules, early returns, `UiComponent` inheritance, `@code` member order, two-way binding.

**If any of these are true:**
- New grain interface / state class.
- New deploy-epoch type (DeployLifetime, LiveState, IDeployAware, etc.).
- New file in `docs/`.
- New Blazor component in `backend/Frontend/Shared/`.

Then additionally launch:
- `docs-checker` — documentation gaps.

### Test file handling

Files matching `*Test.cs` / `*Tests.cs`:
- **DO include** in: code-style-checker, state-checker (test state classes), lifetimes-inspector.
- **DO NOT include** in: error-handling-checker (tests can throw on failure), public-interface-prettifier (test method naming differs).
- Annotate test files: `[TEST] path/to/MyTest.cs` so agents can adjust severity.

### Step 4 — Launch agents in PARALLEL

All selected agents in a single message, multiple Agent tool calls. Each agent receives:

```
Validate the following files for <agent-specific scope>:

New files (just created):
<list or "none">

Modified files (already existed):
<list or "none">

Read each file and apply your validation rules. Report findings in your standard format.
```

### Step 5 — Collect and summarize

If an agent fails, times out, or returns empty — include `[AGENT-ERROR] agent-name — <description>` in the report and continue.

---

## Output Format

```
## /check Report

### Запущенные агенты
- code-style-checker — <VERDICT>
- lifetimes-inspector — <VERDICT>
- state-checker — <VERDICT>
- transaction-checker — <VERDICT>
- race-condition-checker — <VERDICT>
- ...

---

### Критические проблемы (CRITICAL)

> Из: lifetimes-inspector
`backend/Meta/Users/UserGrain.cs:42` — `Advise(null, ...)` memory leak
Fix: передать lifetime из OnActivateAsync или аналога

> Из: state-checker
`backend/Meta/Users/UserState.cs` — не зарегистрирован в AddStates()
Fix: добавить `Add<UserState>(StatesLookup.User)` в ProjectsSetupExtensions

---

### Ошибки (ERROR)

> Из: transaction-checker
`backend/Meta/Wallet/WalletService.cs:87` — multi-grain call без InTransaction
Fix: обернуть в `_orleans.InTransaction(...)`

---

### Предупреждения (WARNING)

> Из: code-style-checker
`backend/Common/Utils.cs:15` — поле `_o` → `_orleans`

---

### Документация

> Из: docs-checker
`docs/COMMON_ORLEANS.md` — Key Files: нет ссылки на UserGrain
Action: запустить docs-writer

---

## Итог
Файлов проверено: N
Агентов запущено: M
CRITICAL: X | ERROR: Y | WARNING: Z

VERDICT: PASS | FAIL
```

---

## Rules

1. **Parallel execution mandatory** — все агенты в одном сообщении.
2. **Only relevant agents** — не запускай state-checker на Blazor-файлах.
3. **Deduplicate** — если два агента репортят одно и то же, показать один раз с обоими источниками.
4. **Order by severity** — CRITICAL → ERROR → WARNING → docs.
5. **Include file:line** где агенты его дают.
6. **VERDICT** — FAIL если есть CRITICAL или ERROR; PASS при только WARNING или чисто.
7. **Russian prose** в репорте, English для идентификаторов.
8. **Agent failure handling** — зафиксируй `[AGENT-ERROR]` и продолжи.
9. **Docs follow-up** — если docs-checker = NEEDS-UPDATE, добавь: "Рекомендация: запустить docs-writer для обновления документации".
