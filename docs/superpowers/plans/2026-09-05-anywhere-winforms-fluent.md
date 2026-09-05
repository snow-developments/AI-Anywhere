# Anywhere.WinForms.Fluent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `Anywhere.WinForms.Fluent`, a standalone, publicly
NuGet-published WinForms class library that emulates the Microsoft Fluent
Design System using Win32 APIs and Direct2D/DirectWrite (Vortice), so the
Anywhere app — and anyone else — gets WinUI-3-flavoured controls without XAML
Islands or a commercial suite.

**Architecture:** A `FluentControl : Control` base owns a Direct2D
`ID2D1HwndRenderTarget` and paints in `WM_PAINT` (the pattern
`Anywhere.Controls/MarkdownLabel.cs` already uses, generalised). A `FluentTheme`
token set resolves the Fluent 2 "Windows" ramps for light/dark. Concrete
controls (`FluentButton`, `FluentTextBox`, `FluentComboBox`, `FluentListBox`,
`FluentCard`) subclass the base and render per Fluent control metrics, with a
small easing-driven `StateAnimator` for hover/pressed/focus transitions.
`FluentWindow : Form` adds a Win32 custom caption plus DWM Mica/Acrylic backdrop
and rounded corners. The library ships multi-targeted (net8/9/10-windows) with
full NuGet packaging metadata.

**Tech Stack:** .NET 8/9/10 (`net{8,9,10}.0-windows`), WinForms
(`<UseWindowsForms>`), `Vortice.Direct2D1` 3.8.3 (brings `Vortice.DirectWrite`
transitively — same versions `Anywhere.Controls` already pins), Win32 P/Invoke
(`user32`, `dwmapi`, `uxtheme`), xUnit for the testable core.

**Spec:** `docs/superpowers/specs/2026-09-05-anywhere-winforms-fluent.md`
(embedded below under "Spec" — no separate file yet; promote it if this library
grows past v1).

## Global Constraints

- **Target frameworks:** `net8.0-windows;net9.0-windows;net10.0-windows`, all
  with `<UseWindowsForms>true</UseWindowsForms>`, `<Nullable>enable</Nullable>`,
  `<ImplicitUsings>enable</ImplicitUsings>`. `net10.0-windows` is what the
  Anywhere app consumes; the lower TFMs exist for public reuse.
- **No XAML / Windows App SDK / WinUI dependency.** The whole point of this
  library is Fluent *without* those. Direct2D + Win32 only.
- **One rendering stack:** `Vortice.Direct2D1` `Version="3.8.3"` and its
  transitive `Vortice.DirectWrite` — identical to
  `src/Anywhere.Controls/Anywhere.Controls.csproj`. Do not add `DirectN`,
  `SharpDX`, or `TerraFX`.
- **License:** MIT. `LICENSE` file at the library project root, `PackageLicenseExpression`
  = `MIT` in the csproj.
- **Public API surface is a contract.** Everything under
  `Anywhere.WinForms.Fluent` that is `public` is shipped to nuget.org — treat
  renames/removals as breaking. Keep internals `internal`.
- **Style:** repo `.editorconfig` / `.agents/guidance/Style Guide.md` — 2-space
  indent, K&R braces, `PascalCase` types/methods, `camelCase` locals/privates
  (no `_` prefix — matches `MarkdownLabel.cs`), file-scoped namespaces, UTF-8
  BOM + CRLF (run `dotnet format` before every commit).
- **Prose:** no first-person narration or "implementation notes" recaps in
  `src/`. XML-doc every public member with what it does, not how it evolved.
- **Git:** the agent never runs `git commit`. Each task's final "Commit" step
  means *stage and stop* — print `git status`, propose the message from
  `git-guidance`, wait for the user.
- **`dotnet watch` is always running** against `Anywhere.slnx`. Build/test lock
  errors (`CS2012`, `MSB3021`, `MSB3027` on `Anywhere*.dll/.pdb`) are the
  expected steady state — verify with `dotnet format --verify-no-changes`,
  `dotnet cslint`, and `dotnet test --artifacts-path <scratch>` into an
  isolated output dir; note the lock once and move on.

---

## Spec: Anywhere.WinForms.Fluent (v1)

### Motivation

Every "modern WinForms" library implements a *different* design language on
Win32 (Krypton → Office, MaterialSkin → Material, AntdUI → Ant, Sunny.UI →
Metro). None emulate the WinUI 3 Fluent Design System. `WinForms.Fluent.UI`
(sagemodeninja, MIT, 13★, last commit 2022-12) sketched the right architecture
— a `Control` with a D2D render target, an `IndicatedSurfaceBase` for the
hover/press indicator, `SegoeFluentIcons`, easing helpers — but is abandoned
and its core (`IndicatedSurfaceBase.DrawSurface`) is an empty stub.
`evorajhonj/WinForms.Fluent` (MIT, 2025) only does DWM window backdrops. This
library finishes that idea: a real, maintained, D2D-rendered Fluent control set.

### Fluent metrics (Fluent 2 "Windows" / WinUI 3 `generic.xaml`, v1 targets)

| Token | Light | Dark | Notes |
|---|---|---|---|
| `ControlCornerRadius` | 4 dip | 4 dip | rounded rect on buttons/inputs/cards |
| `OverlayCornerRadius` | 8 dip | 8 dip | flyouts / dropdown popup |
| Control stroke width | 1 dip | 1 dip | bottom edge slightly darker ("elevation border") |
| `ControlFillColorDefault` | `#B3FFFFFF` | `#0FFFFFFF` | button/input rest fill (over layer) |
| `ControlFillColorSecondary` | `#80F9F9F9` | `#15FFFFFF` | hover fill |
| `ControlFillColorTertiary` | `#4DF9F9F9` | `#0BFFFFFF` | pressed fill |
| `ControlFillColorDisabled` | `#4DF9F9F9` | `#0BFFFFFF` | disabled fill |
| `ControlStrokeColorDefault` | `#0F000000` | `#12FFFFFF` | rest border |
| `ControlStrokeColorSecondary` | `#29000000` | `#18FFFFFF` | bottom "accent" edge of border |
| `TextFillColorPrimary` | `#E4000000` | `#FFFFFFFF` | body text |
| `TextFillColorSecondary` | `#9E000000` | `#C8FFFFFF` | secondary text |
| `TextFillColorDisabled` | `#5C000000` | `#5DFFFFFF` | disabled text |
| `AccentFillColorDefault` | OS accent | OS accent | from `UISettings`/registry; fallback `#0078D4` |
| `LayerFillColorDefault` | `#80FFFFFF` | `#4C3A3A3A` | card background |
| Focus stroke | `#E4000000` outer + accent inner, 2 dip | mirror | keyboard focus rect |
| Accent underline (text input, focused) | accent, 2 dip, animates in from centre | same | the signature Fluent input affordance |
| Type ramp: Caption 12 / Body 14 / BodyStrong 14 semibold / Subtitle 20 / Title 28 | — | — | `Segoe UI Variable Text` (fallback `Segoe UI`), sizes in dip |
| Control min height | 32 dip | — | buttons, single-line inputs |
| Control default padding | `11,5,11,6` dip | — | button content |
| Motion | 150 ms hover, 83 ms pressed, `cubic-bezier(0, 0, 0, 1)` (FluentEase) | — | `StateAnimator` |

OS accent colour: read `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Accent`
→ `AccentColorMenu`, or `Windows.UI.ViewManagement.UISettings` if the runtime
allows; fall back to `#0078D4`. Dark/light: `Application.IsDarkModeEnabled`
(.NET 9+) when available, else `HKCU\...\Themes\Personalize\AppsUseLightTheme == 0`.

### Public API (v1)

```
Anywhere.WinForms.Fluent
  FluentTheme                     // static; Current, Changed event, Resolve(token)
    enum FluentThemeMode { System, Light, Dark }
    FluentTheme.Mode { get; set; }
  FluentColor                     // static ColorRef helpers: AlphaBlend, IsDark, WithAlpha
  FluentControl : Control         // abstract D2D base
    protected abstract void RenderContent(FluentRenderContext ctx);
  FluentRenderContext             // { ID2D1RenderTarget Target; IDWriteFactory DWrite; FluentTheme Theme; float Dpi; RectF Bounds; }
  enum ControlVisualState { Rest, Hover, Pressed, Disabled, Focused }
  FluentButton : FluentControl    // ButtonStyle { Standard, Accent, Subtle }
  FluentTextBox : FluentControl   // hosts a child TextBox; Text, PlaceholderText, ReadOnly
  FluentComboBox : FluentControl  // hosts a child ComboBox; Items, SelectedItem, SelectedIndex, DropDownStyle
  FluentListBox : FluentControl   // Items, SelectedItem, SelectedIndex, DataSource, DisplayMember
  FluentCard : FluentControl      // container; CornerRadius, Elevation
  FluentWindow : Form             // custom caption + Mica/Acrylic; BackdropType { Mica, MicaAlt, Acrylic, None }
  SegoeFluentIcons                // static string glyph constants + Font(size) factory
```

### Non-goals (v1)

Navigation view, tab view, ribbon, data grid, teaching tips, content dialogs,
info bars, expander, tree view, a XAML-style resource-dictionary system,
RTL mirroring, a visual designer experience beyond "drops on the surface and
paints." No animation system beyond per-control state transitions.

### Testing strategy

- **xUnit** for everything non-visual: `FluentTheme` token resolution per mode,
  `FluentColor` math, DPI dip↔px conversion, rounded-rect geometry point
  output, `StateAnimator` transition values over time, shared-factory
  ref-counting.
- **Manual smoke + render capture** for pixels: a `Anywhere.WinForms.Fluent.Gallery`
  WinExe, plus PNG captures via the `winforms-render-capture` skill
  (`PrintWindow` + `PW_RENDERFULLCONTENT`) checked into `docs/` for the README.
- No attempt to pixel-diff in CI (v1).

---

## File Structure

```
src/Anywhere.WinForms.Fluent/
  Anywhere.WinForms.Fluent.csproj   multi-target, packable, MIT
  LICENSE                            MIT text
  README.md                         nuget README (PackageReadmeFile)
  Theming/
    FluentTheme.cs                   token store, mode, Changed event
    FluentThemeTokens.cs             the light/dark value tables (from the Spec metrics)
    FluentColor.cs                   AlphaBlend / IsDark / WithAlpha / accent lookup
    Dpi.cs                           DipToPx / PxToDip / PointsToDip
  Rendering/
    FluentControl.cs                 abstract Control + D2D lifecycle
    FluentRenderContext.cs           readonly struct passed to RenderContent
    D2DFactories.cs                  shared ref-counted ID2D1Factory / IDWriteFactory
    Geometry.cs                      RoundedRect path-geometry builder
  Motion/
    FluentEase.cs                    cubic-bezier(0,0,0,1) sampler
    StateAnimator.cs                 timer + easing → 0..1 per visual state
    ControlVisualState.cs            enum
  Controls/
    FluentButton.cs
    FluentTextBox.cs
    FluentComboBox.cs
    FluentListBox.cs
    FluentCard.cs
  Windowing/
    FluentWindow.cs                  custom caption + DWM backdrop
    NativeCaption.cs                 WM_NCCALCSIZE / WM_NCHITTEST helpers
    Dwm.cs                           dwmapi P/Invoke (backdrop, dark titlebar, corners)
  Icons/
    SegoeFluentIcons.cs              glyph constants + Font(size)
  Interop/
    NativeMethods.cs                 user32 / uxtheme P/Invoke

src/Anywhere.WinForms.Fluent.Tests/
  Anywhere.WinForms.Fluent.Tests.csproj   net10.0-windows, xUnit
  FluentThemeTests.cs
  FluentColorTests.cs
  DpiTests.cs
  GeometryTests.cs
  StateAnimatorTests.cs
  D2DFactoriesTests.cs

src/Anywhere.WinForms.Fluent.Gallery/
  Anywhere.WinForms.Fluent.Gallery.csproj   net10.0-windows WinExe
  Program.cs
  GalleryForm.cs                     one section per control, light/dark toggle
```

Add all three projects to `Anywhere.slnx`.

**Prerequisite (done in the session that produced this plan):** the Anywhere
app was reverted off `Krypton.Toolkit` back to stock WinForms controls +
`Application.SetColorMode(SystemColorMode.System)`. Phase 5 re-introduces
styling, this time via this library.

---

## Task 1: Project skeleton + packaging

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Anywhere.WinForms.Fluent.csproj`
- Create: `src/Anywhere.WinForms.Fluent/LICENSE`
- Create: `src/Anywhere.WinForms.Fluent/README.md`
- Create: `src/Anywhere.WinForms.Fluent/Theming/Dpi.cs`
- Create: `src/Anywhere.WinForms.Fluent.Tests/Anywhere.WinForms.Fluent.Tests.csproj`
- Create: `src/Anywhere.WinForms.Fluent.Tests/DpiTests.cs`
- Modify: `Anywhere.slnx`

**Interfaces:**

- Consumes: nothing.
- Produces: `Anywhere.WinForms.Fluent.Theming.Dpi` — `static float DipToPx(float dip, float dpi)`,
  `static float PxToDip(float px, float dpi)`, `static float PointsToDip(float points)`
  (`points * 96f / 72f`, matching `MarkdownLabel.FontExtensions.SizeInDips`).

- [ ] **Step 1: Write the csproj**

```xml
<!-- src/Anywhere.WinForms.Fluent/Anywhere.WinForms.Fluent.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net8.0-windows;net9.0-windows;net10.0-windows</TargetFrameworks>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>

    <PackageId>Anywhere.WinForms.Fluent</PackageId>
    <Version>0.1.0-preview.1</Version>
    <Authors>Chance Snow</Authors>
    <Description>Fluent Design System controls for WinForms, rendered with Direct2D/DirectWrite. No XAML Islands, no Windows App SDK.</Description>
    <PackageTags>winforms;fluent;winui;direct2d;fluent-design;windows-11</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageProjectUrl>https://github.com/chances/ai-anywhere</PackageProjectUrl>
    <RepositoryUrl>https://github.com/chances/ai-anywhere</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <Deterministic>true</Deterministic>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <None Include="LICENSE" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <!-- Same versions Anywhere.Controls pins; DirectWrite comes transitively. -->
    <PackageReference Include="Vortice.Direct2D1" Version="3.8.3" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Add `LICENSE` (MIT) and a first-pass `README.md`**

`LICENSE`: standard MIT text, copyright `2026 Chance Snow`. `README.md`: one
paragraph of what it is (from the Spec Motivation), a "Status: preview" line, a
30-second usage snippet placeholder (`new FluentButton { Text = "Click" }`), and
a "Built with Direct2D via Vortice" note. Screenshots get added in Phase 5.

- [ ] **Step 3: Write the failing test**

```csharp
// src/Anywhere.WinForms.Fluent.Tests/DpiTests.cs
using Anywhere.WinForms.Fluent.Theming;
using Xunit;

public class DpiTests {
  [Fact]
  public void DipToPx_scales_by_dpi_over_96() {
    Assert.Equal(150f, Dpi.DipToPx(100f, 144f), 3);
    Assert.Equal(100f, Dpi.DipToPx(100f, 96f), 3);
  }

  [Fact]
  public void PxToDip_is_the_inverse_of_DipToPx() {
    Assert.Equal(100f, Dpi.PxToDip(Dpi.DipToPx(100f, 120f), 120f), 3);
  }

  [Fact]
  public void PointsToDip_converts_72pt_basis_to_96dip_basis() {
    Assert.Equal(12f, Dpi.PointsToDip(9f), 3); // 9pt Segoe UI -> 12 dip
  }
}
```

- [ ] **Step 4: Run it, expect FAIL**

`dotnet test src/Anywhere.WinForms.Fluent.Tests/Anywhere.WinForms.Fluent.Tests.csproj --artifacts-path "$SCRATCH/fluent" --filter DpiTests`
Expected: FAIL — `Dpi` does not exist.

- [ ] **Step 5: Implement `Dpi`**

```csharp
// src/Anywhere.WinForms.Fluent/Theming/Dpi.cs
namespace Anywhere.WinForms.Fluent.Theming;

/// <summary>Device-independent-pixel (dip; 1 dip = 1/96 inch) conversions.</summary>
public static class Dpi {
  public const float Default = 96f;

  public static float DipToPx(float dip, float dpi) => dip * dpi / Default;

  public static float PxToDip(float px, float dpi) => px * Default / dpi;

  /// <summary>WinForms <see cref="System.Drawing.Font.SizeInPoints"/> (1/72 inch) to dip.</summary>
  public static float PointsToDip(float points) => points * Default / 72f;
}
```

- [ ] **Step 6: Run it, expect PASS**

Same command as Step 4. Expected: PASS (3 tests).

- [ ] **Step 7: Register projects in `Anywhere.slnx`**

Add `<Project Path="src/Anywhere.WinForms.Fluent/Anywhere.WinForms.Fluent.csproj" />`
and the `.Tests` project to `Anywhere.slnx`. Create the test csproj now
(net10.0-windows, `<UseWindowsForms>true`, xUnit + `xunit.runner.visualstudio` +
`Microsoft.NET.Test.Sdk`, `ProjectReference` to the library) — copy the package
versions from `src/Anywhere.Tests/Anywhere.Tests.csproj`.

- [ ] **Step 8: `dotnet format` + verify build**

`dotnet format Anywhere.slnx` then
`dotnet build src/Anywhere.WinForms.Fluent/Anywhere.WinForms.Fluent.csproj --artifacts-path "$SCRATCH/fluent"`.
Expected: builds for all three TFMs, 0 warnings.

- [ ] **Step 9: Commit**

```bash
git add src/Anywhere.WinForms.Fluent src/Anywhere.WinForms.Fluent.Tests Anywhere.slnx
git commit -m "feat(fluent): scaffold Anywhere.WinForms.Fluent library + DPI helpers"
```

---

## Task 2: Colour helpers + theme tokens

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Theming/FluentColor.cs`
- Create: `src/Anywhere.WinForms.Fluent/Theming/FluentThemeTokens.cs`
- Create: `src/Anywhere.WinForms.Fluent/Theming/FluentTheme.cs`
- Test: `src/Anywhere.WinForms.Fluent.Tests/FluentColorTests.cs`
- Test: `src/Anywhere.WinForms.Fluent.Tests/FluentThemeTests.cs`

**Interfaces:**

- Consumes: nothing.
- Produces:
  - `FluentColor` (static): `Color AlphaBlend(Color over, Color under)` (premultiplied
    source-over), `bool IsDark(Color c)` (`c.GetBrightness() < 0.5f`),
    `Color WithAlpha(Color c, int a)`, `Color AccentColor()` (registry lookup +
    `#0078D4` fallback).
  - `enum FluentThemeMode { System, Light, Dark }`.
  - `enum FluentToken { ControlFillDefault, ControlFillSecondary, ControlFillTertiary,
    ControlFillDisabled, ControlStrokeDefault, ControlStrokeSecondary, TextPrimary,
    TextSecondary, TextDisabled, AccentDefault, LayerFillDefault, FocusStrokeOuter }`
    (extend as controls need; keep additive).
  - `FluentTheme` (static): `FluentThemeMode Mode { get; set; }` (default `System`),
    `bool IsDark { get; }` (resolves `System` via `Application.IsDarkModeEnabled`
    when present else the `AppsUseLightTheme` registry value),
    `Color Get(FluentToken token)`, `float CornerRadius => 4f`,
    `float OverlayCornerRadius => 8f`, `float ControlMinHeight => 32f`,
    `Padding ControlPadding => new(11, 5, 11, 6)`,
    `event EventHandler? Changed` (raised from `Mode` setter and from a
    `SystemEvents.UserPreferenceChanged` subscription).

- [ ] **Step 1: Failing tests for `FluentColor`**

```csharp
// src/Anywhere.WinForms.Fluent.Tests/FluentColorTests.cs
using System.Drawing;
using Anywhere.WinForms.Fluent.Theming;
using Xunit;

public class FluentColorTests {
  [Fact]
  public void AlphaBlend_opaque_over_returns_over() {
    var result = FluentColor.AlphaBlend(Color.FromArgb(255, 10, 20, 30), Color.White);
    Assert.Equal(Color.FromArgb(255, 10, 20, 30), result);
  }

  [Fact]
  public void AlphaBlend_transparent_over_returns_under() {
    var result = FluentColor.AlphaBlend(Color.FromArgb(0, 10, 20, 30), Color.FromArgb(255, 200, 200, 200));
    Assert.Equal(Color.FromArgb(255, 200, 200, 200), result);
  }

  [Fact]
  public void AlphaBlend_half_over_white_is_midpoint() {
    var result = FluentColor.AlphaBlend(Color.FromArgb(128, 0, 0, 0), Color.White);
    Assert.InRange(result.R, 126, 129);
  }

  [Fact]
  public void IsDark_true_for_near_black_false_for_near_white() {
    Assert.True(FluentColor.IsDark(Color.FromArgb(20, 20, 20)));
    Assert.False(FluentColor.IsDark(Color.FromArgb(240, 240, 240)));
  }
}
```

- [ ] **Step 2: Run, expect FAIL** (`FluentColor` undefined).
  `dotnet test ... --filter FluentColorTests`

- [ ] **Step 3: Implement `FluentColor`**

```csharp
// src/Anywhere.WinForms.Fluent/Theming/FluentColor.cs
using System.Drawing;
using Microsoft.Win32;

namespace Anywhere.WinForms.Fluent.Theming;

/// <summary>Colour math shared by the Fluent renderers.</summary>
public static class FluentColor {
  /// <summary>Source-over composite of <paramref name="over"/> onto opaque <paramref name="under"/>.</summary>
  public static Color AlphaBlend(Color over, Color under) {
    float a = over.A / 255f;
    return Color.FromArgb(
      255,
      (int)MathF.Round(over.R * a + under.R * (1 - a)),
      (int)MathF.Round(over.G * a + under.G * (1 - a)),
      (int)MathF.Round(over.B * a + under.B * (1 - a)));
  }

  public static bool IsDark(Color c) => c.GetBrightness() < 0.5f;

  public static Color WithAlpha(Color c, int alpha) => Color.FromArgb(alpha, c);

  /// <summary>OS accent colour, or <c>#0078D4</c> when it cannot be read.</summary>
  public static Color AccentColor() {
    try {
      using var key = Registry.CurrentUser.OpenSubKey(
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");
      if (key?.GetValue("AccentColorMenu") is int bgr) {
        return Color.FromArgb((bgr >> 0) & 0xFF, (bgr >> 8) & 0xFF, (bgr >> 16) & 0xFF);
      }
    } catch { /* fall through */ }
    return Color.FromArgb(0x00, 0x78, 0xD4);
  }
}
```

- [ ] **Step 4: Run, expect PASS.**

- [ ] **Step 5: Failing tests for `FluentTheme`**

```csharp
// src/Anywhere.WinForms.Fluent.Tests/FluentThemeTests.cs
using System.Drawing;
using Anywhere.WinForms.Fluent.Theming;
using Xunit;

public class FluentThemeTests {
  [Fact]
  public void Explicit_light_and_dark_resolve_different_text_colors() {
    FluentTheme.Mode = FluentThemeMode.Light;
    var light = FluentTheme.Get(FluentToken.TextPrimary);
    FluentTheme.Mode = FluentThemeMode.Dark;
    var dark = FluentTheme.Get(FluentToken.TextPrimary);
    FluentTheme.Mode = FluentThemeMode.System;

    Assert.NotEqual(light, dark);
    Assert.True(FluentColor.IsDark(light));   // dark text on light theme
    Assert.False(FluentColor.IsDark(dark));   // light text on dark theme
  }

  [Fact]
  public void Changed_fires_when_mode_changes() {
    var fired = 0;
    void H(object? s, EventArgs e) => fired++;
    FluentTheme.Changed += H;
    FluentTheme.Mode = FluentTheme.Mode == FluentThemeMode.Dark ? FluentThemeMode.Light : FluentThemeMode.Dark;
    FluentTheme.Changed -= H;
    FluentTheme.Mode = FluentThemeMode.System;

    Assert.True(fired >= 1);
  }

  [Fact]
  public void Metrics_match_the_spec() {
    Assert.Equal(4f, FluentTheme.CornerRadius);
    Assert.Equal(32f, FluentTheme.ControlMinHeight);
  }
}
```

- [ ] **Step 6: Run, expect FAIL.**

- [ ] **Step 7: Implement `FluentThemeTokens` + `FluentTheme`**

`FluentThemeTokens.cs`: two `static readonly Dictionary<FluentToken, Color>`
(or a `switch` expression) — `Light` and `Dark` — filled verbatim from the
Spec's "Fluent metrics" table (`ControlFillColorDefault` etc.), each value the
*flattened* colour (`FluentColor.AlphaBlend` the token's `#AARRGGBB` over the
base layer `#FFFFFF` / `#202020` at authoring time, so renderers get opaque
colours). `AccentDefault` returns `FluentColor.AccentColor()` live.

```csharp
// src/Anywhere.WinForms.Fluent/Theming/FluentTheme.cs (shape)
using System.Drawing;
using Microsoft.Win32;

namespace Anywhere.WinForms.Fluent.Theming;

public enum FluentThemeMode { System, Light, Dark }

public enum FluentToken {
  ControlFillDefault, ControlFillSecondary, ControlFillTertiary, ControlFillDisabled,
  ControlStrokeDefault, ControlStrokeSecondary,
  TextPrimary, TextSecondary, TextDisabled,
  AccentDefault, LayerFillDefault, FocusStrokeOuter,
}

/// <summary>App-wide Fluent token store. Not thread-safe; touch from the UI thread.</summary>
public static class FluentTheme {
  private static FluentThemeMode mode = FluentThemeMode.System;

  static FluentTheme() =>
    SystemEvents.UserPreferenceChanged += (_, _) => Changed?.Invoke(null, EventArgs.Empty);

  public static event EventHandler? Changed;

  public static FluentThemeMode Mode {
    get => mode;
    set {
      if (mode == value) return;
      mode = value;
      Changed?.Invoke(null, EventArgs.Empty);
    }
  }

  public static bool IsDark => mode switch {
    FluentThemeMode.Light => false,
    FluentThemeMode.Dark => true,
    _ => SystemPrefersDark(),
  };

  public static float CornerRadius => 4f;
  public static float OverlayCornerRadius => 8f;
  public static float ControlMinHeight => 32f;
  public static Padding ControlPadding => new(11, 5, 11, 6);

  public static Color Get(FluentToken token) {
    if (token == FluentToken.AccentDefault) return FluentColor.AccentColor();
    return (IsDark ? FluentThemeTokens.Dark : FluentThemeTokens.Light)[token];
  }

  private static bool SystemPrefersDark() {
    // Prefer the framework signal when the running TFM has it.
    try {
      var prop = typeof(Application).GetProperty("IsDarkModeEnabled");
      if (prop?.GetValue(null) is bool b) return b;
    } catch { /* fall through */ }
    try {
      using var key = Registry.CurrentUser.OpenSubKey(
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
      return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
    } catch { return false; }
  }
}
```

- [ ] **Step 8: Run, expect PASS** (all `FluentThemeTests` + `FluentColorTests`).

- [ ] **Step 9: `dotnet format`, then commit**

```bash
git add src/Anywhere.WinForms.Fluent/Theming src/Anywhere.WinForms.Fluent.Tests
git commit -m "feat(fluent): theme token store + colour helpers"
```

---

## Task 3: Shared D2D factories + geometry

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Rendering/D2DFactories.cs`
- Create: `src/Anywhere.WinForms.Fluent/Rendering/Geometry.cs`
- Test: `src/Anywhere.WinForms.Fluent.Tests/D2DFactoriesTests.cs`
- Test: `src/Anywhere.WinForms.Fluent.Tests/GeometryTests.cs`

**Interfaces:**

- Consumes: `Vortice.Direct2D1`, `Vortice.DirectWrite`.
- Produces:
  - `D2DFactories` (static): `static ID2D1Factory AcquireD2D()`,
    `static IDWriteFactory AcquireDWrite()`, `static void Release()` — each
    `Acquire*` bumps a refcount and lazily creates the singleton; `Release`
    decrements and disposes at zero. `int RefCount { get; }` for tests.
    Rationale: one `MarkdownLabel`-style factory per control (as the abandoned
    lib did) wastes handles when a form has 30 Fluent controls.
  - `Geometry` (static): `static ID2D1PathGeometry RoundedRect(ID2D1Factory factory,
    RectF rect, float radius)` — closed rounded rectangle, corner arcs of
    `radius`, clamped to `min(rect.Width, rect.Height) / 2`.

- [ ] **Step 1: Failing test for factory ref-counting**

```csharp
// src/Anywhere.WinForms.Fluent.Tests/D2DFactoriesTests.cs
using Anywhere.WinForms.Fluent.Rendering;
using Xunit;

public class D2DFactoriesTests {
  [Fact]
  public void Acquire_twice_reuses_the_same_instance_and_refcounts() {
    var a = D2DFactories.AcquireD2D();
    var b = D2DFactories.AcquireD2D();
    try {
      Assert.Same(a, b);
      Assert.Equal(2, D2DFactories.RefCount);
    } finally {
      D2DFactories.Release();
      D2DFactories.Release();
    }
    Assert.Equal(0, D2DFactories.RefCount);
  }
}
```

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement `D2DFactories`**

```csharp
// src/Anywhere.WinForms.Fluent/Rendering/D2DFactories.cs
using Vortice.Direct2D1;
using Vortice.DirectWrite;

namespace Anywhere.WinForms.Fluent.Rendering;

/// <summary>
/// Process-wide, ref-counted Direct2D / DirectWrite factories. Each
/// <see cref="FluentControl"/> acquires on handle-create and releases on
/// handle-destroy so a form full of Fluent controls shares one of each.
/// </summary>
public static class D2DFactories {
  private static readonly object gate = new();
  private static ID2D1Factory? d2d;
  private static IDWriteFactory? dwrite;
  private static int refs;

  public static int RefCount { get { lock (gate) return refs; } }

  public static ID2D1Factory AcquireD2D() {
    lock (gate) {
      d2d ??= D2D1.D2D1CreateFactory<ID2D1Factory>(FactoryType.SingleThreaded);
      refs++;
      return d2d;
    }
  }

  public static IDWriteFactory AcquireDWrite() {
    lock (gate) {
      dwrite ??= DWrite.DWriteCreateFactory<IDWriteFactory>();
      // note: DWrite acquisitions share the D2D refcount for simplicity —
      // a control always acquires both together.
      return dwrite;
    }
  }

  public static void Release() {
    lock (gate) {
      if (refs == 0) return;
      if (--refs > 0) return;
      d2d?.Dispose(); d2d = null;
      dwrite?.Dispose(); dwrite = null;
    }
  }
}
```

- [ ] **Step 4: Run, expect PASS.**

- [ ] **Step 5: Failing test for `Geometry.RoundedRect`**

```csharp
// src/Anywhere.WinForms.Fluent.Tests/GeometryTests.cs
using System.Drawing;
using Anywhere.WinForms.Fluent.Rendering;
using Xunit;

public class GeometryTests {
  [Fact]
  public void RoundedRect_bounds_match_input_and_it_is_closed() {
    var factory = D2DFactories.AcquireD2D();
    try {
      using var geo = Geometry.RoundedRect(factory, new RectangleF(0, 0, 100, 40), 4f);
      var bounds = geo.GetBounds();
      Assert.Equal(0f, bounds.Left, 1);
      Assert.Equal(0f, bounds.Top, 1);
      Assert.Equal(100f, bounds.Right, 1);
      Assert.Equal(40f, bounds.Bottom, 1);
    } finally {
      D2DFactories.Release();
    }
  }

  [Fact]
  public void RoundedRect_clamps_radius_to_half_the_short_side() {
    var factory = D2DFactories.AcquireD2D();
    try {
      // radius 999 on a 20-tall rect must not throw or invert
      using var geo = Geometry.RoundedRect(factory, new RectangleF(0, 0, 100, 20), 999f);
      var b = geo.GetBounds();
      Assert.Equal(20f, b.Bottom - b.Top, 1);
    } finally {
      D2DFactories.Release();
    }
  }
}
```

- [ ] **Step 6: Run, expect FAIL.**

- [ ] **Step 7: Implement `Geometry.RoundedRect`**

Use `ID2D1Factory.CreateRoundedRectangleGeometry` if Vortice exposes it
directly (`RoundedRectangle { Rect, RadiusX, RadiusY }`); that is simpler and
faster than a hand-built path sink. Fall back to a `CreatePathGeometry` +
`GeometrySink` (arc segments) only if the rounded-rect primitive is missing.
Clamp `radius = MathF.Min(radius, MathF.Min(rect.Width, rect.Height) / 2f)`.
Return type is `ID2D1Geometry`-compatible; if using the primitive, the test's
`using var geo` type becomes `ID2D1RoundedRectangleGeometry` — adjust the test
signature to `ID2D1Geometry geo` accordingly.

- [ ] **Step 8: Run, expect PASS.**

- [ ] **Step 9: `dotnet format`, commit**

```bash
git add src/Anywhere.WinForms.Fluent/Rendering src/Anywhere.WinForms.Fluent.Tests
git commit -m "feat(fluent): shared D2D factories + rounded-rect geometry"
```

---

## Task 4: Motion — easing + state animator

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Motion/ControlVisualState.cs`
- Create: `src/Anywhere.WinForms.Fluent/Motion/FluentEase.cs`
- Create: `src/Anywhere.WinForms.Fluent/Motion/StateAnimator.cs`
- Test: `src/Anywhere.WinForms.Fluent.Tests/StateAnimatorTests.cs`

**Interfaces:**

- Consumes: nothing (pure math + a `System.Windows.Forms.Timer`).
- Produces:
  - `enum ControlVisualState { Rest, Hover, Pressed, Disabled, Focused }`.
  - `FluentEase` (static): `static float Sample(float t)` — cubic-bezier
    `(0,0,0,1)` for `t` in `[0,1]`, clamped.
  - `StateAnimator` : `IDisposable` — ctor `StateAnimator(Action invalidate)`;
    `void GoTo(ControlVisualState state, int durationMs)`;
    `float Progress { get; }` (0..1 eased toward the target);
    `ControlVisualState Current { get; }`; raises the `invalidate` callback on
    each tick. Uses a 16 ms `WinForms.Timer`; stops itself at `Progress == 1`.

- [ ] **Step 1: Failing tests**

```csharp
// src/Anywhere.WinForms.Fluent.Tests/StateAnimatorTests.cs
using Anywhere.WinForms.Fluent.Motion;
using Xunit;

public class StateAnimatorTests {
  [Fact]
  public void Ease_endpoints_are_exact_and_midpoint_is_past_halfway() {
    Assert.Equal(0f, FluentEase.Sample(0f), 3);
    Assert.Equal(1f, FluentEase.Sample(1f), 3);
    Assert.True(FluentEase.Sample(0.5f) > 0.5f); // decelerating curve
  }

  [Fact]
  public void Ease_clamps_out_of_range_input() {
    Assert.Equal(0f, FluentEase.Sample(-1f), 3);
    Assert.Equal(1f, FluentEase.Sample(2f), 3);
  }

  [Fact]
  public void GoTo_zero_duration_snaps_progress_to_one() {
    using var a = new StateAnimator(() => { });
    a.GoTo(ControlVisualState.Hover, 0);
    Assert.Equal(ControlVisualState.Hover, a.Current);
    Assert.Equal(1f, a.Progress, 3);
  }
}
```

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement `ControlVisualState` + `FluentEase`**

`FluentEase.Sample`: Newton-iterate the cubic Bézier `x(t)` for the parameter
at the given `x`, then evaluate `y(t)`. Control points `P1=(0,0)`, `P2=(0,1)`
give the standard Fluent "decelerate" curve (`cubic-bezier(0,0,0,1)`).

- [ ] **Step 4: Implement `StateAnimator`**

```csharp
// src/Anywhere.WinForms.Fluent/Motion/StateAnimator.cs
using System.Diagnostics;

namespace Anywhere.WinForms.Fluent.Motion;

/// <summary>Drives a 0..1 eased transition between visual states, ticking a WinForms timer.</summary>
public sealed class StateAnimator : IDisposable {
  private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
  private readonly Action invalidate;
  private readonly Stopwatch clock = new();
  private int durationMs;
  private float from;

  public StateAnimator(Action invalidate) {
    this.invalidate = invalidate;
    timer.Tick += (_, _) => Tick();
  }

  public ControlVisualState Current { get; private set; } = ControlVisualState.Rest;
  public float Progress { get; private set; } = 1f;

  public void GoTo(ControlVisualState state, int durationMs) {
    if (state == Current && Progress >= 1f) return;
    Current = state;
    this.durationMs = durationMs;
    from = 0f;
    if (durationMs <= 0) { Progress = 1f; invalidate(); return; }
    Progress = 0f;
    clock.Restart();
    timer.Start();
  }

  private void Tick() {
    float linear = Math.Clamp((float)clock.Elapsed.TotalMilliseconds / durationMs, 0f, 1f);
    Progress = FluentEase.Sample(linear);
    invalidate();
    if (linear >= 1f) { timer.Stop(); clock.Reset(); }
  }

  public void Dispose() { timer.Dispose(); }
}
```

- [ ] **Step 5: Run, expect PASS.**

- [ ] **Step 6: `dotnet format`, commit**

```bash
git add src/Anywhere.WinForms.Fluent/Motion src/Anywhere.WinForms.Fluent.Tests
git commit -m "feat(fluent): easing curve + state transition animator"
```

---

## Task 5: `FluentControl` D2D base + `FluentRenderContext`

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Rendering/FluentRenderContext.cs`
- Create: `src/Anywhere.WinForms.Fluent/Rendering/FluentControl.cs`
- Create: `src/Anywhere.WinForms.Fluent.Gallery/*` (skeleton WinExe with one
  `FluentControl` subclass proving paint works)

**Interfaces:**

- Consumes: `D2DFactories` (Task 3), `Geometry` (Task 3), `FluentTheme`
  (Task 2), `StateAnimator` (Task 4).
- Produces:
  - `readonly struct FluentRenderContext { ID2D1RenderTarget Target; IDWriteFactory DWrite;
    RectangleF Bounds; float Dpi; bool IsDark; }` plus helper
    `ID2D1SolidColorBrush Brush(FluentToken token)` (cached per render pass).
  - `abstract class FluentControl : Control` —
    `protected abstract void RenderContent(in FluentRenderContext ctx);`
    `protected StateAnimator State { get; }`
    `protected void RequestPaint();` (`Invalidate()` from any thread via
    `BeginInvoke`). Handles `SetStyle(UserPaint|AllPaintingInWmPaint|
    OptimizedDoubleBuffer|ResizeRedraw|SupportsTransparentBackColor, true)`,
    `OnHandleCreated` (acquire factories + create `ID2D1HwndRenderTarget`),
    `OnHandleDestroyed` (dispose target, `D2DFactories.Release()`),
    `OnResize` (`renderTarget.Resize`), `WndProc` `WM_PAINT`
    (`BeginDraw`/`Clear`/`RenderContent`/`EndDraw`, and on
    `EndDraw` returning `D2DERR_RECREATE_TARGET` drop + rebuild the target and
    invalidate), `OnDpiChangedAfterParent` (rebuild target, `RequestPaint`),
    and subscribes to `FluentTheme.Changed` → `RequestPaint`.

- [ ] **Step 1: Write `FluentRenderContext`**

Readonly struct as specified. `Brush(token)` creates an
`ID2D1SolidColorBrush` from `FluentTheme.Get(token)` (converted via a
`ToColor4()` extension copied from `MarkdownLabel.ColorExtensions`); the
caller (`FluentControl`) owns a per-pass `List<IDisposable>` and disposes
after `EndDraw`.

- [ ] **Step 2: Write `FluentControl`** (full lifecycle)

**REQUIRED SUB-SKILL:** Use `.agents/skills/winforms-direct2d-interop` — the
render-target lifecycle, `D2DERR_RECREATE_TARGET` recovery, DPI, and factory
sharing are all specified there. Model directly on
`src/Anywhere.Controls/MarkdownLabel.cs` lines 121-156 & 307-315 (render-target create / resize / paint / handle-destroy), but:
generalise `OnPaint` → `WndProc(WM_PAINT)` per the abandoned lib's
`IndicatedButton`, add `D2DERR_RECREATE_TARGET` handling (compare the `HRESULT`
from `EndDraw`; on `0x8899000C` dispose `renderTarget`, null it, `Invalidate()`),
and pull the factory from `D2DFactories` instead of creating a private one.

```csharp
// key shape only — the executor writes the rest against MarkdownLabel's pattern
protected override void WndProc(ref Message m) {
  if (m.Msg == 0x000F /* WM_PAINT */) { PaintD2D(); }
  base.WndProc(ref m);
}

private void PaintD2D() {
  if (!EnsureTarget()) return;
  renderTarget!.BeginDraw();
  renderTarget.Clear(Parent?.BackColor.ToColor4() ?? BackColor.ToColor4());
  var ctx = new FluentRenderContext(renderTarget, dwrite!, ClientRectangle, DeviceDpi, FluentTheme.IsDark);
  try { RenderContent(in ctx); } finally { ctx.DisposeBrushes(); }
  if (renderTarget.EndDraw().Failure) { renderTarget.Dispose(); renderTarget = null; Invalidate(); }
}
```

- [ ] **Step 3: Gallery skeleton**

`Anywhere.WinForms.Fluent.Gallery` WinExe: `Program.cs`
(`ApplicationConfiguration.Initialize()`, `Application.SetColorMode(System)`,
`Application.Run(new GalleryForm())`), `GalleryForm` with a single
`DebugSwatch : FluentControl` whose `RenderContent` fills a
`Geometry.RoundedRect` with `FluentToken.AccentDefault` — proves the base
paints. Add a light/dark toggle button wired to `FluentTheme.Mode`.

- [ ] **Step 4: Manual verification (render capture)**

Per the `winforms-render-capture` skill: build the Gallery into an isolated
artifacts path, run it, `PrintWindow` + `PW_RENDERFULLCONTENT` to a PNG,
`Read` the PNG. Expected: a crisp rounded accent-coloured rectangle, no
GDI tearing on resize, correct colour after the dark toggle.

- [ ] **Step 5: `dotnet format`, commit**

```bash
git add src/Anywhere.WinForms.Fluent/Rendering src/Anywhere.WinForms.Fluent.Gallery Anywhere.slnx
git commit -m "feat(fluent): FluentControl Direct2D base + gallery harness"
```

---

## Task 6: `FluentButton`

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Controls/FluentButton.cs`
- Create: `src/Anywhere.WinForms.Fluent/Icons/SegoeFluentIcons.cs`
- Modify: `src/Anywhere.WinForms.Fluent.Gallery/GalleryForm.cs` (button section)
- Test: `src/Anywhere.WinForms.Fluent.Tests/FluentButtonTests.cs`

**Interfaces:**

- Consumes: `FluentControl`, `FluentTheme`, `StateAnimator`, `SegoeFluentIcons`.
- Produces:
  - `enum FluentButtonStyle { Standard, Accent, Subtle }`.
  - `class FluentButton : FluentControl` — `string Text`, `FluentButtonStyle Style`,
    `string? IconGlyph` (a `SegoeFluentIcons` constant), `event EventHandler? Click`
    (raised on mouse-up-inside and on `Space`/`Enter` when focused). Fires the
    inherited `Control.Click` too. `GetPreferredSize` returns text + icon +
    `FluentTheme.ControlPadding`, min height `FluentTheme.ControlMinHeight`.
  - `SegoeFluentIcons` (static): `const string ChromeClose = "";` … (add
    the handful the app needs: close, chevron down, refresh, add, mic), plus
    `static Font Font(float sizeDip)` → `new("Segoe Fluent Icons", sizeDip, ...)`
    with `"Segoe MDL2 Assets"` fallback for Win10.

- [ ] **Step 1: Failing tests**

```csharp
// src/Anywhere.WinForms.Fluent.Tests/FluentButtonTests.cs
using System.Drawing;
using Anywhere.WinForms.Fluent.Controls;
using Xunit;

public class FluentButtonTests {
  [Fact]
  public void PreferredSize_is_at_least_the_control_min_height() {
    using var b = new FluentButton { Text = "OK" };
    Assert.True(b.GetPreferredSize(Size.Empty).Height >= 32);
  }

  [Fact]
  public void PreferredSize_grows_with_text_length() {
    using var shortB = new FluentButton { Text = "OK" };
    using var longB = new FluentButton { Text = "A much longer caption" };
    Assert.True(longB.GetPreferredSize(Size.Empty).Width > shortB.GetPreferredSize(Size.Empty).Width);
  }

  [Fact]
  public void PerformClick_raises_Click_once() {
    using var b = new FluentButton { Text = "OK" };
    var n = 0;
    b.Click += (_, _) => n++;
    b.PerformClick();
    Assert.Equal(1, n);
  }
}
```

- [ ] **Step 2: Run, expect FAIL.**

- [ ] **Step 3: Implement `SegoeFluentIcons`** (constants + `Font`).

- [ ] **Step 4: Implement `FluentButton`**

`RenderContent`: fill `Geometry.RoundedRect(Bounds, FluentTheme.CornerRadius)`
with the state-blended fill —
`Standard` = `ControlFillDefault`→`ControlFillSecondary`(hover)→`ControlFillTertiary`(pressed),
`Accent` = `AccentDefault` with a lighten/darken on hover/pressed,
`Subtle` = transparent→`ControlFillSecondary`(hover). Stroke a 1-dip
`ControlStrokeDefault` rounded-rect, plus a 1-dip `ControlStrokeSecondary`
bottom-edge line (the Fluent "elevation" border). Draw the icon glyph (if any)
then the text with DirectWrite (`Body` ramp, `TextPrimary`, or white on
`Accent`), centred, using the `MarkdownLabel.CreateLayout` approach
(`CreateGdiCompatibleTextLayout`, `pixelsPerDip` from `DeviceDpi/96`). On
focus, draw the 2-dip focus rect (outer `FocusStrokeOuter` + inner accent).
Interpolate fills by `State.Progress` between the from/to colours (lerp in
sRGB is fine at v1). Mouse/keyboard → `State.GoTo(...)` with 150/83 ms.

- [ ] **Step 5: Run, expect PASS.**

- [ ] **Step 6: Gallery + render capture**

Add a row of `Standard` / `Accent` / `Subtle` buttons (one with an icon) to
`GalleryForm`. Capture light & dark PNGs; eyeball against
`https://github.com/microsoft/winui-gallery` Button page. Verify hover/press
animate and focus rect shows on Tab.

- [ ] **Step 7: `dotnet format`, commit**

```bash
git add src/Anywhere.WinForms.Fluent/Controls/FluentButton.cs src/Anywhere.WinForms.Fluent/Icons src/Anywhere.WinForms.Fluent.Gallery src/Anywhere.WinForms.Fluent.Tests
git commit -m "feat(fluent): FluentButton with standard/accent/subtle styles"
```

---

## Task 7: `FluentTextBox`

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Controls/FluentTextBox.cs`
- Modify: `src/Anywhere.WinForms.Fluent.Gallery/GalleryForm.cs`
- Test: `src/Anywhere.WinForms.Fluent.Tests/FluentTextBoxTests.cs`

**Interfaces:**

- Consumes: `FluentControl`, `FluentTheme`, `StateAnimator`.
- Produces: `class FluentTextBox : FluentControl` — hosts a borderless child
  `System.Windows.Forms.TextBox` (`BorderStyle.None`, transparent-ish) inset by
  `ControlPadding`; forwards `Text`, `PlaceholderText`, `ReadOnly`, `Multiline`,
  `MaxLength`, `SelectAll()`, `event EventHandler? TextChanged`. Paints the
  Fluent surface around the child: rounded fill, 1-dip border, and — when the
  child has focus — the **accent underline** (2 dip) animating in from centre
  (`State.Progress` scales the underline from 0→full width). Placeholder text
  drawn via DirectWrite in `TextFillColorSecondary` when `Text` is empty and
  unfocused.

  Rationale for hosting a real `TextBox`: caret, selection, IME, undo,
  clipboard, and accessibility are enormous to re-implement; the abandoned libs
  never did. Painting a Fluent frame around the stock editor is the pragmatic,
  correct v1 move (documented trade-off).

- [ ] **Step 1: Failing tests**

```csharp
// src/Anywhere.WinForms.Fluent.Tests/FluentTextBoxTests.cs
using Anywhere.WinForms.Fluent.Controls;
using Xunit;

public class FluentTextBoxTests {
  [Fact]
  public void Text_round_trips_through_the_hosted_editor() {
    using var t = new FluentTextBox();
    t.Text = "hello";
    Assert.Equal("hello", t.Text);
  }

  [Fact]
  public void TextChanged_fires_on_programmatic_set() {
    using var t = new FluentTextBox();
    var n = 0;
    t.TextChanged += (_, _) => n++;
    t.Text = "x";
    Assert.True(n >= 1);
  }

  [Fact]
  public void ReadOnly_propagates_to_the_editor() {
    using var t = new FluentTextBox { ReadOnly = true };
    Assert.True(t.ReadOnly);
  }
}
```

- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement `FluentTextBox`.**
- [ ] **Step 4: Run, expect PASS.**
- [ ] **Step 5: Gallery + render capture** — empty (placeholder), typed, focused
  (underline), disabled. Light + dark PNGs.
- [ ] **Step 6: `dotnet format`, commit**

```bash
git add src/Anywhere.WinForms.Fluent/Controls/FluentTextBox.cs src/Anywhere.WinForms.Fluent.Gallery src/Anywhere.WinForms.Fluent.Tests
git commit -m "feat(fluent): FluentTextBox (hosted editor + Fluent frame/underline)"
```

---

## Task 8: `FluentComboBox` + `FluentListBox`

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Controls/FluentListBox.cs`
- Create: `src/Anywhere.WinForms.Fluent/Controls/FluentComboBox.cs`
- Modify: `src/Anywhere.WinForms.Fluent.Gallery/GalleryForm.cs`
- Test: `src/Anywhere.WinForms.Fluent.Tests/FluentListControlsTests.cs`

**Interfaces:**

- Consumes: `FluentControl`, `FluentTheme`.
- Produces:
  - `class FluentListBox : FluentControl` — `ObjectCollection Items`,
    `object? SelectedItem`, `int SelectedIndex`, `object? DataSource`,
    `string? DisplayMember`, `event EventHandler? SelectedIndexChanged`.
    Owner-drawn rows (32 dip), hover + selected fills (`ControlFillSecondary` /
    accent-tinted `LayerFillDefault`), selected row gets the 3-dip accent
    left indicator (Fluent list pattern). Vertical scroll via a hosted
    `VScrollBar` or `WM_MOUSEWHEEL` + offset.
  - `class FluentComboBox : FluentControl` — `Items`, `SelectedItem`,
    `SelectedIndex`, `DropDownStyle` (`DropDownList` only in v1),
    `event EventHandler? SelectedIndexChanged`. Closed state paints like a
    `FluentButton` with a trailing `SegoeFluentIcons.ChevronDown`; opening
    shows a top-level borderless popup `Form` hosting a `FluentListBox` with
    `OverlayCornerRadius` and `$shadow16`-ish drop shadow (`CS_DROPSHADOW` or a
    layered form). Closes on select / focus-loss / `Esc`.

- [ ] **Step 1: Failing tests** (selection round-trip + `SelectedIndexChanged`
  for both controls; `DisplayMember` honoured by `FluentListBox`).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement `FluentListBox`.**
- [ ] **Step 4: Implement `FluentComboBox`** (reuse `FluentListBox` in the popup).
- [ ] **Step 5: Run, expect PASS.**
- [ ] **Step 6: Gallery + render capture** — list with a selected row; combo
  closed and open. Light + dark.
- [ ] **Step 7: `dotnet format`, commit**

```bash
git add src/Anywhere.WinForms.Fluent/Controls/FluentListBox.cs src/Anywhere.WinForms.Fluent/Controls/FluentComboBox.cs src/Anywhere.WinForms.Fluent.Gallery src/Anywhere.WinForms.Fluent.Tests
git commit -m "feat(fluent): FluentListBox + FluentComboBox with overlay popup"
```

---

## Task 9: `FluentCard` container

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Controls/FluentCard.cs`
- Modify: `src/Anywhere.WinForms.Fluent.Gallery/GalleryForm.cs`
- Test: `src/Anywhere.WinForms.Fluent.Tests/FluentCardTests.cs`

**Interfaces:**

- Consumes: `FluentControl`, `FluentTheme`, `Geometry`.
- Produces: `class FluentCard : FluentControl` — a container
  (`ControlStyles.ContainerControl`, hosts child controls in normal WinForms
  layout with `Padding`), painting a `LayerFillDefault` rounded-rect
  (`CornerRadius` settable, default 8), 1-dip `ControlStrokeDefault` border, and
  an optional soft shadow (`Elevation` enum `None|Low|Medium` → blur/offset).
  Child controls paint on top normally (they are separate HWNDs).

- [ ] **Step 1: Failing test** — `CornerRadius` / `Padding` round-trip;
  `Controls.Add` reparents a child (`child.Parent == card`).
- [ ] **Step 2–4: FAIL → implement → PASS.**
- [ ] **Step 5: Gallery + render capture** — a card wrapping two buttons and a
  textbox, light + dark.
- [ ] **Step 6: `dotnet format`, commit**

```bash
git add src/Anywhere.WinForms.Fluent/Controls/FluentCard.cs src/Anywhere.WinForms.Fluent.Gallery src/Anywhere.WinForms.Fluent.Tests
git commit -m "feat(fluent): FluentCard container with elevation"
```

---

## Task 10: `FluentWindow` — custom caption + DWM backdrop

**Files:**

- Create: `src/Anywhere.WinForms.Fluent/Windowing/Dwm.cs`
- Create: `src/Anywhere.WinForms.Fluent/Windowing/NativeCaption.cs`
- Create: `src/Anywhere.WinForms.Fluent/Windowing/FluentWindow.cs`
- Create: `src/Anywhere.WinForms.Fluent/Interop/NativeMethods.cs`
- Modify: `src/Anywhere.WinForms.Fluent.Gallery/GalleryForm.cs` (make it a
  `FluentWindow`)

**Interfaces:**

- Consumes: `FluentTheme`, `SegoeFluentIcons`.
- Produces:
  - `Dwm` (static): `void SetBackdrop(IntPtr hwnd, FluentBackdrop type)`
    (`DWMWA_SYSTEMBACKDROP_TYPE` = 38; `1 auto / 2 Mica / 3 Acrylic / 4 MicaAlt`),
    `void SetDarkTitleBar(IntPtr hwnd, bool dark)` (`DWMWA_USE_IMMERSIVE_DARK_MODE`
    = 20), `void SetCornerPreference(IntPtr hwnd, DwmCorner corner)`
    (`DWMWA_WINDOW_CORNER_PREFERENCE` = 33). All no-op with a `try`/HRESULT
    guard on Windows 10 < 22000.
  - `enum FluentBackdrop { None, Mica, MicaAlt, Acrylic }`.
  - `class FluentWindow : Form` — `FluentBackdrop Backdrop { get; set; }`
    (default `Mica`), custom-drawn caption (title + min/max/close) via
    `WM_NCCALCSIZE` (strip the standard frame) + `WM_NCHITTEST` (caption drag,
    resize borders, caption-button hit codes), following
    `sagemodeninja/winforms-fluent-ui/FluentForm.cs`. Applies dark titlebar +
    backdrop on handle-create and re-applies on `FluentTheme.Changed` /
    `WM_SETTINGCHANGE`. `ClientPadding` accounts for the drawn caption so
    docked children sit below it.

- [ ] **Step 1: Implement `NativeMethods` + `Dwm`** (P/Invoke; guarded).
- [ ] **Step 2: Implement `NativeCaption`** — pure helpers: `int HitTest(Rectangle
  window, Rectangle caption, Rectangle min, Rectangle max, Rectangle close,
  Point screenPt, bool resizable)` returning the `HT*` code. **Unit-test this**
  (`NativeCaptionTests`): point in caption → `HTCAPTION`; in close rect →
  `HTCLOSE`; in bottom-right 8 px → `HTBOTTOMRIGHT`; interior → `HTCLIENT`.
- [ ] **Step 3: Run caption tests, FAIL → implement → PASS.**
- [ ] **Step 4: Implement `FluentWindow`** against `FluentForm.cs`'s WndProc
  structure (drop the DirectN bits; draw the caption with GDI+
  `TextRenderer` + `SegoeFluentIcons`, or a small `FluentControl` caption strip).
- [ ] **Step 5: Manual verification** — Gallery as a `FluentWindow`: Mica
  backdrop visible behind the client, dark titlebar follows theme, drag/resize/
  min/max/close work, rounded corners on Win11. Capture a PNG.
- [ ] **Step 6: `dotnet format`, commit**

```bash
git add src/Anywhere.WinForms.Fluent/Windowing src/Anywhere.WinForms.Fluent/Interop src/Anywhere.WinForms.Fluent.Gallery src/Anywhere.WinForms.Fluent.Tests
git commit -m "feat(fluent): FluentWindow with custom caption + DWM Mica/Acrylic"
```

---

## Task 11: Package polish + README + CI publish workflow

**Files:**

- Modify: `src/Anywhere.WinForms.Fluent/Anywhere.WinForms.Fluent.csproj`
- Modify: `src/Anywhere.WinForms.Fluent/README.md`
- Create: `docs/fluent/*.png` (gallery captures, light + dark)
- Create: `.github/workflows/publish-fluent.yml` (documented, manual-dispatch)

**Interfaces:** none (packaging only).

- [ ] **Step 1: Verify the pack**

`dotnet pack src/Anywhere.WinForms.Fluent/Anywhere.WinForms.Fluent.csproj -c Release --artifacts-path "$SCRATCH/fluentpack"`.
Inspect the `.nupkg` (`unzip -l`): all three TFM `lib/` folders present, README +
LICENSE at root, `.snupkg` emitted, XML docs included. Expected: no
`NU5xxx` warnings.

- [ ] **Step 2: README with screenshots** — embed `docs/fluent/*.png`, a real
  usage sample (button + textbox + window), the "emulated, not XAML Islands"
  caveat, supported-OS matrix (Mica needs Win11 22000+; controls work on Win10
  1809+), and a link to the Gallery.

- [ ] **Step 3: CI workflow** — `.github/workflows/publish-fluent.yml`:
  `workflow_dispatch` trigger, `windows-latest`, `dotnet pack -c Release`,
  `dotnet nuget push` gated on a `NUGET_API_KEY` secret. Comment at the top:
  "manual until the API is stable; bump `<Version>` per release." Do **not** add
  push triggers.

- [ ] **Step 4: Commit**

```bash
git add src/Anywhere.WinForms.Fluent .github/workflows/publish-fluent.yml docs/fluent
git commit -m "chore(fluent): packaging metadata, README, manual publish workflow"
```

---

## Task 12: Adopt in Anywhere.Controls + the app

**Files:**

- Modify: `src/Anywhere.Controls/Anywhere.Controls.csproj` (ProjectReference to
  the library)
- Modify: `src/Anywhere.Controls/ChatInputPanel.cs`
- Modify: `src/Anywhere/Anywhere.csproj` (ProjectReference)
- Modify: `src/Anywhere/AgentProfileForm.cs` / `.Designer.cs`
- Modify: `src/Anywhere/SplashForm.cs` / `.Designer.cs`
- Modify: `src/Anywhere/ChatForm.cs` / `.Designer.cs`
- Modify: `src/Anywhere/Program.cs`

**Interfaces:**

- Consumes: everything public from `Anywhere.WinForms.Fluent`.
- Produces: no new public API. Behavioural parity with today's app, Fluent skin.

- [ ] **Step 1: Reference the library** from `Anywhere.Controls` and `Anywhere`
  (`ProjectReference`, not the NuGet package — dogfood the local build).
  In `Program.cs`, after `Application.SetColorMode`, set
  `FluentTheme.Mode = FluentThemeMode.System;` (it already follows the OS).

- [ ] **Step 2: `AgentProfileForm`** → `FluentWindow`; its `TextBox`es →
  `FluentTextBox`, `ListBox` → `FluentListBox`, `Button`s → `FluentButton`
  (`saveButton` = `Accent`). Keep the `AgentProfileForm.cs` logic
  (`editingId`, parser round-trip) unchanged — only the control types in
  `.Designer.cs` and the field declarations change. Manual smoke:
  add/edit/delete still work.

- [ ] **Step 3: `ChatForm`** — `restartButton` → `FluentButton` (`Accent`),
  `restartBar` → `FluentCard` (or plain panel). Leave `ChatTranscriptPanel` /
  `PermissionDiffPanel` / `DebugLog` alone (custom-painted / plain by design).
  The `MenuStrip` + `ToolStripComboBox` profile picker stay stock in v1
  (Fluent menu is a non-goal) — revisit with the `chat-input.png` redesign.

- [ ] **Step 4: `SplashForm`** — its hand-rolled dark panel becomes a
  `FluentWindow` with `Backdrop = MicaAlt`; `newConversationButton` →
  `FluentButton` (`Accent`), `manageProfilesButton` → `FluentButton`
  (`Subtle`), `profilePicker` → `FluentComboBox`, `recentList` stays a
  `ListView` (Details view, no Fluent equivalent in v1) or becomes a
  `FluentListBox` of formatted strings if the columns aren't needed. Decide
  during execution; default to keeping `ListView`.

- [ ] **Step 5: `ChatInputPanel`** (`Anywhere.Controls`) — swap its inner
  `TextBox` for `FluentTextBox` and its send/cancel `Button`s for
  `FluentButton`. This is the smallest step toward `chat-input.png`; the full
  bottom-action-bar redesign remains a separate plan.

- [ ] **Step 6: Manual verification** — run the app (`dotnet watch` picks it
  up). Splash + conversation + profile dialog all render Fluent, follow OS
  light/dark, and every Phase-4 behaviour (crash recovery, profile
  add/edit/delete, prompt send/cancel) still works. Capture before/after PNGs.

- [ ] **Step 7: `dotnet format`, `dotnet cslint`, full test run
  (`--artifacts-path`), commit**

```bash
git add src/Anywhere.Controls src/Anywhere
git commit -m "feat: adopt Anywhere.WinForms.Fluent across the app UI"
```

---

## Self-Review Notes

- **Spec coverage:** tokens/theme (Task 2), D2D base + DPI/geometry/factories
  (Tasks 1, 3, 5), motion (Task 4), the six v1 controls (Tasks 6-9),
  `FluentWindow` + backdrop (Task 10), NuGet packaging + CI (Tasks 1, 11),
  app adoption (Task 12). Non-goals (nav view, data grid, RTL, resource
  dictionaries) are called out in the Spec and not scheduled.
- **Modelled on the abandoned libs, as asked:** `FluentControl` generalises
  `sagemodeninja`'s `IndicatedSurfaceBase`/`IndicatedButton` D2D pattern (and
  our own `MarkdownLabel`); `StateAnimator` replaces its `Utilities/Classes/
  Timer` + `EasingFunctions`; `SegoeFluentIcons` mirrors its glyph enum;
  `FluentWindow` follows its `FluentForm` WndProc; the DWM backdrop piece
  follows `evorajhonj/WinForms.Fluent`. The key gap we close: their
  `DrawSurface` was an empty stub — Tasks 6-9 are the actual rendering.
- **References pulled via Context7/MCP:** Fluent 2 design tokens
  (`/websites/fluent2_microsoft_design` — elevation `$shadow2..16`, Windows
  type ramp `Segoe UI Variable`, neutral/accent token families), WinUI 3
  Gallery (`/microsoft/winui-gallery` — `ControlCornerRadius`, themed
  `ButtonBackground*` brush keys, `AccentButtonStyle`/`SubtleButtonStyle`),
  Vortice (`/amerkoleci/vortice.windows` — `D2D1CreateFactory`,
  `CreateHwndRenderTarget`, `CreateTextLayout`, `DWRITE_WORD_WRAPPING`). Exact
  per-control pixel specs beyond the Spec table are to be eyeballed against the
  WinUI 3 Gallery app during each control's render-capture step, same as
  `acp-csharp` was read before coding in the app's earlier phases.
- **Unconfirmed at plan time (verify in the task, don't guess):**
  (a) whether Vortice 3.8.3 exposes `ID2D1Factory.CreateRoundedRectangleGeometry`
  directly (Task 3 Step 7 has the path-sink fallback);
  (b) the exact `HRESULT` constant name for `D2DERR_RECREATE_TARGET` in
  Vortice (`0x8899000C`) — Task 5;
  (c) `Application.IsDarkModeEnabled` availability per TFM — Task 2 already
  guards it via reflection;
  (d) `Segoe Fluent Icons` present on the dev box (Win11 yes; Win10 fallback
  `Segoe MDL2 Assets`) — Task 6.
- **Type consistency:** `FluentControl.RenderContent(in FluentRenderContext)`,
  `StateAnimator.GoTo(ControlVisualState, int)` / `.Progress`,
  `FluentTheme.Get(FluentToken)` / `.Mode` / `.Changed`,
  `D2DFactories.AcquireD2D()/AcquireDWrite()/Release()/RefCount`,
  `Geometry.RoundedRect(ID2D1Factory, RectangleF, float)`,
  `FluentButtonStyle { Standard, Accent, Subtle }`,
  `FluentBackdrop { None, Mica, MicaAlt, Acrylic }` — used consistently across
  Tasks 2-12.
- **TFM note:** tests target `net10.0-windows` only (matches
  `Anywhere.Tests`); the library still multi-targets. Run every `dotnet test` /
  `dotnet build` with `--artifacts-path "$SCRATCH/…"` because the app's
  `dotnet watch` holds the normal output lock.
```
