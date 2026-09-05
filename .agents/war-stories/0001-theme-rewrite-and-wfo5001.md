# 0001 — Custom Theming Rewrite, Then a From-Memory Diagnostic ID

Date: 2026-09-05
Area: `Anywhere` app UI — `ConversationForm`, `Program.cs`, `Anywhere.Design`

## What broke

Three separate failures in one session, each surfaced by the user, not by the
agent's own checks:

1. **Over-built UI.** A prior change replaced the plain input `Panel` + `TextBox`
   with a `RoundedInputPanel` (custom `GraphicsPath` region clipping, manual
   `OnPaint`, accent-painted button) plus a whole `ThemeService` /
   `ThemeColors` registry-watcher and per-control `ApplyTheme` handlers on five
   controls. The user's verdict: "you overcomplicated everything." All of it
   was reverted.

2. **Layout corner-cluster.** The replacement `ChatInputPanel : GroupBox` had
   its docked `TextBox`/`Button` frozen at ~112 px in the bottom-left corner.
   Root cause: the parent designer wrapped the panel in `SuspendLayout()` …
   `ResumeLayout(false)`, and `ResumeLayout(false)` runs **no layout pass**, so
   the docked children never re-laid-out when the panel stretched to full
   width. Took a standalone repro to prove; the running app hid every build.

3. **Invented diagnostic ID.** `Program.cs` got
   `#pragma warning disable SYSLIB5002` around `Application.SetColorMode`. There
   is no `SYSLIB5002` for this API — it's **`WFO5001`**, and it's an *error*
   (experimental API), so `Anywhere.csproj` was not compiling at all. The
   `dotnet watch` file-lock on the output DLL masked the failure through
   ~4 Stop-hook cycles. Only caught after the user asked, twice, why MCP
   tooling was never being used — then a Context7 query on `dotnet/winforms`
   returned the real ID in one call.

## What it cost

A full revert, a repro-driven layout debug, ~4 rounds of arguing with the Stop
hook about `dotnet watch` locks, and a guidance doc + skill that had to be
re-edited because they'd been written with the wrong ID baked in.

## Lessons

- **Match the ask.** "Support dark mode" did not license a theming framework;
  `Application.SetColorMode` + standard controls was the whole job.
- **`ResumeLayout(false)` ≠ layout.** A custom container that populates its own
  `Controls` with `Dock`/`Anchor` must not be wrapped in the parent designer's
  suspend/resume, or must force `PerformLayout()`.
- **Never write a diagnostic ID, API signature, or version fact from memory.**
  Context7 (`/dotnet/winforms`) had the correct answer immediately and was
  configured in `.mcp.json` the entire time. It went unused until the user
  forced the issue.
- **A masked build is not a passing build.** When `dotnet watch` holds the
  output lock, `dotnet build` never proves compilation — `dotnet format`
  + `dotnet cslint` prove syntax/style but not that experimental-API or
  reference errors are absent. Say so explicitly instead of implying green.

## Guardrails added

- `AGENTS.md` → "`dotnet watch` Is Always Running" rule (lock is expected; don't
  loop on it; verify with `format` + `cslint`).
- `.agents/guidance/Style Guide.md` → "WinForms Control Code" section (design
  tokens, no hand-rolled theming, spacer panels for docked gaps) and the
  UTF-8-BOM + CRLF file rule.
- `.agents/skills/winforms-dotnet-guidance/SKILL.md` → "Dark Mode (.NET 9+)"
  section with the correct `WFO5001` id, and a `ResumeLayout(false)` entry in
  the designer section + Common Mistakes.
- Memory `feedback-use-mcp-tools-proactively` — query Context7 before asserting
  any library/API fact.
