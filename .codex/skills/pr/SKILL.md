---
name: pr
description: Prepare or inspect pull request content for post-radio. Use when the user invokes /pr, asks to create PR text, summarize branch changes for a PR, or prepare review-ready PR notes.
---

# PR Skill

When the user runs `/pr`, create a GitHub pull request for the current branch.

## Rules

### 1. PR Title Format
- Extract ticket ID from branch name (e.g., `ATS-123`) → `[ATS-123] Brief description`
- If no ticket ID → `[Scope] Brief description`
  - Scope examples: [Visual], [Timeline], [Objects], [Editor], [Core], [UI], [Animation], [Anchors]
- Title must be SHORT (under 70 characters) and descriptive
- ALL text in ENGLISH

### 2. PR Body Format
```
## Overview

[2-3 sentences describing the feature/fix and its purpose]

## Changes

### Feature/Area Name (add/fix/refactor)
- Brief bullet describing what was added or changed
- Another bullet

### Another Area (refactor)
- Brief bullet
```

## Execution Steps

1. Get branch info:
   - `git rev-parse --abbrev-ref HEAD` → current branch name
   - `git log main..HEAD --oneline` → all commits in this branch
   - `git diff main...HEAD --name-only` → all changed files

2. Analyze the scope of work:
   - Extract ticket ID from branch name (pattern: `[A-Z]+-[0-9]+`)
   - Group changed files by area (Schemes, GamePlay, Internal, etc.)
   - Read 2-4 KEY source files (not prefabs/meta/scenes) to understand what was implemented
   - Focus on: new .cs files, modified core files — skip .meta, .prefab, .unity, .asset

3. Generate PR title:
   - Use ticket ID if found, else derive scope from the work done
   - Summarize the main feature in a few words

4. Generate PR body:
   - Overview: what problem is solved / what feature is added
   - Changes: group by logical area, use same add:/fix:/refactor: convention as commits
   - Test Plan: specific, actionable steps to verify the changes

5. Push branch if needed:
   - Check if remote tracking branch exists: `git status -sb`
   - If not pushed: `git push -u origin HEAD`

6. Create PR:
   ```
   gh pr create --title "..." --body "..." --base main
   ```

7. Report the PR URL to the user

## Key Principles

- Read ACTUAL source files to understand what was built, not just file names
- PR body should explain the WHY and WHAT, not just list file names
- Test plan must be concrete — not "test the feature" but specific steps
- If branch has many commits, synthesize them into coherent summary (not just list commits)
- Base branch is always `main` unless user says otherwise

## Example

**Branch:** `feature/anchor-points`

**Title:** `[Anchors] Add anchor point editing for sprite frames`

**Body:**
```
## Overview

Implements per-frame anchor point editing in the sprite animation editor.
Anchor points allow attaching effects or objects to named positions on animated sprites.

## Changes

### Anchor Point System (new feature)
- add: ObjectAnchorPointScheme / ObjectAnchorsScheme for per-frame anchor serialization
- add: ObjectAnchorsSelector panel with up to 8 toggle buttons per frame
- add: ObjectAnchorEdit draggable element for repositioning anchors on canvas

### Common Drag Infrastructure (refactoring)
- add: ObjectEditDraggableElement and ObjectEditElementPointerHandler base classes
- refactor: ObjectCollisionEdit to use new common drag base

### Prefab Organization (refactoring)
- refactor: move collision/animation/timeline prefabs into dedicated subfolders
```
