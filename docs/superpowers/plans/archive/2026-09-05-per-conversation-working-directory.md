# Per-conversation working directory

Status: implemented (2026-09-05) — tests not run locally (dotnet watch pdb lock)
Date: 2026-09-05

## Problem

`cwd` is currently a required field on `AgentProfile` (`WorkingDir`). It is not
a property of the agent — it is a runtime detail of a conversation. A user
should pick a directory when starting a conversation and change it at any point
during an existing one.

## Protocol constraint

ACP fixes a session's `cwd` at `session/new` (also `session/resume` /
`session/fork` / `session/load`). There is **no set-cwd RPC**. Changing the
directory of a live conversation therefore means tearing down the current ACP
session (and its agent subprocess) and starting a fresh one with the new `cwd`.
The agent loses in-memory context; the persisted transcript is untouched.

## Decisions

1. **DB shape on dir change:** one `Session` row. `Session.WorkingDir` is
   overwritten in place; a `system` divider message records the change
   (`"Working directory changed to <path>."`).
2. **New-conversation default:** always require an explicit pick. No
   auto-start, no persisted "last used" setting.
3. **`AgentProfile.WorkingDir`:** kept, but made **nullable** — an optional
   per-profile default that pre-fills the directory picker. Never used directly
   as a session `cwd`.

## Changes

### `Anywhere.Models`

- `AgentProfile.WorkingDir` → `string?` (drop `required`).
- EF migration `MakeProfileWorkingDirOptional`: alter column to nullable.
  `Session.WorkingDir` stays `NOT NULL`.
- Update `AnywhereDbContextModelSnapshot`.

### `Anywhere.Persistence`

- `SessionRepository.InsertAsync(int profileId, string workingDir)` — unchanged
  signature; `workingDir` now always supplied by the caller from the picker.
- Add `SessionRepository.UpdateWorkingDirAsync(int sessionId, string workingDir)`.

### `Anywhere` app

- **`SplashForm`**
  - New Conversation button: show a `FolderBrowserDialog` (seeded with
    `SelectedProfile?.WorkingDir` when non-null, else `Environment.CurrentDirectory`).
    Cancel → abort, do not open `ChatForm`.
  - `OpenConversation` / `ChatForm` construction takes the chosen directory.
- **`ChatForm`**
  - Constructor gains a `string? workingDirectory` parameter. `StartAgentAsync`
    uses it as the session `cwd` and the agent process `WorkingDirectory`
    instead of `profile.WorkingDir`.
  - If `workingDirectory` is null on load, prompt with `FolderBrowserDialog`
    before the first `StartAgentAsync`; if the user cancels, show the
    "no directory" empty state and leave `agent` null.
  - Track `currentWorkingDir` field; `StartAgentAsync` reads it.
  - New toolbar control next to `profilePicker`: `ToolStripButton`
    (`changeDirButton`) whose text is the basename of `currentWorkingDir`,
    tooltip the full path. Click → `FolderBrowserDialog` seeded with current →
    on OK and a different path: set `currentWorkingDir`, call
    `sessions.UpdateWorkingDirAsync`, append the `system` divider, then
    `await StartAgentAsync()` (tears down + respawns per the protocol
    constraint above).
  - `OnProfilePickerChanged` keeps `currentWorkingDir` as-is across a profile
    switch (dir is independent of profile now).
  - `DevFakeProfile()` — `WorkingDir` left null; dir comes from the picker.
    Tests / dev flow pass an explicit dir.
- **`AgentProfileForm`**
  - Working-directory field becomes optional: relabel "Working directory
    (optional default)", drop it from the required-fields validation in
    `OnSaveClicked`, write `null` when blank.

### `Anywhere.Agents`

- `AgentProcess` already takes `cwd` only via `profile.WorkingDir` at two
  sites (`ProcessStartInfo.WorkingDirectory`, `NewSessionRequest.Cwd`). Add a
  `string workingDirectory` constructor parameter (or `StartAsync` parameter)
  and use it at both sites; stop reading `profile.WorkingDir`.

## Tests

- `ProfileRepositoryTests`, `MessageRepositoryTests`: drop `WorkingDir` from
  the `AgentProfile` fixtures (or set null) — confirm insert/round-trip with a
  null profile working dir.
- New `SessionRepositoryTests.UpdateWorkingDirAsync_overwrites_in_place`.
- `AgentProcessIntegrationTests`: pass the working directory explicitly to
  `AgentProcess` instead of via the profile; existing assertions unchanged.
- Manual smoke: new conversation forces a folder pick; change-dir button
  respawns the agent and drops a divider; profile switch leaves the dir intact.

## Out of scope

- Resuming an existing `Session` from the splash (the `OpenConversation`
  `sessionId` FIXME) — unrelated, still unimplemented.
- `additionalDirectories` support.
- Persisting a "last used directory" app setting.
