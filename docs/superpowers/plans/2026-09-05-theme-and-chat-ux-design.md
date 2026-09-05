# Theme & Chat UX — Status and Next Steps

## Done (2026-09-05)

Dark mode and the chat input frame shipped. An earlier draft (custom
`ThemeService` + `RoundedInputPanel` + cancel-in-flight UI) was built, judged
over-engineered, and reverted — see
`.agents/war-stories/0001-theme-rewrite-and-wfo5001.md`.

- **Dark mode:** `Application.SetColorMode(SystemColorMode.System)` in
  `Program.cs` (behind `#pragma warning disable WFO5001` — experimental API
  reported as an _error_). Follows the OS setting; the framework themes standard
  controls, including multiline `TextBox`. No `ThemeService`, no `ThemeColors`,
  no per-control theming.
  `Anywhere.Design/Colors.cs`/`Spacing.cs`/`Typography.cs` unchanged;
  `Anywhere.Design` stays a plain `net10.0` library.
- **Chat input:** `src/Anywhere.Controls/ChatInputPanel.cs` — a `GroupBox` with
  a multiline `TextBox` (default `Fixed3D` border), an `AutoSize` Send button,
  and a `Spacing.Small` spacer panel between them. Exposes `InputBox` and
  `event Action? SendRequested` (Send click or Enter; Shift+Enter = newline).
  Wired in `ChatForm` via `OnSendRequested`.
- Metrics from `Anywhere.Design` tokens only — codified in
  `.agents/guidance/Style Guide.md` → "WinForms Control Code".

## Next

### 1. Transcript bubbles — DONE 2026-09-05

Pre-existing zero-height bug from `42348f2`: bubbles had no `Height`/`AutoSize`/
`DefaultSize`, and the one self-size path had a `float.MaxValue →
Convert.ToInt32` overflow.

- `MarkdownLabel`: `RenderBlock`/`RenderInlines` took a `bool draw` flag so one
  code path measures and paints; DWrite factory lazy (`DwFactory`) so measuring
  works before the handle exists; broken incremental-grow removed. New
  `MeasureContent(int maxWidth) → Size` (width = widest line, capped) and
  `GetPreferredHeight(int)` delegating to it. `RenderInlines` `indent` default
  `4 → 0` and the trailing `BlockSpacing` is no longer counted after the last
  block — both were padding asymmetries.
- **New `ChatBubble : Panel`** (`src/Anywhere.Controls/ChatBubble.cs`, enum
  `ChatRole { User, Agent, System }`): a `MarkdownLabel` body inset by
  `Padding` on a role-tinted rounded-rect (`FillPath`). User = `Colors.Accent`
  / white text, Agent/System = `SystemColors.ControlLight`/`Control`. Host
  drives geometry via `Measure(maxWidth)` + `LayoutBody()`.
  `SetStyle(ControlStyles.ResizeRedraw, true)` — required, or the size-relative
  rounded fill leaves stacked edges as streaming grows the bubble.
- `ChatTranscriptPanel` rewritten around `ChatBubble`: shrink-wrap to ≤¾ width,
  user messages right-aligned via left `Margin`, others left.
- Verified with a `PrintWindow`/`PW_RENDERFULLCONTENT` capture harness
  (`DrawToBitmap` can't see the Direct2D HWND) — streaming, wrapping, lists,
  both roles. See the `winforms-render-capture` skill.

Follow-ups (not blocking):

- `MarkdownLabel.brush` / `ChatBubble` colors are read once (brush in
  `OnHandleCreated`); a live OS light/dark switch won't recolor existing
  bubbles — hook `OnSystemColorsChanged` if that matters.
- `MarkdownLabel.AutoSize`/`AutoSizeMode` overrides now unused for sizing; left
  as public API.

Also renamed `ConversationForm` → `ChatForm` (`ChatForm.cs` /
`ChatForm.Designer.cs`, refs in `SplashForm`).

### 2. Submit feedback — DONE 2026-09-05

`ChatInputPanel.SetBusy(bool)`: disables `InputBox`, swaps the Send button to
"Stop", sets the `GroupBox` caption to "Working…". `ChatForm.OnSendRequested`
calls `SetBusy(true)` after `StartAgentMessage()` and `SetBusy(false)` in a
`finally` covering success / error / cancel. No spinner control — the caption is
the signal, no custom painting.

### 3. Cancel in flight — DONE 2026-09-05

`SendPromptAsync` kept its `CancellationToken`. `ChatForm` holds a
`CancellationTokenSource? sendCts` per send, passes `sendCts.Token`, and
disposes/nulls it in the same `finally` as item 2. `ChatInputPanel` raises
`CancelRequested` when the Stop button is clicked while busy (Enter is ignored
while busy); `ChatForm.OnCancelRequested` calls `sendCts?.Cancel()`. A
`catch (OperationCanceledException)` writes `"Cancelled."` to the transcript
instead of `"Agent error: …"`.

### 4. User-configurable theme override — split out

Moved to its own plan: `2026-09-05-theme-override.md`. Not started.

### 5. Phase 4 toolbar row

Model/workspace/mic controls. Out of scope until Phase 4's profile UI; the
earlier draft's disabled-placeholder row was reverted, not kept.

## Implementation notes

- **`ResumeLayout(false)` runs no layout pass.** A custom container that builds
  its own `Dock`/`Anchor` children must not sit inside the parent designer's
  `SuspendLayout()` … `ResumeLayout(false)` — children freeze at their initial
  bounds (symptom: clustered small in the panel's top-left). `ChatInputPanel`
  builds its children in its constructor and the designer does not suspend it.
- **New `.cs` files: UTF-8 with BOM + CRLF** (`.editorconfig` `charset` /
  `end_of_line`, `cslint` `CSLINT003`).
- **`dotnet watch` is always running.** `dotnet build` failing only with
  `MSB3021`/`MSB3026`/`MSB3027` ("locked by: Anywhere (PID)") = compilation
  succeeded, output copy hit the live app. Verify with
  `dotnet format --verify-no-changes` + `dotnet cslint`.
- `SetColorMode`/`WFO5001` is still experimental in .NET 10 — re-check on a TFM
  bump.

## Testing

UI is manual smoke-test only (repo v1 strategy). `SetColorMode` and the
`GroupBox` layout were validated with throwaway `dotnet run` repros. Item 1
above needs the same before it's called done.
