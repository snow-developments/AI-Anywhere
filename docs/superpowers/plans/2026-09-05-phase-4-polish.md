# ACP WinForms Client — Phase 4: Crash Recovery, Profile Management, Visual Styling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Take the working-but-rough app from Phase 3 to the full v1 spec:
visible agent-crash recovery and a protocol debug log, a real agent-profile
management UI replacing the hardcoded test profile, and Fluent/WinUI3-styled
visuals.

**Architecture:** `DebugLogPanel` (new, `Anywhere.Controls`) and a "Restart
agent" affordance hook into `AgentProcess`'s existing `OnAgentExited` event
(Phase 2) plus a new `OnProtocolWarning` event. `AgentProfileForm` (new,
`Anywhere`) is a modal `Form` over `ProfileRepository` (Phase 1) that replaces
`MainForm`'s Phase 3 hardcoded profile with a user-selected one. Visual styling
applies `WinForms.Fluent.UI` (already referenced by `Anywhere.Controls` since
Phase 1 scaffolding) to `MainForm`.

**Tech Stack:** .NET 10, WinForms, `WinForms.Fluent.UI` (NuGet, MIT —
Fluent/WinUI3-styled controls; already a project dependency of
`Anywhere.Controls`, not newly added here).

**Spec:**
[docs/superpowers/specs/2026-09-04-design.md](../specs/2026-09-04-design.md)

**Plan series:** This is Phase 4 of 4 (final). Requires Phases 1-3 complete. See
also:

- Phase 1 —
  [2026-09-05-anywhere-phase1-foundation.md](2026-09-05-anywhere-phase1-foundation.md)
  (scaffolding, EF Core persistence, canonical file structure)
- Phase 2 —
  [2026-09-05-anywhere-phase2-protocol-and-controls.md](2026-09-05-anywhere-phase2-protocol-and-controls.md)
  (MarkdownLabel control, ACP agent process wrapper)
- Phase 3 —
  [2026-09-05-anywhere-phase3-core-ui.md](2026-09-05-anywhere-phase3-core-ui.md)
  (chat transcript UI, permission/diff panel)

## Global Constraints

Full project-wide constraints are listed in
[Phase 1's Global Constraints](2026-09-05-anywhere-phase1-foundation.md#global-constraints).
The one that bound this phase's original draft and has been corrected here:

- Visual styling: `WinForms.Fluent.UI` NuGet package (referenced only by
  `Anywhere.Controls`) supplies Fluent/WinUI3-styled controls — it is an "add
  new controls" library, not a full app-wide visual-style override, so default
  WinForms controls not replaced by a Fluent equivalent keep the default Win32
  look. **Do not add `MaterialSkin.2` or another full-override library.** (The
  single-repo plan this series replaces had drifted to `MaterialSkin.2` in its
  Task 10, contradicting the spec's explicit choice; Task 10 below has been
  rewritten to use `WinForms.Fluent.UI` instead — see Self-Review Notes.)
- v1 scope excludes agent-specific presets, multiple concurrent session tabs,
  slash-command UI, MCP config UI, attachments, plan-mode UI, and a WinForms UI
  automation framework — none of that is introduced here either.

## File Structure

Files this phase adds/modifies, within the canonical structure defined in
[Phase 1](2026-09-05-anywhere-phase1-foundation.md#file-structure):

```
src/
  Anywhere.Controls/
    DebugLogPanel.cs            (new — in Anywhere.Controls, not Anywhere/Controls/)
  Anywhere/
    Persistence/
      ProfileRepository.cs      (modified — add UpdateAsync/DeleteAsync)
    Agents/
      AgentProcess.cs           (modified — add OnProtocolWarning)
      AgentProfileParser.cs     (new)
    AgentProfileForm.cs         (new)
    AgentProfileForm.Designer.cs (new)
    MainForm.cs                 (modified)
    MainForm.Designer.cs        (modified)
  Anywhere.Tests/
    ProfileRepositoryTests.cs   (modified — add update/delete cases)
    AgentProfileParsingTests.cs (new)
```

**Interfaces summary added by this phase (in addition to Phases 1-3's):**

- `AgentProcess.OnProtocolWarning : event Action<string>` (added to the Phase 2
  type).
- `DebugLogPanel : TextBox` — `AppendLine(string line)`.
- `ProfileRepository.UpdateAsync(AgentProfile p) : Task`,
  `ProfileRepository.DeleteAsync(int id) : Task` (added to the Phase 1 type).
- `AgentProfileParser` (`Anywhere.Agents`, static):
  `ParseArgs(string raw) : string[]`.
- `AgentProfileForm : Form` — modal, backed by `ProfileRepository` (Phase 1),
  supports add/edit/delete.

---

## Task 8: Agent crash recovery and debug log

**Files:**

- Create: `src/Anywhere.Controls/DebugLogPanel.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/Agents/AgentProcess.cs`

**Interfaces:**

- Consumes: `AgentProcess.OnAgentExited` (Phase 2, Task 5),
  `Anywhere.Design.Spacing` (Phase 1, Task 1b).
- Produces: a transcript system message + "Restart agent" button on crash; a
  debug panel (toggle-visible) logging malformed JSON-RPC traffic.

- [ ] **Step 1: Add a malformed-message event to `AgentProcess`**

Modify `src/Anywhere/Agents/AgentProcess.cs` to add
`event Action<string> OnProtocolWarning`, raised whenever the underlying
`acp-csharp` connection fails to parse an incoming message (wrap the relevant
try/catch around the library's read loop, confirmed from Phase 2 Task 5 Step 3's
API read).

- [ ] **Step 2: Implement `DebugLogPanel`**

**Note on project placement:** this file goes in `Anywhere.Controls` (referenced
by `Anywhere`), not `Anywhere/Controls/` inside the app project — matching this
phase's File Structure section and the spec's Architecture section, both of
which list `DebugLogPanel` as one of `Anywhere.Controls`'s widgets alongside
`ChatTranscriptPanel`/`PermissionDiffPanel`.

```csharp
// src/Anywhere.Controls/DebugLogPanel.cs
using Anywhere.Design;

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
        Font = Typography.Monospace;
        Margin = new Padding(Spacing.Small);
    }

    public void AppendLine(string line) => AppendText(line + Environment.NewLine);
}
```

(Use whichever `Typography`/`Spacing` member names Phase 1's Task 1b actually
defined — `Typography.Monospace`/`Spacing.Small` above are illustrative; confirm
the real names from `src/Anywhere.Design/Typography.cs`/`Spacing.cs` before
writing this.)

- [ ] **Step 3: Wire crash + protocol-warning handling into MainForm**

In `MainForm.cs`: subscribe `AgentProcess.OnAgentExited` to append a system-role
message to `ChatTranscriptPanel` reading `"Agent exited."` plus a `Button`
labeled "Restart agent" that re-runs the Phase 3 Task 6 Step 3 startup logic;
subscribe `OnProtocolWarning` to call `_debugLogPanel.AppendLine(...)`. Add a
menu item or keyboard shortcut (e.g. `Ctrl+D`) toggling
`_debugLogPanel.Visible`.

- [ ] **Step 4: Manual verification**

Run the app, kill the agent subprocess externally (e.g. via Task Manager) while
connected. Expected: transcript shows "Agent exited." with a working "Restart
agent" button that re-establishes the connection.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: add agent crash recovery and protocol debug log"
```

---

## Task 9: Agent profile management UI

**Files:**

- Modify: `src/Anywhere/Persistence/ProfileRepository.cs` (add
  `UpdateAsync`/`DeleteAsync`)
- Create: `src/Anywhere/Agents/AgentProfileParser.cs`
- Create: `src/Anywhere/AgentProfileForm.cs`
- Create: `src/Anywhere/AgentProfileForm.Designer.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/MainForm.Designer.cs`
- Test: `src/Anywhere.Tests/ProfileRepositoryTests.cs` (add update/delete cases)
- Test: `src/Anywhere.Tests/AgentProfileParsingTests.cs`

**Interfaces:**

- Consumes: `ProfileRepository` (Phase 1, Task 2).
- Produces: `ProfileRepository.UpdateAsync(AgentProfile p) : Task`,
  `ProfileRepository.DeleteAsync(int id) : Task`,
  `AgentProfileParser.ParseArgs(string raw) : string[]`, and a menu item opening
  a modal form to add/edit/delete/list agent profiles, replacing the Phase 3
  Task 6 Step 3 hardcoded profile with a user-selected one from a dropdown in
  `MainForm`.

**2026-09-06 fix (review finding #2):** the spec's Scope
(`docs/superpowers/specs/2026-09-04-design.md:12`) requires an "editable
agent-profile list" — the original draft of this task only wired "Save" to
`InsertAsync`, with no way to modify or remove an existing profile. This
revision adds real update/delete.

- [ ] **Step 1: Write the failing test for `UpdateAsync`/`DeleteAsync`**

Add to `src/Anywhere.Tests/ProfileRepositoryTests.cs` (from Phase 1, Task 2):

```csharp
[Fact]
public async Task UpdateAsync_then_GetAsync_returns_the_updated_fields()
{
    var repo = new ProfileRepository(_db);
    var id = await repo.InsertAsync(new AgentProfile
    {
        Name = "Original",
        Command = "cmd1",
        Args = Array.Empty<string>(),
        Env = new System.Collections.Generic.Dictionary<string, string>(),
        WorkingDir = @"C:\work",
    });

    var toUpdate = await repo.GetAsync(id);
    toUpdate!.Name = "Renamed";
    toUpdate.Command = "cmd2";
    await repo.UpdateAsync(toUpdate);

    var fetched = await repo.GetAsync(id);
    Assert.Equal("Renamed", fetched!.Name);
    Assert.Equal("cmd2", fetched.Command);
}

[Fact]
public async Task DeleteAsync_removes_the_profile()
{
    var repo = new ProfileRepository(_db);
    var id = await repo.InsertAsync(new AgentProfile
    {
        Name = "Temp",
        Command = "cmd",
        Args = Array.Empty<string>(),
        Env = new System.Collections.Generic.Dictionary<string, string>(),
        WorkingDir = @"C:\work",
    });

    await repo.DeleteAsync(id);

    Assert.Null(await repo.GetAsync(id));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
`dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter ProfileRepositoryTests`
Expected: FAIL (compile error — `UpdateAsync`/`DeleteAsync` not defined).

- [ ] **Step 3: Implement `UpdateAsync`/`DeleteAsync` on `ProfileRepository`**

Add to `src/Anywhere/Persistence/ProfileRepository.cs` (from Phase 1, Task 2):

```csharp
public async Task UpdateAsync(AgentProfile profile)
{
    _db.Profiles.Update(profile);
    await _db.SaveChangesAsync();
}

public async Task DeleteAsync(int id)
{
    var profile = await _db.Profiles.FindAsync(id);
    if (profile is null) return;
    _db.Profiles.Remove(profile);
    await _db.SaveChangesAsync();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
`dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter ProfileRepositoryTests`
Expected: PASS.

- [ ] **Step 5: Write the failing test for `AgentProfileParser`**

The form's Args field is free-text, comma-separated — this is the "agent-profile
parsing/validation" the spec's Testing Strategy
(`docs/superpowers/specs/2026-09-04-design.md:67`) calls for, and the reason
`AgentProfileParsingTests.cs` exists in Phase 1's file map without a task of its
own (review finding #5 — this task is where it's implemented).

```csharp
// src/Anywhere.Tests/AgentProfileParsingTests.cs
using Anywhere.Agents;
using Xunit;

public class AgentProfileParsingTests
{
    [Fact]
    public void ParseArgs_splits_on_commas_and_trims_whitespace()
    {
        var args = AgentProfileParser.ParseArgs(" --stdio, --verbose ,--port 4000 ");

        Assert.Equal(new[] { "--stdio", "--verbose", "--port 4000" }, args);
    }

    [Fact]
    public void ParseArgs_returns_empty_array_for_blank_input()
    {
        Assert.Empty(AgentProfileParser.ParseArgs(""));
        Assert.Empty(AgentProfileParser.ParseArgs("   "));
    }

    [Fact]
    public void ParseArgs_skips_empty_entries_from_consecutive_commas()
    {
        var args = AgentProfileParser.ParseArgs("--stdio,,--verbose");

        Assert.Equal(new[] { "--stdio", "--verbose" }, args);
    }
}
```

- [ ] **Step 6: Run test to verify it fails**

Run:
`dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter AgentProfileParsingTests`
Expected: FAIL (compile error — `AgentProfileParser` not defined).

- [ ] **Step 7: Implement `AgentProfileParser`**

```csharp
// src/Anywhere/Agents/AgentProfileParser.cs
namespace Anywhere.Agents;

public static class AgentProfileParser
{
    public static string[] ParseArgs(string raw)
        => raw.Split(',')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();
}
```

- [ ] **Step 8: Run test to verify it passes**

Run:
`dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter AgentProfileParsingTests`
Expected: PASS.

- [ ] **Step 9: Implement `AgentProfileForm` with add and edit modes**

Build a modal `Form` with: a `ListBox` of existing profiles (populated via
`ProfileRepository.ListAllAsync()`), input fields (`TextBox` for Name, Command,
Args (comma-separated, parsed via `AgentProfileParser.ParseArgs`), WorkingDir),
a "Save" button, a "Delete" button (enabled only when a profile is selected),
and a "New" button that clears the fields and selection.

Track the currently-loaded profile's `Id` in a nullable field (e.g.
`_editingId`, `null` while composing a new profile). Wire the `ListBox`'s
`SelectedIndexChanged` to populate the text fields from the selected
`AgentProfile` and set `_editingId` to its `Id`. "Save" calls
`ProfileRepository.InsertAsync(...)` when `_editingId is null`, or builds an
`AgentProfile` with `Id = _editingId.Value` and calls
`ProfileRepository.UpdateAsync(...)` otherwise. "Delete" calls
`ProfileRepository.DeleteAsync(_editingId.Value)` (disabled when
`_editingId is null`) and refreshes the `ListBox`.

- [ ] **Step 10: Add a profile picker to MainForm**

In `MainForm.Designer.cs`, add a `ComboBox` (`_profilePicker`) docked `Top`,
populated from `ProfileRepository.ListAllAsync()` on load, plus a "Manage
Profiles..." menu item opening `AgentProfileForm`. In `MainForm.cs`, replace the
Phase 3 Task 6 Step 3 hardcoded profile with
`(AgentProfile)_profilePicker.SelectedItem`, and refresh `_profilePicker`'s
items when `AgentProfileForm` closes (profiles may have been
added/edited/deleted).

- [ ] **Step 11: Manual verification**

Run the app, open "Manage Profiles...", add a profile for a real ACP agent (e.g.
Claude Code's `claude-code-acp` binary if installed locally), select it from the
dropdown, send a prompt. Then reopen "Manage Profiles...", select that profile
from its `ListBox`, change its Command, click Save, and confirm
`ProfileRepository.GetAsync` reflects the change rather than creating a
duplicate row. Finally select it again and click Delete, and confirm it
disappears from both the `ListBox` and `MainForm`'s `_profilePicker`. Expected:
real agent responds in the transcript; edit updates the existing row in place;
delete removes it.

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "feat: add agent profile management UI with edit/delete and args parsing"
```

---

## Task 10: Apply WinForms.Fluent.UI visual styling

**Files:**

- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/MainForm.Designer.cs`

**Interfaces:**

- Consumes: nothing new (applies to the existing `MainForm` and its controls
  from Phase 1 Task 1 and Tasks 6-9).
- Produces: a `MainForm` and its child controls rendered with
  `WinForms.Fluent.UI`'s Fluent/WinUI3-styled equivalents, instead of default
  Win32 visual styles, for the controls the library covers — default WinForms
  controls with no Fluent equivalent keep their default Win32 look (this is an
  "add new controls" library, not an app-wide visual-style override; see Global
  Constraints).

- [ ] **Step 1: Confirm the `WinForms.Fluent.UI` package reference**

`Anywhere.Controls.csproj` should already reference `WinForms.Fluent.UI` from
Phase 1's scaffolding (it's the library
`ChatTranscriptPanel`/`PermissionDiffPanel`/`DebugLogPanel` were built against
per the spec). Run:

```bash
dotnet list src/Anywhere.Controls/Anywhere.Controls.csproj package
```

Expected: `WinForms.Fluent.UI` appears in the output. If it's missing, add it
before continuing:

```bash
dotnet add src/Anywhere.Controls/Anywhere.Controls.csproj package WinForms.Fluent.UI
```

- [ ] **Step 2: Read WinForms.Fluent.UI's setup docs before wiring it in**

Read the `WinForms.Fluent.UI` README/NuGet listing for its exact initialization
and per-control API (which Fluent control classes exist — e.g. a Fluent-styled
`Button`/`TextBox` replacement — whether it needs an app-level initialization
call in `Program.cs`, and whether `MainForm` needs a specific base class or a
theme/accent-color setter) — do not guess the API surface. This mirrors how
Phase 2 Task 5 required reading `acp-csharp`'s real API before coding against
it.

- [ ] **Step 3: Apply the confirmed styling API to MainForm, using
      `Anywhere.Design`'s palette**

Using the exact API confirmed in Step 2: swap `MainForm`'s Win32 controls for
their `WinForms.Fluent.UI` equivalents where the library provides one (buttons,
the input textbox, etc.), and add any required one-time initialization (in
`Program.cs` before `Application.Run`, or in `MainForm`'s constructor, per what
Step 2 found). Wherever that initialization API accepts an accent/theme color (a
`MaterialSkin`-style `ColorScheme`-equivalent, if `WinForms.Fluent.UI` exposes
one), pass `Anywhere.Design.Colors`' accent constant rather than a literal —
this is `Anywhere.Design`'s one real integration point in the WinForms UI itself
(its other consumers,
`ChatTranscriptPanel`/`PermissionDiffPanel`/`DebugLogPanel`, use its
`Spacing`/`Typography` members directly; see Phase 1 Task 1b and Phase 3 Tasks
6-7). Controls the library has no equivalent for — e.g. `ChatTranscriptPanel`'s
`FlowLayoutPanel` base, `PermissionDiffPanel`'s `TableLayoutPanel` base — are
left as-is per the Global Constraints note that this is an additive styling
library, not an override.

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Anywhere/Anywhere.csproj` Expected: window opens
with `WinForms.Fluent.UI`'s styled controls in place of the default Win32 look
for the controls it covers; existing chat/permission-panel/profile-management
functionality from Tasks 6-9 still works unchanged.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: apply WinForms.Fluent.UI visual styling"
```

---

## Self-Review Notes

- **Spec coverage:** agent-agnostic, _editable_ profile management (Task 9),
  crash recovery + debug log (Task 8), and Fluent/WinUI3 visual styling using
  `Anywhere.Design`'s palette (Task 10) — all covered per spec. This phase
  completes v1 as scoped; v1.1 presets, multi-tab sessions, and other
  out-of-scope items remain untouched.
- **2026-09-05 fix — Task 10 rewritten from `MaterialSkin.2` to
  `WinForms.Fluent.UI`:** the single-file plan this series replaces had a Task
  10 that added `MaterialSkin.2` and made `MainForm` inherit
  `MaterialSkin.Controls.MaterialForm`, directly contradicting the spec's
  explicit choice of `WinForms.Fluent.UI` over `MaterialSkin.2` (the spec calls
  out that trade-off by name — an "add new controls" library was chosen over a
  full app-wide visual override specifically to avoid a MaterialSkin-style
  takeover). This plan corrects that: Task 10 no longer adds a package (it's
  already a Phase 1 dependency of `Anywhere.Controls`), doesn't change
  `MainForm`'s base class, and treats unstyled default-Win32 controls as the
  expected, intentional result for anything Fluent has no equivalent for.
- **2026-09-06 fixes from `2026-09-05-anywhere-phases.review.md`:**
  - _Finding #2 (High):_ Task 9 was add-only, contradicting the spec's "editable
    agent-profile list." Added `ProfileRepository.UpdateAsync`/`DeleteAsync`
    plus tests, and gave `AgentProfileForm` real edit/delete via a tracked
    `_editingId`.
  - _Finding #3 (High):_ Task 8 told the implementer to create
    `DebugLogPanel.cs` under `src/Anywhere/Controls/`, contradicting this file's
    own File Structure section and the spec's Architecture section (both place
    it in `Anywhere.Controls`). Fixed the file path, the code block's path
    comment, and added an explicit note on why.
  - _Finding #5 (Medium):_ `AgentProfileParsingTests.cs` was listed in Phase 1's
    file map with no task producing it. Added `AgentProfileParser.ParseArgs`
    (Task 9, Steps 5-8) as the parsing logic the spec's Testing Strategy bullet
    was referring to, with real tests.
  - _Finding #4 (Medium), partial:_ Task 8's `DebugLogPanel` now uses
    `Anywhere.Design.Spacing`/`Typography`, and Task 10 passes
    `Anywhere.Design.Colors`' accent value into `WinForms.Fluent.UI`'s theme API
    (once Step 2 confirms that API accepts one). The rest of this finding's fix
    lives in Phase 1 (the token definitions) and Phase 3
    (`ChatTranscriptPanel`/`PermissionDiffPanel`).
- **Unconfirmed external API surface:** `WinForms.Fluent.UI`'s exact control
  classes and initialization API, including whether/how it accepts a theme color
  (Task 10 Step 2) — flagged inline rather than guessed, same pattern as
  `acp-csharp` in Phase 2.
- **Type consistency check:** `AgentProcess.OnProtocolWarning` (Task 8),
  `AgentProfileParser.ParseArgs` and
  `ProfileRepository.UpdateAsync`/`DeleteAsync` (Task 9) match the signatures
  introduced here consistently across this file's Interfaces summary and code
  blocks; `AgentProfileForm`'s use of
  `ProfileRepository.InsertAsync(AgentProfile)`/`ListAllAsync()` (Task 9) still
  match the signatures defined in Phase 1 (Task 2) and Phase 2 (Task 5) exactly
  — no renamed methods.

## Implementation Notes (2026-09-05)

Notes from the upstream work this plan builds on (Phases 1–3) that affect exact
code-block shape, plus gotchas worth recording before this plan runs.

- **`Typography.Monospace` is a _method_, not a property.** The Task 8 Step 2
  code block uses `Font = Typography.Monospace` — that won't compile because the
  actual definition in `src/Anywhere.Design/Typography.cs` is
  `public static Font Monospace() => new("Cascadia Mono", 9f)`. Change the
  example to `Font = Typography.Monospace()`. Same fix applies wherever
  `Typography.Body` would be used.
- **`OnProtocolWarning` cannot simply hook the library's read-loop try/catch.**
  The `acp-csharp` `JsonRpcEndpoint.ReadMessagesAsync` swallows parse exceptions
  internally and routes them to its `errorWriteFunc` callback (which
  `ClientSideConnection` wires to a no-op). `AgentProcess` has no direct
  visibility into malformed messages from the upstream code alone. Three
  options, ordered by recommendation:
  1. **Hook stderr.** `Process.StandardError` is already redirected in
     `AgentProcess.StartAsync` but never read. Read it asynchronously and raise
     `OnProtocolWarning(exitCode, stderrLine)` for each non-empty line. This
     surfaces both the agent's own stderr (which is usually the interesting one)
     and any future `errorWriteFunc` plumbing from the library.
  2. **Wire a custom `errorWriteFunc` into `ClientSideConnection`** — that
     constructor only exposes `reader`/`writer`; the internal `JsonRpcEndpoint`
     is constructed with a third `errorWriteFunc` parameter the wrapper doesn't
     expose. Without forking the library this path is closed; don't try it.
  3. **Skip `OnProtocolWarning` for v1 and rely on `OnAgentExited` only.** Add
     the event for parity but never raise it. Simpler; loses the "malformed
     message" warning but keeps the restart path. Acceptable if the debug log
     ends up empty during normal operation — which it usually will, since real
     agents don't emit malformed JSON.
- **`ProfileRepository.UpdateAsync(profile)` works with an
  `AsNoTracking()`-loaded entity.** `GetAsync` returns a detached `AgentProfile`
  because it uses `AsNoTracking()`. `Update(profile)` on a detached instance
  with its `Id` set is a valid EF Core pattern and round-trips the
  JSON-serialized `Args` / `Env` properties unchanged — those are mapped via
  `HasConversion(... JsonSerializer.Serialize ...)` in
  `AnywhereDbContext.OnModelCreating`. No extra plumbing required; just keep
  `Id` populated on the entity passed to `UpdateAsync`.
- **`AgentProfileForm`'s `Args` field is free text** — wrap
  `AgentProfileParser.ParseArgs` (Task 9 Steps 5–8) around the value going into
  `Args`, and around the value coming out when populating the `TextBox` (join
  with `", "` so the user sees a re-editable string). Without round-tripping,
  editing an existing profile that has spaces in args (`"--port 4000"`) corrupts
  the saved profile.
- **`AgentProfileForm`'s `Env` field** isn't in this plan; the spec is silent on
  UI for env vars, and the `Dictionary<string, string>` on `AgentProfile` is
  fine to leave unexposed for v1. Don't add it as a free-text `TextBox` —
  newline-separated `KEY=VALUE` would silently drop malformed entries with no UI
  feedback.
- **`OnAgentExited` was wired in `AgentProcess.Dispose` to the `Process.Exited`
  event** during Phase 2's implementation — the handler fires with
  `"exit code {N}"`. The Task 8 Step 3 code expects a bare `OnAgentExited`
  callback (no argument). Replace the signature with
  `event Action<string>? OnAgentExited` in this plan's interfaces summary if it
  isn't already — the Phase 2 implementation actually uses `Action<string>`
  carrying the exit-code message; the original Phase 2 plan documented it as
  `Action<string>` as well, so no rename is required.
- **`AnywhereDbContext.DefaultDbPath()` already exists** from Phase 1, and is
  the canonical path. Phase 4's `MainForm` startup logic should reuse it (do not
  invent a second path).
- **`WinForms.Fluent.UI` package id and API are not yet verified.** Task 10 Step
  1 / Step 2 ask the implementer to verify them — keep that gate. As of Phase 2
  the package was never installed (no usage in the built codebase), so the
  implementer is starting from a clean slate for this package, not from an
  existing reference.
- **`SplashForm`** (the user-requested addition in
  `2026-09-05-splash-form-and-mainform-rename.md`) renames `MainForm` to
  `ConversationForm`, since renamed again to `ChatForm`; if this plan runs after
  those, every reference to `MainForm.cs` / `MainForm.Designer.cs` below should
  be read as `ChatForm.cs` / `ChatForm.Designer.cs`. The Tasks 8-10 File
  Structure sections list `MainForm.cs`/`MainForm.Designer.cs` (not updated for
  the renames) — substitute the current file names as needed.
- **`MarkdownLabel` text rendering is synchronous on the UI thread**, so Task
  8's `DebugLogPanel.AppendLine` is safe to call directly from
  `OnProtocolWarning`/`OnAgentExited` only when those handlers marshal to the UI
  thread first (see Phase 3 implementation notes). Use
  `_debugLogPanel.BeginInvoke(() => _debugLogPanel.AppendLine(line))` if the
  raising thread is anything other than the UI thread.
