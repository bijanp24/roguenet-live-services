# RogueNet Live Services — Claude Code Context

This file is the in-repo brief for Claude Code. The companion `CLAUDE_PROJECT_SETUP.md` is
for the claude.ai web Project; it does not configure this repo.

## What this is

A .NET 10 always-online game-services portfolio project. See `docs/ProjectPlan.md` for the
4-week build schedule and `docs/Architecture.md` for module boundaries and rationale.

## Stack

- C# / .NET 10 (LTS, pinned via `global.json`)
- ASP.NET Core minimal-API Web API
- SQL Server (local: `docker run mcr.microsoft.com/mssql/server:2022-latest`)
- EF Core for admin and CRUD paths; **Dapper isolated** to the mission-completion repository
- Background `IHostedService` worker for outbox processing
- xUnit for unit + integration tests; `WebApplicationFactory` for integration
- Serilog for structured logging

## Module layout

```
src/
  RogueNet.Domain          pure types/rules; no I/O, no framework deps
  RogueNet.Application     use cases, orchestration; depends on Domain
  RogueNet.Infrastructure  EF Core, Dapper, outbox; depends on Domain + Application
  RogueNet.Api             HTTP surface, composition root
  RogueNet.Worker          outbox processor (BackgroundService)
tests/
  RogueNet.UnitTests       Domain + Application
  RogueNet.IntegrationTests  Api + Infrastructure (real SQL Server)
```

Dependency rule: arrows point inward only. `Domain` and `Application` know nothing about EF
Core, ASP.NET Core, or SQL Server.

## Conventions

- File-scoped namespaces (`namespace Foo.Bar;`) — enforced by editorconfig.
- `Nullable` and `ImplicitUsings` enabled globally (see `Directory.Build.props`).
- `TreatWarningsAsErrors=true` and `EnforceCodeStyleInBuild=true` — warnings break the build.
- Unused usings (IDE0005) are errors.
- Central package management via `Directory.Packages.props`. Add versions there, reference in csproj without `Version=`.
- EF Core migrations live in `src/RogueNet.Infrastructure`.
- Dapper is permitted **only** in `MissionCompletionRepository`-style classes on the hot path.

## Common commands

```pwsh
dotnet build                            # build entire solution
dotnet test                             # run unit + integration tests
dotnet test --filter Category=Unit      # unit only (fast)
dotnet format --verify-no-changes       # editorconfig compliance check
dotnet run --project src/RogueNet.Api   # run API
dotnet run --project src/RogueNet.Worker # run outbox worker
```

## Branch and commit conventions

- Feature branches off `main`: `feature/<short-slug>` (e.g. `feature/mission-rewards`).
- Rebase onto `main` before merging; fast-forward only — no squash, no merge commits.
- Per-step commits over big bundles. The history itself is interview signal.
- Imperative, sentence-case subject lines (e.g. `Add idempotency key handling on mission completion`).

## Pointers

- Build schedule: `docs/ProjectPlan.md`
- Architectural rationale: `docs/Architecture.md`
- Failure modes: `docs/FailureModes.md`
- Interview narrative: `docs/InterviewNarrative.md`
