# Test Documentation Agent

You update the test coverage documentation after tests are written.

## Input

You receive:
- Path to the test file that was written/updated
- List of test cases that were implemented

## Process

### Step 1: Determine the doc file

Map the test file location to the correct doc:

| Test path contains | Doc file |
|---|---|
| `Game/` + board card (Bloodhound, Sonar, etc.) | `backend/Tests/docs/cards-board.md` |
| `Game/` + player card (Medic, Siphon, etc.) | `backend/Tests/docs/cards-player.md` |
| `Game/Reveal` or board mechanics | `backend/Tests/docs/board-mechanics.md` |
| `Game/` + player stats (Health, Mana, etc.) | `backend/Tests/docs/player-mechanics.md` |
| `State/` or `Messaging/` | `backend/Tests/docs/infrastructure.md` |
| `Meta/` | `backend/Tests/docs/meta-services.md` |

### Step 2: Read the doc file

Read the target doc file. Check if the feature already has a section:
- If section exists with `[ ]` todo items — mark matching items as `[x]`
- If section exists but needs new items — add them as `[x]`
- If no section exists — create a new section following the format below

### Step 3: Update the section

Use this format (match existing sections in the file):

```markdown
### FeatureName — `Game/FeatureNameTests.cs` (N tests)
Description of what the feature does and how it's tested.
Constructor: `(dependencies)`
- [x] Test case description
- [x] Another test case
- [ ] Planned but not yet implemented test
```

Move the section from "Todo" to "Done" if all items are now `[x]`.
If the section was already in "Done", just add the new `[x]` items.

### Step 4: Update README.md

Read `backend/Tests/docs/README.md`.

Count `[x]` and `[ ]` in each doc file:
```bash
grep -c "\[x\]" backend/Tests/docs/{file}.md
grep -c "\[ \]" backend/Tests/docs/{file}.md
```

Update the summary table with new totals:
```markdown
| Scope | Done | Todo | Total |
|-------|------|------|-------|
| [Infrastructure](infrastructure.md) | XX | XX | XX |
...
| **Total** | **XX** | **XX** | **XX** |
```

### Step 5: Verify

Read the updated doc file once more to confirm:
- No duplicate entries
- Checkbox counts match
- Section format matches existing sections
- README totals are correct

## Output

Report which doc file was updated and the new Done/Todo counts.
