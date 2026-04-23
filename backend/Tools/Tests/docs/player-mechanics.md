# Player Mechanics Tests

Unit tests (no Orleans). Player stats, deck, hand, stash management.
Use `ValueProperty<T>.ForTest()` for state without network sync.

## Done

### Health — `Game/PlayerMechanicsTests.cs` (12 tests)
- [x] SetMax sets maximum HP
- [x] SetCurrent clamps to [0, Max]
- [x] SetCurrent above max clamped
- [x] SetCurrent below 0 clamped
- [x] TakeDamage reduces current HP
- [x] TakeDamage below 0 clamps to 0
- [x] TakeDamage negative throws
- [x] Heal increases current HP
- [x] Heal above max clamps to max
- [x] Heal negative throws
- [x] State sync on change
- [x] Initial state

### Mana — `Game/PlayerMechanicsTests.cs` (9 tests)
- [x] SetMax sets maximum mana
- [x] SetMax clamps current if exceeds new max
- [x] Restore sets current = max
- [x] Use reduces current mana
- [x] Use more than available — clamps
- [x] Use negative throws
- [x] SetCurrent clamps to [0, Max]
- [x] State sync
- [x] Initial state

### Moves — `Game/PlayerMechanicsTests.cs` (10 tests)
- [x] SetMax sets max moves per turn
- [x] Restore resets to max
- [x] OnUsed decrements by 1
- [x] OnUsed at 0 — throws
- [x] Lock sets to 0 and IsAvailable = false
- [x] IsAvailable after restore
- [x] SetCurrent clamps
- [x] SetMax clamps left
- [x] State sync
- [x] Initial state

### Deck — `Game/PlayerMechanicsTests.cs` (7 tests)
- [x] Init fills deck with N cards cycling through selected types
- [x] Init cycles through cards
- [x] DrawCard returns a card and removes from deck
- [x] DrawCard from empty deck — throws
- [x] AddCard adds to deck
- [x] RemoveCard removes from deck
- [x] Count reflects current deck size

### Hand — `Game/PlayerMechanicsTests.cs` (7 tests)
- [x] SetSize sets hand capacity
- [x] Add returns ActiveCard with Id and Type
- [x] Add appears in Entries
- [x] Remove by Id removes card
- [x] Remove non-existent — no error
- [x] Entries count tracking
- [x] Unique Ids per card

### Stash — `Game/PlayerMechanicsTests.cs` (6 tests)
- [x] Add puts card on top
- [x] Pick returns top card (LIFO)
- [x] Pick from empty stash — throws
- [x] Collect returns all and clears
- [x] Collect empty — returns empty
- [x] State sync

### RoundActionService — `Game/PlayerMechanicsTests.cs` (8 tests)
- [x] Schedule action with N rounds delay
- [x] Tick decrements all scheduled action counters
- [x] Action executes when RoundsLeft reaches 0
- [x] Multiple actions at same round — all execute
- [x] Schedule with 0/negative rounds — no-op
- [x] No scheduled actions — Tick is no-op
- [x] Entry removed after execution
- [x] Different delays — correct ordering

### Modifiers — `Game/PlayerMechanicsTests.cs` (6 tests)
- [x] Initial modifier values all 0
- [x] Set modifier updates value
- [x] Get returns current value
- [x] Inc increments by 1
- [x] Reset sets to 0
- [x] State sync

### PlayerActions — `Game/PlayerMechanicsTests.cs` (4 tests)
- [x] OnCellOpened fires CellOpened delegate
- [x] OnCardUsed fires CardUsed delegate
- [x] Multiple listeners all receive
- [x] Terminated lifetime unsubscribes

## Todo — Round Mechanics

### TimeLimitedRound — turn timer and win conditions
Requires complex game context mocking (IGameContext, IGameReadyAwaiter, ISnapshotSender).
- [ ] Timer countdown decrements SecondsLeft per player
- [ ] Time bonus on CellOpened action (+TimeGainPerAction)
- [ ] Time bonus on CardUsed action (+TimeGainPerAction)
- [ ] Time reaches 0 — round ends, opponent wins
- [ ] Health reaches 0 — round ends, attacker wins
- [ ] Flag winner detected after 2+ rounds
- [ ] User lifetime terminated — immediate game end
- [ ] Mana.Max increases by 1 each round
- [ ] Cards restored to Hand.Size at round start

### LastManStandingRound — move-based turns
- [ ] Turn ends when Moves.Left reaches 0
- [ ] Turn ends when timer expires (whichever first)
- [ ] TurnsCountdown polls Moves.Left every 0.2s
- [ ] Global timer (not per-player) countdown
- [ ] Mana.Max increases by 1 each round

### RoundPlayers — card restoration
- [ ] RestoreCards fills hand to Hand.Size from deck
- [ ] Deck empty — collects stash cards, re-adds to deck, then draws
- [ ] Both deck and stash empty — draws nothing
- [ ] Each card addition recorded in snapshot

### MoveSnapshot — recording system
Requires IGameContext and board event subscription mocking.
- [ ] RecordCardUse prepends to record list (position 0)
- [ ] RecordCardAdd appends to record list
- [ ] RecordCardRemove appends to record list
- [ ] HandleBoards subscribes to all board events
- [ ] Lock prevents recording board events
- [ ] Unlock resumes recording
- [ ] Collect returns SharedMoveSnapshot with all records
- [ ] Board records grouped by BoardOwnerId
