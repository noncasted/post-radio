# Test Coverage Tracker

This project intentionally contains only post-radio tests that belong in this repository:

- infrastructure unit/integration tests restored under `Execution/`, `Messaging/`, `State/`, `Fixtures/`, and `Grains/`;
- post-radio audio tests under `Meta/Audio/`.

Gameplay and legacy mines-leader Meta tests are intentionally excluded because this repository does not contain those domains.

## How to run

```bash
# Build the restored test project
dotnet build backend/Tools/Tests/Tests.csproj

# Fast audio unit tests
dotnet run --project backend/Tools/Tests/Tests.csproj -- --filter-namespace Tests.Meta.Audio

# Fast execution/task-scheduling tests
dotnet run --project backend/Tools/Tests/Tests.csproj -- --filter-namespace Tests.Execution

# Integration infrastructure tests; requires Docker/Testcontainers PostgreSQL
dotnet run --project backend/Tools/Tests/Tests.csproj -- --filter-namespace Tests.State --filter-namespace Tests.Messaging
```
