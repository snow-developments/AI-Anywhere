# ACP WinForms Client — Phase 3: Core Chat UI + Permission Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire everything built in Phases 1-2 into a running, end-to-end usable app: typing a prompt sends it to a (hardcoded, for now) agent profile, the response renders as a markdown bubble and persists to SQLite, and file-write/edit permission requests surface as an inline docked diff panel instead of blocking silently.

**Architecture:** `ChatTranscriptPanel` (new, `Anywhere.Controls`) stacks `MarkdownLabel` bubbles and is composed into `MainForm` alongside a docked-bottom `PermissionDiffPanel` (new, `Anywhere.Controls`) that shows/hides based on `AgentProcess.OnPermissionRequested`. `MainForm` (in `Anywhere`) is the wiring layer: it owns the `AnywhereDbContext`/repositories from Phase 1 and the `AgentProcess` from Phase 2, and mediates between them and the two new controls.

**Tech Stack:** .NET 10, WinForms (`FlowLayoutPanel`, `TableLayoutPanel`), the `AgentProcess`/`MarkdownLabel`/persistence building blocks from Phases 1-2.

**Spec:** [docs/superpowers/specs/2026-09-04-design.md](../specs/2026-09-04-design.md)

**Plan series:** This is Phase 3 of 4. Requires Phases 1-2 complete. See also:
- Phase 1 — [2026-09-05-anywhere-phase1-foundation.md](2026-09-05-anywhere-phase1-foundation.md) (scaffolding, EF Core persistence, canonical file structure)
- Phase 2 — [2026-09-05-anywhere-phase2-protocol-and-controls.md](2026-09-05-anywhere-phase2-protocol-and-controls.md) (MarkdownLabel control, ACP agent process wrapper)
- Phase 4 — [2026-09-05-anywhere-phase4-polish.md](2026-09-05-anywhere-phase4-polish.md) (crash recovery, profile management UI, visual styling)

## Global Constraints

Full project-wide constraints are listed in [Phase 1's Global Constraints](2026-09-05-anywhere-phase1-foundation.md#global-constraints). The ones that bind this phase specifically:

- Persistence: production startup calls `AnywhereDbContext.Database.Migrate()` — never `EnsureCreated()` (that stays test-only, see Phase 1).
- No permission-request timeouts — the panel built in this phase waits indefinitely, matching editor behavior (ACP itself has no timeout).
- v1 has no multi-tab sessions, slash commands, MCP config UI, attachments, or agent-specific presets — this phase wires a single hardcoded profile; Phase 4 replaces it with a user-selected one.

## File Structure

Files this phase adds/modifies, within the canonical structure defined in [Phase 1](2026-09-05-anywhere-phase1-foundation.md#file-structure):

```
src/
  Anywhere.Controls/
    ChatTranscriptPanel.cs     (new — scrollable stack of message bubbles)
    PermissionDiffPanel.cs     (new — docked bottom panel, TableLayoutPanel-based)
  Anywhere/
    MainForm.cs                (modified — wiring)
    MainForm.Designer.cs       (modified — layout)
```

**Interfaces summary added by this phase (in addition to Phases 1-2's):**
- `ChatTranscriptPanel : FlowLayoutPanel` — `AppendMessage(string role, string markdown)`, `StartAgentMessage()`, `AppendToCurrentAgentMessage(string chunk)`.
- `PermissionDiffPanel : TableLayoutPanel` — `event Action<string, PermissionOutcome>? OutcomeChosen`, `ShowRequest(PermissionRequest request)`.

**2026-09-06 fixes from `2026-09-05-anywhere-phases.review.md`:**
- *Finding #1 (High):* Task 6 now consumes Phase 2's `AgentProcess.OnResponseChunk` incrementally via two new `ChatTranscriptPanel` methods, instead of waiting for `SendPromptAsync`'s `Task` to complete and appending the whole response as one bubble.
- *Finding #4 (Medium), continued from Phase 1:* `ChatTranscriptPanel` (Task 6) and `PermissionDiffPanel` (Task 7) now pull their margins/sizing from `Anywhere.Design.Spacing` (Phase 1, Task 1b) instead of literal `Padding(8)`/`Height = 200` values.

---

## Task 6: Chat transcript UI and MainForm wiring

**Files:**
- Create: `src/Anywhere.Controls/ChatTranscriptPanel.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/MainForm.Designer.cs`

**Interfaces:**
- Consumes: `MarkdownLabel` (Phase 2, Task 4), `AgentProcess` including `OnResponseChunk` (Phase 2, Task 5), `MessageRepository`/`SessionRepository`/`ProfileRepository` (Phase 1, Tasks 2-3), `Anywhere.Design.Spacing` (Phase 1, Task 1b).
- Produces: a running app where typing a prompt and pressing Enter sends it to a configured agent profile and displays the streamed response incrementally as a growing markdown bubble, persisted to SQLite once the turn completes.

- [ ] **Step 1: Implement `ChatTranscriptPanel`, appending incrementally as chunks arrive**

`AppendMessage` still exists for one-shot messages (the user's own prompt, and Phase 4's system messages like "Agent exited."). For the agent's streamed reply, `StartAgentMessage` creates an empty bubble up front and `AppendToCurrentAgentMessage` grows it as `AgentProcess.OnResponseChunk` fires — this is finding #1 from the review: the original draft only had `AppendMessage`, called once after the whole response arrived, contradicting the spec's "stream agent responses" requirement.

```csharp
// src/Anywhere.Controls/ChatTranscriptPanel.cs
using Anywhere.Design;

namespace Anywhere.Controls;

public sealed class ChatTranscriptPanel : FlowLayoutPanel
{
    private MarkdownLabel? _currentAgentBubble;
    private string _currentAgentText = string.Empty;

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
            Width = ClientSize.Width - Spacing.Medium,
            Margin = new Padding(Spacing.Small),
        };
        Controls.Add(bubble);
        ScrollControlIntoView(bubble);
    }

    public void StartAgentMessage()
    {
        _currentAgentText = string.Empty;
        _currentAgentBubble = new MarkdownLabel
        {
            Text = string.Empty,
            Width = ClientSize.Width - Spacing.Medium,
            Margin = new Padding(Spacing.Small),
        };
        Controls.Add(_currentAgentBubble);
        ScrollControlIntoView(_currentAgentBubble);
    }

    public void AppendToCurrentAgentMessage(string chunk)
    {
        if (_currentAgentBubble is null) StartAgentMessage();
        _currentAgentText += chunk;
        _currentAgentBubble!.Text = _currentAgentText;
        ScrollControlIntoView(_currentAgentBubble);
    }
}
```

(Adjust the property used to set markdown content — `Text` vs. whatever Phase 2 Task 4 confirmed as the real property name — and `Spacing`'s exact member names to match Phase 1 Task 1b.)

- [ ] **Step 2: Wire MainForm layout — transcript, input box, send button**

Edit `src/Anywhere/MainForm.Designer.cs` to add: a `ChatTranscriptPanel` docked `Fill`, a `TextBox` (`_inputBox`) docked `Bottom` inside a `Panel`, and hook `_inputBox.KeyDown` for Enter-to-send in `MainForm.cs`.

- [ ] **Step 3: Wire MainForm to start a hardcoded test profile and relay messages incrementally**

In `MainForm.cs`, on load: construct `AnywhereDbContext` at `AnywhereDbContext.DefaultDbPath()`, call `Database.Migrate()` (applies any pending EF Core migrations — never `EnsureCreated()` here, see Global Constraints), construct one default `AgentProfile` (a placeholder "echo" command for manual testing), start an `AgentProcess`, and subscribe `AgentProcess.OnResponseChunk` to call `_transcript.AppendToCurrentAgentMessage(chunk)`.

On send: append the user's message via `_transcript.AppendMessage("user", text)` and persist it via `MessageRepository`, call `_transcript.StartAgentMessage()`, then `await SendPromptAsync(text)` — the chunk event fires the incremental appends while this is in flight — and once it completes, persist the final `PromptResult.Content` via `MessageRepository` (the transcript bubble is already showing it from the accumulated chunks; the persisted row is what `MessageRepository.ListForSessionAsync` will later replay for history).

- [ ] **Step 4: Manual verification**

Run: `dotnet run --project src/Anywhere/Anywhere.csproj`
Expected: window opens, typing text and pressing Enter shows the user's message immediately and (once a real streaming agent profile is configured) the agent's reply growing incrementally as chunks arrive, rendered as markdown.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: wire streaming chat transcript UI to agent process and persistence"
```

---

## Task 7: Permission/diff panel

**Files:**
- Create: `src/Anywhere.Controls/PermissionDiffPanel.cs`
- Modify: `src/Anywhere/MainForm.cs`
- Modify: `src/Anywhere/MainForm.Designer.cs`

**Note on project placement:** `PermissionRequest`/`PermissionOutcome` are NOT created in this task — they were already created in Phase 2, Task 5 (in `Anywhere.Controls`, not `Anywhere/Agents/`, specifically so this task's `PermissionDiffPanel` could consume them without a circular project reference — `Anywhere` → `Anywhere.Controls` is the only allowed direction). This task just implements `PermissionDiffPanel` against those existing types.

**Interfaces:**
- Consumes: `AgentProcess.OnPermissionRequested`/`RespondToPermissionAsync` and `Anywhere.Controls.PermissionRequest`/`PermissionOutcome` (Phase 2, Task 5).
- Produces: a docked panel above the input box that shows pending permission requests with Allow/Allow-always/Deny buttons and an inline diff view, collapsing to zero height when idle.

- [ ] **Step 1: Implement `PermissionDiffPanel`**

```csharp
// src/Anywhere.Controls/PermissionDiffPanel.cs
using Anywhere.Design;

namespace Anywhere.Controls;

public sealed class PermissionDiffPanel : TableLayoutPanel
{
    private const int ExpandedHeight = 200;

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
        Padding = new Padding(Spacing.Small);

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
        Height = ExpandedHeight;
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

(`ExpandedHeight` stays a local constant rather than an `Anywhere.Design.Spacing` member — it's a one-off panel height, not a reusable spacing unit; `Padding`/`Margin` values are what `Spacing` exists for.)

- [ ] **Step 2: Wire into MainForm**

In `MainForm.Designer.cs`, add a `PermissionDiffPanel` docked `Bottom`, placed between the transcript (`Fill`) and the input panel (also `Bottom`) so it sits above the input box. In `MainForm.cs`, subscribe `AgentProcess.OnPermissionRequested` to call `_permissionPanel.ShowRequest(...)`, and `_permissionPanel.OutcomeChosen` to call `AgentProcess.RespondToPermissionAsync(...)`.

- [ ] **Step 3: Manual verification**

Run: `dotnet run --project src/Anywhere/Anywhere.csproj`, trigger a permission request against a real or fake agent that requests file-write permission.
Expected: panel appears above the input box showing the diff and three buttons; clicking one hides the panel and unblocks the agent's turn.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: add inline permission/diff review panel"
```

---

## Self-Review Notes

- **Spec coverage:** streaming chat transcript + markdown rendering (Task 6) and the inline, non-modal permission/diff panel with no timeout (Task 7) are both covered per spec. After this phase, the app is end-to-end usable against a hardcoded profile — Phase 4 adds crash recovery, a real profile-picker UI, and visual styling.
- **Independently testable:** manual verification steps in both tasks confirm the running app; Task 6's persistence path is already covered by Phase 1's automated repository tests, so this phase relies on manual smoke tests for UI behavior specifically, per the spec's stated v1 testing strategy (no WinForms UI automation framework in v1).
- **2026-09-06 fixes from `2026-09-05-anywhere-phases.review.md`:**
  - *Finding #1 (High):* `ChatTranscriptPanel` originally only had `AppendMessage`, called once after `SendPromptAsync`'s `Task` completed — a single-shot design that contradicted the spec's "stream agent responses" line. Added `StartAgentMessage`/`AppendToCurrentAgentMessage`, driven by Phase 2's new `AgentProcess.OnResponseChunk`, so the transcript grows incrementally.
  - *Finding #4 (Medium), continued:* `ChatTranscriptPanel` and `PermissionDiffPanel` now use `Anywhere.Design.Spacing` for their `Padding`/`Margin`/width-inset values instead of literal `8`/`24`, per Phase 1 Task 1b.
- **Type consistency check:** `ChatTranscriptPanel.AppendMessage(string role, string markdown)`, `StartAgentMessage()`, `AppendToCurrentAgentMessage(string chunk)`, and `PermissionDiffPanel.ShowRequest(PermissionRequest)`/`OutcomeChosen` signatures match what Phase 4's crash-recovery and profile-picker wiring (Tasks 8-9) assume when it extends `MainForm.cs` — Task 8's "Agent exited." system message still uses `AppendMessage`, which is unaffected by the streaming change.
