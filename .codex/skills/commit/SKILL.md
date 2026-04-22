---
name: commit
description: Create a local git commit with the project commit-message format. Use when the user invokes /commit or asks Codex to commit current changes, generate a commit title/body, or stage and commit work.
---

# Commit Skill

When the user runs `/commit`, follow these rules for generating commit messages:

## Rules

### 1. Title Format
- Extract the ticket ID from the current branch name (e.g., `ATS-123`)
- If ticket ID exists: `[ATS-123] Brief description`
- If no ticket ID: `[Scope] Brief description` or `[Scope1] [Scope2] Brief description`
  - Use multiple tags when a commit spans several areas (e.g., `[Shared] [Console]`, `[Infra] [Tests]`)
  - Scope is determined by the area of work (e.g., [Visual], [Timeline], [Objects], [Editor], [Core], [UI], [Network], [Animation], [Client], [Shared], [Console], [Infra], [Tests], [Cards], [Docs])
- Keep the title SHORT and descriptive

### 2. Description Format
- Use bullet points, each starting with `-`
- Each bullet must start with one of these tags:
  - `add:` - for new features or additions
  - `fix:` - for bug fixes
  - `refactor:` - for code refactoring
  - `remove:` - for deletions
- Example:
  ```
  - add: support for object pivoting in visual editor
  - refactor: extract transform logic into separate service
  - fix: prevent null reference when loading objects
  - remove: deprecated animation frame caching
  ```

NEVER add AI assistants to co-authors. Do not add Claude, Codex, OpenAI, or Anthropic co-author lines.

## Execution Steps

1. Run `git status` to get unstaged/untracked files
2. Run `git log -40 --oneline` to see recent commits and understand context
3. Get current branch name with `git rev-parse --abbrev-ref HEAD`
4. Analyze changes:
   - Extract ticket ID (e.g., `ATS-123`) from branch name
   - Identify which files changed and their purposes
   - Determine the scope if no ticket ID (Visual, Timeline, Objects, Editor, Core, UI, Network, Animation, etc.)
5. Generate commit title: `[TICKET_ID or SCOPE] Brief description` (use multiple tags like `[Scope1] [Scope2]` when commit spans several areas)
6. Generate description with bullet points using add:/fix:/refactor:/remove: tags
7. [Scope] should reflect the area of work, not just copy branch name
8. Run `git add .` to stage all changes
9. Run `git commit -m "title\n\ndescription"`
10. Report the commit hash and summary to user

## Example Commit

**Title (single scope):** `[ATS-123] Add object pivot point editing`
**Title (multiple scopes):** `[Shared] [Console] Update card config options and editors`

**Description:**
```
- add: visual pivot editor in object properties panel
- add: real-time preview of pivot point changes
- refactor: move transform calculations to ObjectTransformService
- fix: handle negative coordinates in pivot display
```

**Full commit message:**
```
[ATS-123] Add object pivot point editing

- add: visual pivot editor in object properties panel
- add: real-time preview of pivot point changes
- refactor: move transform calculations to ObjectTransformService
- fix: handle negative coordinates in pivot display
```

## Important Notes

- ALL text must be in ENGLISH (titles, scopes, descriptions, tags)
- Be concise but specific
- If multiple features/fixes, list them all with appropriate tags
- Use the commit message format exactly as specified
- Commits are created locally; push to remote is handled separately
