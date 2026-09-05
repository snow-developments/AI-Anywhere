# AGENTS.md

Agent-agnostic guidance for working in this repository. `CLAUDE.md` imports this file.

## Project Status

Pre-implementation. No source code exists yet — only spec, plan, and TODO. The plan below (`docs/superpowers/plans/2026-09-04-acp-winforms-client.md`) has not been executed; when it has been, update this file's "Commands" and "Architecture" sections to match what was actually built, since the plan's task steps are aspirational until then.

## What this is thing?

**Anywhere** — a native WinForms desktop client for the [Agent Client Protocol](https://agentclientprotocol.com/get-started/introduction) (ACP), the JSON-RPC protocol Zed defined for editor↔agent communication. Motivation: Claude's official desktop app is Electron-based and hard to extend; this client is native, hackable, and scoped to daily power-user needs. It is agent-agnostic — it launches and speaks ACP to any configured agent subprocess, not just Claude Code.

Full spec: `docs/superpowers/specs/2026-09-04-design.md`

Implementation plan (split into four phased plans, each independently executable/testable — see `docs/superpowers/plans/2026-09-04-acp-winforms-client.md` for the superseded single-file original):
1. `docs/superpowers/plans/2026-09-05-anywhere-phase1-foundation.md` — project scaffolding + EF Core persistence
2. `docs/superpowers/plans/2026-09-05-anywhere-phase2-protocol-and-controls.md` — MarkdownLabel control + ACP agent process wrapper
3. `docs/superpowers/plans/2026-09-05-anywhere-phase3-core-ui.md` — chat transcript UI + permission/diff panel
4. `docs/superpowers/plans/2026-09-05-anywhere-phase4-polish.md` — crash recovery, profile management UI, visual styling (WinForms.Fluent.UI, not MaterialSkin.2)

## Commands

```bash
dotnet tool restore                                                           # required before anything else — installs dotnet-ef, cslint
dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj                          # run all tests
dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter <TestClassName> # run one test class
dotnet run --project src/Anywhere/Anywhere.csproj                             # run the app
dotnet build Anywhere.sln                                                     # build everything
dotnet ef migrations add <Name> --project src/Anywhere.Models --startup-project src/Anywhere  # new migration
dotnet ef database update --project src/Anywhere.Models --startup-project src/Anywhere        # apply migrations locally
dotnet format Anywhere.slnx --verify-no-changes                              # style/.editorconfig check
dotnet cslint                                                                 # lint (required tool, see .config/dotnet-tools.json)
```

`dotnet-ef` and `cslint` are local tools pinned in `.config/dotnet-tools.json` (the standard tool-manifest path created by `dotnet new tool-manifest`) — run `dotnet tool restore` from the repo root before using either of them.

### Testing Strategy

Use xUnit for persistence (EF Core CRUD against a real, temp-file SQLite database — `Database.EnsureCreated()` in test fixtures, never `Database.Migrate()` there) and agent-profile parsing. The ACP protocol layer (`AgentProcess`) is integration-tested against a real fake ACP agent subprocess (`src/Anywhere.Tests/FakeAgent/fake_agent.py`) speaking real stdio JSON-RPC — not by mocking `acp-csharp` internals. UI is manual smoke-test only in v1.

## Architecture

Four .NET 10 projects. Dependency direction — **never reverse either arrow**:

`Anywhere` → `Anywhere.Controls` → `Anywhere.Design`
`Anywhere` → `Anywhere.Models` (parallel branch, sibling to `Anywhere.Design`)

- **`Anywhere.Design`** (`net10.0`, no UI deps) — design-token constants only (`Colors`, `Spacing`, `Typography`). Framework-agnostic so a future macOS client (see `TODO.md`) can reuse it. Must never reference WinForms or `Anywhere.Controls`.
- **`Anywhere.Models`** (`net10.0`, no UI deps) — EF Core data layer: entities (`AgentProfile`, `Session`, `Message`), `AnywhereDbContext` (SQLite via `Microsoft.EntityFrameworkCore.Sqlite`), a design-time `IDesignTimeDbContextFactory`, and the generated `Migrations/` folder (the schema's source of truth). Also framework-agnostic for future macOS reuse; never referenced by `Anywhere.Controls` or `Anywhere.Design`.
- **`Anywhere.Controls`** (`net10.0-windows`) — WinForms control library. References `Anywhere.Design` + `WinForms.Fluent.UI`. Houses `MarkdownLabel`, `ChatTranscriptPanel`, `PermissionDiffPanel`, `DebugLogPanel`. Also houses `PermissionRequest`/`PermissionOutcome` records even though `AgentProcess` (in `Anywhere`) produces them — placed here specifically so `PermissionDiffPanel` can consume them without creating a circular reference.
- **`Anywhere`** (`net10.0-windows`, the app) — three internal layers:
  - **Protocol layer** (`Agents/AgentProcess.cs`) — wraps `acp-csharp`'s `ClientSideConnection`, one agent subprocess per session over stdio JSON-RPC. Knows nothing agent-specific.
  - **Agent registry** (`Persistence/ProfileRepository.cs`, etc.) — user-editable list of agent profiles (name, command, args, env, working dir), persisted via EF Core against SQLite. Concrete repository classes wrap `AnywhereDbContext` directly — **no `IProfileRepository`-style interfaces**: per the [Framework Design Guidelines on abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces), an abstraction earns its place only once proven by multiple concrete implementations or a real substitution need, and this project has one data store and tests that exercise real backends rather than mocks.
  - **UI layer** (`MainForm` + `Anywhere.Controls` widgets) — chat transcript, input box, a permission/diff panel docked above the input, agent/session picker.

Data model (SQLite, under local — never roaming — `%APPDATA%\Anywhere\`): `AgentProfile`, `Session`, `Message` entities on one `AnywhereDbContext`. Schema is managed by **EF Core Migrations** generated into `Anywhere.Models` (not `PRAGMA user_version`/hand-rolled SQL). Production startup calls `Database.Migrate()`; `Database.EnsureCreated()` is for test fixtures only, never production.

## Constraints (from the spec/plan — do not violate silently)

- Dependency direction `Anywhere` → `Anywhere.Controls` → `Anywhere.Design`, and `Anywhere` → `Anywhere.Models`, is one-way. `Anywhere.Design` and `Anywhere.Models` must stay UI-framework-agnostic, and neither is ever referenced by `Anywhere.Controls`.
- ACP transport goes through the `acp-csharp` NuGet package (nuskey8/acp-csharp) — do not hand-roll JSON-RPC framing. Contribute fixes upstream rather than forking.
- Markdown rendering is `MarkdownLabel` (Markdig + Vortice/Direct2D+DirectWrite), adapted from an external source file, not a WebView2-based renderer.
- Persistence goes through EF Core (`Microsoft.EntityFrameworkCore.Sqlite`) against `AnywhereDbContext` in `Anywhere.Models` — not raw `Microsoft.Data.Sqlite`/ADO.NET. Schema changes go through `dotnet ef migrations add` (never hand-edited `CREATE TABLE`), applied via `Database.Migrate()` at startup.
- No repository interfaces (`IProfileRepository`, etc.) — concrete classes over `AnywhereDbContext` only. See the "Design decision" note below before adding one.
- Visual styling: `WinForms.Fluent.UI` (an "add new controls" library referenced only by `Anywhere.Controls`), not a full app-wide skin override. The superseded single-file plan's Task 10 had drifted to `MaterialSkin.2` instead, contradicting the spec — this was fixed when the plan was split into phases; Phase 4's Task 10 uses `WinForms.Fluent.UI`.
- No permission-request timeouts — the permission/diff panel waits indefinitely, matching editor behavior (ACP itself has no timeout).
- v1 scope excludes: agent-specific presets, multiple concurrent session tabs, slash-command UI, MCP server config UI, attachments, plan-mode UI, WinForms UI automation. See the spec's "Out of scope" section before adding any of these.

## Design Decisions

### No repository interfaces

Data access uses concrete `ProfileRepository`/`SessionRepository`/`MessageRepository` classes wrapping `AnywhereDbContext` directly — no `IProfileRepository` abstraction layer. This follows the [.NET Framework Design Guidelines on abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces): "DO NOT provide abstractions unless they are tested by developing several concrete implementations." There is exactly one concrete data store (SQLite via EF Core) with no second implementation planned, and the project already tests the ACP layer against a real fake agent rather than mocks — the same "test the real thing" philosophy applies to persistence. `DbContext`/`DbSet<T>` are themselves EF Core's abstraction; don't wrap them a second time. Revisit this only if a genuine second backing store or a concrete testability need for a fake actually materializes.

## GitHub Queries

Always use `gh api` to query GitHub (issues, PRs, file contents, etc.) rather than guessing at URLs or answering from memory — GitHub's data changes and only a live query is authoritative.

## Other Resources

- `.agents/skills/uspto-wordmark-search/` — a project-local skill for trademark searches; see `TODO.md` for the pending "Anywhere" wordmark check that gates finalizing the project name.
- `.mcp.json` configures the `context7` MCP server for library-docs lookups.
