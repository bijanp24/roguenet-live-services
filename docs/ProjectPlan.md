# RogueNet Live Services — Project Plan

## Purpose

This is a focused portfolio system designed to demonstrate backend and platform engineering skill for always-online games and live-service platforms. It targets senior, staff, and principal-level interviews at companies including Google and Rockstar Games.

The interview story:

> I built a .NET 8 always-online game services demo around player profiles, inventory, mission rewards, cloud saves, leaderboards, and live-ops tooling. The main focus was operational correctness: idempotent reward grants, append-only inventory transactions, optimistic concurrency, an outbox pattern, integration tests, load tests, and runbooks for failure modes.

## Scope Discipline

Three principles govern this project:

1. **The flagship feature is the idempotent mission completion flow.** Everything else is supporting cast. If the schedule slips, that one feature must still ship complete with tests and documentation.
2. **Cut features before cutting tests or docs.** Tests and docs are the interview signal. A working endpoint with no test and no architectural reasoning is barely better than no endpoint.
3. **Resist scope creep.** No mobile clients. No admin dashboard in the four-week plan. No microservice split. No premature distributed infrastructure.

## Stack Decisions

- **C# / .NET 8** — target framework
- **ASP.NET Core Web API** — HTTP surface
- **SQL Server** — single source of truth for player state
- **EF Core** — admin and CRUD endpoints where developer velocity matters
- **Dapper** — hot path (mission completion, inventory transaction writes) where predictable SQL and absence of change tracking matter
- **xUnit + WebApplicationFactory** — integration tests against a real SQL Server (Testcontainers optional)
- **Background worker (`IHostedService` / `BackgroundService`)** — outbox processing
- **k6** — load tests (week 4)

Explicitly deferred: Redis, message bus, OpenTelemetry, Docker Compose orchestration, admin UI.

## Architecture Choice: Modular Monolith

A modular monolith with clear seams beats a half-finished microservice mess. This project uses a single deployable API plus a single worker process, organized into modules with explicit boundaries (`Domain`, `Application`, `Infrastructure`, `Api`, `Worker`). The boundaries are designed so any module could later split into its own service if scale demanded it — but no module does today.

This is itself a senior-level talking point: choosing the simpler architecture deliberately, not by default.

## Core Domain Modules

### Player Profile

- Create player
- Read player profile (XP, level, cash balance, reputation)
- Profile versioning for optimistic concurrency

### Inventory

- Track player-owned items
- Append-only ledger of every grant, removal, or adjustment
- Prevent duplicate grants

### Mission Rewards (flagship)

- Accept mission completion requests
- Validate completion
- Grant XP, cash, and items
- Idempotency to prevent duplicate rewards
- Emit outbox events for leaderboard and stat updates

Endpoint:

```http
POST /players/{playerId}/mission-completions
```

Request body:

```json
{
  "missionId": "mission_escape_001",
  "completionId": "client-generated-guid",
  "score": 12450,
  "durationSeconds": 622,
  "difficulty": "Hard"
}
```

Behavior:

- First request grants rewards
- Duplicate request with same `completionId` returns the original result
- Duplicate request must not grant cash, XP, or items twice

### Cloud Saves

- Versioned save slots
- Conflict detection on writes

### Leaderboards

- Player scores by leaderboard
- Top-N queries
- Updated asynchronously through outbox processing

> A leaderboard update failure must never duplicate or roll back authoritative player rewards.

## Database Tables

```text
Players
PlayerProfiles
InventoryItems
InventoryTransactions   ← append-only ledger
MissionCompletions
CloudSaveSlots
LeaderboardScores
OutboxEvents            ← outbox pattern
IdempotencyKeys         ← duplicate-request handling
```

The four tables that matter most for interview discussion: `InventoryTransactions`, `OutboxEvents`, `IdempotencyKeys`, `MissionCompletions`.

## API Endpoints (minimum useful set)

```http
POST /players
GET  /players/{playerId}/profile

GET  /players/{playerId}/inventory
POST /players/{playerId}/mission-completions

GET  /leaderboards/{leaderboardId}

PUT  /players/{playerId}/cloud-saves/{slot}
GET  /players/{playerId}/cloud-saves/{slot}

GET  /admin/players/{playerId}/audit
GET  /admin/outbox
POST /admin/reconciliation/run
```

## Principal Engineer Concepts to Demonstrate

- Idempotency
- Optimistic concurrency
- Append-only ledgers
- Outbox pattern
- Background workers
- Failure-mode analysis
- Observability
- Load testing
- Database indexing
- Operational runbooks
- Backward-compatible migrations
- Clear architectural tradeoffs

The code matters. The explanation matters as much.

## Four-Week Schedule

### Week 1 — Foundation

**Goal:** Working repo, solution, schema, first endpoint, and architectural reasoning written down.

Day 1 — Repo and solution scaffold. Run setup commands. Verify `dotnet build` and `dotnet test` pass. Commit `ProjectPlan.md`.

Day 2 — `Architecture.md` first draft. No code. Modular monolith rationale, module boundaries, EF Core + Dapper split with explicit reasoning, why SQL + outbox over event sourcing, optimistic concurrency choice.

Day 3 — Database schema and initial migration. Tables in section 6 above. Verify against local SQL Server.

Day 4 — `POST /players` and `GET /players/{playerId}/profile` end-to-end with EF Core. One integration test using `WebApplicationFactory`.

Day 5 — `FailureModes.md` draft for the mission completion flow specifically. Walks through duplicate request, GUID collision, DB timeout, partial grant, outbox failure, leaderboard failure. For each: detection, mitigation, recovery, player-visible behavior.

Day 6 — Mission completion domain types in `RogueNet.Domain`. No persistence yet. Pure unit tests for reward calculation.

Day 7 — Buffer day. Catch up, clean commits, open draft PR.

**Acceptance criteria:**

- `dotnet build` and `dotnet test` pass
- API runs locally
- Player profile creation and retrieval work
- `Architecture.md` and `FailureModes.md` drafts exist
- Domain model for mission completion compiles with passing unit tests for reward calculation

### Week 2 — Flagship Reward Flow

**Goal:** Idempotent mission rewards with append-only inventory ledger, fully tested.

Deliverables:

- Mission completion endpoint
- Reward calculation (Dapper for the write path)
- Inventory transaction ledger (append-only)
- `IdempotencyKeys` table and handling
- `MissionCompletions` table
- Integration tests proving duplicate requests do not double-grant
- `FailureModes.md` filled in to completion

**Acceptance criteria:**

- First mission completion grants rewards correctly
- Retry with the same `completionId` returns the original response
- Retry does not create a second set of inventory transactions
- Inventory ledger is verifiably append-only (no UPDATEs, only INSERTs)
- Tests exercise both the happy path and the duplicate path

This week is the most important week. If it slips, slip everything else.

### Week 3 — Outbox, Worker, Observability

**Goal:** Demonstrate live-service architecture: async side effects, retryable, observable.

Deliverables:

- `OutboxEvents` table with status, retry count, last error
- Mission completion writes outbox events in the same DB transaction as the reward grant
- Background worker that polls and processes outbox events
- Leaderboard update consumer (can be minimal — a single top-N table works)
- Structured logging with correlation IDs through the request and the worker
- Metrics plan documented (even if not all are emitted yet)
- Integration test for the outbox flow end to end

**Acceptance criteria:**

- Mission completion produces an outbox event atomically with the reward
- Worker picks up and processes the event
- Failed events are retried; permanently failed events are visible
- Logs include `playerId`, `missionId`, `completionId`, `correlationId`, `outcome`, `elapsedMs`

If time gets tight: skip leaderboard ranking logic, ship a no-op consumer that just logs. The outbox is the architectural signal.

### Week 4 — Polish and Narrative

**Goal:** Make the project presentable for job search.

Deliverables:

- `InterviewNarrative.md` — written first, before README polish
- `README.md` polish — quickstart, demo path, what it demonstrates
- `Runbook.md` — operational procedures (see `docs/Runbook.md` for topics)
- `ScalingPlan.md` — how this scales from 1 server to a region
- `Tradeoffs.md` — decisions made, decisions deferred, why
- `load-tests/mission-completion-load-test.js` (k6)
- Final architecture diagram
- Short demo script (the 5-minute walkthrough)

**Acceptance criteria:**

- A recruiter understands the project in 60 seconds reading the README
- A staff/principal engineer sees the technical depth in `Architecture.md` + `FailureModes.md` + `InterviewNarrative.md` in 15 minutes
- The README has runnable setup commands
- The repo has a clear demo path
- Load test produces a graph of latency vs. concurrent requests

## Branching

```text
main
feature/project-foundation
feature/mission-rewards
feature/outbox-leaderboards
feature/docs-polish
```

Commit small. Each green test or completed doc section is a commit. The GitHub history itself is interview signal.

## Parallel Tracks

This project is one of three concurrent prep tracks:

1. **RogueNet** — this document
2. **Algorithms** — daily, ~45-60 min, C++, working through Blind 75 / NeetCode 150 with focus on hashing, two pointers, sliding window, binary search, trees/graphs, DP
3. **System design study** — ~30 min/day, 3-4 days/week, breadth topics not covered by RogueNet (consensus, sharding, consistent hashing, CDC, etc.)

Capacity: ~2-4 hours/day average. On a 2-hour day: algorithms + RogueNet, skip system design. On a 4-hour day: all three.

## First Task

```text
Create the repo.
Initialize the .NET solution.
Add this document as docs/ProjectPlan.md.
Create Architecture.md.
Build the first player profile endpoint.
Commit.
```

First commit message:

```text
Initialize RogueNet live services project plan
```
