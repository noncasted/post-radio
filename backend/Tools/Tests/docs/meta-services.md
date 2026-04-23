# Meta Services Tests

Integration tests requiring Orleans TestCluster + PostgreSQL.
Test grain-level business logic for user management, matchmaking, bots.

## Done

### UserRating — `Meta/MetaGrainTests.cs` (7 tests)
- [x] Win record increases total rating
- [x] Loss record decreases total rating (negative)
- [x] Win + Loss net rating calculation
- [x] Multiple wins accumulate correctly
- [x] No records — GetTotal returns 0
- [x] Rating can go negative (no floor at grain level)
- [x] State persists across grain references

### UserProgression — `Meta/MetaGrainTests.cs` (5 tests)
- [x] Win record adds positive experience
- [x] Loss record adds positive experience (both positive)
- [x] Win + Loss both accumulate
- [x] No records — GetTotal returns 0
- [x] Multiple records — sum all (3 wins + 2 losses)

### UserAuth — `Meta/MetaGrainTests.cs` (4 tests)
- [x] Fresh grain — IsExists returns false
- [x] OnRegistered sets IsExists to true
- [x] OnRegistered stores registration date
- [x] OnRegistered called twice — idempotent

### UserDeck — `Meta/MetaGrainTests.cs` (6 tests)
- [x] Initialize creates MaxDecks (3) with BaseDeck cards
- [x] GetSelected after init returns BaseDeck
- [x] Update single deck — persists custom cards
- [x] Update all decks and selectedIndex — persisted
- [x] GetSelected after changing selectedIndex returns different deck
- [x] Initialize overwrites previous state

### UserMatchHistory — `Meta/MetaGrainTests.cs` (4 tests)
- [x] Add single match — stored in history
- [x] GetBlock returns last N matches
- [x] GetBlock more than total — returns all
- [x] Empty history — returns empty list

### Match lifecycle — `Meta/MetaGrainTests.cs` (9 tests)
- [x] Setup stores GameMatchType and participants
- [x] Setup fetches participant decks from UserDeck grains
- [x] OnComplete sets winner and calculates duration
- [x] OnComplete updates winner progression (+100 XP)
- [x] OnComplete updates loser progression (+30 XP)
- [x] OnComplete updates winner rating (+25)
- [x] OnComplete updates loser rating (-15)
- [x] OnComplete stores rating changes in state
- [x] OnComplete adds match to both players' histories

## Todo

### UserGrain — `Meta/Users/Common/UserGrain.cs`
Requires derived fixture with IUserCollection StateCollection.
- [ ] Create user — state persisted
- [ ] Read user — returns stored data
- [ ] User projection — updated on state change

### UserProjection — payload delivery and caching
- [ ] SendCached to connected user — delivered immediately
- [ ] SendCached to disconnected user — cached for later
- [ ] OnConnected — delivers all cached payloads
- [ ] OnDisconnected — marks user as disconnected
- [ ] SendOneTime to connected user — delivered
- [ ] SendOneTime to disconnected user — discarded (not cached)
- [ ] ForceNotify — resends all cached payloads
- [ ] Cache stores payload without sending

### BotFactory — `Meta/Bots/BotFactory.cs`
Requires derived fixture with BotCollection + UserCollection.
- [ ] Create bot — user + deck initialized
- [ ] Bot deck has random cards from BotPool
- [ ] Bot added to collection after creation
- [ ] Bot collection — add/remove/list

### BotCollection — `Meta/Bots/BotCollection.cs`
- [ ] Add bot to collection
- [ ] Remove bot from collection
- [ ] Random bot selection
- [ ] Empty collection — random selection returns empty/fallback

### MatchFactory — match creation
- [ ] Create with 2 participants — match grain initialized
- [ ] CreateWithBot picks random bot from BotCollection
- [ ] CreateWithBot with empty bot collection — falls back to Guid.Empty
- [ ] Match projection sent to all participants

### Matchmaking — queue logic (unit tests with mocks)
- [ ] SearchMatch adds user to type-specific queue
- [ ] New search removes user from all other queues
- [ ] CancelMatchSearch removes from all queues
- [ ] 2+ users in same queue — PvP match created
- [ ] Wait time exceeds threshold — bot match created
- [ ] Disconnected user skipped during matching
- [ ] User connected check before bot match creation
- [ ] Loop polling interval (100ms search, 500ms idle)

### ConnectedUsers — session tracking (unit tests)
- [ ] Add session — user marked connected
- [ ] Remove session — user marked disconnected
- [ ] IsConnected returns correct status
- [ ] Lifetime callback cleanup on session removal
