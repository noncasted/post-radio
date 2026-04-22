# Workflow Split Skill

When the user runs `/workflow-split [task_name]`, save all current session context into the task's `_info.md` and `_progress.md` files so that the next Claude Code session can seamlessly continue.

Use this when context is running low mid-workflow and the work needs to carry over to a fresh session.

If `task_name` is omitted, find the most recently modified task folder in `docs/tasks/current/`.

---

## Phase 1 — Identify the Task

1. Find the task folder: `docs/tasks/current/<task_name>/`
2. Read all existing files: `<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`
3. If no task folder is found — tell the user and stop

---

## Phase 2 — Collect Current State

### Step 1 — Gather what was done

- Run `git diff main --name-only` to get all changed files
- Run `git diff --name-only` (unstaged) and `git diff --cached --name-only` (staged) for uncommitted work
- Run `git log main..HEAD --oneline` to see commits on the branch (if any)
- Review conversation context: what steps were completed, what was being worked on

### Step 2 — Determine what remains

- Compare completed work against the plan in `<task_name>_info.md`
- For each step: mark as done, in-progress, or not started
- Identify the exact point where work stopped — what was being done right now

### Step 3 — Collect non-obvious findings

From the current session, gather:
- Discovered edge cases or complications
- Decisions made and why
- Workarounds applied
- Files that turned out to be important but weren't in the original plan
- Any failed approaches and why they failed

---

## Phase 3 — Update `<task_name>_info.md`

If the original plan needs corrections based on what was learned during implementation:
- Update file paths that turned out to be different
- Add steps that were discovered to be necessary
- Remove steps that turned out to be unnecessary
- Add new key files discovered during work
- **Do NOT rewrite the whole file** — only update what changed

If the plan is still accurate — do not touch `_info.md`.

---

## Phase 4 — Update `<task_name>_progress.md`

This is the CRITICAL file. It must contain everything the next session needs to continue.

Update the file with the following structure:

```markdown
## [Task name] — Рабочие заметки

### Статус: В работе (разделение сессии)

### Выполнено
- [x] Шаг 1.1 — краткое описание что сделано
- [x] Шаг 1.2 — краткое описание что сделано
- [ ] Шаг 2.1 — не начато
- [ ] Шаг 2.2 — не начато

### Текущий момент остановки
[Точное описание: над чем работали прямо сейчас, какой файл редактировали,
что именно делали. Достаточно деталей чтобы новая сессия могла продолжить
без потери контекста.]

### Важные находки
[Все неочевидное что было обнаружено в этой сессии и что нужно знать
для продолжения работы. Конкретные файлы, паттерны, edge cases.]

### Измененные файлы (на момент разделения)

| Файл | Статус | Что изменено |
|------|--------|-------------|
| `path/to/File.cs` | committed/uncommitted | [описание] |

### Заметки из сессии
[Предыдущие заметки из _progress.md сохраняются здесь]
```

Rules for `_progress.md`:
- **Preserve all existing notes** — append, don't replace
- The "Текущий момент остановки" section must be detailed enough for a cold start
- Include file paths, line numbers, and specific context where relevant
- If there are uncommitted changes — note exactly what they are

---

## Phase 5 — Output Continuation Prompt

After updating the files, output ONLY the following text that the user should paste into the next session:

```
В рамках /workflow продолжаем работу над docs/tasks/current/<task_name>/. Прочитай <task_name>_info.md и <task_name>_progress.md и продолжай с момента где остановились.
```

Nothing else. No summaries, no explanations. Just the prompt string.

---

## Rules

- Code identifiers and file paths — English
- Prose — Russian
- NEVER guess file paths — verify with Glob/Grep
- Git diff is the source of truth for changed files
- Do NOT create commits — only update documentation files
- Do NOT modify any source code files
- Preserve all existing content in `_progress.md` — only append and restructure
- The goal is zero information loss between sessions
- Keep "Текущий момент остановки" as specific as possible — vague descriptions defeat the purpose
- If the task has no meaningful progress yet — say so and skip the update
