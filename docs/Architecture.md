# Architecture

Four .NET 10 projects with directed dependencies; **never reverse either
arrow**:

`Anywhere` → `Anywhere.Controls` → `Anywhere.Design` `Anywhere` →
`Anywhere.Models` (parallel branch, sibling to `Anywhere.Design`)

- **`Anywhere.Design`** (`net10.0`, no UI deps) — design-token constants only
  (`Colors`, `Spacing`, `Typography`). Framework-agnostic so a future macOS
  client (see `TODO.md`) can reuse it. Must never reference WinForms or
  `Anywhere.Controls`.
- **`Anywhere.Models`** (`net10.0`, no UI deps) — EF Core data layer: entities
  (`AgentProfile`, `Session`, `Message`), `AnywhereDbContext` (SQLite via
  `Microsoft.EntityFrameworkCore.Sqlite`), a design-time
  `IDesignTimeDbContextFactory`, and the generated `Migrations/` folder (the
  schema's source of truth). Also framework-agnostic for future macOS reuse;
  never referenced by `Anywhere.Controls` or `Anywhere.Design`.
- **`Anywhere.Controls`** (`net10.0-windows`) — WinForms control library.
  References `Anywhere.Design` + `WinForms.Fluent.UI`. Houses `MarkdownLabel`,
  `ChatTranscriptPanel`, `PermissionDiffPanel`, `DebugLogPanel`. Also houses
  `PermissionRequest`/`PermissionOutcome` records even though `AgentProcess` (in
  `Anywhere`) produces them — placed here specifically so `PermissionDiffPanel`
  can consume them without creating a circular reference.
- **`Anywhere`** (`net10.0-windows`, the app) — three internal layers:
  - **Protocol layer** (`Agents/AgentProcess.cs`) — wraps `acp-csharp`'s
    `ClientSideConnection`, one agent subprocess per session over stdio
    JSON-RPC. Knows nothing agent-specific.
  - **Agent registry** (`Persistence/ProfileRepository.cs`, etc.) —
    user-editable list of agent profiles (name, command, args, env, working
    dir), persisted via EF Core against SQLite. Concrete repository classes wrap
    `AnywhereDbContext` directly — **no `IProfileRepository`-style interfaces**.
    See [Design Decisions — No repository interfaces](#no-repository-interfaces)
    for the rationale and links to the spec, the agent guidance file, and the
    plans that depend on it.
  - **UI layer** (`MainForm` + `Anywhere.Controls` widgets) — chat transcript,
    input box, a permission/diff panel docked above the input, agent/session
    picker.

Data model (SQLite, under local — never roaming — `%APPDATA%\Anywhere\`):
`AgentProfile`, `Session`, `Message` entities on one `AnywhereDbContext`. Schema
is managed by **EF Core Migrations** generated into `Anywhere.Models` (not
`PRAGMA user_version`/hand-rolled SQL). Production startup calls
`Database.Migrate()`; `Database.EnsureCreated()` is for test fixtures only,
never production.

## Design Decisions

### No repository interfaces

Data access uses concrete `ProfileRepository`/`SessionRepository`/
`MessageRepository` classes wrapping `AnywhereDbContext` directly — no
`IProfileRepository`-style abstraction layer. This follows the
[.NET Framework Design Guidelines on abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces):
"DO NOT provide abstractions unless they are tested by developing several
concrete implementations." There is exactly one concrete data store (SQLite via
EF Core) with no second implementation planned, and the project already tests
the ACP layer against a real fake agent rather than mocks — the same "test the
real thing" philosophy applies to persistence. `DbContext`/`DbSet<T>` are
themselves EF Core's abstraction; don't wrap them a second time. Revisit this
only if a genuine second backing store or a concrete testability need for a fake
actually materializes.

**Cross-references:**

- The constraint as a one-line agent rule: `AGENTS.md` → Constraints → "No
  repository interfaces"
- The spec, in the "Architecture" → "Agent registry" bullet:
  `docs/superpowers/specs/Design.md`
- The project-local guidance file that grounds the rule in the Framework Design
  Guidelines: `.agents/guidance/Abstractions.md`
- The implementation rationale in the original single-file plan and the
  Foundation plan: `docs/superpowers/plans/2026-09-04-acp-winforms-client.md`
  and `docs/superpowers/plans/archive/2026-09-05-phase-1-foundation.md`
