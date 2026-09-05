---
name: winforms-render-capture
description: Use when you need to visually verify a WinForms control that paints via Direct2D / Direct3D / DirectWrite / any hardware-composited or child-HWND surface (e.g. this repo's MarkdownLabel / ChatBubble) and Control.DrawToBitmap comes back blank or missing the composited content. Captures what is actually on screen from a headless dotnet-run harness.
---

# WinForms Live-Render Capture

## Why `DrawToBitmap` isn't enough

`Control.DrawToBitmap` sends `WM_PRINT`/`WM_PRINTCLIENT` and captures only what
GDI/GDI+ draws in response. It does **not** capture:

- Direct2D / Direct3D content on an `ID2D1HwndRenderTarget` or swap chain
  (this repo's `MarkdownLabel` renders its text this way)
- child windows that don't honor `WM_PRINTCLIENT`
- anything the DWM composites rather than the control painting itself

Symptom: your repro screenshot shows the GDI+ parts (a `Panel`'s
`FillPath` background, standard controls) but the Direct2D text/graphics area
is blank or stale. You then can't see the bug the user is reporting.

## The capture that works: `PrintWindow` + `PW_RENDERFULLCONTENT`

`user32!PrintWindow(hwnd, hdc, 0x2)` — the `PW_RENDERFULLCONTENT` flag (Win 8.1+)
tells the OS to include DirectComposition/DWM-rendered content. Capture the
**top-level form**, not the individual control.

```csharp
[System.Runtime.InteropServices.DllImport("user32.dll")]
static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

// after the form is shown and painting has settled:
using var bmp = new Bitmap(form.Width, form.Height);
using (var g = Graphics.FromImage(bmp)) {
  var hdc = g.GetHdc();
  PrintWindow(form.Handle, hdc, 0x2 /* PW_RENDERFULLCONTENT */);
  g.ReleaseHdc(hdc);
}
bmp.Save(pngPath);
```

## Headless harness pattern

A throwaway `WinExe` in the scratchpad that references the control's project,
drives it, captures, and exits — no manual clicking, works over SSH:

```csharp
// t.csproj: <OutputType>WinExe</OutputType>, net10.0-windows, UseWindowsForms,
//           <ProjectReference Include=".../Anywhere.Controls.csproj" />
#pragma warning disable WFO5001
Application.SetColorMode(SystemColorMode.System);   // match the real app's theme
#pragma warning restore WFO5001
ApplicationConfiguration.Initialize();

var f = new Form { ClientSize = new Size(560, 360),
                   StartPosition = FormStartPosition.Manual, Location = new Point(50, 50) };
var t = new ChatTranscriptPanel();
f.Controls.Add(t);
f.Shown += async (_, _) => {
  // Exercise BOTH code paths — bugs often live in one only.
  t.AppendMessage("user", "Hello?");                 // non-streaming
  t.StartAgentMessage();                             // streaming
  foreach (var w in words) { t.AppendToCurrentAgentMessage(w); await Task.Delay(8); }

  await Task.Delay(300);          // let async layout / D2D present settle
  Application.DoEvents();
  CaptureWithPrintWindow(f, pngPath);

  // also dump geometry — numbers catch what a blank capture can't
  foreach (Control c in t.Controls)
    Console.Error.WriteLine($"{c.GetType().Name} bounds={c.Bounds} child={((Control)c).Controls[0].Bounds}");
  f.Close();
};
Application.Run(f);
```

Run it while the app's own `dotnet watch` holds the project's normal build
lock by giving the *harness* an isolated build:

```bash
dotnet build src/Anywhere.Controls/Anywhere.Controls.csproj --artifacts-path "$SCRATCH/cb"
dotnet run --project "$SCRATCH/t/t.csproj"   # ProjectReference rebuilds Controls into t's output
```

Then `Read` the PNG.

## Gotchas

- Capture **after** `f.Shown` + a short `Task.Delay` + `Application.DoEvents()`.
  D2D `EndDraw` presents asynchronously; too early and you snapshot a
  half-drawn frame.
- Position the form on-screen (`StartPosition = Manual`, small `Location`).
  A form placed off-screen or minimized can still `PrintWindow`, but a
  0-size / not-yet-laid-out form captures nothing useful.
- `PrintWindow` returns `false` on failure — check it.
- Geometry `Console.Error.WriteLine` dumps are the other half of the evidence:
  when the capture is ambiguous, `control.Bounds` / `Location` / `Size` /
  `BackColor` tell you whether the bug is layout or paint.
- Bugs that only appear when a control is **resized after first paint**
  (streaming text growing a bubble) need the harness to actually grow it in a
  loop — a single static message won't reproduce a missing
  `ControlStyles.ResizeRedraw`.
