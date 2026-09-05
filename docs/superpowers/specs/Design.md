---
created: 2026-09-04
---

# Native ACP Client for Windows — Design Spec

**Date:** 2026-09-04 **Status:** Approved for planning

## Goal

Build a thin, native WinForms desktop client for the
[Agent Client Protocol](https://agentclientprotocol.com/get-started/introduction)
(ACP) — the JSON-RPC protocol Zed defined for editor↔agent communication.
Motivation: Claude's official desktop app is Electron-based (heavy, slow to
start) and difficult to extend with custom UI/behavior. This client is native,
hackable, and scoped to what a power user actually needs day to day.

## Scope (v1)

- Launch and speak ACP to **any** ACP-compatible agent subprocess, configured
  via an editable agent-profile list (display name, launch command, args,
  working directory, env vars). Not hardcoded to Claude Code.
- Chat transcript: send prompts, stream agent responses, render markdown.
- Permission requests (`session/request_permission`): inline docked panel, not
  modal, with Allow / Allow-always / Deny actions.
- File-edit diffs: rendered inline in the same docked panel when the pending
  permission request is for a file write/edit.
- Session persistence: chat history and agent-profile configs survive app
  restarts.
- Basic agent-crash recovery (restart button), and visibility into malformed
  protocol traffic (debug log), rather than silent failure.

## Out of scope (v1) — deferred to v1.1+

- First-class presets for specific agents (Claude Code, Zed, Antigravity,
  OpenCode) — v1 ships the generic "arbitrary command" profile only; v1.1 adds
  named presets with sensible defaults for these four.
- Multiple concurrent session tabs, slash-command UI, MCP server config UI,
  image/file attachments, plan-mode UI, WinForms UI automation/test framework,
  permission-request timeouts.

## Tech Stack

- **.NET 10**, WinForms.
- **ACP protocol/transport:**
  [`nuskey8/acp-csharp`](https://github.com/nuskey8/acp-csharp) (MIT-licensed,
  unofficial C# SDK; implements both `ClientSideConnection`/`IAcpClient` and
  agent-side roles). This project will contribute fixes/gaps upstream as they're
  found rather than forking.
- **Markdown rendering:** adapt `MarkdownLabel.cs` from
  `D:\Users\enigm\GitHub\family-lock-out\Controls\MarkdownLabel.cs` (Markdig for
  parsing, Vortice/Direct2D+DirectWrite for hardware-accelerated custom
  drawing), moved into `Anywhere.Controls` as an independent component.
- **Visual styling:** `WinForms.Fluent.UI` (MIT-licensed; adds
  Fluent/WinUI3-styled controls to WinForms) — chosen over `MaterialSkin.2` for
  a native-feeling Windows 11 look, at the cost of being a smaller "add new
  controls" library rather than a full app-wide visual-style override; default
  WinForms controls not replaced by a Fluent equivalent keep the default Win32
  look.
- **Persistence:** SQLite via EF Core (`Microsoft.EntityFrameworkCore.Sqlite`),
  stored under the local (non-roaming) `%APPDATA%\Anywhere\` directory. Schema
  is managed by EF Core Migrations, generated into a dedicated `Anywhere.Models`
  project rather than by hand-rolled SQL. _(Amended 2026-09-05: originally
  specified as raw `Microsoft.Data.Sqlite` with `PRAGMA user_version`; switched
  to EF Core for standard migrations tooling and entity mapping.)_
- **Testing:** xUnit for persistence and agent-profile parsing. The ACP protocol
  layer is tested against a small fake ACP-speaking agent script (real stdio
  JSON-RPC round trip), not by mocking library internals.

## Architecture

Four .NET projects:

1. **`Anywhere.Design`** — framework-agnostic design-token library (net10.0, no
   UI dependencies): color, spacing, and typography constants. Carries no
   WinForms or Fluent dependency so it can be reused by a future macOS client
   (see project TODO.md).
2. **`Anywhere.Models`** — framework-agnostic EF Core data layer (net10.0, no UI
   dependencies), sibling to `Anywhere.Design`. Contains the entities
   (`AgentProfile`, `Session`, `Message`) and `AnywhereDbContext`, plus the EF
   Core migrations that are the schema's source of truth. Carries no WinForms
   dependency for the same macOS-reuse reason as `Anywhere.Design`.
3. **`Anywhere.Controls`** (net10.0-windows) — WinForms control library,
   references `Anywhere.Design` and `WinForms.Fluent.UI`. Contains the app's
   custom widgets as subclasses of Fluent/WinForms controls, styled using
   `Anywhere.Design`'s tokens: `MarkdownLabel`, `ChatTranscriptPanel`,
   `PermissionDiffPanel`, `DebugLogPanel`.
4. **`Anywhere`** (net10.0-windows, the app) — references `Anywhere.Controls`
   and `Anywhere.Models`. Three internal layers:
   - **Protocol layer** — wraps `acp-csharp`'s `ClientSideConnection`, driving
     one agent subprocess over stdio JSON-RPC per active session.
     Agent-agnostic: it only knows how to speak ACP to whatever process it
     launched.
   - **Agent registry** — config-driven list of agent profiles (name, command,
     args, env, working directory), persisted via EF Core against SQLite
     (`Anywhere.Models`), user-editable without a rebuild. Exposed through thin
     concrete repository classes (`ProfileRepository`, etc.) — no repository
     interfaces, since there's one data store and the project's tests already
     favor real backends over mocks/fakes.
   - **UI layer** — `MainForm` composes `Anywhere.Controls` widgets: chat
     transcript (one `MarkdownLabel`-based bubble per message), input textbox, a
     docked permission/diff panel above the input, and an agent/session picker.

## Data Model (SQLite via EF Core)

- `AgentProfile` — `Id`, `Name`, `Command`, `Args` (`string[]`, stored as a JSON
  column via an EF Core value converter), `Env` (`Dictionary<string,string>`,
  same conversion), `WorkingDir`, `CreatedAt`.
- `Session` — `Id`, `ProfileId` (FK), `WorkingDir`, `CreatedAt`.
- `Message` — `Id`, `SessionId` (FK), `Role` (`user`/`agent`/`system`),
  `Content` (text), `ToolCallJson` (nullable, JSON blob for tool-call metadata),
  `CreatedAt`.

All three are `DbSet<T>`s on one `AnywhereDbContext`. Schema is managed by EF
Core Migrations (generated into `Anywhere.Models`, applied via
`Database.Migrate()` at app startup) — not `PRAGMA user_version`/hand-rolled
`CREATE TABLE IF NOT EXISTS`. `Database.EnsureCreated()` is used only in test
fixtures that want a fast, throwaway schema; it's never used at production
startup since it bypasses the migration history.

## UI Behavior

- **Permission/diff panel**: `TableLayoutPanel`-based, docked `Bottom`, sits
  above the input box. Populates when the agent sends
  `session/request_permission`. For file-write/edit tool calls, renders an
  inline diff (old vs. new, colored line-by-line) alongside
  Allow/Allow-always/Deny buttons. Collapses to zero height when nothing is
  pending. No timeout — waits indefinitely, matching editor behavior (ACP has no
  built-in timeout).
- **Agent crash/exit**: surfaced as a system message in the transcript with a
  "Restart agent" button.
- **Malformed JSON-RPC from agent**: logged to a debug panel, never silently
  swallowed.

## Error Handling

- Subprocess crash → system message + restart button, no auto-retry loop.
- Protocol parse errors → visible in debug panel; session continues if the
  connection itself is still alive.
- Permission requests → block only that session's turn; other sessions (if any
  exist later) are unaffected.

## Testing Strategy

- Unit tests: EF Core persistence layer (CRUD for profiles/sessions/messages)
  against a real, temp-file SQLite database via `AnywhereDbContext` (using
  `Database.EnsureCreated()` for test-only schema setup), agent-profile
  parsing/validation.
- Integration test: a minimal fake agent (script speaking ACP over stdio) drives
  the real protocol layer through initialize → prompt → response, and through a
  permission-request round trip.
- UI: manual smoke test only in v1 (no automation framework).
