# Board Mechanics Tests

Unit tests (no Orleans). Core board logic: generation, reveal, scanner, cells, patterns.

## Done

### Reveal (flood-fill) — `Game/RevealTests.cs` (11 tests)
- [x] Empty board — opens everything
- [x] Single mine — stops at border
- [x] Mine ring — contains flood-fill
- [x] Corridor between mines — adjacent cells stay Taken
- [x] Click on mine-adjacent cell — limited expansion
- [x] Click at corner — expands with mine pockets
- [x] Dense mine field — minimal expansion
- [x] Two regions separated by mine wall — only clicked side opens
- [x] Flagged cell without mine — reveal ignores flag
- [x] Mines along edge — fills below mine row
- [x] Diagonal mines — dense blocking

### Board Generation — `Game/BoardGenerationTests.cs` (7 tests)
- [x] Creates Size x Size cells, all Taken
- [x] Places exactly Mines count of mines
- [x] Start position is mine-free
- [x] Start neighbours are mine-free (safe zone)
- [x] Corner start — neighbours are mine-free
- [x] Mines distributed randomly (statistical test)
- [x] Max mines — fills all non-safe positions

### MinesScanner — `Game/MinesScannerTests.cs` (7 tests)
- [x] Free cell with 1 adjacent mine → MinesAround = 1
- [x] Free cell with 0 adjacent mines → MinesAround = 0
- [x] Diagonal mines counted correctly
- [x] Free cell surrounded by mines → MinesAround = 8
- [x] Recalculates after cell status change (Taken→Free)
- [x] Taken cells are not scanned
- [x] Mine in Taken cell counted by adjacent Free cell

### Cell State Transitions — `Game/CellStateTests.cs` (13 tests)
- [x] TakenCell.ToFree() → creates FreeCell, updates board
- [x] FreeCell.ToTaken() → creates TakenCell, updates board
- [x] TakenCell.ToTaken() returns self (no-op)
- [x] FreeCell.ToFree() returns self (no-op)
- [x] SetMine sets HasMine
- [x] SetFlag sets IsFlagged
- [x] RemoveFlag clears IsFlagged
- [x] Default state — no mine, no flag
- [x] AddEffect on TakenCell
- [x] RemoveEffect on TakenCell
- [x] AddEffect on FreeCell
- [x] RemoveEffect on FreeCell
- [x] ToFree updates board.Cells dictionary

### PatternShapes — `Game/PatternShapesTests.cs` (12 tests)
- [x] Rhombus(3) — cross shape, 5 cells
- [x] Rhombus(4) — even, small diamond
- [x] Rhombus(5) — larger diamond, 13 cells
- [x] Rhombus symmetry verified
- [x] SelectTaken filters only Taken cells
- [x] SelectFree filters only Free cells
- [x] Pattern clips at top-left edge
- [x] Pattern clips at bottom-right edge
- [x] Center of board — no clipping, full count
- [x] Invalid center (-1,-1) returns empty
- [x] Line horizontal shape
- [x] Line vertical shape

### BoardEvents — `Game/BoardEventsTests.cs` (15 tests)
Board event system: CellSet, Flag, Explode, EffectAdded, EffectRemoved events.
- [x] CellSet event fires on ToFree transition
- [x] CellSet event fires on ToTaken transition
- [x] CellSet does not fire on same-type transition
- [x] Flag event fires on SetFlag
- [x] Flag event fires on RemoveFlag
- [x] Explode event fires on cell Explode()
- [x] EffectAdded event fires on AddEffect
- [x] EffectRemoved event fires on RemoveEffect
- [x] Lock() suppresses all event firing
- [x] Unlock() resumes event firing
- [x] Lock/Unlock is simple bool (not ref-counted)
- [x] MinesAround fires on UpdateMinesAround
- [x] MinesAround skips when same value
- [x] ForceRecord bypasses lock
- [x] Record event fires via ForceRecord

### Flag Actions — `Game/FlagActionTests.cs` (13 tests)
Flag placement and removal on cells.
- [x] SetFlag on Taken cell — places flag
- [x] SetFlag on Taken cell with mine — works
- [x] RemoveFlag on flagged cell — removes flag
- [x] RemoveFlag on unflagged cell — no change
- [x] Flag roundtrip (set then remove)
- [x] Independent flags across cells
- [x] Flag events fire correctly
- [x] Flagging does not affect mine state
- [x] Flagging does not change cell status
- [x] Multiple cells can be flagged independently
- [x] Default cell not flagged
- [x] Flag on cell without mine
- [x] Flag persists across board updates

### Cell Effects — `Game/CellEffectsTests.cs` (16 tests)
- [x] AddEffect on TakenCell — effect persisted in Effects list
- [x] AddEffect on FreeCell — effect persisted in Effects list
- [x] RemoveEffect by Guid — correct effect removed
- [x] RemoveEffect with unknown Guid — no-op
- [x] Effects NOT carried over on ToFree/ToTaken transition (new cell instance)
- [x] Multiple effects on same cell — all tracked
- [x] Events fire for add/remove
- [x] Default empty effects
- [x] Smoke effect type correct
- [x] Fog effect type correct
- [x] AddEffect on TakenCell fires event
- [x] AddEffect on FreeCell fires event
- [x] RemoveEffect fires event
- [x] RemoveEffect unknown fires event
- [x] Multiple effects tracked
- [x] Effect with unique Guid

### Board Utility Extensions — `Game/BoardUtilsTests.cs` (23 tests)
- [x] NeighbourPositions in center — 8 neighbors
- [x] NeighbourPositions at corner — 3 neighbors
- [x] NeighbourPositions at edge — 5 neighbors
- [x] IterateNeighbours visits correct count
- [x] IterateNeighbours skips missing cells
- [x] HasMinesAround detects adjacent mines
- [x] HasMinesAround returns false when no mines
- [x] HasMinesAround returns false for Free cells
- [x] RandomPosition within bounds
- [x] RandomPosition produces variety
- [x] GetClosedShape returns empty for (-1,-1)
- [x] GetClosedShape returns empty for all-Taken
- [x] GetClosedShape finds connected regions adjacent to Free cells
- [x] Board size configuration
- [x] Board owner ID configuration
- [x] + additional edge cases (23 total)

### GetFlagWinner — `Game/GetFlagWinnerTests.cs` (10 tests)
- [x] All opponent mines flagged — returns winner ID
- [x] Some mines unflagged — returns Guid.Empty
- [x] No mines on board (vacuous truth) — returns winner
- [x] Both boards flagged — returns first iterated player
- [x] Empty board (0 cells) — skipped
- [x] Flagged non-mine cells don't satisfy win condition
- [x] Free cells skipped in check
- [x] + additional edge cases (10 total)

## Todo

### OpenMultipleCellsCommand — chord opening
Classic minesweeper chord: auto-open neighbors when flag count matches MinesAround.
Requires GameCommandUtils mocking (complex command infrastructure).
- [ ] Correct flag count — opens all unflagged neighbors
- [ ] Incorrect flag count — fails (not enough flags)
- [ ] Source is Taken — fails (must be Free cell)
- [ ] Unflagged neighbor has mine — explodes, deals damage
- [ ] Multiple neighbors opened — all revealed correctly
- [ ] Recursive reveal after chord open

### EnsureGenerated — lazy board init
Requires IBoardGenerator/IBoardRevealer mocking.
- [ ] EnsureGenerated on empty board — generates then reveals
- [ ] EnsureGenerated on existing board — no-op, just reveals
- [ ] First click position is mine-free after generation

### SkipTurn command
Requires GameCommandUtils mocking.
- [ ] SkipTurn on current player's turn — succeeds
- [ ] SkipTurn not on player's turn — fails
