# ACP WinForms Client Implementation Plan

> **SUPERSEDED 2026-09-05:** This plan was split into four phased plans, each independently executable and testable. Use those instead of this file:
> - [Phase 1 — Foundation (scaffolding + EF Core persistence)](2026-09-05-anywhere-phase1-foundation.md) — Tasks 1-3
> - [Phase 2 — Protocol layer + controls (MarkdownLabel, ACP agent process)](2026-09-05-anywhere-phase2-protocol-and-controls.md) — Tasks 4-5
> - [Phase 3 — Core UI (chat transcript, permission/diff panel)](2026-09-05-anywhere-phase3-core-ui.md) — Tasks 6-7
> - [Phase 4 — Polish (crash recovery, profile management UI, visual styling)](2026-09-05-anywhere-phase4-polish.md) — Tasks 8-10 (Task 10 also fixes a `MaterialSkin.2`/spec discrepancy present in this file)
>
> This file is kept for history only — do not execute it.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a native WinForms .NET 10 desktop client that speaks the Agent Client Protocol (ACP) to any ACP-compatible agent subprocess, with chat, inline permission/diff review, and persisted history.

**Architecture:** Four .NET projects — `Anywhere.Design` (framework-agnostic design tokens, no UI deps, reusable by a future macOS client), `Anywhere.Models` (framework-agnostic EF Core data layer, no UI deps, sibling to `Anywhere.Design`: entities + `AnywhereDbContext` + migrations), `Anywhere.Controls` (WinForms control library depending on Design + WinForms.Fluent.UI: `MarkdownLabel`, `ChatTranscriptPanel`, `PermissionDiffPanel`, `DebugLogPanel`), and `Anywhere` (the app: protocol layer wrapping `acp-csharp`'s `ClientSideConnection`, an agent-profile registry persisted via EF Core against SQLite, and `MainForm` composing `Anywhere.Controls` widgets).

**Tech Stack:** .NET 10, WinForms, `acp-csharp` (NuGet, MIT), EF Core (`Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`, `dotnet-ef`), Markdig + Vortice (adapted `MarkdownLabel`), `WinForms.Fluent.UI` (NuGet, MIT — Fluent/WinUI3-styled controls), xUnit.

**Spec:** [docs/superpowers/specs/2026-09-04-design.md](../specs/2026-09-04-design.md)

## Global Constraints

- Target framework: `net10.0-windows` for WinForms projects (`Anywhere`, `Anywhere.Controls`); plain `net10.0` for `Anywhere.Design` and `Anywhere.Models` (both must stay UI-framework-agnostic — no WinForms/Fluent reference — for future reuse by a macOS client, see TODO.md).
- Project dependency direction: `Anywhere` → `Anywhere.Controls` → `Anywhere.Design`, and `Anywhere` → `Anywhere.Models` as a second, parallel branch. Never reverse either arrow: `Anywhere.Design` and `Anywhere.Models` must not reference `Anywhere.Controls` or `Anywhere`, and `Anywhere.Controls`/`Anywhere.Design` must not reference `Anywhere.Models` (persistence is an app-layer concern, not a UI concern).
- ACP transport dependency: `acp-csharp` NuGet package — do not hand-roll JSON-RPC framing.
- Markdown rendering: adapt `MarkdownLabel.cs` from `family-lock-out`, living in `Anywhere.Controls` — do not add a WebView2-based renderer.
- Persistence: EF Core (`Microsoft.EntityFrameworkCore.Sqlite`) against a SQLite file under local (non-roaming) `%APPDATA%\Anywhere\` — never `%APPDATA%\Roaming`. Entities and `AnywhereDbContext` live in `Anywhere.Models`; `Anywhere` consumes the context through thin repository classes (see next bullet).
- No repository interfaces (`IProfileRepository`, `ISessionRepository`, etc.). Per the [Framework Design Guidelines on abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces) — "DO NOT provide abstractions unless they are tested by developing several concrete implementations" — there is exactly one concrete data store (SQLite via EF Core) and no plan for a second, and the project's existing test philosophy already favors exercising real backends over mocks (see Testing Strategy). `ProfileRepository`/`SessionRepository`/`MessageRepository` stay concrete classes wrapping `AnywhereDbContext`; `DbContext`/`DbSet<T>` are themselves EF Core's abstraction layer and are not wrapped a second time.
- Visual styling: `WinForms.Fluent.UI` NuGet package (referenced only by `Anywhere.Controls`) supplies Fluent/WinUI3-styled controls — it is an "add new controls" library, not a full app-wide visual-style override, so default WinForms controls not replaced by a Fluent equivalent keep the default Win32 look. Do not add `MaterialSkin.2` or another full-override library alongside it.
- Schema is managed by **EF Core Migrations**, generated into `Anywhere.Models` (a dedicated project with its own design-time factory, per EF Core's guidance to keep migrations in a project separate from the WinForms startup project). Not `PRAGMA user_version` + hand-rolled `CREATE TABLE IF NOT EXISTS`, and not runtime `Database.EnsureCreated()` outside of tests (`EnsureCreated()` is fine for test fixtures that need a fast, disposable schema, but production startup always calls `Database.Migrate()` so the migration history stays authoritative).
- No permission-request timeouts — panel waits indefinitely.
- v1 has no multi-tab sessions, slash commands, MCP config UI, attachments, or agent-specific presets (those are v1.1+).

---

## File Structure

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
    MarkdownLabel.cs           (adapted from family-lock-out)
    ChatTranscriptPanel.cs     (scrollable stack of message bubbles)
    PermissionDiffPanel.cs     (docked bottom panel, TableLayoutPanel-based)
    DebugLogPanel.cs
  Anywhere/
    Anywhere.csproj            (net10.0-windows; refs Anywhere.Controls + Anywhere.Models)
    Program.cs
    MainForm.cs
    MainForm.Designer.cs
    Agents/
      AgentProcess.cs            (wraps Process + acp-csharp ClientSideConnection over its stdio)
    Persistence/
      ProfileRepository.cs       (wraps AnywhereDbContext; concrete class, no interface — see Global Constraints)
      SessionRepository.cs
      MessageRepository.cs
  Anywhere.Tests/
    Anywhere.Tests.csproj
    ProfileRepositoryTests.cs
    MessageRepositoryTests.cs
    AgentProfileParsingTests.cs
    MarkdownLabelTests.cs
    FakeAgent/
      fake_agent.py              (minimal ACP-speaking stdio script used by integration test)
    AgentProcessIntegrationTests.cs
```

**Interfaces summary (for cross-task reference):**
- `AgentProfile` (`Anywhere.Models`, EF entity class): `int Id, string Name, string Command, string[] Args, Dictionary<string,string> Env, string WorkingDir`
- `AnywhereDbContext`: `DbSet<AgentProfile> Profiles`, `DbSet<Session> Sessions`, `DbSet<Message> Messages`; constructed with a SQLite file path, configures itself via `OnConfiguring`/`UseSqlite`.
- `ProfileRepository(AnywhereDbContext db)`: `Task<int> InsertAsync(AgentProfile p)`, `Task<AgentProfile?> GetAsync(int id)`, `Task<List<AgentProfile>> ListAllAsync()`
- `SessionRepository(AnywhereDbContext db)`: `Task<int> InsertAsync(int profileId, string workingDir)`, `Task<List<Session>> ListAllAsync()`
- `MessageRepository(AnywhereDbContext db)`: `Task InsertAsync(int sessionId, string role, string content, string? toolCallJson)`, `Task<List<Message>> ListForSessionAsync(int sessionId)`
- `AgentProcess`: `Task StartAsync()`, `Task<PromptResult> SendPromptAsync(string text)`, `event Action<PermissionRequest> OnPermissionRequested`, `event Action<string> OnAgentExited`, `Task RespondToPermissionAsync(string requestId, PermissionOutcome outcome)`
- `Anywhere.Design`: static classes `Colors` (named `Color` constants), `Spacing` (named `int` pixel constants), `Typography` (named `Font`-factory constants) — consumed by every widget in `Anywhere.Controls`.

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
- Produces: `AgentProfile` entity, `AnywhereDbContext` (`DbSet<AgentProfile> Profiles`), `ProfileRepository` with `InsertAsync`/`GetAsync`/`ListAllAsync` — consumed by Task 3 (Session/Message entities share this context), Task 5 (agent process), and Task 6 (UI agent picker).

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

A plain mutable class, not a record — EF Core's value converters (used below for `Args`/`Env`) and change tracking work most predictably against settable properties, and the entity is never handed across the `Anywhere.Controls` boundary the way `PermissionRequest` is (see Task 5), so there's no reuse pressure pushing toward a record here.

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

This is the schema's source of truth going forward — inspect the generated `Migrations/*_InitialCreate.cs` and commit it like any other source file. Production startup (wired in Task 6) calls `AnywhereDbContext.Database.Migrate()`, never `EnsureCreated()` — the test fixture in Step 2 is the one place `EnsureCreated()` belongs (see Global Constraints).

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
- Produces: `SessionRepository.InsertAsync(int profileId, string workingDir) : Task<int>`, `MessageRepository.InsertAsync(int sessionId, string role, string content, string? toolCallJson) : Task`, `MessageRepository.ListForSessionAsync(int sessionId) : Task<List<Message>>` — consumed by Task 5 (agent process wiring) and Task 6 (chat UI).

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

## Task 4: Adapt MarkdownLabel control

**Files:**
- Create: `src/Anywhere.Controls/MarkdownLabel.cs` (adapted from `D:\Users\enigm\GitHub\family-lock-out\Controls\MarkdownLabel.cs`)
- Test: `src/Anywhere.Tests/MarkdownLabelTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `MarkdownLabel : Control` with a `string Markdown { get; set; }` property (renamed/generalized if the source used a different property name), living in the `Anywhere.Controls` project — consumed by Task 6 (chat transcript panel).

- [ ] **Step 1: Read the source control**

Read `D:\Users\enigm\GitHub\family-lock-out\Controls\MarkdownLabel.cs` in full to understand its exact public API (property names, constructor, `OnPaint` override, Markdig/Vortice usage) before copying — do not assume the property names guessed in the spec.

- [ ] **Step 2: Add Markdig and Vortice package references**

```bash
dotnet add src/Anywhere.Controls/Anywhere.Controls.csproj package Markdig
dotnet add src/Anywhere.Controls/Anywhere.Controls.csproj package Vortice.Direct2D1
dotnet add src/Anywhere.Controls/Anywhere.Controls.csproj package Vortice.DirectWrite
```

(Adjust exact Vortice package names to match whichever `Vortice.*` namespaces the source file actually imports — confirm from Step 1's read.)

- [ ] **Step 3: Copy the control into the new project, renaming the namespace**

Copy the file to `src/Anywhere.Controls/MarkdownLabel.cs`, change `namespace FamilyLockout.Controls` to `namespace Anywhere.Controls`, and fix any using-directives that referenced the old project.

- [ ] **Step 4: Write a smoke test that constructs the control off-UI-thread-safely**

```csharp
// src/Anywhere.Tests/MarkdownLabelTests.cs
using Anywhere.Controls;
using Xunit;

public class MarkdownLabelTests
{
    [WinFormsFact]
    public void Setting_markdown_text_does_not_throw()
    {
        using var label = new MarkdownLabel();
        label.Text = "**bold** and _italic_ and a [link](https://example.com)";
        Assert.Equal("**bold** and _italic_ and a [link](https://example.com)", label.Text);
    }
}
```

(If the source control exposes a different property than `Text` for the markdown source, use that property name here instead — confirmed in Step 1.)

- [ ] **Step 5: Add the WinForms test SDK for `[WinFormsFact]`**

```bash
dotnet add src/Anywhere.Tests/Anywhere.Tests.csproj package WinForms.UITest.Foundation
```

If `WinForms.UITest.Foundation` isn't available/needed, replace `[WinFormsFact]` with plain `[Fact]` (WinForms controls can be constructed off-thread in a headless test as long as no message loop is required) — try plain `[Fact]` first since it avoids an extra dependency.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter MarkdownLabelTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: adapt MarkdownLabel control from family-lock-out"
```

---

## Task 5: Agent process wrapper over acp-csharp

**Files:**
- Create: `src/Anywhere/Agents/AgentProcess.cs`
- Create: `src/Anywhere/Agents/PromptResult.cs`
- Create: `src/Anywhere.Controls/PermissionRequest.cs`
- Create: `src/Anywhere.Controls/PermissionOutcome.cs`
- Test: `src/Anywhere.Tests/FakeAgent/fake_agent.py`
- Test: `src/Anywhere.Tests/AgentProcessIntegrationTests.cs`

**Note on project placement:** `PermissionRequest`/`PermissionOutcome` are created here in `Anywhere.Controls` rather than `Anywhere/Agents/`, even though `AgentProcess` (which uses them) lives in `Anywhere`. This is intentional: Task 7's `PermissionDiffPanel` lives in `Anywhere.Controls`, and `Anywhere` → `Anywhere.Controls` is the only allowed reference direction (never the reverse) — see Global Constraints. `AgentProcess` can reference `Anywhere.Controls` types freely since `Anywhere` already depends on `Anywhere.Controls` (Task 1, Step 6).

**Interfaces:**
- Consumes: `AgentProfile` (`Anywhere.Models`) from Task 2.
- Produces: `AgentProcess.StartAsync()`, `SendPromptAsync(string) : Task<PromptResult>`, `event Action<Anywhere.Controls.PermissionRequest> OnPermissionRequested`, `event Action<string> OnAgentExited`, `RespondToPermissionAsync(string requestId, Anywhere.Controls.PermissionOutcome outcome)` — consumed by Task 6 (UI wiring) and Task 7 (`PermissionDiffPanel`, which consumes the `PermissionRequest`/`PermissionOutcome` types created here rather than redefining them).

- [ ] **Step 1: Add the acp-csharp package**

```bash
dotnet add src/Anywhere/Anywhere.csproj package AcpCSharp
```

(Confirm exact NuGet package id from https://github.com/nuskey8/acp-csharp's README/NuGet listing — adjust the id above if it differs, e.g. it may be published as `Acp.CSharp` or similar.)

- [ ] **Step 2: Write a minimal fake ACP agent for integration testing**

```python
# src/Anywhere.Tests/FakeAgent/fake_agent.py
import sys, json

def send(msg):
    data = json.dumps(msg)
    sys.stdout.write(f"Content-Length: {len(data)}\r\n\r\n{data}")
    sys.stdout.flush()

def read_message():
    line = sys.stdin.readline()
    length = int(line.split(":")[1].strip())
    sys.stdin.readline()  # blank line
    body = sys.stdin.read(length)
    return json.loads(body)

while True:
    msg = read_message()
    if msg.get("method") == "initialize":
        send({"jsonrpc": "2.0", "id": msg["id"], "result": {"protocolVersion": "1"}})
    elif msg.get("method") == "session/prompt":
        send({"jsonrpc": "2.0", "id": msg["id"], "result": {"content": "fake agent response"}})
```

(This uses ACP's `Content-Length`-framed stdio transport per the spec. If `acp-csharp`'s actual wire framing differs, adjust `send`/`read_message` to match after reading the library's transport implementation in Step 3.)

- [ ] **Step 3: Read acp-csharp's public API for `ClientSideConnection`**

Before writing `AgentProcess`, read the actual `ClientSideConnection`/`IAcpClient` API from the installed `acp-csharp` package (via NuGet cache or its GitHub source) to get exact method/event names — do not guess signatures.

- [ ] **Step 4: Write the failing integration test**

```csharp
// src/Anywhere.Tests/AgentProcessIntegrationTests.cs
using System.Threading.Tasks;
using Anywhere.Agents;
using Anywhere.Models;
using Xunit;

public class AgentProcessIntegrationTests
{
    [Fact]
    public async Task SendPromptAsync_returns_the_fake_agents_response()
    {
        var profile = new AgentProfile
        {
            Name = "Fake",
            Command = "python",
            Args = new[] { "src/Anywhere.Tests/FakeAgent/fake_agent.py" },
            Env = new System.Collections.Generic.Dictionary<string, string>(),
            WorkingDir = System.IO.Directory.GetCurrentDirectory(),
        };

        var process = new AgentProcess(profile);
        await process.StartAsync();

        var result = await process.SendPromptAsync("hello");

        Assert.Equal("fake agent response", result.Content);
    }
}
```

- [ ] **Step 5: Run test to verify it fails**

Run: `dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter AgentProcessIntegrationTests`
Expected: FAIL (compile error — `AgentProcess`, `PromptResult` not defined).

- [ ] **Step 6: Implement `PromptResult`, `PermissionRequest`, `PermissionOutcome`**

```csharp
// src/Anywhere/Agents/PromptResult.cs
namespace Anywhere.Agents;

public record PromptResult(string Content);
```

```csharp
// src/Anywhere.Controls/PermissionRequest.cs
namespace Anywhere.Controls;

public record PermissionRequest(string RequestId, string ToolName, string Description, string? OldContent, string? NewContent);
```

```csharp
// src/Anywhere.Controls/PermissionOutcome.cs
namespace Anywhere.Controls;

public enum PermissionOutcome { Allow, AllowAlways, Deny }
```

- [ ] **Step 7: Implement `AgentProcess`**

Implement `AgentProcess` in `src/Anywhere/Agents/AgentProcess.cs`, wrapping a `System.Diagnostics.Process` (launching `Profile.Command` with `Profile.Args`/`Profile.Env`/`Profile.WorkingDir`, redirecting stdin/stdout) and an `acp-csharp` `ClientSideConnection` bound to those streams, using the exact API confirmed in Step 3. Add `using Anywhere.Controls;` for the `PermissionRequest`/`PermissionOutcome` types. Wire `OnPermissionRequested` to the library's `session/request_permission` callback, and `OnAgentExited` to the underlying `Process.Exited` event.

- [ ] **Step 8: Run the test to verify it passes**

Run: `dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter AgentProcessIntegrationTests`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: wrap acp-csharp ClientSideConnection in AgentProcess"
```

---

## Task 6: Chat transcript UI and MainForm wiring

**Files:**
- Create: `src/Anywhere.Controls/ChatTranscriptPanel.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/MainForm.Designer.cs`

**Interfaces:**
- Consumes: `MarkdownLabel` (Task 4), `AgentProcess` (Task 5), `MessageRepository`/`SessionRepository`/`ProfileRepository` (Tasks 2-3).
- Produces: a running app where typing a prompt and pressing Enter sends it to a configured agent profile and displays the streamed response as a markdown bubble, persisted to SQLite.

- [ ] **Step 1: Implement `ChatTranscriptPanel`**

```csharp
// src/Anywhere.Controls/ChatTranscriptPanel.cs
namespace Anywhere.Controls;

public sealed class ChatTranscriptPanel : FlowLayoutPanel
{
    public ChatTranscriptPanel()
    {
        FlowDirection = FlowDirection.TopDown;
        AutoScroll = true;
        WrapContents = false;
        Dock = DockStyle.Fill;
    }

    public void AppendMessage(string role, string markdown)
    {
        var bubble = new MarkdownLabel
        {
            Text = markdown,
            Width = ClientSize.Width - 24,
            Margin = new Padding(8),
        };
        Controls.Add(bubble);
        ScrollControlIntoView(bubble);
    }
}
```

(Adjust the property used to set markdown content — `Text` vs. whatever Task 4 confirmed as the real property name.)

- [ ] **Step 2: Wire MainForm layout — transcript, input box, send button**

Edit `src/Anywhere/MainForm.Designer.cs` to add: a `ChatTranscriptPanel` docked `Fill`, a `TextBox` (`_inputBox`) docked `Bottom` inside a `Panel`, and hook `_inputBox.KeyDown` for Enter-to-send in `MainForm.cs`.

- [ ] **Step 3: Wire MainForm to start a hardcoded test profile and relay messages**

In `MainForm.cs`, on load: construct `AnywhereDbContext` at `AnywhereDbContext.DefaultDbPath()`, call `Database.Migrate()` (applies any pending EF Core migrations — never `EnsureCreated()` here, see Global Constraints), construct one default `AgentProfile` (a placeholder "echo" command for manual testing), start an `AgentProcess`, and on send: call `SendPromptAsync`, append both the user message and the agent's response to `ChatTranscriptPanel`, and persist both via `MessageRepository`.

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Anywhere/Anywhere.csproj`
Expected: window opens, typing text and pressing Enter shows the user's message and (once a real agent profile is configured) the agent's reply as rendered markdown bubbles.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: wire chat transcript UI to agent process and persistence"
```

---

## Task 7: Permission/diff panel

**Files:**
- Create: `src/Anywhere.Controls/PermissionDiffPanel.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/MainForm.Designer.cs`

**Note on project placement:** `PermissionRequest`/`PermissionOutcome` are NOT created in this task — they were already created in Task 5 (in `Anywhere.Controls`, not `Anywhere/Agents/`, specifically so this task's `PermissionDiffPanel` could consume them without a circular project reference — `Anywhere` → `Anywhere.Controls` is the only allowed direction). This task just implements `PermissionDiffPanel` against those existing types.

**Interfaces:**
- Consumes: `AgentProcess.OnPermissionRequested`/`RespondToPermissionAsync` and `Anywhere.Controls.PermissionRequest`/`PermissionOutcome` (Task 5).
- Produces: a docked panel above the input box that shows pending permission requests with Allow/Allow-always/Deny buttons and an inline diff view, collapsing to zero height when idle.

- [ ] **Step 1: Implement `PermissionDiffPanel`**

```csharp
// src/Anywhere.Controls/PermissionDiffPanel.cs
namespace Anywhere.Controls;

public sealed class PermissionDiffPanel : TableLayoutPanel
{
    public event Action<string, PermissionOutcome>? OutcomeChosen;

    private readonly Label _description = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly TextBox _oldContent = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _newContent = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button _allow = new() { Text = "Allow" };
    private readonly Button _allowAlways = new() { Text = "Allow Always" };
    private readonly Button _deny = new() { Text = "Deny" };

    private string? _currentRequestId;

    public PermissionDiffPanel()
    {
        Dock = DockStyle.Bottom;
        ColumnCount = 2;
        RowCount = 2;
        Height = 0;
        Visible = false;

        Controls.Add(_description, 0, 0);
        Controls.Add(_oldContent, 0, 1);
        Controls.Add(_newContent, 1, 1);

        var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        buttonRow.Controls.AddRange(new Control[] { _allow, _allowAlways, _deny });
        Controls.Add(buttonRow, 0, 2);

        _allow.Click += (_, _) => Choose(PermissionOutcome.Allow);
        _allowAlways.Click += (_, _) => Choose(PermissionOutcome.AllowAlways);
        _deny.Click += (_, _) => Choose(PermissionOutcome.Deny);
    }

    public void ShowRequest(PermissionRequest request)
    {
        _currentRequestId = request.RequestId;
        _description.Text = $"{request.ToolName}: {request.Description}";
        _oldContent.Text = request.OldContent ?? string.Empty;
        _newContent.Text = request.NewContent ?? string.Empty;
        Visible = true;
        Height = 200;
    }

    private void Choose(PermissionOutcome outcome)
    {
        if (_currentRequestId is null) return;
        OutcomeChosen?.Invoke(_currentRequestId, outcome);
        _currentRequestId = null;
        Visible = false;
        Height = 0;
    }
}
```

- [ ] **Step 3: Wire into MainForm**

In `MainForm.Designer.cs`, add a `PermissionDiffPanel` docked `Bottom`, placed between the transcript (`Fill`) and the input panel (also `Bottom`) so it sits above the input box. In `MainForm.cs`, subscribe `AgentProcess.OnPermissionRequested` to call `_permissionPanel.ShowRequest(...)`, and `_permissionPanel.OutcomeChosen` to call `AgentProcess.RespondToPermissionAsync(...)`.

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Anywhere/Anywhere.csproj`, trigger a permission request against a real or fake agent that requests file-write permission.
Expected: panel appears above the input box showing the diff and three buttons; clicking one hides the panel and unblocks the agent's turn.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add inline permission/diff review panel"
```

---

## Task 8: Agent crash recovery and debug log

**Files:**
- Create: `src/Anywhere/Controls/DebugLogPanel.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/Agents/AgentProcess.cs`

**Interfaces:**
- Consumes: `AgentProcess.OnAgentExited` (Task 5).
- Produces: a transcript system message + "Restart agent" button on crash; a debug panel (toggle-visible) logging malformed JSON-RPC traffic.

- [ ] **Step 1: Add a malformed-message event to `AgentProcess`**

Modify `src/Anywhere/Agents/AgentProcess.cs` to add `event Action<string> OnProtocolWarning`, raised whenever the underlying `acp-csharp` connection fails to parse an incoming message (wrap the relevant try/catch around the library's read loop, confirmed from Task 5 Step 3's API read).

- [ ] **Step 2: Implement `DebugLogPanel`**

```csharp
// src/Anywhere/Controls/DebugLogPanel.cs
namespace Anywhere.Controls;

public sealed class DebugLogPanel : TextBox
{
    public DebugLogPanel()
    {
        Multiline = true;
        ReadOnly = true;
        ScrollBars = ScrollBars.Vertical;
        Dock = DockStyle.Fill;
        Visible = false;
    }

    public void AppendLine(string line) => AppendText(line + Environment.NewLine);
}
```

- [ ] **Step 3: Wire crash + protocol-warning handling into MainForm**

In `MainForm.cs`: subscribe `AgentProcess.OnAgentExited` to append a system-role message to `ChatTranscriptPanel` reading `"Agent exited."` plus a `Button` labeled "Restart agent" that re-runs the Task 6 Step 3 startup logic; subscribe `OnProtocolWarning` to call `_debugLogPanel.AppendLine(...)`. Add a menu item or keyboard shortcut (e.g. `Ctrl+D`) toggling `_debugLogPanel.Visible`.

- [ ] **Step 4: Manual verification**

Run the app, kill the agent subprocess externally (e.g. via Task Manager) while connected.
Expected: transcript shows "Agent exited." with a working "Restart agent" button that re-establishes the connection.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add agent crash recovery and protocol debug log"
```

---

## Task 9: Agent profile management UI

**Files:**
- Create: `src/Anywhere/AgentProfileForm.cs`
- Create: `src/Anywhere/AgentProfileForm.Designer.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/MainForm.Designer.cs`

**Interfaces:**
- Consumes: `ProfileRepository` (Task 2).
- Produces: a menu item opening a modal form to add/edit/list agent profiles, replacing the Task 6 Step 3 hardcoded profile with a user-selected one from a dropdown in `MainForm`.

- [ ] **Step 1: Implement `AgentProfileForm`**

Build a simple modal `Form` with: a `ListBox` of existing profiles (populated via `ProfileRepository.ListAllAsync()`), and input fields (`TextBox` for Name, Command, Args (comma-separated), WorkingDir) plus a "Save" button calling `ProfileRepository.InsertAsync(...)`.

- [ ] **Step 2: Add a profile picker to MainForm**

In `MainForm.Designer.cs`, add a `ComboBox` (`_profilePicker`) docked `Top`, populated from `ProfileRepository.ListAllAsync()` on load, plus a "Manage Profiles..." menu item opening `AgentProfileForm`. In `MainForm.cs`, replace the Task 6 Step 3 hardcoded profile with `(AgentProfile)_profilePicker.SelectedItem`.

- [ ] **Step 3: Manual verification**

Run the app, open "Manage Profiles...", add a profile for a real ACP agent (e.g. Claude Code's `claude-code-acp` binary if installed locally), select it from the dropdown, send a prompt.
Expected: real agent responds in the transcript.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add agent profile management UI"
```

---

## Task 10: Apply MaterialSkin.2 visual styling

**Files:**
- Modify: `src/Anywhere/Anywhere.csproj`
- Modify: `src/Anywhere/Program.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/MainForm.Designer.cs`

**Interfaces:**
- Consumes: nothing new (applies to the existing `MainForm` and its controls from Tasks 1, 6-9).
- Produces: a MainForm that inherits `MaterialSkin.Controls.MaterialForm` and renders with MaterialSkin.2's reskinned controls app-wide, instead of default Win32 visual styles.

- [ ] **Step 1: Add the MaterialSkin.2 package**

```bash
dotnet add src/Anywhere/Anywhere.csproj package MaterialSkin.2
```

- [ ] **Step 2: Read MaterialSkin.2's setup docs before wiring it in**

Read the `MaterialSkin.2` README (https://github.com/leocb/MaterialSkin) for the exact `MaterialSkinManager` initialization API (theme enum names, color scheme setup, and whether `MainForm` must inherit `MaterialForm` or just add a manager instance) — do not guess the API surface.

- [ ] **Step 3: Change `MainForm` to a `MaterialForm` and initialize the skin manager**

Edit `src/Anywhere/MainForm.cs` and `MainForm.Designer.cs`: change `MainForm`'s base class from `Form` to `MaterialSkin.Controls.MaterialForm`, and in the constructor (after `InitializeComponent()`) initialize `MaterialSkinManager.Instance`, add `this` to its form list, and set a color scheme, using the exact API confirmed in Step 2.

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Anywhere/Anywhere.csproj`
Expected: window opens with MaterialSkin.2's reskinned title bar, buttons, and input box instead of default Win32 chrome; existing chat/permission-panel functionality from Tasks 6-9 still works unchanged.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: apply MaterialSkin.2 visual styling"
```

---

## Self-Review Notes

- **Spec coverage:** agent-agnostic launch (Task 9), chat + markdown (Tasks 4/6), permission/diff panel (Task 7), persistence (Tasks 2/3), crash recovery + debug log (Task 8) — all covered. v1.1 presets and multi-tab sessions are explicitly out of scope per the spec and not tasked here.
- **Unconfirmed external API surfaces** (flagged inline rather than guessed): exact `acp-csharp` NuGet package id and its `ClientSideConnection` method/event names (Task 5 Steps 1 & 3), `MarkdownLabel`'s real public property name (Task 4 Step 1), and MaterialSkin.2's `MaterialSkinManager` initialization API (Task 10 Step 2) — each task instructs the implementer to read the real source/package before writing code against it, rather than trusting a guessed signature.
- **2026-09-05 amendment — EF Core persistence:** Tasks 2 and 3 were rewritten to use EF Core (`Microsoft.EntityFrameworkCore.Sqlite`) via a new `Anywhere.Models` project, replacing the original hand-rolled `Microsoft.Data.Sqlite` + `PRAGMA user_version` approach. Schema is now the generated EF Core `Migrations/` folder in `Anywhere.Models`, applied via `Database.Migrate()` at startup (Task 6) and `Database.EnsureCreated()` in test fixtures only. No repository interfaces were introduced — per the [Framework Design Guidelines on abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces), an abstraction earns its place only once proven by multiple concrete implementations, which doesn't apply to a single-database, no-mocking test setup; `ProfileRepository`/`SessionRepository`/`MessageRepository` remain concrete classes over `AnywhereDbContext`. Downstream tasks (5, 6, 9) were updated to import `Anywhere.Models` instead of the old `Anywhere.Persistence.AgentProfile`/`AppDatabase` types; `SessionSummary`/`StoredMessage` DTOs were dropped in favor of returning the EF entities (`Session`/`Message`) directly.
- **Still-open discrepancy (pre-existing, not touched by this amendment):** Task 10 has the implementer add `MaterialSkin.2`, but the spec (`docs/superpowers/specs/2026-09-04-design.md`) explicitly chose `WinForms.Fluent.UI` over `MaterialSkin.2`. Resolve before executing Task 10.
