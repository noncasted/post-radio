---
name: improve-prompt
description: "Rewrite and improve a Claude Code prompt for maximum effectiveness on the post-radio backend. Use this skill whenever the user asks to improve, rewrite, optimize, or enhance a prompt — or when they paste a draft prompt and say something like 'make this better', 'polish this', 'how should I phrase this', 'fix my prompt'. Also trigger when the user says /improve-prompt or mentions prompt quality."
---
# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /improve-prompt, improve prompt, улучши промпт, fix my prompt
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.


# Improve Prompt

You receive a raw prompt from the user. Your job: return a significantly better version that will produce more accurate, focused results from Claude Code on the post-radio backend repo.

## Output format

Return ONLY the improved prompt inside a fenced code block. No preamble, no explanation, no "here's what I changed". The user will copy it and go.

If the prompt is already strong and you'd only make cosmetic changes, say so in one line instead of rewriting.

## How to improve

Read the original prompt carefully. Apply these techniques in order of impact:

### 1. Add specificity where it's vague

- Bad: "fix the coordinator bug"
- Good: "`backend/Cluster/Coordination/CoordinatorBootstrap.cs` — `WaitCoordinatorReady()` зависает на 55 сек после рестарта координатора. Пропусти `WaitCoordinatorReady()` если `_discovery.Self.Tag == ServiceTag.Coordinator`, потому что координатор только что локально сделал `await grain.MarkCoordinatorReady()`."

If the user references something in the project but doesn't specify where — add likely file paths, class names, or grep hints.

### 2. Scope the task boundaries

Add scope boundaries when missing:
- Which files/classes to touch.
- Which files NOT to touch.
- Whether tests are expected.
- Whether to commit or just edit.
- Which env — dev (Aspire), prod (Coolify), or both.

### 3. Point to existing patterns

Instead of describing desired code structure, point to an existing example.

- Bad: "create a new Orleans grain for wallet"
- Good: "create `IWallet` grain following the same pattern as `backend/Meta/Users/User.cs` — `[State] State<WalletState>`, `[Transaction]` методы, регистрация в `StatesLookup` + `AddStates()`".

### 4. Add verification criteria

- "собери `dotnet build backend/post-radio.slnx`"
- "запусти `aspire run` и проверь что сервис поднимается без ошибок"
- "проверь, что Coolify compose всё ещё валидный: `docker compose -f backend/Tools/deploy/docker-compose.yaml config`"

### 5. Structure with clear sections

Для сложных промптов — разбей на секции. Короткие заголовки.

### 6. State the WHY when it matters

- Bad: "не используй async void"
- Good: "используй `Task` вместо `async void` — exception в async void падает в `AppDomain.UnhandledException` вместо caller'а, тестовый кластер при этом зависнет."

### 7. Prefer positive instructions

- Bad: "не создавай новые файлы"
- Good: "правь только существующие файлы"

### 8. Remove noise

Strip filler words, politeness padding, redundant context Claude already knows from CLAUDE.md.

## Project-specific improvements

You know this codebase. When improving prompts:

- Reference correct CLAUDE.md rules (SDK-style csproj, Lifetime rules, Orleans grain checklist, `[Reentrant]` rule).
- Add right file paths under `backend/Cluster/`, `backend/Meta/`, `backend/Infrastructure/`, `backend/Orchestration/`, `backend/Frontend/`, `backend/Console/`, `backend/Tools/`.
- Mention right base classes and attributes (`Grain`, `State<T>`, `[State]`, `[Transaction]`, `[Reentrant]`, `UiComponent`).
- Add build/test commands specific to the project.

## What NOT to do

- Don't add instructions that duplicate CLAUDE.md — Claude loads those automatically.
- Don't add generic "write clean code" filler.
- Don't make the prompt 10x longer than necessary.
- Don't change the user's intent — improve HOW they ask, not WHAT they ask.
- Don't mention Unity / MonoBehaviour / VContainer / UniTask — this is a backend-only repo.
