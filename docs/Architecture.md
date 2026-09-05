# Architecture

Four .NET 10 projects with directed dependencies; **never reverse either arrow**:

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
    `AnywhereDbContext` directly — **no `IProfileRepository`-style interfaces**:
    per the
    [Framework Design Guidelines on abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces),
    an abstraction earns its place only once proven by multiple concrete
    implementations or a real substitution need, and this project has one data
    store and tests that exercise real backends rather than mocks.
  - **UI layer** (`MainForm` + `Anywhere.Controls` widgets) — chat transcript,
    input box, a permission/diff panel docked above the input, agent/session
    picker.

Data model (SQLite, under local — never roaming — `%APPDATA%\Anywhere\`):
`AgentProfile`, `Session`, `Message` entities on one `AnywhereDbContext`. Schema
is managed by **EF Core Migrations** generated into `Anywhere.Models` (not
`PRAGMA user_version`/hand-rolled SQL). Production startup calls
`Database.Migrate()`; `Database.EnsureCreated()` is for test fixtures only,
never production.
