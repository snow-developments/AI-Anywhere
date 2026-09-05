# User-Configurable Theme Override

Split out of `2026-09-05-theme-and-chat-ux-design.md` §4. Not started.

## Goal

Let the user force Light or Dark regardless of the OS setting, with "System"
(follow the OS) as the default. The choice persists across restarts.

## Background

`Program.cs` currently hard-codes the OS-follow mode:

```csharp
#pragma warning disable WFO5001 // Application.SetColorMode is experimental.
Application.SetColorMode(SystemColorMode.System);
#pragma warning restore WFO5001
```

`SetColorMode` accepts `SystemColorMode.System` / `Dark` / `Classic` (Classic =
light). There is no `ThemeService` — an earlier draft added one and was reverted
(`.agents/war-stories/0001-theme-rewrite-and-wfo5001.md`). This task must not
reintroduce it; it is a single persisted enum feeding the existing call.

## Persistence

Use WinForms application settings (`Properties.Settings`, user scope) — chosen
over a new EF settings table (too heavy for one enum) and a hand-rolled JSON
file. Add one setting:

- `ThemeOverride` : `string`, user scope, default `"System"` — stores the
  `SystemColorMode` enum name.

Read it in `Program.cs` before `SetColorMode`; parse with
`Enum.TryParse<SystemColorMode>` and fall back to `System` on any miss.

## UI

No settings window exists yet. Options, cheapest first:

1. Tray-icon context menu (`TrayIcon.cs`) submenu "Theme" with
   System / Light / Dark radio items. Writing the setting + a `MessageBox`
   telling the user to restart is acceptable for v1 — `SetColorMode` must be
   called before any window is created, so a live swap is out of scope here.
2. A real Preferences form — defer to Phase 4's profile-management UI
   (`2026-09-05-phase-4-polish.md`).

Pick (1) for this task.

## Steps

1. Add `ThemeOverride` to `Properties.Settings` (Anywhere project).
2. `Program.cs`: read + parse the setting, pass the result to `SetColorMode`
   instead of the literal `SystemColorMode.System`.
3. `TrayIcon.cs`: add the "Theme" submenu with three radio items bound to the
   setting; on change, `Settings.Default.Save()` then show a restart-required
   `MessageBox`.
4. Manual smoke test: set Dark, restart, confirm the app renders dark with the
   OS in light mode (and vice versa); confirm "System" still follows the OS.

## Out of scope

- Live theme switching without restart.
- Per-conversation-window theme.
- Any `ThemeService` / per-control theming revival.
