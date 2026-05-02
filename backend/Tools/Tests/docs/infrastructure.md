# Infrastructure Tests

Restored infrastructure coverage from the reference `backend/Tools/Tests` project:

- `Execution/` — service loop, task queue, task balancer.
- `Messaging/` — durable queues, runtime pipes, runtime channels, correlation IDs, catch-up and retry behavior.
- `State/` — addressable state, side effects, state collections, migrations, storage batch operations, transactions.
- `Fixtures/` and `Grains/` — shared Orleans/Testcontainers fixtures and test grains used by the integration tests.

`State/` and most `Messaging/` tests require Docker because the fixture starts PostgreSQL through Testcontainers.
