# ACP WinForms Client — Phase 1: Foundation (Scaffolding + EF Core Persistence) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the four-project solution skeleton and the EF Core/SQLite data layer (agent profiles, sessions, messages) that every later phase builds on.

**Architecture:** Four .NET projects — `Anywhere.Design` (framework-agnostic design tokens, no UI deps, reusable by a future macOS client), `Anywhere.Models` (framework-agnostic EF Core data layer, no UI deps, sibling to `Anywhere.Design`: entities + `AnywhereDbContext` + migrations), `Anywhere.Controls` (WinForms control library depending on Design + WinForms.Fluent.UI), and `Anywhere` (the app, referencing Controls + Models). This phase creates all four projects but only fills in `Anywhere.Models` and the app's `Persistence/` folder — `Anywhere.Controls` and `Anywhere`'s protocol/UI layers stay empty shells until Phases 2-4.

**Tech Stack:** .NET 10, WinForms (shell only in this phase), EF Core (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `dotnet-ef`), xUnit.

**Spec:** [docs/superpowers/specs/2026-09-04-design.md](../specs/2026-09-04-design.md)

**Plan series:** This is Phase 1 of 4. See also:
- Phase 2 — [2026-09-05-anywhere-phase2-protocol-and-controls.md](2026-09-05-anywhere-phase2-protocol-and-controls.md) (MarkdownLabel control, ACP agent process wrapper)
- Phase 3 — [2026-09-05-anywhere-phase3-core-ui.md](2026-09-05-anywhere-phase3-core-ui.md) (chat transcript UI, permission/diff panel)
- Phase 4 — [2026-09-05-anywhere-phase4-polish.md](2026-09-05-anywhere-phase4-polish.md) (crash recovery, profile management UI, visual styling)

## Global Constraints

These apply to the whole `Anywhere` project, not just this phase:

- Target framework: `net10.0-windows` for WinForms projects (`Anywhere`, `Anywhere.Controls`); plain `net10.0` for `Anywhere.Design` and `Anywhere.Models` (both must stay UI-framework-agnostic — no WinForms/Fluent reference — for future reuse by a macOS client, see TODO.md).
- Project dependency direction: `Anywhere` → `Anywhere.Controls` → `Anywhere.Design`, and `Anywhere` → `Anywhere.Models` as a second, parallel branch. Never reverse either arrow: `Anywhere.Design` and `Anywhere.Models` must not reference `Anywhere.Controls` or `Anywhere`, and `Anywhere.Controls`/`Anywhere.Design` must not reference `Anywhere.Models` (persistence is an app-layer concern, not a UI concern).
- ACP transport dependency: `acp-csharp` NuGet package — do not hand-roll JSON-RPC framing. (Not used in this phase; applies starting Phase 2.)
- Markdown rendering: adapt `MarkdownLabel.cs` from `family-lock-out`, living in `Anywhere.Controls` — do not add a WebView2-based renderer. (Not used in this phase; applies starting Phase 2.)
- Persistence: EF Core (`Microsoft.EntityFrameworkCore.Sqlite`) against a SQLite file under local (non-roaming) `%APPDATA%\Anywhere\` — never `%APPDATA%\Roaming`. Entities and `AnywhereDbContext` live in `Anywhere.Models`; `Anywhere` consumes the context through thin repository classes (see next bullet).
- No repository interfaces (`IProfileRepository`, `ISessionRepository`, etc.). Per the [Framework Design Guidelines on abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces) — "DO NOT provide abstractions unless they are tested by developing several concrete implementations" — there is exactly one concrete data store (SQLite via EF Core) and no plan for a second, and the project's existing test philosophy already favors exercising real backends over mocks (see Testing Strategy). `ProfileRepository`/`SessionRepository`/`MessageRepository` stay concrete classes wrapping `AnywhereDbContext`; `DbContext`/`DbSet<T>` are themselves EF Core's abstraction layer and are not wrapped a second time.
- Visual styling: `WinForms.Fluent.UI` NuGet package (referenced only by `Anywhere.Controls`) supplies Fluent/WinUI3-styled controls — it is an "add new controls" library, not a full app-wide visual-style override, so default WinForms controls not replaced by a Fluent equivalent keep the default Win32 look. Do not add `MaterialSkin.2` or another full-override library alongside it. (Applied starting Phase 4.)
- Schema is managed by **EF Core Migrations**, generated into `Anywhere.Models` (a dedicated project with its own design-time factory, per EF Core's guidance to keep migrations in a project separate from the WinForms startup project). Not `PRAGMA user_version` + hand-rolled `CREATE TABLE IF NOT EXISTS`, and not runtime `Database.EnsureCreated()` outside of tests (`EnsureCreated()` is fine for test fixtures that need a fast, disposable schema, but production startup always calls `Database.Migrate()` so the migration history stays authoritative).
- No permission-request timeouts — panel waits indefinitely. (Applies starting Phase 3.)
- v1 has no multi-tab sessions, slash commands, MCP config UI, attachments, or agent-specific presets (those are v1.1+).

---

## File Structure

This is the canonical full-solution file map; later phase plans reference back to this section rather than repeating it. Files this phase creates are unmarked; files later phases will add are marked `(Phase N)`.

```
Anywhere.sln
src/
  Anywhere.Design/
    Anywhere.Design.csproj      (net10.0, no UI deps)
    Colors.cs                   (design-token constants)
    Spacing.cs
    Typography.cs
  Anywhere.Models/
    Anywhere.Models.csproj       (net10.0, no UI deps; refs Microsoft.EntityFrameworkCore.Sqlite + .Design)
    AgentProfile.cs               (EF entity: Id, Name, Command, Args, Env, WorkingDir, CreatedAt)
    Session.cs                    (EF entity: Id, ProfileId, WorkingDir, CreatedAt)
    Message.cs                    (EF entity: Id, SessionId, Role, Content, ToolCallJson, CreatedAt)
    AnywhereDbContext.cs          (DbContext: DbSets + JSON value-converter config for Args/Env)
    AnywhereDbContextFactory.cs   (IDesignTimeDbContextFactory<AnywhereDbContext>, for `dotnet ef` tooling)
    Migrations/                   (generated by `dotnet ef migrations add`)
  Anywhere.Controls/
    Anywhere.Controls.csproj    (net10.0-windows; refs Anywhere.Design + WinForms.Fluent.UI)
    MarkdownLabel.cs           (Phase 2 — adapted from family-lock-out)
    ChatTranscriptPanel.cs     (Phase 3 — scrollable stack of message bubbles)
    PermissionRequest.cs       (Phase 2)
    PermissionOutcome.cs       (Phase 2)
    PermissionDiffPanel.cs     (Phase 3 — docked bottom panel, TableLayoutPanel-based)
    DebugLogPanel.cs           (Phase 4)
  Anywhere/
    Anywhere.csproj            (net10.0-windows; refs Anywhere.Controls + Anywhere.Models)
    Program.cs
    MainForm.cs
    MainForm.Designer.cs
    AgentProfileForm.cs         (Phase 4)
    AgentProfileForm.Designer.cs (Phase 4)
    Agents/
      AgentProcess.cs            (Phase 2 — wraps Process + acp-csharp ClientSideConnection over its stdio)
      PromptResult.cs            (Phase 2)
    Persistence/
      ProfileRepository.cs       (wraps AnywhereDbContext; concrete class, no interface — see Global Constraints)
      SessionRepository.cs
      MessageRepository.cs
  Anywhere.Tests/
    Anywhere.Tests.csproj
    ProfileRepositoryTests.cs
    MessageRepositoryTests.cs
    AgentProfileParsingTests.cs (Phase 4 — tests AgentProfileParser, added alongside the profile management UI)
    MarkdownLabelTests.cs        (Phase 2)
    FakeAgent/
      fake_agent.py              (Phase 2 — minimal ACP-speaking stdio script used by integration test)
    AgentProcessIntegrationTests.cs (Phase 2)
```

**Interfaces summary (for cross-task reference, this phase only — later phases add their own):**
- `AgentProfile` (`Anywhere.Models`, EF entity class): `int Id, string Name, string Command, string[] Args, Dictionary<string,string> Env, string WorkingDir`
- `AnywhereDbContext`: `DbSet<AgentProfile> Profiles`, `DbSet<Session> Sessions`, `DbSet<Message> Messages`; constructed with a SQLite file path, configures itself via `OnConfiguring`/`UseSqlite`.
- `ProfileRepository(AnywhereDbContext db)`: `Task<int> InsertAsync(AgentProfile p)`, `Task<AgentProfile?> GetAsync(int id)`, `Task<List<AgentProfile>> ListAllAsync()`
- `SessionRepository(AnywhereDbContext db)`: `Task<int> InsertAsync(int profileId, string workingDir)`, `Task<List<Session>> ListAllAsync()`
- `MessageRepository(AnywhereDbContext db)`: `Task InsertAsync(int sessionId, string role, string content, string? toolCallJson)`, `Task<List<Message>> ListForSessionAsync(int sessionId)`
- `Anywhere.Design` (Task 1b): static classes `Colors` (named `Color` constants), `Spacing` (named `int` pixel constants), `Typography` (named `Font`-factory methods) — consumed starting Phase 3's `ChatTranscriptPanel`/`PermissionDiffPanel` (Tasks 6-7) and Phase 4's `DebugLogPanel`/Fluent theming step (Tasks 8, 10). `Anywhere.Models` (Phase 1, Tasks 2-3) has no dependency on it and never will (Global Constraints).
- `ProfileRepository` gains `UpdateAsync(AgentProfile p) : Task` and `DeleteAsync(int id) : Task` in Phase 4, Task 9 — the base `InsertAsync`/`GetAsync`/`ListAllAsync` here are what Task 2 itself produces.

---

## Task 1: Project scaffolding and solution setup

**Files:**
- Create: `Anywhere.sln`
- Create: `src/Anywhere.Design/Anywhere.Design.csproj`
- Create: `src/Anywhere.Models/Anywhere.Models.csproj`
- Create: `src/Anywhere.Controls/Anywhere.Controls.csproj`
- Generated by template: `src/Anywhere/Anywhere.csproj`
- Generated by template: `src/Anywhere/Program.cs`
- Generated by template: `src/Anywhere/MainForm.cs`
- Generated by template: `src/Anywhere/MainForm.Designer.cs`
- Create: `src/Anywhere.Tests/Anywhere.Tests.csproj`
- Test: `src/Anywhere.Tests/SmokeTest.cs`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: a buildable, runnable empty WinForms shell (`MainForm` with a title bar), empty `Anywhere.Design`, `Anywhere.Models`, and `Anywhere.Controls` class libraries wired into the solution and referenced correctly (`Anywhere` → `Anywhere.Controls` → `Anywhere.Design`, and `Anywhere` → `Anywhere.Models`), and a runnable empty test project — every later task builds on top of this.

- [ ] **Step 1: Create the WinForms project from the .NET SDK's included `winforms` template**

Do not hand-write `Program.cs`/`MainForm.cs`/`MainForm.Designer.cs` — let the template generate them, then edit the generated files in later steps.

```bash
dotnet new winforms -n Anywhere -o src/Anywhere --framework net10.0-windows
```

- [ ] **Step 2: Create the Anywhere.Design class library (framework-agnostic, plain net10.0)**

```bash
dotnet new classlib -n Anywhere.Design -o src/Anywhere.Design --framework net10.0
```

- [ ] **Step 3: Create the Anywhere.Controls class library (WinForms-targeted)**

```bash
dotnet new classlib -n Anywhere.Controls -o src/Anywhere.Controls --framework net10.0-windows
```

Edit the generated `src/Anywhere.Controls/Anywhere.Controls.csproj` to add `<UseWindowsForms>true</UseWindowsForms>` inside the existing `<PropertyGroup>` (the plain `classlib` template doesn't enable WinForms by default the way the `winforms` app template does).

- [ ] **Step 3b: Create the Anywhere.Models class library (framework-agnostic, plain net10.0)**

```bash
dotnet new classlib -n Anywhere.Models -o src/Anywhere.Models --framework net10.0
```

`Anywhere.Models` never references WinForms, `Anywhere.Controls`, or `Anywhere.Design` — it holds EF Core entities and the `DbContext` only (see Task 2). It's a sibling to `Anywhere.Design` in the dependency graph, not a child of it.

- [ ] **Step 4: Create the test project**

```bash
dotnet new xunit -n Anywhere.Tests -o src/Anywhere.Tests --framework net10.0
```

- [ ] **Step 5: Create the solution and add all five projects**

```bash
dotnet new sln -n Anywhere
dotnet sln add src/Anywhere.Design/Anywhere.Design.csproj
dotnet sln add src/Anywhere.Models/Anywhere.Models.csproj
dotnet sln add src/Anywhere.Controls/Anywhere.Controls.csproj
dotnet sln add src/Anywhere/Anywhere.csproj
dotnet sln add src/Anywhere.Tests/Anywhere.Tests.csproj
```

- [ ] **Step 6: Wire up project references (Anywhere → Anywhere.Controls → Anywhere.Design, and Anywhere → Anywhere.Models)**

```bash
dotnet add src/Anywhere.Controls/Anywhere.Controls.csproj reference src/Anywhere.Design/Anywhere.Design.csproj
dotnet add src/Anywhere/Anywhere.csproj reference src/Anywhere.Controls/Anywhere.Controls.csproj
dotnet add src/Anywhere/Anywhere.csproj reference src/Anywhere.Models/Anywhere.Models.csproj
dotnet add src/Anywhere.Tests/Anywhere.Tests.csproj reference src/Anywhere/Anywhere.csproj
dotnet add src/Anywhere.Tests/Anywhere.Tests.csproj reference src/Anywhere.Controls/Anywhere.Controls.csproj
dotnet add src/Anywhere.Tests/Anywhere.Tests.csproj reference src/Anywhere.Models/Anywhere.Models.csproj
```

- [ ] **Step 6b: Initialize the local dotnet-ef tool manifest**

```bash
dotnet new tool-manifest
dotnet tool install dotnet-ef
```

This pins `dotnet-ef` as a per-repo local tool (invoked as `dotnet ef ...`) rather than requiring a global install — needed by Task 2's migrations.

- [ ] **Step 7: Write a trivial smoke test**

```csharp
// src/Anywhere.Tests/SmokeTest.cs
using Xunit;

public class SmokeTest
{
    [Fact]
    public void Solution_builds_and_runs_tests()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 8: Run the test to verify the project graph builds**

Run: `dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj`
Expected: PASS (1 test).

- [ ] **Step 9: Set MainForm's window title**

Edit `src/Anywhere/MainForm.Designer.cs`, set `this.Text = "ACP Client";` in `InitializeComponent()`.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "chore: scaffold WinForms app, EF Core models project, and test project"
```

---

## Task 1b: `Anywhere.Design` tokens

**Files:**
- Create: `src/Anywhere.Design/Colors.cs`
- Create: `src/Anywhere.Design/Spacing.cs`
- Create: `src/Anywhere.Design/Typography.cs`

**Interfaces:**
- Consumes: `Anywhere.Design` project scaffolded in Task 1.
- Produces: `Colors`, `Spacing`, `Typography` static classes with real values — consumed starting Phase 3 by `ChatTranscriptPanel`/`PermissionDiffPanel` (Tasks 6-7) and Phase 4 by `DebugLogPanel` (Task 8) and the `WinForms.Fluent.UI` theming step (Task 10).

**2026-09-06 fix (review finding #4):** the original draft of this phase created the `Anywhere.Design` project (Task 1) but never filled it in or wired it to any control, despite the plan's own claim that it would be "consumed by every widget." This task exists specifically to make that claim true — every later task that adds a WinForms control is expected to use these tokens instead of literal margin/padding/font values, and Tasks 6-8 and Phase 4's Task 10 have been amended accordingly.

- [ ] **Step 1: Implement `Spacing`**

```csharp
// src/Anywhere.Design/Spacing.cs
namespace Anywhere.Design;

public static class Spacing
{
    public const int Tiny = 4;
    public const int Small = 8;
    public const int Medium = 16;
    public const int Large = 24;
}
```

- [ ] **Step 2: Implement `Typography`**

```csharp
// src/Anywhere.Design/Typography.cs
using System.Drawing;

namespace Anywhere.Design;

public static class Typography
{
    public static Font Body() => new("Segoe UI", 9f);
    public static Font Monospace() => new("Cascadia Mono", 9f);
}
```

(Factory methods, not static `Font` fields — `Font` is `IDisposable`, so a shared static instance would risk being disposed by one consumer out from under another; each caller gets its own instance.)

- [ ] **Step 3: Implement `Colors`**

```csharp
// src/Anywhere.Design/Colors.cs
using System.Drawing;

namespace Anywhere.Design;

public static class Colors
{
    public static readonly Color Accent = Color.FromArgb(0x00, 0x78, 0xD4); // Windows 11 accent blue
    public static readonly Color Background = Color.FromArgb(0xF3, 0xF3, 0xF3);
    public static readonly Color Danger = Color.FromArgb(0xC4, 0x2B, 0x1C);
}
```

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add Anywhere.Design color, spacing, and typography tokens"
```

---

## Task 2: EF Core data layer (`Anywhere.Models`) and agent-profile persistence

**Files:**
- Create: `src/Anywhere.Models/AgentProfile.cs`
- Create: `src/Anywhere.Models/AnywhereDbContext.cs`
- Create: `src/Anywhere.Models/AnywhereDbContextFactory.cs`
- Create: `src/Anywhere.Models/Migrations/*` (generated)
- Create: `src/Anywhere/Persistence/ProfileRepository.cs`
- Test: `src/Anywhere.Tests/ProfileRepositoryTests.cs`

**Interfaces:**
- Consumes: `Anywhere.Models` project scaffolded in Task 1.
- Produces: `AgentProfile` entity, `AnywhereDbContext` (`DbSet<AgentProfile> Profiles`), `ProfileRepository` with `InsertAsync`/`GetAsync`/`ListAllAsync` — consumed by Task 3 (Session/Message entities share this context) and by Phase 2/Phase 4 (agent process, UI agent picker).

**Design note on abstraction:** No `IProfileRepository` interface is created here. Per the [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces), an abstraction should exist only once it's proven out by more than one concrete implementation or a genuine substitution need — neither applies here (one data store, and tests exercise the real SQLite provider rather than a fake). `ProfileRepository` is a concrete class over the already-abstract `DbContext`/`DbSet<T>` EF Core gives us.

- [ ] **Step 1: Add EF Core packages to `Anywhere.Models`**

```bash
dotnet add src/Anywhere.Models/Anywhere.Models.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/Anywhere.Models/Anywhere.Models.csproj package Microsoft.EntityFrameworkCore.Design
```

`Microsoft.EntityFrameworkCore.Design` supplies the design-time services `dotnet ef` needs to scaffold/apply migrations; it only needs to live in the project that owns the `DbContext` (`Anywhere.Models`), not in `Anywhere`.

- [ ] **Step 2: Write the failing test for profile insert/get against a real SQLite file**

```csharp
// src/Anywhere.Tests/ProfileRepositoryTests.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Anywhere.Models;
using Anywhere.Persistence;
using Xunit;

public class ProfileRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AnywhereDbContext _db;

    public ProfileRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"acp_test_{Guid.NewGuid():N}.db");
        _db = new AnywhereDbContext(_dbPath);
        _db.Database.EnsureCreated(); // test fixtures use EnsureCreated for a fast disposable schema;
                                       // production startup uses Database.Migrate() instead (see Task 2 Step 6).
    }

    [Fact]
    public async Task InsertAsync_then_GetAsync_returns_the_same_profile()
    {
        var repo = new ProfileRepository(_db);
        var profile = new AgentProfile
        {
            Name = "Claude Code",
            Command = "claude-code-acp",
            Args = new[] { "--stdio" },
            Env = new System.Collections.Generic.Dictionary<string, string>(),
            WorkingDir = @"C:\work",
        };

        var id = await repo.InsertAsync(profile);
        var fetched = await repo.GetAsync(id);

        Assert.NotNull(fetched);
        Assert.Equal("Claude Code", fetched!.Name);
        Assert.Equal("claude-code-acp", fetched.Command);
        Assert.Equal(new[] { "--stdio" }, fetched.Args);
    }

    public void Dispose()
    {
        _db.Dispose();
        File.Delete(_dbPath);
    }
}
```

- [ ] **Step 2b: Run test to verify it fails (types don't exist yet)**

Run: `dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter ProfileRepositoryTests`
Expected: FAIL (compile error — `AnywhereDbContext`, `AgentProfile`, `ProfileRepository` not defined).

- [ ] **Step 3: Implement `AgentProfile`**

A plain mutable class, not a record — EF Core's value converters (used below for `Args`/`Env`) and change tracking work most predictably against settable properties, and the entity is never handed across the `Anywhere.Controls` boundary the way `PermissionRequest` is (see Phase 2), so there's no reuse pressure pushing toward a record here.

```csharp
// src/Anywhere.Models/AgentProfile.cs
namespace Anywhere.Models;

public sealed class AgentProfile
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Command { get; set; }
    public string[] Args { get; set; } = Array.Empty<string>();
    public Dictionary<string, string> Env { get; set; } = new();
    public required string WorkingDir { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: Implement `AnywhereDbContext`**

```csharp
// src/Anywhere.Models/AnywhereDbContext.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Models;

public sealed class AnywhereDbContext : DbContext
{
    private readonly string _dbPath;

    public AnywhereDbContext(string dbPath) => _dbPath = dbPath;

    public DbSet<AgentProfile> Profiles => Set<AgentProfile>();

    public static string DefaultDbPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Anywhere");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "acp-client.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={_dbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentProfile>()
            .Property(p => p.Args)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<string[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<string>());

        modelBuilder.Entity<AgentProfile>()
            .Property(p => p.Env)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new());
    }
}
```

(Task 3 extends `OnModelCreating` and adds `DbSet<Session>`/`DbSet<Message>` to this same context — one `DbContext` for the whole app's persistence, matching the spec's single-database design.)

- [ ] **Step 5: Implement the design-time factory (needed for `dotnet ef` to work without running the app)**

`AnywhereDbContext`'s constructor takes a `dbPath` argument, so EF's tooling can't just `new` it up — a design-time factory tells `dotnet ef` how to construct one for migration generation. Per EF Core's guidance for platform-specific apps, this factory (and the generated `Migrations/` folder) live in `Anywhere.Models`, not in the WinForms startup project.

```csharp
// src/Anywhere.Models/AnywhereDbContextFactory.cs
using Microsoft.EntityFrameworkCore.Design;

namespace Anywhere.Models;

public sealed class AnywhereDbContextFactory : IDesignTimeDbContextFactory<AnywhereDbContext>
{
    public AnywhereDbContext CreateDbContext(string[] args)
        => new(AnywhereDbContext.DefaultDbPath());
}
```

**Note (review finding #6, Low):** `DefaultDbPath()` calls `Directory.CreateDirectory(...)`, so every `dotnet ef migrations add`/`dotnet ef database update` invocation creates a real `%LOCALAPPDATA%\Anywhere\` directory on the machine running it as a side effect of generating a migration file — harmless, but worth knowing about rather than being surprised by it the first time it happens.

- [ ] **Step 6: Implement `ProfileRepository`**

```csharp
// src/Anywhere/Persistence/ProfileRepository.cs
using Anywhere.Models;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Persistence;

public sealed class ProfileRepository
{
    private readonly AnywhereDbContext _db;

    public ProfileRepository(AnywhereDbContext db) => _db = db;

    public async Task<int> InsertAsync(AgentProfile profile)
    {
        _db.Profiles.Add(profile);
        await _db.SaveChangesAsync();
        return profile.Id;
    }

    public Task<AgentProfile?> GetAsync(int id)
        => _db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public Task<List<AgentProfile>> ListAllAsync()
        => _db.Profiles.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
}
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter ProfileRepositoryTests`
Expected: PASS.

- [ ] **Step 8: Generate the initial migration**

```bash
dotnet ef migrations add InitialCreate --project src/Anywhere.Models --startup-project src/Anywhere
```

This is the schema's source of truth going forward — inspect the generated `Migrations/*_InitialCreate.cs` and commit it like any other source file. Production startup (wired in Phase 3) calls `AnywhereDbContext.Database.Migrate()`, never `EnsureCreated()` — the test fixture in Step 2 is the one place `EnsureCreated()` belongs (see Global Constraints).

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: add EF Core data layer and agent profile persistence"
```

---

## Task 3: Session and message entities/repositories

**Files:**
- Create: `src/Anywhere.Models/Session.cs`
- Create: `src/Anywhere.Models/Message.cs`
- Modify: `src/Anywhere.Models/AnywhereDbContext.cs` (add `DbSet<Session>`, `DbSet<Message>`)
- Create: `src/Anywhere/Persistence/SessionRepository.cs`
- Create: `src/Anywhere/Persistence/MessageRepository.cs`
- Test: `src/Anywhere.Tests/MessageRepositoryTests.cs`

**Interfaces:**
- Consumes: `AnywhereDbContext`, `AgentProfile`/`ProfileRepository` from Task 2.
- Produces: `SessionRepository.InsertAsync(int profileId, string workingDir) : Task<int>`, `MessageRepository.InsertAsync(int sessionId, string role, string content, string? toolCallJson) : Task`, `MessageRepository.ListForSessionAsync(int sessionId) : Task<List<Message>>` — consumed by Phase 2 (agent process wiring) and Phase 3 (chat UI).

Same abstraction rationale as Task 2: `SessionRepository`/`MessageRepository` stay concrete, no interfaces. `Session`/`Message` are returned directly as EF entities rather than mapped to separate `SessionSummary`/`StoredMessage` DTOs — with EF Core already tracking/materializing these types cleanly, an extra DTO layer would just be a second, parallel definition of the same shape (see Global Constraints on abstractions).

- [ ] **Step 1: Implement `Session` and `Message` entities**

```csharp
// src/Anywhere.Models/Session.cs
namespace Anywhere.Models;

public sealed class Session
{
    public int Id { get; set; }
    public int ProfileId { get; set; }
    public required string WorkingDir { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// src/Anywhere.Models/Message.cs
namespace Anywhere.Models;

public sealed class Message
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public required string Role { get; set; } // "user" | "agent" | "system"
    public required string Content { get; set; }
    public string? ToolCallJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Add the `DbSet`s to `AnywhereDbContext`**

Edit `src/Anywhere.Models/AnywhereDbContext.cs`:

```csharp
public DbSet<Session> Sessions => Set<Session>();
public DbSet<Message> Messages => Set<Message>();
```

No new `OnModelCreating` configuration is needed — `Session`/`Message` have no JSON-converted columns, so EF Core's default conventions (int PK, FK by naming convention on `ProfileId`/`SessionId`) are sufficient.

- [ ] **Step 3: Write the failing test for session + message round trip**

```csharp
// src/Anywhere.Tests/MessageRepositoryTests.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Anywhere.Models;
using Anywhere.Persistence;
using Xunit;

public class MessageRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly AnywhereDbContext _db;

    public MessageRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"acp_test_{Guid.NewGuid():N}.db");
        _db = new AnywhereDbContext(_dbPath);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Messages_persist_and_list_in_insertion_order()
    {
        var profiles = new ProfileRepository(_db);
        var profileId = await profiles.InsertAsync(new AgentProfile
        {
            Name = "Test Agent",
            Command = "echo",
            Args = Array.Empty<string>(),
            Env = new System.Collections.Generic.Dictionary<string, string>(),
            WorkingDir = @"C:\work",
        });

        var sessions = new SessionRepository(_db);
        var sessionId = await sessions.InsertAsync(profileId, @"C:\work");

        var messages = new MessageRepository(_db);
        await messages.InsertAsync(sessionId, "user", "hello", null);
        await messages.InsertAsync(sessionId, "agent", "hi there", null);

        var history = await messages.ListForSessionAsync(sessionId);

        Assert.Equal(2, history.Count);
        Assert.Equal("user", history[0].Role);
        Assert.Equal("hello", history[0].Content);
        Assert.Equal("agent", history[1].Role);
    }

    public void Dispose()
    {
        _db.Dispose();
        File.Delete(_dbPath);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter MessageRepositoryTests`
Expected: FAIL (compile error — `SessionRepository`/`MessageRepository` not defined).

- [ ] **Step 5: Implement `SessionRepository`**

```csharp
// src/Anywhere/Persistence/SessionRepository.cs
using Anywhere.Models;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Persistence;

public sealed class SessionRepository
{
    private readonly AnywhereDbContext _db;

    public SessionRepository(AnywhereDbContext db) => _db = db;

    public async Task<int> InsertAsync(int profileId, string workingDir)
    {
        var session = new Session { ProfileId = profileId, WorkingDir = workingDir };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();
        return session.Id;
    }

    public Task<List<Session>> ListAllAsync()
        => _db.Sessions.AsNoTracking().OrderByDescending(s => s.Id).ToListAsync();
}
```

- [ ] **Step 6: Implement `MessageRepository`**

```csharp
// src/Anywhere/Persistence/MessageRepository.cs
using Anywhere.Models;
using Microsoft.EntityFrameworkCore;

namespace Anywhere.Persistence;

public sealed class MessageRepository
{
    private readonly AnywhereDbContext _db;

    public MessageRepository(AnywhereDbContext db) => _db = db;

    public async Task InsertAsync(int sessionId, string role, string content, string? toolCallJson)
    {
        _db.Messages.Add(new Message
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            ToolCallJson = toolCallJson,
        });
        await _db.SaveChangesAsync();
    }

    public Task<List<Message>> ListForSessionAsync(int sessionId)
        => _db.Messages.AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Id)
            .ToListAsync();
}
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter MessageRepositoryTests`
Expected: PASS.

- [ ] **Step 8: Add a migration for the new tables**

```bash
dotnet ef migrations add AddSessionsAndMessages --project src/Anywhere.Models --startup-project src/Anywhere
```

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: add session and message persistence"
```

---

## Self-Review Notes

- **Spec coverage:** this phase covers the spec's persistence data model in full (`profiles`/`sessions`/`messages`, now as EF Core entities), real `Anywhere.Design` tokens, and the four-project solution skeleton. Protocol, UI, and styling are explicitly deferred to Phases 2-4.
- **Independently testable:** after Task 3, `dotnet test` exercises real CRUD against a real (temp-file) SQLite database for all three entities, and the app builds/runs as an empty WinForms shell — this phase produces working, verifiable software on its own even though the window is empty.
- **Abstraction check:** no repository interfaces were introduced (see Global Constraints); confirmed no other phase in this series needs one either, since `Anywhere.Models` types are consumed directly downstream.
- **2026-09-06 fix from `2026-09-05-anywhere-phases.review.md`, finding #4 (Medium):** added Task 1b to actually populate `Anywhere.Design`'s three files with real values — the original draft of this phase created the empty project and claimed (in this section's Interfaces summary) that it was "consumed by every widget," but no task anywhere in the four phases created the files or referenced them. Phase 3 (Tasks 6-7) and Phase 4 (Tasks 8, 10) have been amended to actually consume `Spacing`/`Typography`/`Colors` from this task.
- **Note:** `ProfileRepository.UpdateAsync`/`DeleteAsync` and `AgentProfileParser`/`AgentProfileParsingTests.cs` (both listed in this phase's canonical File Structure, tagged Phase 4) are implemented in Phase 4, Task 9, alongside the profile management UI that's their only consumer — see [2026-09-05-anywhere-phases.review.md](2026-09-05-anywhere-phases.review.md) findings #2 and #5 for why.
