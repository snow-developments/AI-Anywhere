using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Drawing = System.Drawing;
using FontStyle = Vortice.DirectWrite.FontStyle;
using TextAntialiasMode = Vortice.Direct2D1.TextAntialiasMode;

namespace Anywhere.Controls;

// Adapted from family-lock-out's Controls/MarkdownLabel.cs.
// Renders Markdown to a WinForms Control via Direct2D + DirectWrite, with
// Markdig doing the AST work and our own walker translating it to a flat list
// of (text, bold, italic) runs that DirectWrite lays out and paints.
[ToolboxBitmap(typeof(Label))]
public class MarkdownLabel : Control {
  private ID2D1HwndRenderTarget? renderTarget;
  private IDWriteFactory? dwFactory;
  private ID2D1SolidColorBrush? brush;
  private float blockSpacing = 6;
  private AutoSizeMode autoSizeMode = AutoSizeMode.GrowOnly;

  // Width used by the layout builders. During a measurement call the control
  // may not be sized (or handled) yet, so measurement passes its own width in
  // here; drawing falls back to the live Width.
  private int? measureWidth;

  // Widest laid-out block seen during the current measurement pass.
  private float measuredWidth;

  // DirectWrite only needs a factory to measure/lay out text — no window
  // handle, no render target. Created lazily so GetPreferredHeight works before
  // the handle exists; the render target (drawing only) is still built in
  // OnHandleCreated.
  private IDWriteFactory DwFactory => dwFactory ??= DWrite.DWriteCreateFactory<IDWriteFactory>();
  private int LayoutWidth => measureWidth ?? Width;

  [NotNull]
  [Category("Appearance")]
  public override Drawing.Color BackColor {
    get => base.BackColor;
    [param: NotNull]
    set {
      base.BackColor = value;
      Invalidate();
    }
  }

  [NotNull]
  [Category("Appearance")]
  public override Drawing.Color ForeColor {
    get => base.ForeColor;
    [param: NotNull]
    set {
      base.ForeColor = value;
      brush?.Dispose();
      brush = renderTarget?.CreateSolidColorBrush(value.ToColor4());
      Invalidate();
    }
  }

  [NotNull]
  [Category("Appearance")]
  public override Drawing.Font Font {
    get => base.Font;
    set {
      base.Font = value;
      Invalidate();
    }
  }

  [Category("Layout")]
  public override bool AutoSize {
    get => base.AutoSize;
    set {
      base.AutoSize = value;
      Invalidate();
    }
  }

  [Category("Layout")]
  [DefaultValue(AutoSizeMode.GrowOnly)]
  public AutoSizeMode AutoSizeMode {
    get => autoSizeMode;
    set {
      autoSizeMode = value;
      Invalidate();
    }
  }

  [Category("Appearance")]
  [Description("Amount of space between blocks of the redered Markdown.")]
  [DefaultValue(6f)]
  public float BlockSpacing {
    get => blockSpacing;
    set {
      blockSpacing = value;
      Invalidate();
    }
  }

  [Category("Appearance")]
  [Editor(typeof(System.ComponentModel.Design.MultilineStringEditor), typeof(System.Drawing.Design.UITypeEditor))]
  public override string Text {
    get => base.Text;
    set {
      base.Text = value;
      Invalidate();
    }
  }

  protected override void OnHandleCreated(EventArgs e) {
    base.OnHandleCreated(e);

    _ = DwFactory;
    var d2dFactory = D2D1.D2D1CreateFactory<ID2D1Factory>();
    renderTarget = d2dFactory.CreateHwndRenderTarget(
      new RenderTargetProperties(),
      new HwndRenderTargetProperties {
        Hwnd = Handle,
        PixelSize = new SizeI(Width, Height)
      }
    );
    renderTarget.TextAntialiasMode = TextAntialiasMode.Cleartype;
    brush = renderTarget.CreateSolidColorBrush(ForeColor.ToColor4());
  }

  protected override void OnResize(EventArgs e) {
    base.OnResize(e);
    renderTarget?.Resize(new SizeI(Width, Height));
    Invalidate();
  }

  protected override void OnPaint(PaintEventArgs e) {
    if (renderTarget == null || dwFactory == null || brush == null) return;

    renderTarget.BeginDraw();
    renderTarget.Clear(BackColor.ToColor4());

    float y = Padding.Top;
    foreach (var block in Markdown.Parse(Text)) {
      y += RenderBlock(block, y, draw: true);
      y += BlockSpacing;
    }

    renderTarget.EndDraw();
  }

  /// <summary>
  /// Pixel size needed to render the current <see cref="Text"/> within
  /// <paramref name="maxWidth"/>: <c>Width</c> is the widest laid-out line (up
  /// to <paramref name="maxWidth"/>), <c>Height</c> the total. Pure measurement
  /// — needs no window handle or render target, so callers can size the control
  /// before it is shown.
  /// </summary>
  public Drawing.Size MeasureContent(int maxWidth) {
    if (maxWidth <= 0 || string.IsNullOrEmpty(Text)) return Drawing.Size.Empty;

    measureWidth = maxWidth;
    measuredWidth = 0;
    try {
      float y = Padding.Top;
      bool any = false;
      foreach (var block in Markdown.Parse(Text)) {
        y += RenderBlock(block, y, draw: false);
        y += BlockSpacing;
        any = true;
      }
      if (any) y -= BlockSpacing; // spacing goes *between* blocks, not after the last
      return new Drawing.Size(
        Math.Min(maxWidth, (int)Math.Ceiling(measuredWidth) + Padding.Left + Padding.Right),
        (int)Math.Ceiling(y + Padding.Bottom));
    } finally {
      measureWidth = null;
    }
  }

  /// <summary>Pixel height needed to render the current <see cref="Text"/> at <paramref name="width"/>.</summary>
  public int GetPreferredHeight(int width) => MeasureContent(width).Height;

  private float RenderBlock(Block block, float y, bool draw) {
    var size = Font.SizeInDips();

    if (block is HeadingBlock heading) {
      // FIXME: Use a configurable property for heading font sizes, `HeadingSizes`
      float HeadingFontSize = heading.Level switch { 1 => 22, 2 => 18, 3 => 16, _ => 14 };
      return RenderInlines(heading.Inline, y, HeadingFontSize, isBold: true, draw: draw);
    } else if (block is ParagraphBlock para) {
      return RenderInlines(para.Inline, y, size, isBold: false, draw: draw);
    } else if (block is ListBlock list) {
      float total = 0;
      int index = 1;
      foreach (Block item in list) {
        string bullet = list.IsOrdered ? $"{index++}." : "•";
        // render bullet
        using var bulletLayout = CreateLayout($"{bullet} ", size, false, false);
        if (draw)
          renderTarget!.DrawTextLayout(new Vector2(Padding.Left + 4, y + total), bulletLayout, brush!);
        float bulletW = bulletLayout.Metrics.Width;

        // render item inlines indented
        if (item is ListItemBlock listItem) foreach (var child in listItem)
          if (child is ParagraphBlock p)
            total += RenderInlines(p.Inline, y + total, size, isBold: false, draw: draw, indent: bulletW + Padding.Left + 8);

        total += 2;
      }
      return total;
    }

    return 0;
  }

  private float RenderInlines(
    ContainerInline? inlines, float y, float fontSize, bool isBold, bool draw, float indent = 0
  ) {
    if (inlines == null) return 0;

    // Build a flat list of (text, bold, italic)
    var runs = new List<(string text, bool bold, bool italic)>();
    CollectRuns(inlines, runs, isBold, false);

    // Concatenate full text
    var sb = new System.Text.StringBuilder();
    foreach (var r in runs) sb.Append(r.text);
    string fullText = sb.ToString();

    using var layout = CreateLayout(
      fullText,
      Font.SizeInDips(),
      indent: indent,
      format: DwFactory.CreateTextFormat(
        Font.Name, isBold ? FontWeight.Bold : FontWeight.Normal,
        FontStyle.Normal,
        FontStretch.Normal,
        fontSize
      )
    );

    // Apply per-run formatting
    uint pos = 0;
    foreach (var (text, bold, italic) in runs) {
      var length = Convert.ToUInt32(text.Length);
      var range = new TextRange(pos, length);
      if (bold) layout.SetFontWeight(FontWeight.Bold, range);
      if (italic) layout.SetFontStyle(FontStyle.Italic, range);
      pos += length;
    }

    if (draw) {
      renderTarget!.DrawTextLayout(new Vector2(indent, y), layout, brush!);
    } else {
      measuredWidth = Math.Max(measuredWidth, indent + layout.Metrics.Width);
    }
    return layout.Metrics.Height;
  }

  private IDWriteTextLayout CreateLayout(
    string text, float size,
    bool bold = false, bool italic = false, float indent = 0,
    IDWriteTextFormat? format = null
  ) {
    return DwFactory.CreateGdiCompatibleTextLayout(
      text,
      Convert.ToUInt32(text.Length),
      format ?? DwFactory.CreateTextFormat(
        Font.Name,
        bold ? FontWeight.Bold : FontWeight.Normal,
        italic ? FontStyle.Italic : FontStyle.Normal,
        FontStretch.Normal,
        size
      ),
      LayoutWidth - indent - Padding.Right,
      float.MaxValue,
      transform: null,
      pixelsPerDip: 1.0f,
      useGdiNatural: true
    );
  }

  private static void CollectRuns(
      ContainerInline inlines,
      List<(string, bool, bool)> runs,
      bool bold, bool italic) {
    foreach (var inline in inlines) {
      if (inline is LiteralInline lit) {
        runs.Add((lit.Content.ToString(), bold, italic));
      } else if (inline is EmphasisInline em) {
        bool b = bold || em.DelimiterCount == 2;
        bool i = italic || em.DelimiterCount == 1;
        CollectRuns(em, runs, b, i);
      } else if (inline is CodeInline code) {
        runs.Add((code.Content, false, false)); // could set monospace here
      }
    }
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    brush?.Dispose();
    brush = null;
    renderTarget?.Dispose();
    renderTarget = null;
    dwFactory?.Dispose();
    dwFactory = null;
    base.OnHandleDestroyed(e);
  }
}

internal static class ColorExtensions {
  public static Color4 ToColor4(this Drawing.Color color) {
    return new Color4(new ColorBgra(color.R, color.G, color.B, color.A));
  }
}

internal static class FontExtensions {
  /// <summary>
  /// Convert the given font's size to device-independent pixels (DIPs).
  /// </summary>
  /// <remarks>
  /// DirectWrite uses device-independent pixels, where 1 DIP = 1/96 inch.
  /// WinForms <see cref="Font.Size"/> is in points, where 1 point = 1/72 inch.
  /// </remarks>
  /// <param name="font"></param>
  public static float SizeInDips(this Font font) {
    return font.SizeInPoints * 96f / 72f;
  }
}
