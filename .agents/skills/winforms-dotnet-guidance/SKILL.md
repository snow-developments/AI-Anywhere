---
name: winforms-dotnet-guidance
description: Use when building or modifying a .NET Windows Forms (WinForms) app on .NET 6+ — writing Form/UserControl code, wiring events, touching a Designer.cs file, choosing between data-binding and manual updates, doing cross-thread UI updates, or configuring DPI/dark-mode/project settings. Reference for WinForms' architecture and current (.NET 9/10) platform behavior, not project-specific conventions.
---

# WinForms (.NET) Guidance

## Overview

WinForms is a thin, event-driven .NET wrapper over the Win32/User32 windowing
APIs. Everything follows from three facts:

1. **One UI thread, one message loop.** `Application.Run` pumps Win32 messages
   (clicks, paint, timer ticks) and dispatches them as .NET events. Controls
   created on that thread may only be touched from that thread.
2. **Controls are a tree.** Every `Control` (a `Form` is a `Control`) has a
   `Controls` collection of children; layout, painting, and disposal all
   recurse through this tree.
3. **The designer generates code, it doesn't run a separate model.** Dragging
   a button in Visual Studio's designer just emits C# into
   `<Form>.Designer.cs` that builds and wires that same control tree. There is
   no XAML-style separate markup — designer output *is* the control tree
   construction code.

Full source docs: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview

## When to Use

- Writing or editing a `Form`/`UserControl`/custom `Control` in a .NET 6–10
  WinForms project
- Deciding how a background operation should update the UI (thread affinity)
- Wiring a control to data (`BindingSource`, `DataBindings`, `DataGridView`)
- Touching `.Designer.cs`, `InitializeComponent`, or project DPI/theme settings
- Migrating WinForms code across .NET versions (each version's behavior
  differs — see Version Notes)

Not for: WPF, MAUI, or Avalonia (different frameworks, similar-sounding APIs
but not interchangeable) or project-specific style rules (put those in the
repo's own `AGENTS.md`/style guide).

## Application Model

```csharp
// Program.cs — .NET 6+ minimal host
internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize(); // generated; replaces manual
                                                // EnableVisualStyles() +
                                                // SetCompatibleTextRenderingDefault()
        Application.Run(new MainForm());
    }
}
```

- `[STAThread]` is required — WinForms (via COM/OLE interop underneath) needs
  a single-threaded apartment.
- `ApplicationConfiguration.Initialize()` (SDK-generated, from project
  properties like `<ApplicationHighDpiMode>`) replaced the old
  `Application.EnableVisualStyles()` / `SetCompatibleTextRenderingDefault()`
  pair as of .NET 6.
- `Application.Run(Form)` blocks the thread pumping messages until that form
  (the "main form") closes; closing it ends the app unless another modeless
  form was created with a different owner relationship.
- A second message loop can run on its own thread (e.g. exposing a form to
  COM callers) — each thread gets at most one loop.

## The Control Tree and the Designer

- `Form` and every widget derive from `Control`. Adding a child calls
  `parent.Controls.Add(child)`.

### The `*.Designer.cs` partial-class split

- A designed `Form`/`UserControl` is really **one class split across two
  files** via the `partial` keyword: `Form1.cs` (your hand-written
  constructor call and event handlers) and `Form1.Designer.cs` (the
  designer's generated field declarations and `InitializeComponent()`). Both
  files declare `partial class Form1 : Form` — the compiler merges them into
  a single type. This is the *only* mechanism WinForms uses to separate
  markup-like layout code from your logic; there is no separate markup
  format (no XAML equivalent).
- **A given type may have only one `partial` definition per file.** Splitting
  `Form1`'s designer output across two designer files, or declaring it twice
  in the same file, is a compile/designer error ("The type is made of several
  partial classes in the same file") — keep the designer's half in exactly
  one `<Name>.Designer.cs`.
- `InitializeComponent()` is generated into the `.Designer.cs` partial and is
  called once, from your `.cs` partial's constructor:
  ```csharp
  public Form1()
  {
      InitializeComponent();
  }
  ```
  It is marked "do not modify the contents of this method with the code
  editor" for a reason: the designer regenerates the whole method from
  scratch on every save of the visual designer and does not merge manual
  edits. Put your own initialization logic in the constructor *after* the
  `InitializeComponent()` call, or in the `Load` event, never inside that
  method body. If you're not using the Visual Studio designer at all (pure
  command-line/hand-written UI), the `InitializeComponent()` call itself can
  be omitted — it exists to support the designer's infrastructure, not
  because WinForms requires it.
- **`InitializeComponent()` is always one flat method — never split into
  private helper methods** (no `BuildTitleBar()`/`BuildToolbar()`/
  `BuildFooCollection()` calls). Every real designer-generated example —
  across simple forms, MDI children, custom controls, and even the C++/CLI
  and VB.NET generators — emits a single method body: field construction
  first, then one `//\n// controlName\n//` comment block per control with
  its properties and event-handler wireups assigned in place, then the
  parent's own settings, then every `Controls.Add` call grouped together at
  the end (added in reverse z-order — last-added ends up frontmost/topmost).
  The whole thing is bracketed once by `SuspendLayout()`/`ResumeLayout(false)`
  (plus `ISupportInitialize.BeginInit`/`EndInit` for controls that need it).
  This is a hard consequence of the designer being unable to synthesize
  helper-method calls in what it round-trips — the design surface reads back
  through `InitializeComponent()` line-by-line, so hand-written code that
  splits it into helpers isn't just non-idiomatic, it silently opts the form
  out of ever being re-openable in the visual designer. If you're building
  controls programmatically at real scale and want to break up the sheer line
  count, extract construction into a separate non-designer method with a
  distinct name (not `InitializeComponent`) called from the constructor
  *after* `InitializeComponent()`, or split unrelated sections of the form
  into their own `UserControl`s — don't fragment the one designer method
  itself.
- Whether a designed control becomes a named field (`this.button1`, declared
  in the `.Designer.cs` partial and usable from your `.cs` partial) or stays
  a method-local variable invisible outside `InitializeComponent()` is
  controlled by that control's **`Modifiers`**/**`GenerateMember`**
  properties in the designer's Properties window — set `GenerateMember` to
  `false` for a control you never need to reference from code, and the
  designer emits it as a local instead of a field.
- Non-trivial property state you don't want the designer to persist into
  `InitializeComponent()` (e.g. a derived/cached value) can be excluded with
  `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]`
  on that property.
- The designer file can become locked/unopenable if the file is read-only or
  the app is currently running under the debugger — "the designer cannot be
  modified at this time" means stop debugging (or check out the file) before
  editing the design surface, not a code problem to chase.
- `InitializeComponent()` always wraps mutations in
  `SuspendLayout()`/`ResumeLayout(false)` (and `ISupportInitialize.BeginInit`/
  `EndInit` for controls like `DataGridView` that need it) to avoid re-layout
  on every property set. Match that pattern if you build controls
  programmatically at scale.
- Override `Dispose(bool disposing)` and dispose `components` (an
  `IContainer` holding non-visual designer components like timers) — this is
  designer boilerplate, don't delete it even if a class looks unused.
- **`ResumeLayout(false)` does not run a layout pass.** The real designer gets
  away with this because `InitializeComponent()` also assigns every child's
  explicit `Location`/`Size`. A hand-rolled `.Designer.cs` or a custom
  container that instead relies on `Dock`/`Anchor` for its children will leave
  them frozen at whatever bounds they had when added, if the container is
  resized after the `SuspendLayout()` … `ResumeLayout(false)` pair (e.g. a
  `Dock = Bottom` panel that stretches to full width only once it's added to
  the form). Symptom: docked children clustered at their original small size in
  a corner. Fixes, cheapest first: don't `SuspendLayout` a container that has
  no designer-assigned child bounds; call `ResumeLayout(true)`; or add an
  explicit `PerformLayout()` after. A custom container that populates its own
  `Controls` in its constructor should not be wrapped in the parent designer's
  suspend/resume at all.

## Dark Mode (.NET 9+)

- `Application.SetColorMode(SystemColorMode.Classic | System | Dark)` — call it
  once at startup, alongside `ApplicationConfiguration.Initialize()`. It is
  **experimental**: the compiler reports diagnostic **`WFO5001`** as an *error*
  (not a warning) on `SetColorMode` and `SystemColorMode`, so the call site
  must `#pragma warning disable WFO5001` / `restore` (or the project must set
  `<NoWarn>$(NoWarn);WFO5001</NoWarn>`). Introduced experimental in .NET 9 and
  still experimental in .NET 10 — verify the current status against
  `https://learn.microsoft.com/dotnet/api/system.windows.forms.application.setcolormode`
  or the `dotnet/winforms` `docs/list-of-diagnostics.md` for the target TFM
  rather than assuming.
- With a color mode set, standard controls follow it automatically through
  `SystemColors` — including **multiline `TextBox`** (older "dark mode doesn't
  theme multiline text boxes" lore no longer holds when `SetColorMode` is
  actually applied). Don't hand-set `BackColor`/`ForeColor` to make a control
  dark; let it resolve `SystemColors.Window`/`WindowText`.
- Setting `BorderStyle = BorderStyle.None` on a `TextBox` opts out of the
  themed border too — keep the default `Fixed3D` (or `FixedSingle`) for a
  border that renders correctly in both light and dark.

## Thread Affinity — the Rule You Cannot Skip

A control's handle belongs to the thread that created it. Any property get/set
or method call from another thread either throws
(`InvalidOperationException` in debug, via a cross-thread-call check) or
corrupts state silently in release.

```csharp
public void WriteTextSafe(string text)
{
    if (textBox1.InvokeRequired)
        textBox1.Invoke(() => WriteTextSafe(text));
    else
        textBox1.Text += text;
}
```

- `InvokeRequired` compares the calling thread to the control's creating
  thread.
- `Invoke` marshals synchronously (blocks caller until the UI thread runs it);
  `BeginInvoke` marshals asynchronously (fire-and-forget, batch it for
  high-frequency updates rather than invoking per item).
- With `async`/`await`, the continuation after an `await` on a UI-thread
  method already resumes on the UI thread (via `SynchronizationContext`) — no
  manual `Invoke` needed there; only genuinely separate `Thread`/
  `Task.Run`-spawned callbacks need marshaling.
- A control created before its handle exists (`IsHandleCreated == false`)
  can't be targeted yet — defer or check `OnHandleCreated`.

Reference: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls

## Data Binding

- `Control.DataBindings.Add("Text", source, "PropertyName")` binds one
  property directly.
- `BindingSource` is the standard indirection layer: point it at a list or
  business object, then bind every control to the `BindingSource` instead of
  the raw data. This lets the underlying data source change at runtime
  without re-wiring each control, and it adapts a plain `IList` into
  `IBindingList` (add/remove/change notifications) even when the underlying
  type doesn't implement it.
- For live UI updates on property changes, the bound type must implement
  `INotifyPropertyChanged`; for structural list changes, use
  `BindingList<T>` or back the `BindingSource` with something that raises
  `ListChanged`.
- `BindingSource.Filter` / `.Sort` (string expressions, e.g.
  `"Country DESC, Address ASC"`) push filtering/sorting into the binding layer
  instead of the control.

Reference: https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/bindingsource-component-architecture

## Version Notes (.NET 6 → 10)

| Version | Relevant change |
|---|---|
| .NET 6 | `ApplicationConfiguration.Initialize()` replaces manual visual-styles setup; new project SDK-style `.csproj` with `<UseWindowsForms>true</UseWindowsForms>` |
| .NET 7 | High-DPI scaling for nested controls (e.g. buttons inside panels inside tab pages) — opt-in |
| .NET 8 | Nested-control DPI scaling from .NET 7 now on **by default** |
| .NET 9 | Async `Form`/control support patterns; `BinaryFormatter` removed (security); experimental dark-mode support added; high-DPI config should go through `Application.SetHighDpiMode`/`<ApplicationHighDpiMode>`, not `app.manifest` (see compiler warning `WFO0003`) |
| .NET 10 | `SetColorMode` dark mode **still experimental** (`WFO5001`); clipboard handling, accessibility, and designer improvements; new `ScreenCaptureMode` APIs; improved code analyzers |
| .NET 11 (preview) | Further dark-mode polish (e.g. `PropertyGrid`/`ProgressBar` default colors under Fluent theme) |

Always check `https://learn.microsoft.com/en-us/dotnet/desktop/winforms/whats-new`
(or query it via Context7) for the target TFM before assuming a pattern from
an older tutorial still applies — DPI and dark-mode behavior in particular has
changed every release since .NET 7.

## Quick Reference

| Need | API / pattern |
|---|---|
| Run the app | `Application.Run(new MainForm())` under `[STAThread] Main()` |
| Update UI from a background thread | `control.InvokeRequired` → `control.Invoke`/`BeginInvoke` |
| Bind a list to a grid | `BindingSource.DataSource = list;` then `grid.DataSource = bindingSource;` |
| React to a click | `+=` an `EventHandler` (designer wires this in `InitializeComponent`) |
| Configure DPI | project file `<ApplicationHighDpiMode>` or `Application.SetHighDpiMode` — not `app.manifest` (.NET 9+) |
| Clean up non-visual designer components | `components.Dispose()` inside overridden `Dispose(bool)` |

## Common Mistakes

- Editing inside `InitializeComponent()`/the designer's generated region by
  hand — the designer regenerates the whole method from scratch and doesn't
  merge manual edits; it gets clobbered on the next designer save.
- Splitting `InitializeComponent()` into helper methods like `BuildTitleBar()`
  / `BuildContent()` / `BuildFooList()` — real designer output is always one
  flat method; a hand-rolled `.Designer.cs` should match that shape even when
  written by hand, not invent a nicer-looking structure the designer itself
  never produces.
- Declaring a second `partial class Form1` block in the same file as an
  existing one, or splitting one form's designer output across two
  `.Designer.cs` files — only one partial definition per file is allowed.
- Editing a `.Designer.cs` file while the app is running under the debugger,
  then wondering why the designer refuses changes — stop debugging first.
- Touching a control from a `Task.Run`/`Thread` callback without checking
  `InvokeRequired` — works in a quick test, throws or corrupts state under
  real timing.
- Wrapping a custom container's children in the parent designer's
  `SuspendLayout()` … `ResumeLayout(false)` when those children use
  `Dock`/`Anchor` and the container gets resized afterward — they never
  re-layout and end up stuck at their initial bounds. See "The Control Tree
  and the Designer".
- Hand-setting `BackColor`/`ForeColor` (or a whole `ThemeService`) to fake
  dark mode instead of `Application.SetColorMode` — the framework themes
  standard controls for you on .NET 9+.
- A custom-painted control whose drawing depends on its size (rounded fill,
  gradient, size-relative layout in `OnPaint`) that does not
  `SetStyle(ControlStyles.ResizeRedraw, true)` — on a grow, WinForms
  invalidates only the newly exposed strip, so the previous size's paint
  stays underneath. Symptom: stacked/ghosted edges when the control is
  repeatedly resized (e.g. a chat bubble growing as text streams in).
- Trying to screenshot a Direct2D / DirectWrite / child-HWND control with
  `Control.DrawToBitmap` and getting a blank area — `DrawToBitmap` only
  captures GDI/GDI+ `WM_PRINT` output. See the `winforms-render-capture`
  skill for `PrintWindow` + `PW_RENDERFULLCONTENT`.
- Setting high-DPI mode via `app.manifest` on .NET 9+ — use the project
  property or `Application.SetHighDpiMode` instead (manifest approach is
  deprecated, flagged by `WFO0003`).
- Binding controls straight to a raw `List<T>` and expecting live UI updates —
  wrap it in a `BindingList<T>`/`BindingSource` and implement
  `INotifyPropertyChanged` on the bound type, or changes won't propagate.
- Assuming a WPF/MAUI concept (e.g. XAML markup, `DataContext`) applies here —
  WinForms has no separate markup layer and no implicit data-context
  inheritance; every binding is explicit.
