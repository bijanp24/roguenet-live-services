# Architecture

This document captures the architectural decisions for RogueNet Live Services. It is intentionally written before significant code is committed, because the act of writing forces design questions to be resolved before they are locked in.

## Architectural Style: Modular Monolith

RogueNet is a modular monolith: a single deployable API process plus a single deployable worker process, organized internally into modules with explicit boundaries. It is **not** a set of microservices today, and it is **not** intended to become one without a real reason.

### Why monolith first

A four-week portfolio project does not have the operational budget for a microservice architecture. Distributed systems impose costs — service discovery, distributed tracing, deployment orchestration, schema evolution across multiple services, network failure handling at every call site — that only pay off once you have sufficient scale, team size, or domain isolation to justify them. None of those conditions hold here.

A clean modular monolith with well-defined seams is a stronger interview signal than a half-finished microservice mess.

### Why the modules still matter

The internal module boundaries are designed so that any module *could* split into its own service later if the situation demanded it. The boundaries are:

| Module | Responsibility |
|---|---|
| `RogueNet.Domain` | Pure domain types and business rules. No persistence, no I/O, no framework dependencies. |
| `RogueNet.Application` | Use cases, orchestration, transaction boundaries. Depends on `Domain`. |
| `RogueNet.Infrastructure` | Persistence (EF Core, Dapper), outbox, external integrations. Depends on `Domain` and `Application` (implements interfaces from them). |
| `RogueNet.Api` | HTTP surface. Composition root. Depends on `Application` and `Infrastructure`. |
| `RogueNet.Worker` | Background processor for outbox events. Depends on `Application` and `Infrastructure`. |

`Domain` and `Application` know nothing about EF Core, ASP.NET Core, or SQL Server. This makes them trivially unit-testable and gives the project an honest dependency inversion story.

## Data Access: EF Core + Dapper Hybrid

This project uses **two** data access libraries deliberately:

- **EF Core** for admin endpoints, CRUD operations, and any path where developer velocity matters more than predictable SQL
- **Dapper** for the mission completion hot path — specifically for reward grants and inventory transaction inserts

### The reasoning

EF Core is excellent at modeling object graphs, change tracking, and migration management. It is less excellent when you need to know exactly what SQL is being issued, want to avoid the overhead of the change tracker, or are inserting many rows into an append-only ledger where there is no "update" semantically.

The mission completion flow is the hottest path in the system. It must be:

- Predictable in its SQL output (so query plans can be reasoned about and indexes can be designed)
- Free of accidental N+1 queries
- Free of unnecessary object materialization

Dapper gives me hand-written SQL with the right shape and lets the change tracker stay out of the way.

For everything else — admin endpoints, player creation, profile reads — EF Core's productivity wins, and the performance ceiling is high enough that hand-tuning SQL is not justified.

The boundary is documented and enforced: Dapper lives in a single `MissionCompletionRepository`-style class. It does not sprinkle into other repositories.

## Persistence: SQL Server as Source of Truth

Player state is authoritative in SQL Server. This includes profiles, inventory, completed missions, cloud saves, and leaderboards. The design choices that follow from this:

### Optimistic concurrency on player profiles

`PlayerProfiles` carries a `Version` column. Reads return the current version; writes specify the expected version and fail with a 409 Conflict if it has changed. This is preferable to pessimistic locking because:

- It is wait-free under normal load
- It surfaces concurrency conflicts to the client where they can be resolved with a retry
- It does not hold transactions open across HTTP roundtrips

### Append-only inventory ledger

`InventoryTransactions` is INSERT-only. Every grant, every removal, every adjustment is a row. The current state of a player's inventory is computed from the ledger (or maintained as a denormalized projection in `InventoryItems` for read performance, with the ledger as the source of truth).

Why append-only:

- Debuggability — every state change has an audit trail
- Fraud and abuse investigation
- Customer support — "the player says they didn't get their reward" is answerable
- Reconciliation — projections can be rebuilt from the ledger
- Interview credibility — this is how real game economies work

### Idempotency keys

The `IdempotencyKeys` table records `(key, request_hash, response_payload, created_at)` for client-supplied keys. The mission completion endpoint hashes the request body and stores the response. A duplicate request with the same key returns the stored response without executing the operation.

If a duplicate request arrives with the same key but a *different* body, the system rejects it as a 409 — same key, different intent is a client bug.

### Outbox pattern

The classic distributed-systems failure is:

```text
1. Database write succeeds
2. Message publish fails (network blip, broker down)
3. System is now inconsistent — the player has rewards but the leaderboard never updates
```

The outbox pattern fixes this:

```text
1. Mission completion grants rewards
2. Outbox event is INSERTED in the same DB transaction
3. Both succeed or both fail — atomic
4. A background worker reads the outbox and dispatches events
5. Failures are retried; permanent failures are surfaced for ops
```

In this project, the worker is in-process to start (`IHostedService` in `RogueNet.Worker`) and the "dispatch" is to internal consumers (leaderboard updater, telemetry). The pattern is the same one that would scale to Kafka or a message bus later — the outbox table is the integration seam.

### Why not event sourcing

I considered modeling the entire system as event-sourced. I rejected it because:

- Event sourcing adds significant complexity (event store, projections, replay machinery, schema evolution of events)
- The append-only inventory ledger gives me 80% of the audit benefits with 20% of the complexity
- Player profile state is naturally a small mutable object; modeling it as a stream of events is overkill

Event sourcing is the right answer for some systems. It is not the right answer for a four-week portfolio project where the goal is to demonstrate operational correctness, not architectural fashion.

## API Surface

The API is RESTful with resource-oriented URLs. Mission completions are modeled as a sub-resource of a player:

```http
POST /players/{playerId}/mission-completions
```

This is preferable to a procedural `POST /complete-mission` because:

- It scopes the operation to its owning aggregate
- It makes idempotency natural (the resource has an identity)
- It composes cleanly with admin endpoints (`GET /admin/players/{playerId}/audit`)

Errors are returned as RFC 7807 Problem Details. Idempotency conflicts are 409 with a `type` URI explaining the specific conflict.

## Background Worker

The outbox worker runs as a separate process (`RogueNet.Worker`) using `BackgroundService`. It:

1. Polls `OutboxEvents` for pending rows (with a status filter and `ORDER BY CreatedAt`)
2. Dispatches each event to its consumer (in-process for now, message bus later)
3. Updates status on success
4. On failure, increments retry count and records the error
5. After N retries, marks the event as permanently failed and surfaces it via the admin endpoint

The polling loop uses `FOR UPDATE SKIP LOCKED` semantics (or the SQL Server equivalent — `READPAST` hint with `UPDLOCK`) so multiple worker instances could run concurrently without double-processing.

## Observability

Even before OpenTelemetry is added, the code is structured to be observable:

- Every request has a `CorrelationId` (generated at the API edge, propagated through `HttpContext.Items`, included in worker logs when an outbox event is dispatched)
- Structured logs use Serilog with consistent field names (`playerId`, `missionId`, `completionId`, `correlationId`, `outcome`, `elapsedMs`)
- The mission completion endpoint emits a single summary log line per request with all relevant fields
- Metrics are designed but not yet emitted (see `docs/ProjectPlan.md` section on observability)

OpenTelemetry instrumentation is deferred to keep the four-week scope honest. The code is not coupled to Serilog in a way that would block migration.

## Tradeoffs Worth Naming

| Decision | Cost | Benefit |
|---|---|---|
| Modular monolith | Cannot scale modules independently today | Vastly simpler to build, test, deploy, and reason about |
| EF Core + Dapper hybrid | Two libraries to know; boundary discipline required | Right tool for each path; explicit performance story for the hot path |
| SQL outbox over Kafka | Database becomes a queue; not infinitely scalable | No additional infrastructure; transactional with the data write |
| Optimistic concurrency | Clients must handle 409 retries | Wait-free; no held locks |
| Append-only ledger over event sourcing | More disk than minimal CRUD | Audit trail without full event-sourcing complexity |
| In-process worker first | Single point of failure for outbox processing | Trivial deployment; clean migration path to a separate service |

## What This Architecture Defers

- Distributed caching (Redis)
- Distributed message bus (Kafka, RabbitMQ, Azure Service Bus)
- Full distributed tracing (OpenTelemetry)
- Multi-region deployment
- Read replicas
- Sharding
- gRPC or other non-HTTP transports
- Admin UI

Each of these is documented in `docs/ScalingPlan.md` with the conditions under which it would be added.
