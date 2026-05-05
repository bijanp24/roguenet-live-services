# Interview Narrative

This is the spoken summary of RogueNet Live Services, calibrated for different audiences and time budgets. Practice these out loud until they feel natural.

## The 30-second version (recruiter, hallway)

> RogueNet is a .NET 8 backend portfolio project for always-online game services. I built it around player profiles, mission rewards, inventory, cloud saves, and leaderboards. The most important feature is the mission completion flow — it uses idempotency keys, an append-only inventory ledger, optimistic concurrency, and an outbox pattern so duplicate client retries don't grant duplicate rewards and leaderboard failures don't corrupt player state. I also wrote architecture docs, failure-mode docs, and a runbook because I wanted the project to demonstrate operational thinking, not just CRUD.

## The 90-second version (technical phone screen)

> RogueNet is a .NET 8 backend for always-online game services. The architecture is a modular monolith — one API process, one worker process, organized into Domain, Application, Infrastructure, and API modules with explicit boundaries. I deliberately avoided a microservice split because the operational cost wasn't justified at this scale, and a clean monolith is a stronger signal than a half-finished distributed system.
>
> The flagship feature is the mission completion endpoint. When a player completes a mission, the API does four things in a single SQL transaction: it records an idempotency key with the response payload, it inserts append-only rows into the inventory transaction ledger, it updates the player profile with optimistic concurrency on a version column, and it inserts an outbox event for downstream consumers. All four either commit together or none of them do.
>
> A background worker reads the outbox table and dispatches events to the leaderboard updater and telemetry. If a downstream consumer fails, the event retries with backoff. If it permanently fails, it surfaces via an admin endpoint. The key property is that downstream failures never affect authoritative player state — the player has their reward; the leaderboard might lag.
>
> I used EF Core for admin and CRUD endpoints and Dapper for the mission completion hot path, because the hot path needed predictable SQL without the change tracker in the way. I documented this decision and the boundary is enforced — Dapper lives in one repository class.
>
> I also wrote a failure-modes doc covering eight ways the mission completion flow can break and how the system responds, plus a runbook for operations.

## The 5-minute version (system design round)

Use this when an interviewer says "tell me about a project you've worked on." Walk through:

1. **The problem domain** — always-online game services, why operational correctness matters more than feature breadth, why duplicate rewards are a customer-trust issue.
2. **The architecture choice** — modular monolith with seams, why not microservices, what would change the answer.
3. **The flagship feature in depth** — walk through what happens on `POST /players/{id}/mission-completions`, end to end. Idempotency check, transaction begin, ledger insert, profile update with version check, outbox insert, transaction commit, response.
4. **The outbox pattern** — why it exists, what it solves (the dual-write problem), how the worker processes it, what happens on failure.
5. **The data access split** — EF Core for productivity, Dapper for the hot path, the discipline of keeping them separate.
6. **What I would do next** — Redis for the leaderboard cache, message bus instead of in-process dispatch, OpenTelemetry, multi-region.

## Likely follow-up questions and how to answer them

### "Why didn't you just use a transaction across the database and the message bus?"

> Because there isn't one. SQL Server and a message bus are separate systems with separate failure modes. A "transaction" across them is what XA tries to provide and it has terrible operational characteristics — coordinator failures, hung transactions, performance penalties. The outbox pattern is the standard alternative: write to one system transactionally, dispatch to the other asynchronously. Eventual consistency where it's acceptable; strong consistency where it isn't.

### "What if the outbox table gets huge?"

> Two answers. First, the worker deletes (or archives) processed events, so the table tracks pending and recently-failed events, not all-time history. Second, if dispatch volume exceeded what the database could comfortably hold, that's the migration trigger to move from SQL outbox to a message bus — Kafka, for example. The outbox pattern is the path *to* a message bus, not a replacement for one forever.

### "How do you handle a slow consumer that's holding up the queue?"

> The worker dispatches events in parallel up to a configured concurrency, with `READPAST` and `UPDLOCK` hints so a stuck event doesn't block others. Per-event timeouts move slow events to a retry status. If a specific event type is consistently slow, the operator can move it to a separate worker pool — the outbox table has a `topic` column so consumers can be partitioned.

### "Why optimistic concurrency instead of locks?"

> Locks held across HTTP request boundaries are a recipe for deadlocks and head-of-line blocking. Optimistic concurrency is wait-free in the common case — most operations don't actually conflict. When they do, the loser retries against the new state. The cost is that clients must handle 409 Conflict, but that's a small cost for the throughput benefit.

### "How would you scale this to a million concurrent players?"

> A few moves, in order: add Redis as a read-through cache for player profiles and leaderboards (the read-write ratio is heavily skewed); shard the player tables by `playerId` hash once a single SQL Server can't handle the write load; move the outbox to Kafka so dispatch is independent of the writing database; split the leaderboard service out so its hot updates don't compete with player profile writes. Each of these changes the architecture; none of them is needed at the scale this project targets.

### "What's the worst bug you could imagine in this system?"

> A subtle one in the idempotency check that compares request body hashes incorrectly — say, including a timestamp field in the hash so retries always look like new requests. The system would silently double-grant rewards under network retries. I'd catch it with an integration test that explicitly retries the same request with a slightly delayed timestamp, but the test has to be deliberate. This is why I have the `FailureModes.md` doc — naming the failure modes makes the tests obvious.

## Things to *not* say

- Don't oversell. This is a portfolio project, not a production system. Saying "I scaled this to millions of users" when you scaled it to a load test is the kind of thing that ends interviews.
- Don't apologize for what you didn't build. The deferrals are documented and intentional.
- Don't pretend the modular monolith is a microservice architecture. The choice is the point.
- Don't read this document. Internalize it. The 30-second version should come out naturally.

## Practice protocol

1. Read each version aloud once.
2. Close the document and say it from memory.
3. Compare to the document. Note where you stumbled.
4. Repeat once a day for a week before any interview.

The goal is not memorization. The goal is fluency — being able to talk about this project without searching for words, so cognitive bandwidth goes to the interviewer's questions instead of recall.
