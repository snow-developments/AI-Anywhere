# AGENTS.md

Agent-agnostic guidance for working in this repository. `CLAUDE.md` imports this
file.

## Project Status

Phases 1-3 (`docs/superpowers/plans/2026-09-05-phase-{1,2,3}-*.md`) and the
splash-form/`ConversationForm`-rename plan are implemented. Phase 4
(`docs/superpowers/plans/2026-09-05-phase-4-polish.md` — crash recovery, profile
management UI, visual styling) has not been executed yet.

UI layer: `SplashForm` is the borderless app-shell window shown at startup
(custom title bar, "New conversation" button, recent-conversations list);
`ChatForm` (formerly `MainForm`, then `ConversationForm`) is the per-conversation window,
composed of `Anywhere.Controls.ChatTranscriptPanel` and `PermissionDiffPanel`.
The splash owns the message loop and stays open as `Owner` of conversation
windows; closing it exits the app.

## What this is thing?

**Anywhere** — a native WinForms desktop client for the
[Agent Client Protocol](https://agentclientprotocol.com/get-started/introduction)
(ACP), the JSON-RPC protocol Zed defined for editor↔agent communication.
Motivation: Claude's official desktop app is Electron-based and hard to extend;
this client is native, hackable, and scoped to daily power-user needs. It is
agent-agnostic — it launches and speaks ACP to any configured agent subprocess,
not just Claude Code.

Full design spec: `docs/superpowers/specs/2026-09-04-design.md`

Implementation plans are split into four phased plans, each independently
executable/testable; see `docs/superpowers/plans/` for details.

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
dependency direction. Architecture-derived constraints agents must follow live
in [`docs/Constraints.md`](docs/Constraints.md).

## Constraints

Project-wide inviolable rules live in
[`docs/Constraints.md`](docs/Constraints.md). Read them once before touching
code; refer back when a plan or skill suggests breaking one. This section is a
pointer, not a copy.

## Style

C# naming/formatting conventions for this repo live in
[`.agents/guidance/Style Guide.md`](.agents/guidance/Style%20Guide.md). Read it
before writing or renaming any `.cs` symbol.

## Rules

- **`dotnet watch` Is Always Running.** The user keeps `dotnet watch` live
  against this repo and edits the app together with the agent in real time.
  `dotnet build` / `dotnet test` failing with `CS2012` (pdb in use) or `MSB3021`
  / `MSB3027` (`*.dll` "locked by: Anywhere (PID)") is the **expected steady
  state**, not a failure and not a regression. Do not hunt for stray processes,
  do not re-run the build in a loop, and do not keep reporting "the build
  failed." Verify code with `dotnet format --verify-no-changes` and
  `dotnet cslint` (neither needs the lock), note the lock once if relevant, and
  move on — the live watch reloads the change. The Stop hook
  (`.claude/hooks/verify-before-stop.sh`) will also trip on this lock; report
  what was done and stop rather than looping to satisfy it.
- **No SDD Workflow.** Do not use the `subagent-driven-development` skill in
  this repo — it's too verbose (ledgers, per-task subagent dispatch, review
  packages) for this project's scale. Use `executing-plans` (or just implement
  directly) for plan execution instead.
- **Git Commits Are the User's Job.** Agents must NEVER create commits, amend,
  rebase, run `git reset` / `git stash`, or otherwise mutate git history on the
  user's behalf. Leave changes staged or unstaged, summarize what is staged, and
  wait. Drafting proposed commit messages is fine — follow the rules in the
  `git-guidance` skill (loaded on demand), propose the message, let the user run
  `git commit`.
- **No Diary in User Code.** First-person narration, self-congratulatory prose,
  "implementation notes" recapping what the agent just did, multi-paragraph
  justifications — all of that belongs only in `.agents/` documents
  (`AGENTS.md`, `.agents/guidance/*`, `.agents/skills/*`). In `src/`, `docs/`,
  plans, README, code comments, commit messages: keep prose terse and reference
  the rationale rather than restating it.
- **GitHub Queries.** Always use `gh api`. Never fetch `api.github.com` /
  `raw.githubusercontent.com` URLs via the `fetch` tool or `curl`; use
  `gh api repos/<owner>/<repo>/contents/<path>` (add `?ref=<branch>` when
  needed) for directory listings and file contents, and
  `gh api repos/<owner>/<repo>/readme` for the rendered README.

## Other Resources

- `.agents/skills/uspto-wordmark-search/` — a project-local skill for trademark
  searches; see `TODO.md` for the pending "Anywhere" wordmark check that gates
  finalizing the project name.
- `.mcp.json` configures the `context7` MCP server for library-docs lookups.
