# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /workflow, workflow, воркфлоу, начни задачу, start workflow
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Workflow Skill

When the user runs `/workflow [description]`, create a structured task workspace with three files and begin implementation on the post-radio backend.

Enhanced version of `/task` that maintains a living workspace throughout the task lifecycle.

---

## Workspace Structure

Active tasks live under `docs/tasks/current/`, completed tasks move to `docs/tasks/complete/`.

```
docs/tasks/
  current/<task_name>/
    <task_name>_info.md         — initial brief (what to do)
    <task_name>_progress.md     — notes during implementation
    <task_name>_result.md       — final result summary
  complete/<task_name>/
    <task_name>.md              — condensed summary (created by /workflow-complete)
```

`<task_name>` — lowercase, underscores (e.g. `deploy_coordinator_pipe`).

---

## Phase 1 — Create Brief (`<task_name>_info.md`)

Same research process as `/task`:

### Step 1 — Extract ALL requirements

Parse the user's message. Preserve every point — list all requirements, constraints, context.

### Step 2 — Search the codebase

`Grep`/`Glob` under `backend/` for mentioned classes/systems. Read key files briefly. Repo is SDK-style csproj (glob auto-include) — no manual `<Compile Include>` needed for new files.

### Step 3 — Map task to documentation

| Task touches... | Include |
|-----------------|---------|
| Grain, State\<T\>, [Transaction], IOrleans, StateCollection, IMessaging, jsonb | `docs/COMMON_ORLEANS.md` |
| Lifetime, Advise, View, ListenQueue, DeployLifetime | `docs/COMMON_LIFETIMES.md` |
| Blazor, razor, @inject, UiComponent | `docs/BLAZOR.md` |
| Deploy, Coolify, docker-compose, Aspire, PgBouncer, migrator, DeployId, DeployIdentity | `docs/DEPLOY.md` |
| Deploy failure, compose logs, pgbouncer error | `docs/DEPLOY_TROUBLESHOOTING.md` |
| Code style, naming, Task vs UniTask | `docs/CODE_STYLE_FULL.md` |
| Telemetry, metrics, logs | `docs/TELEMETRY.md` |
| Past mistake / anti-pattern | `docs/CLAUDE_MISTAKES.md` |
| Terminology | `docs/VOCABULARY.md` |
| Error lookup | `docs/ERRORS.md` |

### Step 4 — Decompose

Two-level decomposition (by user requirement → by file). Name real files; mark new ones `[новый файл]`.

### Step 5 — Verify completeness (CRITICAL)

Re-read user's message. Every requirement must be covered. Add missing ones.

### Step 6 — Write `<task_name>_info.md`

Save to `docs/tasks/current/<task_name>/<task_name>_info.md`:

```markdown
## Задача: [short name in Russian]

### Цель
[Полное описание.]

### Контекст
[Мотивация, ограничения, связь с другими задачами.]

### Шаги реализации

**1. [Требование N1]**
  1.1. [Action] — `path/to/File.cs`

**2. [Требование N2]**
  2.1. [Create X] — `path/to/New.cs` [новый файл]

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `path/to/File.cs` | [what and why] |

### Документация к прочтению
- `docs/COMMON_ORLEANS.md` — [конкретная причина]

### Риски
[Specific gotchas. Omit if none.]
```

### Step 7 — Initialize `<task_name>_progress.md`

```markdown
## [Task name] — Рабочие заметки

### Статус: В работе

### Заметки
<!-- Находки, решения и полезная информация по ходу реализации -->
```

### Step 8 — Initialize `<task_name>_result.md`

```markdown
## [Task name] — Результат

### Статус: Не завершено
```

### Step 9 — Confirm

Output brief to user and ask: **"Начинаем реализацию?"**.

---

## Phase 2 — During Implementation

Actively maintain `<task_name>_progress.md`:

### What to write

- Важные находки при исследовании кода.
- Принятые решения и почему.
- Обнаруженные проблемы и как решены.
- Изменения плана относительно `_info.md`.
- Список реально изменённых/созданных файлов.

### When to update

- Перед началом каждого крупного шага.
- После обнаружения чего-то неочевидного.
- После решения проблемы.
- При изменении плана.

### Format

```markdown
### [HH:MM] Название шага или находки
Содержание заметки.
```

---

## Phase 3 — Completion (`<task_name>_result.md`)

```markdown
## [Task name] — Результат

### Статус: Завершено

### Что сделано
[3-7 пунктов: что реализовано.]

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `path/to/File.cs` | [краткое описание] |

### Отличия от плана
[Omit if plan followed exactly.]

### Нерешенные вопросы
[Omit if everything done.]
```

---

## Rules

- Code identifiers and paths — English.
- Prose — Russian.
- Steps must name real files found via search, never guessed paths.
- Do not lose user requirements.
- `<task_name>_progress.md` обновляется по ходу работы, не только в конце.
- `<task_name>_result.md` заполняется когда задача завершена.
- При повторном `/workflow` на ту же задачу — продолжить в существующей папке.
- Все рабочие файлы — в `docs/tasks/current/<task_name>/`.
- Перемещение в `docs/tasks/complete/` — делает `/workflow-complete`.
- Репо — backend-only. Не упоминай Unity / VContainer / UniTask.
