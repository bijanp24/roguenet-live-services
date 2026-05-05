# Failure Modes

This document enumerates the ways RogueNet can fail and how the system responds. Each failure mode follows the same template:

- **What can go wrong**
- **How we detect it**
- **How we mitigate it**
- **How we recover**
- **Player-visible behavior**

The first section covers the mission completion flow in depth because it is the flagship feature. The remaining sections will be filled in during weeks 2-4 as those components are built.

---

## Mission Completion Flow

### 1. Duplicate mission completion request

**What can go wrong:** A client retries a request because of a network blip, an app backgrounding, or aggressive retry logic. Without idempotency, the player would receive rewards twice.

**Detection:** The request includes a client-generated `completionId`. The server records it in the `IdempotencyKeys` table along with a hash of the request body and the response payload.

**Mitigation:** On receipt, the server looks up the `completionId`. If found and the request body hash matches, it returns the stored response without executing the operation. If found but the body hash differs, it returns 409 Conflict (same key, different intent).

**Recovery:** No recovery needed for duplicates — they are handled inline. If a 409 fires from a hash mismatch, the client must inspect its own state and either retry with a fresh `completionId` or treat the prior response as authoritative.

**Player-visible behavior:** The player sees their reward exactly once, regardless of how many times the client retries.

### 2. Database timeout mid-transaction

**What can go wrong:** The reward grant transaction starts but does not commit within the timeout (slow query, lock contention, network issue between API and DB).

**Detection:** The DB driver throws a timeout exception. The API logs the failure with the `correlationId`, `playerId`, and `completionId`.

**Mitigation:** The transaction is rolled back. No partial state is written — `IdempotencyKeys`, `InventoryTransactions`, and `OutboxEvents` are all in a single transaction.

**Recovery:** The client retries with the same `completionId`. The retry either (a) succeeds normally and writes the idempotency record, or (b) hits the same timeout, in which case the client retries again or the system surfaces an error after N attempts.

**Player-visible behavior:** The player's UI shows a transient error and a retry. After retry success, the reward is granted exactly once.

### 3. Partial reward grant (logical, not transactional)

**What can go wrong:** This *cannot* happen in this design because reward grants are a single atomic transaction. This entry exists to document why.

**Detection:** N/A — by design.

**Mitigation:** All ledger inserts and the idempotency record are in the same transaction. If any insert fails, the transaction rolls back and nothing is granted.

**Recovery:** N/A.

**Player-visible behavior:** Either the player receives the full reward or no reward at all. Never partial.

### 4. Outbox event write succeeds, dispatch fails

**What can go wrong:** The reward is granted and the outbox event is persisted, but the worker fails to dispatch the event (consumer down, transient network failure, code bug in the consumer).

**Detection:** The outbox event remains in `Pending` status past its expected dispatch time. A monitoring alert fires when `outbox_events_pending` exceeds a threshold or when an event's age exceeds N minutes.

**Mitigation:** The worker retries with exponential backoff. After M failures, the event moves to `PermanentlyFailed` status and is exposed via `GET /admin/outbox` for manual inspection.

**Recovery:** An operator inspects the failed event, determines whether the consumer is broken or the event payload is bad, fixes the underlying issue, and replays the event via `POST /admin/reconciliation/run` (or the equivalent admin endpoint).

**Player-visible behavior:** The player has their reward (the authoritative state is correct). Downstream consequences — leaderboard position, achievement unlocks, social notifications — are delayed but eventually consistent. This is the explicit tradeoff of the outbox pattern: authoritative state is strongly consistent; downstream state is eventually consistent.

### 5. Leaderboard update failure

**What can go wrong:** The leaderboard update consumer fails to write the new score (DB issue, schema drift, bug).

**Detection:** Same as above — outbox event stuck in `Pending` or moves to `PermanentlyFailed`.

**Mitigation:** Same retry-and-surface mechanism. Crucially: leaderboard failure does **not** roll back the reward grant. The two are decoupled by the outbox.

**Recovery:** Operator investigates, fixes, replays.

**Player-visible behavior:** The player's reward is correct. Their leaderboard position may lag by minutes. The system never shows the player a duplicated or rolled-back reward because of leaderboard issues.

### 6. Idempotency key collision (different player, same key)

**What can go wrong:** Two different clients generate the same GUID for `completionId`. With UUIDv4, the probability is negligible, but the system should not be sensitive to client behavior.

**Detection:** The `IdempotencyKeys` table is keyed by `(playerId, completionId)`, not by `completionId` alone. Collisions across players cannot occur.

**Mitigation:** Composite key in the schema.

**Recovery:** N/A.

**Player-visible behavior:** None. The two players' requests are independent.

### 7. Idempotency key reuse with a different request body

**What can go wrong:** A client reuses a `completionId` for a different mission, score, or difficulty. This is a client bug, not a normal flow.

**Detection:** The server compares the request body hash against the stored hash in `IdempotencyKeys`. A mismatch is detected.

**Mitigation:** Return 409 Conflict with a Problem Details payload identifying the mismatch.

**Recovery:** The client must use a fresh `completionId` for a different operation. The original record stays intact.

**Player-visible behavior:** An error from the client's perspective, surfaced as a UI message. No corruption of player state.

### 8. Optimistic concurrency conflict on player profile

**What can go wrong:** Two operations attempt to update the same player profile concurrently. For example, a mission completion and a separate XP grant from another source.

**Detection:** The `Version` column on `PlayerProfiles` does not match the expected version. The UPDATE affects zero rows.

**Mitigation:** The transaction is rolled back. The API returns 409 Conflict.

**Recovery:** The client (or the server, if this is a server-internal retry) re-reads the profile, recomputes the operation against the new state, and retries with the new version.

**Player-visible behavior:** A transient UI hiccup at most. Under normal load this is rare; under contention (a player tapping a button rapidly), the second tap returns a no-op via idempotency before reaching the optimistic concurrency check.

---

## Other Failure Modes (to be filled in)

The following sections will be expanded in weeks 2-4 as the corresponding components are built. They are listed here to make the scope of operational thinking explicit.

### Bad database migration

To be filled in.

### Hot leaderboard

To be filled in.

### Slow query under load

To be filled in.

### Cache inconsistency (once Redis is added)

To be filled in.

### Worker crash mid-batch

To be filled in.

### Partial deployment (API new, worker old, or vice versa)

To be filled in.

### Fraudulent client request

To be filled in.

---

## Cross-cutting Principles

A few principles are worth stating once, rather than repeating in every failure mode:

1. **Authoritative state is strongly consistent.** Player profiles, inventory, mission completions — these are correct or they are nothing. They use SQL transactions and idempotency.
2. **Downstream state is eventually consistent.** Leaderboards, telemetry, social notifications. These use the outbox and may lag.
3. **Failure surfaces through the admin endpoint.** Operators have a single place to look (`GET /admin/outbox`) for stuck events.
4. **Retries are safe.** Every player-facing operation is idempotent. Clients can retry without fear of duplication.
5. **Logs include the correlation ID.** Every request and every worker dispatch can be traced end to end.
