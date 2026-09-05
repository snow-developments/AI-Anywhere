# AGENTS.md

Agent-agnostic guidance for working in this repository. `CLAUDE.md` imports this
file.

## Project Status

Pre-implementation. No source code exists yet — only spec, plan, and TODO. The
plan below (`docs/superpowers/plans/2026-09-04-acp-winforms-client.md`) has not
been executed; when it has been, update this file's "Commands" section and
`docs/Architecture.md` to match what was actually built, since the plan's task
steps are aspirational until then.

## What this is thing?

**Anywhere** — a native WinForms desktop client for the
[Agent Client Protocol](https://agentclientprotocol.com/get-started/introduction)
(ACP), the JSON-RPC protocol Zed defined for editor↔agent communication.
Motivation: Claude's official desktop app is Electron-based and hard to extend;
this client is native, hackable, and scoped to daily power-user needs. It is
agent-agnostic — it launches and speaks ACP to any configured agent subprocess,
not just Claude Code.

Full spec: `docs/superpowers/specs/2026-09-04-design.md`

Implementation plan (split into four phased plans, each independently
executable/testable — see
`docs/superpowers/plans/2026-09-04-acp-winforms-client.md` for the superseded
single-file original):

1. `docs/superpowers/plans/2026-09-05-anywhere-phase1-foundation.md` — project
   scaffolding + EF Core persistence
2. `docs/superpowers/plans/2026-09-05-anywhere-phase2-protocol-and-controls.md`
   — MarkdownLabel control + ACP agent process wrapper
3. `docs/superpowers/plans/2026-09-05-anywhere-phase3-core-ui.md` — chat
   transcript UI + permission/diff panel
4. `docs/superpowers/plans/2026-09-05-anywhere-phase4-polish.md` — crash
   recovery, profile management UI, visual styling (WinForms.Fluent.UI, not
   MaterialSkin.2)

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

`dotnet-ef` and `cslint` are local tools pinned in `.config/dotnet-tools.json`
(the standard tool-manifest path created by `dotnet new tool-manifest`) — run
`dotnet tool restore` from the repo root before using either of them.

### Testing Strategy

Use xUnit for persistence (EF Core CRUD against a real, temp-file SQLite
database — `Database.EnsureCreated()` in test fixtures, never
`Database.Migrate()` there) and agent-profile parsing. The ACP protocol layer
(`AgentProcess`) is integration-tested against a real fake ACP agent subprocess
(`src/Anywhere.Tests/FakeAgent/fake_agent.py`) speaking real stdio JSON-RPC —
not by mocking `acp-csharp` internals. UI is manual smoke-test only in v1.

See [docs/Architecture.md](docs/Architecture.md) for the project layout and
dependency direction. The architecture-derived constraints agents must follow
live in the "Constraints" section below.

## Constraints (from the spec/plan — do not violate silently)

- Dependency direction `Anywhere` → `Anywhere.Controls` → `Anywhere.Design`, and
  `Anywhere` → `Anywhere.Models`, is one-way. `Anywhere.Design` and
  `Anywhere.Models` must stay UI-framework-agnostic, and neither is ever
  referenced by `Anywhere.Controls`.
- ACP transport goes through the `acp-csharp` NuGet package (nuskey8/acp-csharp)
  — do not hand-roll JSON-RPC framing. Contribute fixes upstream rather than
  forking.
- Markdown rendering is `MarkdownLabel` (Markdig +
  Vortice/Direct2D+DirectWrite), adapted from an external source file, not a
  WebView2-based renderer.
- Persistence goes through EF Core (`Microsoft.EntityFrameworkCore.Sqlite`)
  against `AnywhereDbContext` in `Anywhere.Models` — not raw
  `Microsoft.Data.Sqlite`/ADO.NET. Schema changes go through
  `dotnet ef migrations add` (never hand-edited `CREATE TABLE`), applied via
  `Database.Migrate()` at startup.
- No repository interfaces (`IProfileRepository`, etc.) — concrete classes over
  `AnywhereDbContext` only. See the "Design decision" note below before adding
  one.
- Visual styling: `WinForms.Fluent.UI` (an "add new controls" library referenced
  only by `Anywhere.Controls`), not a full app-wide skin override. The
  superseded single-file plan's Task 10 had drifted to `MaterialSkin.2` instead,
  contradicting the spec — this was fixed when the plan was split into phases;
  Phase 4's Task 10 uses `WinForms.Fluent.UI`.
- No permission-request timeouts — the permission/diff panel waits indefinitely,
  matching editor behavior (ACP itself has no timeout).
- v1 scope excludes: agent-specific presets, multiple concurrent session tabs,
  slash-command UI, MCP server config UI, attachments, plan-mode UI, WinForms UI
  automation. See the spec's "Out of scope" section before adding any of these.

## Design Decisions

### No repository interfaces

Data access uses concrete
`ProfileRepository`/`SessionRepository`/`MessageRepository` classes wrapping
`AnywhereDbContext` directly — no `IProfileRepository` abstraction layer. This
follows the
[.NET Framework Design Guidelines on abstractions](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/abstractions-abstract-types-and-interfaces):
"DO NOT provide abstractions unless they are tested by developing several
concrete implementations." There is exactly one concrete data store (SQLite via
EF Core) with no second implementation planned, and the project already tests
the ACP layer against a real fake agent rather than mocks — the same "test the
real thing" philosophy applies to persistence. `DbContext`/`DbSet<T>` are
themselves EF Core's abstraction; don't wrap them a second time. Revisit this
only if a genuine second backing store or a concrete testability need for a fake
actually materializes.

## GitHub Queries

Always use `gh api` to query GitHub (issues, PRs, file contents, etc.) rather
than guessing at URLs or answering from memory — GitHub's data changes and only
a live query is authoritative. **Do not** fetch `api.github.com`/`raw.githubusercontent.com`
URLs directly via the `fetch` tool or `curl`; that violates this rule. Use
`gh api repos/<owner>/<repo>/contents/<path>` (add `?ref=<branch>` when needed)
for directory listings and file contents, and `gh api repos/<owner>/<repo>/readme`
for the rendered README. The agent-instruction note "GitHub Queries" was
strengthened on 2026-09-05 after a Phase 2 implementation session silently used
the `fetch` tool against `api.github.com` instead of `gh api` — don't repeat
that.

## Git Commit Messages

Follow the `git-guidance` skill on every commit. Concretely, every commit
message in this repository must satisfy all of:

- **Subject line** begins with a descriptive present-tense verb, contains no
  period, and never references agentic plans/tasks/phases/steps. Describe the
  actual code change, not the planning artifact that prompted it
- **Body bullets** (when present) use GitHub Flavored Markdown, contain no
  trailing periods, and wrap named code constructs in backticks
- **`💅`** prefixes a bullet only when it is strictly non-functional
  (formatting, comments, naming polish, docs wording with no behavior change)
- **`📚`** prefixes a bullet only when it touches documentation files with no
  source behavior change; never combine `💅` and `📚` on the same bullet
- **Subject stays concise**; add a body only when an adequate summary would
  not fit as a single line

Examples of the rule in practice are in the `git-guidance` skill and in the
"Implementation Notes" section of
`docs/superpowers/plans/2026-09-05-phase-2-protocol-and-controls.md`, which
describes the `acp-csharp` and `MarkdownLabel` work that was actually committed
under subjects like `` Add `AgentProcess` over `acp-csharp` `` — no
plan-phase language, just the code change. Future commits should match that
style.

## Other Resources

- `.agents/skills/uspto-wordmark-search/` — a project-local skill for trademark
  searches; see `TODO.md` for the pending "Anywhere" wordmark check that gates
  finalizing the project name.
- `.mcp.json` configures the `context7` MCP server for library-docs lookups.
