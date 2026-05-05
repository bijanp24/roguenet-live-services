# RogueNet Live Services

A .NET 8 always-online game services platform demonstrating idempotent rewards, inventory ledgers, cloud saves, leaderboards, and live-ops tooling.

## What This Project Is

RogueNet is a portfolio system designed to demonstrate backend and platform engineering skill for always-online games and live-service platforms. It is built around the operational concerns that matter in production: idempotent reward grants, append-only inventory transactions, optimistic concurrency, the outbox pattern, integration tests, load tests, and runbooks for failure modes.

It is not a clone of any existing game. It is a credible production-style backend that proves I can design, build, test, operate, and explain a live-service platform end to end.

## Stack

- C# / .NET 8
- ASP.NET Core Web API
- SQL Server
- EF Core for admin/CRUD endpoints
- Dapper for hot paths (mission completion, inventory writes)
- Background worker for outbox processing
- xUnit for unit and integration tests
- k6 for load tests (planned)

Deferred until later: Redis, message bus (Kafka/RabbitMQ/Azure Service Bus), OpenTelemetry, Docker Compose, admin dashboard.

## Flagship Feature

The mission completion endpoint is the heart of this project:

```http
POST /players/{playerId}/mission-completions
```

It demonstrates idempotency keys, an append-only inventory transaction ledger, optimistic concurrency on the player profile, and the outbox pattern for downstream side effects. Duplicate client retries do not grant duplicate rewards, and downstream systems (leaderboards, telemetry) recover from failures without compromising authoritative reward state.

## Repository Layout

```text
/src
  /RogueNet.Api              ASP.NET Core Web API
  /RogueNet.Domain           Pure domain types and business rules
  /RogueNet.Application      Use cases, services, orchestration
  /RogueNet.Infrastructure   Persistence, EF Core, Dapper, outbox
  /RogueNet.Worker           Background worker for outbox processing

/tests
  /RogueNet.UnitTests
  /RogueNet.IntegrationTests

/docs
  ProjectPlan.md             The 4-week build plan
  Architecture.md            Architectural decisions and rationale
  FailureModes.md            What can go wrong and how the system responds
  Runbook.md                 Operational procedures
  ScalingPlan.md             How this scales from 1 server to a region
  InterviewNarrative.md      The spoken summary
  Tradeoffs.md               Decisions made and decisions deferred

/load-tests
  mission-completion-load-test.js

/tools
  seed-data.ps1
  reset-db.ps1
```

## Quickstart

Prerequisites: .NET 8 SDK, Docker (for local SQL Server), Git.

```bash
# Clone
git clone https://github.com/YOUR_USERNAME/roguenet-live-services.git
cd roguenet-live-services

# Start local SQL Server
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Pass1" \
  -p 1433:1433 --name roguenet-sql -d \
  mcr.microsoft.com/mssql/server:2022-latest

# Build and test
dotnet build
dotnet test

# Apply migrations
dotnet ef database update --project src/RogueNet.Infrastructure --startup-project src/RogueNet.Api

# Run the API
dotnet run --project src/RogueNet.Api

# Run the worker (in another terminal)
dotnet run --project src/RogueNet.Worker
```

API available at `https://localhost:5001` (or whatever Kestrel reports).

## Demo Path

A 5-minute demo of the project:

1. `POST /players` — create a player.
2. `GET /players/{id}/profile` — confirm the profile and starting state.
3. `POST /players/{id}/mission-completions` with a fresh `completionId` — observe rewards granted.
4. `GET /players/{id}/inventory` — see the items in the inventory.
5. Repeat step 3 with the **same** `completionId` — observe the response matches the original; no double grant.
6. Show the `InventoryTransactions` table — every grant is an immutable ledger entry.
7. Show the `OutboxEvents` table — the worker has processed the event for downstream side effects.

This sequence proves the four core invariants: idempotency, append-only ledger correctness, optimistic concurrency, and the outbox pattern.

## What This Project Demonstrates

- C# and .NET 8 service development
- SQL-backed service design with explicit indexing and migration strategy
- Distributed systems failure-mode awareness
- Idempotent player-facing API design
- Operational thinking: runbooks, observability, recovery procedures
- Documented tradeoffs — including what was deferred and why
- Communication style appropriate for senior and principal engineering roles

## Status

This is an in-progress portfolio project. See `docs/ProjectPlan.md` for the build schedule and `docs/InterviewNarrative.md` for the spoken summary.

## License

MIT
