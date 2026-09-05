# 0002 — Chat Bubbles: Invisible Text, Then Ghost Rectangles

Date: 2026-09-05
Area: `Anywhere.Controls` — `MarkdownLabel`, `ChatTranscriptPanel`, new `ChatBubble`

## What broke

### Round 1 — messages invisible

Submitted messages appended and persisted but rendered nothing. Root cause:
`ChatTranscriptPanel` created each bubble as a `MarkdownLabel` with a `Width`
but no `Height`, no `AutoSize`, and `MarkdownLabel` has no `DefaultSize`
override → 0 px tall. The only self-size path (`RenderInlines`, guarded by
`AutoSize`, which nothing set) also had a `float.MaxValue → Convert.ToInt32`
overflow. Pre-existing from `42348f2`, surfaced only once the input worked.

Fix: gave `MarkdownLabel` a real `MeasureContent(int) → Size` (a `bool draw`
flag makes one code path both measure and paint; lazy DWrite factory so it
works before the handle exists); `ChatTranscriptPanel` sizes bubbles from it.

### Round 2 — "inconsistent padding" and "weird background color in the text"

User's screenshot of the running app showed agent bubbles with text jammed to
the top-left, overflowing the bubble, on a lighter rectangle. Three causes:

1. **Ghost rectangles** = the new `ChatBubble : Panel` paints a size-dependent
   rounded fill (`FillPath`) but never called
   `SetStyle(ControlStyles.ResizeRedraw, true)`. Each streaming chunk grew the
   bubble width; WinForms invalidated only the newly exposed strip, so every
   previous width's rounded right edge stayed painted underneath → stacked
   arcs that read as a "weird background".
2. **20 px left / 16 px right** text inset = `RenderInlines` carried a legacy
   `indent = 4` default from the control's origin.
3. **8 px top / 14 px bottom** = `MeasureContent` counted `BlockSpacing` after
   the *last* block, not just between blocks.

## What made it slow

- Verified Round 1 with `Control.DrawToBitmap`. It captures only GDI/GDI+
  `WM_PRINT` output, **not** the Direct2D `ID2D1HwndRenderTarget` that
  `MarkdownLabel` draws text into. Every repro screenshot was blank where the
  bug was. Claimed "verified" on geometry numbers alone and shipped Round 1
  with the padding/redraw bugs still in.
- Round 2's real fix needed seeing the actual pixels: `user32!PrintWindow(hwnd,
  hdc, PW_RENDERFULLCONTENT /* 0x2 */)` on the top-level form, from a headless
  `dotnet run` harness. That made the ghost arcs obvious in one capture.
- Reproduced with the non-streaming path only at first; the artifact needs the
  **streaming** path (`StartAgentMessage` + repeated
  `AppendToCurrentAgentMessage`) that grows the control after first paint.
- Fixed `WFO1000` by copying an attribute off another file instead of looking
  it up (it happened to be right — `DesignerSerializationVisibility.Hidden` —
  but that was luck, and the user called it out).

## Lessons

- **A blank repro screenshot is not a pass.** If a control paints via
  Direct2D / Direct3D / DirectWrite / a child HWND, `DrawToBitmap` can't see
  it — use `PrintWindow` + `PW_RENDERFULLCONTENT`. New skill:
  `.agents/skills/winforms-render-capture/`.
- **Custom paint whose shape depends on size ⇒ `ControlStyles.ResizeRedraw`.**
  Otherwise a grow shows stale paint from the previous size.
- **Reproduce the exact code path the app uses.** Streaming vs non-streaming
  hit different resize timing; a bug in one won't show in the other.
- Still counts as guessing: pattern-matching an attribute/fix off another file
  in the repo without confirming it via Context7. See
  `../../memory` note `feedback-use-mcp-tools-proactively`.

## Guardrails added

- `.agents/skills/winforms-render-capture/SKILL.md` — the PrintWindow capture
  technique + headless harness pattern.
- `.agents/skills/winforms-dotnet-guidance/SKILL.md` Common Mistakes — the
  `ResizeRedraw` rule and the `DrawToBitmap`-can't-see-D2D rule.
