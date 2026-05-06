# Daily Note — 2026-05-07

## Why this matters today

Close out Week 2 follow-ups before starting Week 3, so the outbox worker lands on a clean foundation rather than on top of accumulating debt.

## Focus blocks (flexible)

- [ ] Deep focus: outbox `BackgroundService` skeleton + dispatch loop
- [ ] Collaboration / async comms: PR #12 review/merge; commit message hygiene on follow-up commits
- [ ] Admin / cleanup: catch-up migration for DbContext drift; remove the warning suppression once resolved

## Tasks

### A. Merge Week 2 (PR #12)

- [ ] Review PR #12 (`feature/mission-rewards` → `main`)
- [ ] Merge to `main` once tests are confirmed green by you locally — the PR description has the full test plan
- [ ] Delete the feature branch after merge

### B. Week 2 follow-ups (sequence them before Week 3 to avoid compounding drift)

1. **DbContext model drift (carryover task #10)**
   - [ ] `dotnet ef migrations add CatchUpDbContextDrift --project src/RogueNet.Infrastructure --startup-project src/RogueNet.Api`
   - [ ] Inspect the generated migration. If the diff is real (e.g., index/constraint changes), commit it. If the diff is just metadata that EF Core 10 surfaces but doesn't translate to SQL, decide whether to absorb or revert the model change.
   - [ ] Remove the `ConfigureWarnings(... PendingModelChangesWarning)` suppression from `IntegrationTestFixture.cs`
   - [ ] Re-run the integration suite — must stay 19/19

2. **OCC retry exhaustion → 503 + Retry-After**
   - [ ] Catch the OCC-exhausted exception at the API endpoint (or let the application service surface a typed `ConcurrencyExhausted` outcome — the closed-hierarchy approach is more consistent with the rest of the design)
   - [ ] Add a `Retry-After: <seconds>` header on the 503 response
   - [ ] Update `FailureModes.md` Section 8 to drop the "currently 500" caveat
   - [ ] Add an integration test that forces OCC contention and asserts 503

3. **`LevelCalculator` (small, gives the API a complete profile model)**
   - [ ] New file `src/RogueNet.Domain/Services/LevelCalculator.cs` with a simple formula (probably `Level = floor(sqrt(xp / 100)) + 1` or `Level = xp / 1000 + 1`)
   - [ ] Wire into `MissionCompletionRepository` to compute the new level from the new total XP and update the profile
   - [ ] Unit tests for the calculator
   - [ ] Update `MissionCompletionResult.NewLevel` to actually reflect the new level (currently echoes the old one)

### C. Week 3 — Outbox, Worker, Observability (start whatever fits in the remaining session)

The high-level deliverables from `docs/ProjectPlan.md`:

- [ ] `BackgroundService` in `RogueNet.Worker` that polls `OutboxEvents WHERE Status = 'Pending'`
- [ ] Dispatch loop with exponential backoff retry, transitioning to `PermanentlyFailed` after M attempts
- [ ] Minimal `LeaderboardScores` consumer for `mission-completion` events
- [ ] Structured logging with correlation IDs through both API request and worker dispatch
- [ ] Integration test: POST mission completion → assert worker picks up the event and writes a `LeaderboardScores` row within N seconds

The flagship interview signal here is **the worker is decoupled from the API write path**: a leaderboard outage cannot roll back authoritative player rewards. Lead with that property in the test design.

## Impact + Fulfillment

- [ ] I felt fulfilled today because:
- [ ] This task helped me grow in:
- [ ] Value created:

## Well-being + Connection

- [ ] **Connection:** Have I talked to someone I love today?
- [ ] **Movement:** Have I done any exercise (even a 15-minute walk)?
- [ ] **Self-Care:** Have I worked on a personal project or hobby?
- [ ] **Psychological Safety:** Do I feel safe to speak with my coworkers and superiors today?
- [ ] **Daily Affirmation:** I build real things that create real value.

## Energy + mental load check

- Energy: Medium
- Mental load: Medium
- Adjustment for today: do follow-ups *before* starting Week 3 even if the worker feels more interesting — drift compounds.

## Notes

- LocalDB is now the local SQL setup. Connection string for tests:
  `Server=(localdb)\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=True;MultipleActiveResultSets=true`
  Set as `ROGUENET_TEST_SQL_CONNECTION` before `dotnet test` (or update the fixture default — small commit if you want it persistent).
- Week 2 acceptance is verified at 56/56 green (37 unit + 19 integration). Don't re-do; just keep it green as Week 3 changes land.
- The closed-sealed outcome/persist-result hierarchies in `RogueNet.Application/MissionCompletions/` are working well. Reuse the same pattern for outbox dispatch results (e.g., `OutboxDispatchResult: Dispatched | Retried | PermanentlyFailed`).

## Blockers / risks

- Drift between DbContext and migration history will accumulate fast if Week 3 model changes land before the catch-up migration.
- Worker integration tests will need a way to time-bound polling (no infinite waits in CI). Plan for a configurable poll interval and a tight test override.

## Next up

If only one thing fits in the next session: do the catch-up migration (task #10). It's small, unblocks confidence in the schema, and removes the test-fixture warning suppression. Everything else can wait.
