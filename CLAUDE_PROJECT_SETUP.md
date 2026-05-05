# Claude Project Setup — RogueNet Live Services

This file gives you everything you need to set up a Claude Project for RogueNet so future conversations start with full context. Do this once, then every future RogueNet chat is primed.

## Step 1: Create the Project

1. Open claude.ai in your browser.
2. In the left sidebar, click **Projects**.
3. Click **Create Project**.
4. Name it: `RogueNet Live Services`
5. Description: `Principal Engineer interview portfolio — .NET 8 always-online game services platform`

## Step 2: Add Custom Instructions

Paste the following into the project's custom instructions field:

```text
This project is RogueNet Live Services — a .NET 8 always-online game services portfolio system I'm building to demonstrate Principal Engineer readiness for backend and platform roles. Target companies include Google (I have a 2-year interview pass) and Rockstar Games.

## Stack
- C# / .NET 8, ASP.NET Core Web API
- SQL Server
- EF Core for admin/CRUD; Dapper for the mission completion hot path
- xUnit for tests; WebApplicationFactory for integration tests
- Background worker (BackgroundService) for outbox processing
- k6 for load tests (week 4)

Deferred: Redis, message bus (Kafka/RabbitMQ/Azure Service Bus), OpenTelemetry, Docker Compose, admin UI.

## Architecture
Modular monolith. Modules: Domain, Application, Infrastructure, Api, Worker. Clean dependency inversion — Domain and Application have no framework dependencies.

## Flagship feature
POST /players/{playerId}/mission-completions

Demonstrates: idempotency keys, append-only inventory transaction ledger, optimistic concurrency on player profile, outbox pattern for downstream side effects. All four operations in a single SQL transaction.

## Constraints
- Working capacity: 2-4 hours/day, sometimes less
- 4-week target plan, but 2-year ceiling on actual deadline
- Scope discipline matters more than feature breadth — flagship feature must ship complete with tests and docs
- Cut features before cutting tests or docs
- No mobile clients, no admin dashboard, no microservices in the 4-week plan

## How I work
- Use Rider for deep .NET work
- Use VS Code for markdown and quick edits
- Use Claude for planning, architecture review, code review, narrative refinement
- Algorithms practice in C++ runs in parallel (~45-60 min/day) — Blind 75 / NeetCode 150
- System design study runs in parallel (~30 min/day, 3-4 days/week)

## How I want Claude to engage
- Push back on scope creep aggressively
- Default to the simpler architecture, not the more impressive-sounding one
- When I ask for a design review, act as a Principal Engineer reviewer — focus on correctness, operational risk, and interview clarity
- Direct communication; don't soften pushback
- When I'm stuck, propose 2-3 concrete options with tradeoffs rather than asking open-ended questions
- Skip preamble. Get to the point.

## Current week
[Update this as you progress: Week 1 / Week 2 / Week 3 / Week 4]
```

Update the "Current week" line as you progress through the plan. It costs nothing and makes future conversations sharper.

## Step 3: Upload Project Files

Upload these files to the project (Project files panel, "Add content" button):

1. `docs/ProjectPlan.md` — the build plan
2. `docs/Architecture.md` — architectural decisions
3. `docs/FailureModes.md` — failure-mode catalog
4. `docs/InterviewNarrative.md` — spoken summaries
5. `README.md` — repo overview

You can re-upload these as they evolve. The files in the project are what every conversation in the project sees as context.

## Step 4: Test the Setup

Start a new chat in the project and ask something like:

> Review the architecture doc as a Principal Engineer. Where would you push back on the design choices?

If the response references the modular monolith decision, the EF Core/Dapper hybrid, the outbox pattern, or other specifics from `Architecture.md` — the project is wired correctly.

## Maintenance

- Re-upload `ProjectPlan.md` when you want to update the schedule
- Re-upload `Architecture.md` when major design decisions change
- Update the "Current week" line in custom instructions weekly
- Don't dump every file you write into the project — only the docs that establish context. Code lives in the GitHub repo, not in Claude's context.

## What this gets you

Every conversation in the project starts with:
- Knowledge of the stack
- Knowledge of the flagship feature and its design
- Knowledge of your constraints (capacity, timeline, scope discipline)
- The expectation that you want direct feedback, not validation

You won't have to re-explain anything. You can ask things like "review this code" or "what's the next step on the outbox worker" and get answers calibrated to the actual project.
