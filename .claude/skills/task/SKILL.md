# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /task, task, задача, создай задачу, new task
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Task Skill

When the user runs `/task [description]`, transform the raw task description into a structured implementation brief for the post-radio backend.

**Do NOT start implementing.** Output the brief and ask for confirmation.

---

## Execution Steps

### Step 1 — Extract ALL requirements

Parse the user's message after `/task`. **Preserve everything — nothing is noise.**

- Составь полный список ВСЕХ целей, подзадач, ограничений и контекста из сообщения пользователя.
- Если пользователь упомянул 7 пунктов — в списке должно быть 7 пунктов. Ничего не отбрасывай.
- Extract all mentioned class names, system names, file names, features.
- Fix typos, normalize informal names to proper C# identifiers.
- Сохрани пользовательские описания поведения рядом с техническим маппингом.

### Step 2 — Search the codebase

For every class/system/feature mentioned:
- `Grep` for the class name under `backend/` to find its file.
- `Glob` by pattern if a group of files is involved.
- Read key files briefly — достаточно, чтобы понять текущую реализацию и интерфейсы.
- Repo uses SDK-style csproj (glob auto-include) — new `.cs` files do NOT need manual `<Compile Include>` entries.

### Step 3 — Map task to documentation

| Task touches... | Include |
|-----------------|---------|
| Grain, State\<T\>, `[Transaction]`, IOrleans, IMessaging, StateCollection, jsonb | `docs/COMMON_ORLEANS.md` |
| `Advise`, `View`, `Lifetime`, `ListenQueue`, `Updated`, DeployLifetime | `docs/COMMON_LIFETIMES.md` |
| Blazor, razor, `@inject`, `[Inject]`, UiComponent, ToastService | `docs/BLAZOR.md` |
| DeployId, DeployLifetime, DeployIdPipe, Coolify, docker-compose, PgBouncer, Aspire AppHost | `docs/DEPLOY.md` |
| deploy failure, prod issue, compose logs | `docs/DEPLOY_TROUBLESHOOTING.md` |
| member order, naming, braces, nullable, Task vs UniTask | `docs/CODE_STYLE_FULL.md` |
| telemetry, metrics, FileLoggerProvider, .telemetry | `docs/TELEMETRY.md` |
| past mistake, known anti-pattern | `docs/CLAUDE_MISTAKES.md` |
| terminology, primary term | `docs/VOCABULARY.md` |
| error lookup, fix recipe | `docs/ERRORS.md` |

Если сомневаешься — включи. Лишняя ссылка дешёвая, пропущенная — причина ошибок.

### Step 4 — Decompose into steps

Two-level decomposition:

1. **First level — by user requirements.** Each requirement from Step 1 becomes a group.
2. **Second level — by files.** Within each group, concrete file changes.

Rules:
- Name the target file explicitly (real path found in Step 2).
- New file? Отметь: `[новый файл]` (без упоминания csproj — auto-include).
- Sequence steps so dependencies come first.

### Step 5 — Verify completeness (CRITICAL)

Re-read the user's original message. For every requirement / constraint / piece of context:
- Verify it is covered by a concrete step.
- If something is NOT covered — add it now.
- User constraint without matching step → put it in "Контекст" section.

Information loss during transformation is the #1 failure mode. Do not skip.

### Step 6 — Output the brief

Use the format below.

### Step 7 — Save the brief

- Convert the task short name to filename: lowercase, spaces → underscores, remove special characters.
- **Save path: `docs/tasks/<task_name>.md`** (relative to repo root).
- Confirm to the user: `Задача сохранена: docs/tasks/<task_name>.md`.
- Then ask **"Начинаем реализацию?"**.

---

## Output Format

```
## Задача: [short name in Russian]

### Цель
[Полное описание цели. Каждый пункт пользователя должен быть представлен.]

### Контекст
[Мотивация, бизнес-ограничения, предпочтения по реализации, связь с другими задачами.
Опустить секцию только если пользователь не дал контекста.]

### Шаги реализации

**1. [Требование пользователя N1]**
  1.1. [Concrete action] — `path/to/File.cs`
  1.2. [Concrete action] — `path/to/Other.cs`

**2. [Требование пользователя N2]**
  2.1. [Создать X] — `path/to/New.cs` [новый файл]
  2.2. [Concrete action] — `path/to/Existing.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `path/to/File.cs` | [what and why] |

### Документация к прочтению
- `docs/COMMON_ORLEANS.md` — [конкретная причина: добавляем новый grain]
- `docs/DEPLOY.md` — [конкретная причина: меняем compose]

### Риски
[Specific gotchas for this task. Omit if no risks.]
```

После вывода: сохранить (Step 7), затем **"Начинаем реализацию?"**.

---

## Rules

- Code identifiers and file paths — English.
- Prose labels — Russian.
- Steps must name real files found via search, never guessed paths.
- Do not lose user requirements during transformation.
- Переформулируй цели в технических терминах, но сохрани все детали оригинала.
- Репо — backend-only (.NET 10 / Orleans / Aspire / Blazor / Coolify). Не упоминай Unity / VContainer / UniTask.
