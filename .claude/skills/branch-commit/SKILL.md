# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /branch-commit, branch-commit, создай ветку и коммит, в новую ветку, move changes to branch
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Branch Commit Skill

Create a new branch from the current branch, move uncommitted changes there, and commit them — all in one step.

This is useful when you've been working on `main` (or any branch) and realize the changes should live on their own branch before committing. The skill handles branch creation, change transfer, and commit message generation.

## Usage

```
/branch-commit                     — auto-generate branch name from changes
/branch-commit my-feature-name     — use the provided branch name
```

## Execution Steps

### 1. Gather context

Run these in parallel:
- `git status` — see what's changed
- `git branch --show-current` — know the current branch
- `git diff` and `git diff --cached` — understand staged and unstaged changes
- `git log --oneline -5` — recent commits for style reference

If there are no changes (no staged, unstaged, or untracked files), stop and tell the user there's nothing to commit.

### 2. Determine branch name

**If the user provided a name as an argument**, use it as-is.

**If not**, generate one from the changes:
- Look at the diff to understand what was changed
- Format: `<scope>/<short-description>`
  - Scope prefix: `fix/`, `feat/`, `refactor/`, `update/`
- The description part: 2-4 words, lowercase, separated by hyphens
- Examples: `feat/bot-card-strategies`, `fix/null-ref-in-board`, `refactor/extract-player-service`, `update/matchmaking-timeout`

Present the chosen branch name to the user and proceed (don't ask for confirmation unless something is ambiguous — speed matters here).

### 3. Create the branch and commit

```bash
# Create and switch to new branch (carries uncommitted changes automatically)
git checkout -b <branch-name>
```

`git checkout -b` preserves all working tree and index state, so no stashing is needed.

### 4. Stage and commit

**Commit title:**
- `[Scope] Brief description` or `[Scope1] [Scope2] Brief description`
  - Use multiple tags when a commit spans several areas (e.g., `[Backend] [Client]`, `[Shared] [Console]`)
  - Scope: [Client], [Backend], [Shared], [Console], [Infra], [Tests], [Cards], [Claude], [Docs], [Misc]

**Commit description** — bullet points, each starting with a tag:
- `add:` — new features, grains, commands
- `fix:` — bug fixes
- `refactor:` — code restructuring
- `update:` — enhancements to existing functionality
- `remove:` — deletions

Stage files selectively (avoid secrets like `.env`), then commit:

```bash
git add <specific-files>
git commit -m "$(cat <<'EOF'
[Scope] Brief description

- add: new feature X
- fix: issue Y

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

### 5. Report result

Tell the user:
- The branch name that was created
- A summary of what was committed
- Remind that the branch is local only (not pushed)

## Important

- **Language:** All commit text in ENGLISH
- **Don't push** — the branch stays local, user decides when to push
- **Don't amend** — always create a new commit
- **Sensitive files** — never stage `.env`, credentials, or secrets
- **No --no-verify** — respect git hooks
