# Card Tests — Board-Targeting

Unit tests (no Orleans). Cards that modify the game board.
Use `TestBoardBuilder` / `BoardParser` for visual board setup and assertions.
Configs loaded from `config.cards.json` via `CardConfigs.*`.

## Done

### Bloodhound — `Game/BloodhoundTests.cs` (5 tests)
Opens Taken cells in rhombus(Size) pattern, then Reveal flood-fills.
Constructor: `(IBoard target, Config config, Payload payload)`
- [x] Opens target area and flood-fills safe region
- [x] Dense mine ring limits reveal to enclosed area
- [x] All cells in cross already Free — fails
- [x] At corner — clips to board, reveals visible area
- [x] ActionData contains target player ID

### Sonar — `Game/SonarTests.cs` (5 tests)
Auto-flags unflagged mines in rhombus(Size) pattern. No cell status change.
Constructor: `(IBoard target, Config config, Payload payload)`
- [x] Flags mines within range
- [x] Multiple mines in range — all flagged
- [x] Skips already-flagged mines
- [x] Mines outside range untouched
- [x] No mines in range — fails

### OpponentBomb — `Game/OpponentBombTests.cs` (5 tests)
Targets single cell. Mine → explode + damage. No mine → Free + reveal.
Constructor: `(IPlayer opponent, IBoard target, Payload payload)`
- [x] Mine hit — explodes, deals 1 damage
- [x] No mine — reveals large safe area
- [x] Tight mine ring — reveal contained
- [x] Free cell target — fails
- [x] Out of bounds — fails

### ErosionDozer — `Game/ErosionDozerTests.cs` (4 tests)
Opens Taken cells bordering Free (GetClosedShape), closest first, up to Size.
Constructor: `(IBoard target, Config config, Payload payload)`
- [x] Erodes from Free border, reveal expands
- [x] No Free cells — fails
- [x] Mines contain reveal within ring
- [x] Target is Free — fails (GetClosedShape returns empty)

### Trebuchet — `Game/TrebuchetTests.cs` (8 tests)
Closes Free cells in rhombus(Size) pattern on opponent board, places mines on alternating edges.
- [x] Converts Free cells to Taken in pattern
- [x] Places mines on edge cells
- [x] All cells already Taken — fails
- [x] TrebuchetBoost modifier increases size
- [x] Resets TrebuchetBoost after use
- [x] Empty board fails
- [x] Action data returned
- [x] Mine placement pattern verified

### ZipZap — `Game/ZipZapTests.cs` (7 tests)
Chains through unflagged mines in search radius, converts each to Free.
- [x] Finds nearest unflagged mine, opens it
- [x] No Free cells — fails
- [x] No unflagged mines — fails
- [x] Flagged mines skipped
- [x] Chain multiple mines
- [x] Action data with targets
- [x] Target cells converted to Free

### OpponentFlagErase — `Game/OpponentFlagEraseTests.cs` (7 tests)
Removes flags from opponent board in rhombus(Size) pattern.
- [x] Removes flags in pattern
- [x] No flags — still succeeds
- [x] Only flagged cells affected
- [x] Empty board — fails
- [x] All Free — fails
- [x] Action data returned
- [x] Removes flags from non-mine cells

### OpponentFlagReshuffle — `Game/OpponentFlagReshuffleTests.cs` (7 tests)
Reshuffles flags on opponent board.
- [x] Moves flags to different cells
- [x] No flags — no change
- [x] All flagged — no change
- [x] Multiple flags relocated
- [x] Empty board — fails
- [x] All Free — fails
- [x] Action data returned

### MinefieldScout — `Game/MinefieldScoutTests.cs` (7 tests)
Reveals mine locations, flags mines, opens safe cells.
- [x] Flags mines in line pattern
- [x] Opens non-mine cells
- [x] Returns revealed positions
- [x] No cells — fails
- [x] All Free — fails
- [x] Chooses longer line pattern
- [x] Mix of mines and safe cells

### ChainReaction — `Game/ChainReactionTests.cs` (9 tests)
Chain reaction from target — spawns explosions that spread.
- [x] Chains from initial mine
- [x] Target not mine — fails
- [x] MaxChain limits spread
- [x] Spawns mines around targets
- [x] Free cells converted to Taken with mine
- [x] Skips existing mines
- [x] Out of bounds — fails
- [x] Flagged mines skipped in chain
- [x] Action data returned

### Smoke — `Game/SmokeTests.cs` (8 tests)
Adds Smoke CellEffect to cells in pattern for Duration rounds.
- [x] Adds Smoke effect to cells
- [x] Correct effect type (CellEffectType.Smoke)
- [x] Schedules dispose action
- [x] Affects both Taken and Free cells
- [x] Empty board — fails
- [x] Action data returned
- [x] Dispose action removes effects
- [x] All effects share same ID per card use

### FogOfWar — `Game/FogOfWarTests.cs` (8 tests)
Adds Fog CellEffect to cells in pattern for Duration rounds.
- [x] Adds Fog effect to Free cells only
- [x] Taken cells not affected
- [x] No Free cells — fails
- [x] Schedules dispose action
- [x] Correct effect type (CellEffectType.Fog)
- [x] Empty board — fails
- [x] Action data returned
- [x] Dispose action removes effects

### Purge — `Game/PurgeTests.cs` (6 tests)
Removes all cell effects from all board cells.
- [x] Removes all effects from board
- [x] No effects — succeeds (no-op)
- [x] Mixed effect types — all removed
- [x] Multiple effects on same cell
- [x] Action data returned
- [x] Combined smoke+fog cleanup
