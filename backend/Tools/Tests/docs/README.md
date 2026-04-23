# Test Coverage Tracker

## Summary

| Scope | Done | Todo | Total |
|-------|------|------|-------|
| [Infrastructure](infrastructure.md) | 77 | 8 | 85 |
| [Cards — Board](cards-board.md) | 86 | 0 | 86 |
| [Cards — Player](cards-player.md) | 34 | 4 | 38 |
| [Board Mechanics](board-mechanics.md) | 127 | 0 | 127 |
| [Player Mechanics](player-mechanics.md) | 69 | 2 | 71 |
| [Meta Services](meta-services.md) | 79 | 0 | 79 |
| **Total** | **472** | **14** | **486** |

Game tests are pure unit tests (no Orleans). Infrastructure and Meta are integration tests with TestCluster + PostgreSQL.

## Architecture

```
backend/Tests/
  Fixtures/           -- test infrastructure (fixtures, helpers)
  docs/               -- this tracker
  State/              -- state, transactions, side effects (integration)
  Messaging/          -- durable queue, pipe, channel (integration)
  Game/               -- board, cards, reveal (unit)
  Grains/             -- test grain implementations
```

## Test Types

- **Unit** (Game/) — pure logic, no IO, < 1ms per test. Cards, board, player stats.
- **Integration** (State/, Messaging/) — Orleans TestCluster + PostgreSQL in Docker. ~5s startup, ~10ms per test.
- **Meta** (planned) — grain-level business logic. Same infra as integration.

## How to run

```bash
dotnet test backend/Tests/Tests.csproj                                          # all (95 tests, ~5s)
dotnet test backend/Tests/Tests.csproj --filter "FullyQualifiedName~Tests.Game"  # game only (~0.7s)
dotnet test backend/Tests/Tests.csproj --filter "FullyQualifiedName~Tests.State" # state + tx
dotnet test backend/Tests/Tests.csproj --filter "FullyQualifiedName~Messaging"   # messaging
```

## Conventions

- Board tests use visual `BoardParser.Parse()` / `BoardParser.AssertBoard()` format
- Card configs from production JSON via `CardConfigs.*`
- `TestBoardBuilder` for programmatic board setup
- `IPlayer` mocked via NSubstitute for cards that need player
- `[Collection(nameof(OrleansIntegrationCollection))]` for integration tests sharing a cluster
