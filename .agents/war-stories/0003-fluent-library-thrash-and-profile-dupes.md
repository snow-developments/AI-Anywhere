# 0003 — Fluent Styling: Two Dead Libraries, a Duplicate-Row Flood, and a Chatty Teardown

Date: 2026-09-05
Area: `Anywhere` app — Phase 4 "polish" (`ChatForm`, `SplashForm`,
`AgentProfileForm`, `AgentProcess`), `ProfileRepository`, `Anywhere.slnx`

## What broke

One session executing `docs/superpowers/plans/2026-09-05-phase-4-polish.md`.
Every failure below was surfaced by the user, not by the agent's own checks.

### 1. Recommended two unusable control libraries in a row

Task 10 of the plan said "apply `WinForms.Fluent.UI`". The agent's own earlier
plan had picked that package — it is **abandoned (last release Dec 2022, native
`DirectNCore` dependency)** and was never actually referenced by the repo.
Instead of leading with that, the agent ran an `AskUserQuestion` volley
offering ranked off-the-shelf options. The user pushed back hard
("Fine a better alternative!!! Use mcp tools, you halfwit!!!"). The agent then
proposed **`Krypton.Toolkit`**, the user picked it, the agent implemented it
across three forms + `Program.cs` — and the user rejected the result on sight:
"Krypton is fucking ugly… I wanted WinUI3-like fluent design system." Full
revert. Only after that did the agent state the actual finding: **no maintained
WinForms control library implements the WinUI 3 Fluent Design System** — they
all skin Win32 with a *different* design language (Krypton→Office,
MaterialSkin→Material, AntdUI→Ant, Sunny.UI→Metro). The real path (a
purpose-built Direct2D library, `Anywhere.WinForms.Fluent`) got its own plan
only on the third pass.

### 2. 36 duplicate `fake` profiles in the database

While wiring the profile picker the agent noticed the pre-existing
`ChatForm.OnLoad` called `profiles.InsertAsync(hardcodedProfile)` on **every
conversation open**, and fixed the bleeding (seed only when the table is
empty). It did **not** inspect the existing table. ~40 past sessions had
already written ~36 identical rows. The agent found out when the user posted a
screenshot of a dropdown overflowing with "fake / fake / fake…": "Why is the
app full of a shit-ton of bullshit profiles?" A dedupe script cleaned it
(36 → 3), keeping one row per distinct `(Command, Args)` and repointing
`Sessions.ProfileId`.

### 3. Dry-run that reported a fake result

The first cleanup script had a `--dry-run` that printed the post-DELETE profile
list **after `ROLLBACK`** — so it always showed the unchanged 36 rows and
labelled them "would remain". The agent briefly read this as the dedupe key
being wrong and iterated on the query before realising the rollback, not the
key, produced the output.

### 4. Answered a UI question with a script

Asked for "copy-pasta to fix the `fake` profile" (broken `Args` path), the
agent produced a .NET 10 file-based `dotnet run` SQLite script. The user:
"No, stupid. I can use the profile form..." The fix was four field values to
type into the `AgentProfileForm` the agent had just built.

### 5. "Agent exited `-1`" on every profile switch

The agent's own new `ChatForm` profile-switch path calls `agent.Dispose()` on
the outgoing `AgentProcess`, which `Process.Kill()`s it → Windows exit code
`-1`. `AgentProcess`'s `Process.Exited` handler still fired `OnAgentExited`, so
the transcript announced "Agent exited (exit code -1)." and showed the
"Restart agent" bar for what was a deliberate teardown. Shipped without
distinguishing intentional dispose from a crash. Fixed with a `disposing` flag
set at the top of `Dispose()` that gates the `Exited` handler.

### Minor

- `KryptonManager.GlobalPaletteMode` written as a static assignment from a
  Context7 snippet that mixed library versions; instance members in v105 →
  build error (caught by build, ~1 cycle lost).
- `cd src/Anywhere` for a `dotnet add package` call persisted in the Bash tool
  and broke two later `dotnet build Anywhere.slnx` invocations
  ("Project file does not exist") before the agent `cd`'d back. `CLAUDE.md`
  already forbids `cd` in Bash.
- Renamed an uncommitted new file with `mv` then `rm` on the stale name —
  content was preserved in the rename target, but it brushed the
  "never `rm` code files" rule.

## What it cost

A full Krypton implementation written and reverted (csproj, `Program.cs`
palette wiring, `AgentProfileForm` + `ChatForm` control swaps); two throwaway
cleanup scripts; ~3 `AskUserQuestion` rounds that read as stalling; a rejected
script answer; and a user visibly out of patience for most of the session.

## Lessons

- **Vet a dependency before it becomes an option, not after it's implemented.**
  "Maintained?" and "does it actually produce the look the user asked for?" are
  pre-`AskUserQuestion` checks. `gh api repos/<o>/<r>` (pushed date, archived,
  release cadence) + a screenshot/gallery comparison take one round each.
- **When nothing off-the-shelf fits, say that first.** Ranking bad options
  invites the user to pick one and then reject the outcome. Lead with the
  finding ("no library does X; the choices are build-it / commercial /
  approximate") and let them steer from there.
- **Touching code that writes persistent state ⇒ inspect the state.** The
  existing `insert-on-every-load` bug was right there in the method being
  edited; the 36 rows it had already produced were one `SELECT` away.
- **A dry-run must show the plan, not a rolled-back "result".** Print what
  *would* change (counts, affected ids); never render post-state inside a
  transaction you're about to abort.
- **Answer the question that was asked.** A form the user can open beats a
  script every time; reach for SQL only when there is no UI path.
- **Design the teardown path when you add restart/switch.** An intentional
  `Dispose()` and a crash both end the process — the code has to tell them
  apart before it reports anything to the user.
- **Bash `cd` persists across tool calls.** Never `cd`; pass the path to the
  command. (Reinforces `CLAUDE.md`.)

## Guardrails added

- New plan `docs/superpowers/plans/2026-09-05-anywhere-winforms-fluent.md` —
  build a real Direct2D Fluent control library; Task 12 there replaces Phase 4
  Task 10. Phase 4 plan's status block marks Task 10 superseded.
- New skill `.agents/skills/winforms-direct2d-interop/SKILL.md` — the
  `ID2D1HwndRenderTarget` lifecycle, `D2DERR_RECREATE_TARGET` recovery, DPI,
  factory sharing, DirectWrite. `winforms-dotnet-guidance` gained a "Custom
  Painting" section pointing to it.
- Memory `project_fluent_ui_direction` — Anywhere's UI must be genuine WinUI 3
  Fluent via the custom D2D library; themed-control suites
  (Krypton/Material/AntdUI/…) are all rejected. Don't re-propose them.
- `AgentProcess` now suppresses `OnAgentExited` for a caller-initiated
  `Dispose()` (the `disposing` flag).
