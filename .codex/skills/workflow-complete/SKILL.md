---
name: workflow-complete
description: Finalize a post-radio workflow task by updating result notes, creating a condensed completed-task summary, and cleaning up docs/tasks/current. Use when the user invokes /workflow-complete.
---

# Workflow Complete Skill

When the user runs `/workflow-complete [task_name]`, finalize the task, create a condensed summary in `docs/tasks/complete/`, and clean up the working folder from `docs/tasks/current/`.

If `task_name` is omitted, look for the most recently modified task folder in `docs/tasks/current/`.

---

## Phase 1 — Identify the Task

1. Find folder: `docs/tasks/current/<task_name>/`.
2. Read all three files: `_info.md`, `_progress.md`, `_result.md`.
3. If `_result.md` has `Статус: Не завершено` — ask if task is actually done before proceeding.

---

## Phase 2 — Analyze What Was Actually Done

### Step 1 — Collect real changes

Run `git diff main --name-only` (or `git diff main...HEAD --name-only` on a feature branch). Cross-reference with `_info.md` plan and `_progress.md` notes.

### Step 2 — Compare plan vs reality

For each step in `_info.md`:
- Was it done as planned?
- Done differently? How?
- Skipped? Why?

Identify work done but not in plan (emergent changes, bug fixes).

### Step 3 — Identify problems encountered

Scan `_progress.md` and commits for: bugs found + fixed, unexpected complications, workarounds, patterns that didn't work, compilation errors that required changes.

---

## Phase 3 — Update `_result.md` in current/

Fill in or update `docs/tasks/current/<task_name>/<task_name>_result.md`:

```markdown
## [Task name] — Результат

### Статус: Завершено

### Что сделано
[3-7 bullet points]

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `path/to/File.cs` | [description] |

### Отличия от плана
[Omit if plan followed exactly.]

### Проблемы и решения
[Omit if none.]

### Нерешенные вопросы
[Omit if everything done.]
```

File list MUST match actual git diff, not just the plan.

---

## Phase 4 — Create Condensed Summary in complete/

Create `docs/tasks/complete/<task_name>.md`:

```markdown
## [Task name]

### Что сделано
[3-5 bullets — essence, no filler]

### Ключевые файлы
[3-7 most important files]

### Заметки
[Non-obvious decisions, gotchas. Omit if nothing noteworthy.]
```

Rules:
- Maximum ~40 lines.
- No full change list — only key files.
- No "Отличия от плана" — irrelevant after completion.
- No "Нерешенные вопросы" unless blockers for future work.

---

## Phase 5 — Clean Up current/

Delete working folder `docs/tasks/current/<task_name>/` (все три файла).

Full `_result.md` сохранён в git history при необходимости.

---

## Phase 6 — Update Project Documentation

Check and update relevant docs in `.codex/docs/`.

### 6.1 — Mapping

| What changed in the task | Documentation to check/update |
|--------------------------|-------------------------------|
| New grain / state type / StateCollection | `.codex/docs/COMMON_ORLEANS.md` |
| New Lifetime usage pattern | `.codex/docs/COMMON_LIFETIMES.md` |
| New Blazor component / convention | `.codex/docs/BLAZOR.md` |
| Deploy changes (compose, AppHost, migrator, env vars) | `.codex/docs/DEPLOY.md` + `.codex/docs/DEPLOY_TROUBLESHOOTING.md` |
| New telemetry writer / layout change | `.codex/docs/TELEMETRY.md` |
| New vocabulary/concepts | `.codex/docs/VOCABULARY.md` |
| New error patterns | `.codex/docs/ERRORS.md` |
| AI mistakes made during task | `.codex/docs/CLAUDE_MISTAKES.md` |
| New doc file or major concept | `.codex/AGENTS.md` keyword table + `.codex/docs/TRIGGERS.md` |

### 6.2 — What to update

For each relevant doc:
1. **Read** current file.
2. **Identify** what to add/change.
3. **Update** in existing format (match surrounding style).
4. **Cross-reference** between related docs.

### 6.3 — CLAUDE_MISTAKES.md (CRITICAL)

If ANY mistakes visible in `_progress.md`, git history, or context:
- Add new numbered Lesson entry.
- Format: WRONG, CORRECT, short Rule, link to relevant doc.

### 6.4 — VOCABULARY.md

New terms:
- Add entries in existing table format.
- Include "do not mix" notes if similar terms exist.

---

## Phase 7 — Update Memory

Check if any of the following should go to auto-memory:

1. New critical patterns future conversations need.
2. New key files important for navigation.
3. Architecture changes affecting future tasks.
4. User feedback during the task.

Do NOT save:
- File lists (derivable from git).
- Implementation details (in the code).
- Anything already in `_result.md` or `.codex/` docs.

---

## Phase 8 — Report

```markdown
## Workflow Complete: [task name]

### Задача завершена
- `docs/tasks/complete/<task_name>.md` — сводка создана
- `docs/tasks/current/<task_name>/` — рабочие файлы удалены

### Документация обновлена
- `.codex/docs/COMMON_ORLEANS.md` — [what changed]
- `.codex/docs/VOCABULARY.md` — [what added]
- (or "Обновления не требуются")

### Ошибки зафиксированы
- `.codex/docs/CLAUDE_MISTAKES.md` — Lesson N: [description]
- (or "Новых ошибок не обнаружено")

### Memory обновлена
- [what was saved]
- (or "Обновления не требуются")
```

---

## Rules

- Code identifiers and paths — English.
- Prose — Russian.
- NEVER guess file paths — verify with Glob/Grep.
- NEVER add documentation for things that didn't actually change.
- Match existing format in every doc.
- Do NOT create doc files — only update existing.
- Git diff is source of truth, not the plan.
- Conservative with CLAUDE_MISTAKES.md — only genuinely new lessons.
- Condensed summary short and useful.
- Репо — backend-only (.NET 10 / Orleans / Aspire / Blazor / Coolify).
