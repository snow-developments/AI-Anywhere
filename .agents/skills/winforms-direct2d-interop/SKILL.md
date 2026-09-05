---
name: winforms-direct2d-interop
description: Use when rendering a WinForms Control/UserControl with Direct2D + DirectWrite (via Vortice) instead of GDI+ — hardware-accelerated 2D, crisp text, rounded/blurred/gradient fills, or a Fluent-style custom control. Covers the ID2D1HwndRenderTarget lifecycle, device-loss recovery, DPI, factory sharing, and text layout. Also the answer when someone says "DirectDraw" (dead API; they mean Direct2D). This repo: MarkdownLabel.cs, ChatBubble.cs, the planned Anywhere.WinForms.Fluent library.
---

# Direct2D / DirectWrite ↔ WinForms Interop

## Overview

WinForms controls paint with GDI+ (`OnPaint(e.Graphics)`). To get
hardware-accelerated 2D, sub-pixel text, cheap rounded rectangles/gradients, or
a WinUI-flavoured look, you bind a **Direct2D `ID2D1HwndRenderTarget`** to the
control's `Handle` and draw with it instead. Text goes through **DirectWrite**.

- **"DirectDraw" is a dead 1995 API.** Anyone asking for it today means
  **Direct2D** (`d2d1.dll`) for shapes/bitmaps and **DirectWrite**
  (`dwrite.dll`) for text.
- **Binding:** [Vortice.Windows](https://github.com/amerkoleci/vortice.windows)
  (`Vortice.Direct2D1` pulls `Vortice.DirectWrite`, `Vortice.Mathematics`,
  `Vortice.DCommon` transitively). This repo pins `Vortice.Direct2D1` 3.8.3.
- **Working reference:** `src/Anywhere.Controls/MarkdownLabel.cs` — copy its
  shape.

## When to use

- Custom control needs gradients, blur, rounded fills, layers, or many text
  runs with per-range formatting.
- `Control.DrawToBitmap` returns blank for the control (D2D content isn't
  GDI) — screenshot via the `winforms-render-capture` skill instead.
- Building `Anywhere.WinForms.Fluent` (see
  `docs/superpowers/plans/2026-09-05-anywhere-winforms-fluent.md`).

**Not for:** ordinary controls (use GDI+ `OnPaint`), 3D (that's Direct3D),
or anything where `TextRenderer.DrawText` + a `GraphicsPath` already suffices.

## The device model — the one thing to internalise

| Kind | Created from | Survives device loss? | Examples |
|---|---|---|---|
| **Device-independent** | the *factory* | yes — make once, keep | `ID2D1Factory`, `IDWriteFactory`, `IDWriteTextFormat`, geometries, stroke styles |
| **Device-dependent** | the *render target* | **no — recreate after loss** | `ID2D1HwndRenderTarget`, `ID2D1SolidColorBrush`, bitmaps, layers |

Device loss (GPU reset, RDP connect/disconnect, driver update, lock screen)
makes `EndDraw()` fail with **`D2DERR_RECREATE_TARGET` (`0x8899000C`)**. If you
don't handle it the control goes permanently black. Handle it (below).

## Control lifecycle

```csharp
public class FluentThing : Control {
  private ID2D1HwndRenderTarget? rt;
  private IDWriteFactory? dw;                    // device-independent, lazy
  private ID2D1SolidColorBrush? brush;           // device-dependent

  private IDWriteFactory DwFactory => dw ??= DWrite.DWriteCreateFactory<IDWriteFactory>();

  protected override void OnHandleCreated(EventArgs e) {
    base.OnHandleCreated(e);
    _ = DwFactory;                               // DWrite needs no HWND — usable before this
    var factory = D2D1.D2D1CreateFactory<ID2D1Factory>(FactoryType.SingleThreaded);
    rt = factory.CreateHwndRenderTarget(
      new RenderTargetProperties(),               // DpiX/Y = 0 → default 96; see DPI below
      new HwndRenderTargetProperties { Hwnd = Handle, PixelSize = new SizeI(Width, Height) });
    rt.TextAntialiasMode = TextAntialiasMode.Cleartype;
    brush = rt.CreateSolidColorBrush(ForeColor.ToColor4());
  }

  protected override void OnResize(EventArgs e) {
    base.OnResize(e);
    rt?.Resize(new SizeI(Width, Height));         // PixelSize is in *pixels*, not DIPs
    Invalidate();
  }

  protected override void OnPaint(PaintEventArgs e) {
    if (rt is null || DesignMode) return;         // designer + pre-handle guard
    rt.BeginDraw();
    rt.Clear((Parent?.BackColor ?? BackColor).ToColor4());   // fake transparency: clear to parent colour
    // ... DrawGeometry / FillGeometry / DrawTextLayout ...
    try {
      rt.EndDraw();                               // Vortice: throws SharpGenException on failure
    } catch (SharpGenException ex) when ((uint)ex.ResultCode.Code == 0x8899000C) {
      brush?.Dispose(); brush = null;             // D2DERR_RECREATE_TARGET: drop device-dependent resources
      rt.Dispose(); rt = null;
      Invalidate();                               // rebuild the target on the next paint (or rebuild here)
    }
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    brush?.Dispose(); rt?.Dispose(); dw?.Dispose();
    brush = null; rt = null; dw = null;
    base.OnHandleDestroyed(e);
  }
}
```

**`OnPaint` vs `WndProc(WM_PAINT)`:** overriding `OnPaint` (MarkdownLabel) is
simplest and fine. Some samples (sagemodeninja/winforms-fluent-ui) intercept
`WM_PAINT` in `WndProc` + call `BeginPaint`/`EndPaint` themselves; only do that
if you must suppress the base `WM_PAINT`/`WM_ERASEBKGND` entirely. If you go
that route also `SetStyle(ControlStyles.UserPaint | AllPaintingInWmPaint |
Opaque, true)`.

**Always** `SetStyle(ControlStyles.ResizeRedraw, true)` for any size-dependent
paint, or a grow only invalidates the new strip and you get ghosted edges (see
`winforms-dotnet-guidance` Common Mistakes).

## Factories

- `ID2D1Factory` / `IDWriteFactory` are device-independent — one of each per
  process is plenty. A form with 30 D2D controls each newing its own factory
  wastes handles; ref-count a shared pair (the Fluent plan's `D2DFactories`).
- `FactoryType.SingleThreaded` = only touch D2D objects from the UI thread
  (fine for controls). `MultiThreaded` adds an internal lock — only if you
  measure/lay out text on a worker.
- The DirectWrite factory needs **no window handle**, so `CreateTextLayout` /
  `.Metrics` work before the control's handle exists — do measurement
  (`GetPreferredSize`, autosize) against DWrite directly, like
  `MarkdownLabel.MeasureContent`.

## DPI

- A fresh `HwndRenderTarget` is **96 DPI**: 1 DIP = 1 px. Two consistent
  choices:
  1. **Work in pixels** — pass pixel `Width/Height` to `PixelSize`/`Resize`,
     `pixelsPerDip: 1.0f` in `CreateGdiCompatibleTextLayout`. Simple; what
     MarkdownLabel does. You scale font sizes yourself
     (`Font.SizeInPoints * 96/72`, then `* DeviceDpi/96`).
  2. **Work in DIPs** — set `RenderTargetProperties.DpiX = DpiY = DeviceDpi`,
     then draw in DIPs and D2D scales. `pixelsPerDip: DeviceDpi/96f`.
- Per-monitor DPI change: override `OnDpiChangedAfterParent` → recreate or
  `Resize` the target and `Invalidate()`. `DeviceDpi` is already updated by then.

## DirectWrite text

- `factory.CreateTextFormat(family, FontWeight, FontStyle, FontStretch, sizeDip)`
  → device-independent, reuse it.
- `CreateGdiCompatibleTextLayout(text, len, format, maxWidth, maxHeight,
  transform: null, pixelsPerDip, useGdiNatural: true)` gives GDI-consistent,
  crisp small text (matches surrounding WinForms controls). Plain
  `CreateTextLayout` is the pure-D2D path.
- Per-range: `layout.SetFontWeight/SetFontStyle/SetUnderline/SetDrawingEffect(new TextRange(pos,len))`.
- Measure: `layout.Metrics.Width/.Height`; hit-test: `HitTestTextRange`.
- Draw: `rt.DrawTextLayout(new System.Numerics.Vector2(x, y), layout, brush)`.
- **Gotcha:** Vortice's managed `IDWriteTextFormat` does **not** expose
  `SetWordWrapping` (native-only in 3.8.3). If you need
  `DWRITE_WORD_WRAPPING_NO_WRAP`/`WHOLE_WORD`, either accept the default
  wrapping, lay out line-by-line yourself, or `[DllImport]` the vtable slot.
- `Color4` from a `System.Drawing.Color`: `new Color4(new ColorBgra(c.R, c.G, c.B, c.A))` (see `MarkdownLabel.ColorExtensions`).

## Common mistakes

- **No `D2DERR_RECREATE_TARGET` handling** → control turns black forever after
  an RDP session or GPU driver update. Catch the `EndDraw()` failure
  (`0x8899000C`), drop all device-dependent resources, rebuild the target.
  (Confirm the exact exception type / result-code accessor for your Vortice
  version via Context7 — `MarkdownLabel.cs` currently skips recovery entirely.)
- **Recreating the factory every paint** (or every control) — factories are
  device-independent; create once, ideally shared + ref-counted.
- **Resizing with DIPs** — `HwndRenderTargetProperties.PixelSize` and
  `Resize(SizeI)` are pixels. Passing scaled-down DIP values on high DPI gives
  a small render clipped into the corner.
- **Forgetting `ResizeRedraw`** → ghost/stacked edges when the control grows
  (streaming chat bubble).
- **Painting in the VS designer** — no live device; guard
  `if (DesignMode || rt is null) return;` or the designer shows an exception
  box.
- **Not clearing to the parent colour** — D2D has no "transparent control";
  `rt.Clear(Parent.BackColor.ToColor4())` fakes it. True alpha compositing
  needs a layered window.
- **`DrawToBitmap` for tests** — returns blank (D2D isn't `WM_PRINT`). Use the
  `winforms-render-capture` skill (`PrintWindow` + `PW_RENDERFULLCONTENT`).
- **Touching D2D objects off the UI thread** with a `SingleThreaded` factory —
  measure text via the DWrite factory (thread-safe for layout) or make the D2D
  factory `MultiThreaded`.

## See also

- `winforms-dotnet-guidance` — the control tree, designer, thread affinity, DPI/dark-mode project setup.
- `winforms-render-capture` — screenshotting a D2D control that `DrawToBitmap` can't.
- `src/Anywhere.Controls/MarkdownLabel.cs` — the canonical in-repo implementation.
- Vortice API: query Context7 `/amerkoleci/vortice.windows` before guessing a signature.
