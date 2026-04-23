# Card Tests — Player-Targeting

Unit tests (no Orleans). Cards that modify player stats, hand, deck, or opponent.
Require IPlayer mock (NSubstitute) with Health, Mana, Moves, Hand, Deck, Stash, Modifiers.

## Done

### TrebuchetAimer — `Game/PlayerCardTests.cs` (4 tests)
Grants TrebuchetBoost modifier to owner (buffs Trebuchet/ZipZap size).
- [x] Adds TrebuchetBoost modifier
- [x] Stacks with existing modifier value
- [x] Large size config applied
- [x] Action data type correct

### Medic — `Game/PlayerCardTests.cs` (2 tests)
Heals owner for 1 HP.
- [x] Heals 1 HP
- [x] Action data includes owner ID

### Siphon — `Game/PlayerCardTests.cs` (5 tests)
Drains mana from opponent, gives to owner.
- [x] Drains DrainAmount from opponent mana max
- [x] Adds drained mana to owner mana max
- [x] Zero mana edge case
- [x] Action data references opponent
- [x] Large drain amount

### Overclock — `Game/PlayerCardTests.cs` (3 tests)
Grants extra moves to owner.
- [x] Adds ExtraMoves to current moves
- [x] Zero moves edge case
- [x] Action data includes owner ID

### GraveDigger — `Game/PlayerCardTests.cs` (4 tests)
Takes cards from stash and adds to hand.
- [x] Moves card from stash to hand
- [x] Empty stash — fails
- [x] Snapshot records card addition
- [x] Returns action data

### Scavenger — `Game/PlayerCardTests.cs` (5 tests)
Draws cards from deck to hand.
- [x] Draws DrawCount cards from deck
- [x] Deck has fewer cards — draws what's available
- [x] Empty deck — draws nothing
- [x] Snapshot records card additions
- [x] Returns action data

### HandScramble — `Game/PlayerCardTests.cs` (5 tests)
Replaces opponent's hand with random cards from their deck.
- [x] Removes opponent hand cards, draws new from deck
- [x] Empty hand — fails
- [x] Snapshot records removals and additions
- [x] Old cards returned to deck
- [x] Action data references opponent

### Lockdown — `Game/PlayerCardTests.cs` (6 tests)
Reduces opponent's moves for Duration rounds.
- [x] Reduces max moves
- [x] Large reduction clamps to zero
- [x] Schedules restoration via RoundActionService
- [x] Dispose action restores original max
- [x] Action data references opponent
- [x] Dispose does not fire before duration

## Todo — Cross-card player edge cases

### Mana cost validation
All cards check mana before use.
- [ ] Card use with exact mana — succeeds, mana = 0
- [ ] Card use with insufficient mana — fails
- [ ] Card use with excess mana — succeeds, remainder preserved
- [ ] Zero-cost card — always playable

### Card failure states
Cards that operate on empty/invalid targets.
- [ ] Siphon on opponent with 0 mana — drains nothing, still succeeds
- [ ] Overclock when moves already at max — stacks above original max
- [ ] Medic at max HP — heal clamped, still succeeds
- [ ] GraveDigger with empty stash — fails
- [ ] Scavenger with empty deck — draws nothing or partial
- [ ] HandScramble on opponent with empty hand — no-op or fails
- [ ] Lockdown on opponent already locked down — stacks or replaces?

### Modifier system
- [ ] TrebuchetBoost modifier set to 0 initially
- [ ] TrebuchetBoost modifier incremented by TrebuchetAimer.Size
- [ ] Multiple TrebuchetAimer uses accumulate modifier
- [ ] Modifier value persists across turns until consumed
- [ ] Modifier.Set fires SyncState
