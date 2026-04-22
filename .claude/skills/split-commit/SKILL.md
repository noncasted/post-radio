# Split Commit Skill

When the user runs `/split-commit`, analyze all current changes and split them into logical groups, committing each group separately. Goal: clean readable git history instead of one massive commit with unrelated changes mixed together.

Use whenever the user has a large set of uncommitted changes and wants to organize them into multiple focused commits. Also trigger on: "commit everything in parts", "split this into commits", "too many changes for one commit", "organize my changes".

## Commit Message Rules

Follow the EXACT same format as `/commit`:

### Title Format

- Extract ticket ID from current branch name (e.g., `POST-123`).
- If ticket ID exists: `[POST-123] Brief description`.
- If no ticket ID: `[Scope] Brief description` or `[Scope1] [Scope2] Brief description`.
- Multiple tags when a commit spans several areas (e.g. `[Cluster] [Infra]`, `[Deploy] [Tools]`).
- Post-radio scopes: `[Cluster]`, `[Meta]`, `[Infra]`, `[Orchestration]`, `[Frontend]`, `[Console]`, `[Common]`, `[Tools]`, `[Deploy]`, `[Docs]`, `[Claude]`, `[Tests]`, `[Bench]`.
- Keep title SHORT and descriptive.

### Description Format

- Bullet points starting with `-`.
- Each bullet starts with one of:
  - `add:` — new features or additions
  - `fix:` — bug fixes
  - `refactor:` — code refactoring
  - `remove:` — deletions

NEVER NEVER NEVER ADD CLAUDE TO CO-AUTHORS
NEVER NEVER NEVER ADD CLAUDE TO CO-AUTHORS

### Language

- All text in ENGLISH (titles, scopes, descriptions, tags).

## Execution Steps

### Phase 1: Gather Context

1. `git status` — all changed/untracked files.
2. `git diff` and `git diff --cached` — what changed.
3. `git log -40 --oneline` — recent commits for naming style.
4. `git rev-parse --abbrev-ref HEAD` — current branch (for ticket ID).

### Phase 2: Plan the Split

Group changes by logical purpose. Each commit should represent ONE coherent idea that makes sense on its own.

Good grouping:
- **By feature/scope**: all files related to one feature (e.g. "add UserRating grain" = grain interface + implementation + state class + StatesLookup + AddStates + console editor).
- **By layer when independent**: unrelated layer changes go separately (e.g. deploy refactor separate from grain fix).
- **Tests with their code**: test files in the same commit as the code they test.
- **Config/docs separate**: pure config or docs changes their own commit.
- **Renames/moves separate**: bulk renames cleaner as their own commit.

Bad grouping (avoid):
- One commit per file (too granular).
- Grouping by file type regardless of purpose (all `.cs` together, all `.razor` together).
- Mixing unrelated features just because they touch the same directory.

Present the plan to the user as numbered list:
```
Proposed split:
1. [Meta] Add UserRating grain (5 files)
   - backend/Meta/Rating/UserRatingState.cs
   - backend/Meta/Rating/UserRatingGrain.cs
   - ...
2. [Deploy] Add migrator init container (4 files)
   - ...
3. [Tools] Add rating benchmark (3 files)
   - ...
```

Ask user to confirm or adjust.

### Phase 3: Execute Commits

For each group, in order:

1. Stage ONLY the files in that group: `git add <file1> <file2> ...`.
   - NEVER use `git add .` or `git add -A`.
   - For renamed/moved files, stage both old and new paths.
2. Verify staging: `git diff --cached --stat`.
3. Commit with formatted message.
4. Report: commit hash + summary.

After all commits, final summary:
```
Done! Created N commits:
  abc1234 [Meta] Add UserRating grain
  def5678 [Deploy] Add migrator init container
  ghi9012 [Tools] Add rating benchmark
```

## Edge Cases

- **Partial file changes**: one file contains changes for two different groups → mention to user, suggest group (don't `git add -p` — keep simple).
- **Dependencies between groups**: commit dependency first (e.g. shared types before code that uses them).
- **Very few changes**: if natural = 1 group → suggest `/commit` instead.
- **Binary files** (images, assets): group with feature they belong to.

## Important Notes

- ALWAYS ask user to confirm split plan before committing.
- `git add` with explicit paths, never `-A` or `.`.
- Commits created locally; push to remote handled separately.
- If user disagrees — adjust and re-present.
