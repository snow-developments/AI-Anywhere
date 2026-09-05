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

    dwFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();
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
      y += RenderBlock(block, y);
      y += BlockSpacing;
    }

    renderTarget.EndDraw();
  }

  private float RenderBlock(Block block, float y) {
    var size = Font.SizeInDips();

    if (block is HeadingBlock heading) {
      // FIXME: Use a configurable property for heading font sizes, `HeadingSizes`
      float HeadingFontSize = heading.Level switch { 1 => 22, 2 => 18, 3 => 16, _ => 14 };
      return RenderInlines(heading.Inline, y, HeadingFontSize, isBold: true);
    } else if (block is ParagraphBlock para) {
      return RenderInlines(para.Inline, y, size, isBold: false);
    } else if (block is ListBlock list) {
      float total = 0;
      int index = 1;
      foreach (Block item in list) {
        string bullet = list.IsOrdered ? $"{index++}." : "•";
        // render bullet
        using var bulletLayout = CreateLayout($"{bullet} ", size, false, false);
        renderTarget!.DrawTextLayout(new Vector2(Padding.Left + 4, y + total), bulletLayout, brush!);
        float bulletW = bulletLayout.Metrics.Width;

        // render item inlines indented
        if (item is ListItemBlock listItem) foreach (var child in listItem)
          if (child is ParagraphBlock p)
            total += RenderInlines(p.Inline, y + total, size, isBold: false, indent: bulletW + Padding.Left + 8);

        total += 2;
      }
      return total;
    }

    return 0;
  }

  private float RenderInlines(
    ContainerInline? inlines, float y, float fontSize, bool isBold, float indent = 4
  ) {
    if (inlines == null) return 0;

    // Build a flat list of (text, bold, italic)
    var runs = new List<(string text, bool bold, bool italic)>();
    CollectRuns(inlines, runs, isBold, false);

    // Concatenate full text
    var sb = new System.Text.StringBuilder();
    foreach (var r in runs) sb.Append(r.text);
    string fullText = sb.ToString();

    var maxHeight = Height - y - Padding.Bottom;
    using var layout = CreateLayout(
      fullText,
      Font.SizeInDips(),
      indent: indent,
      format: dwFactory!.CreateTextFormat(
        Font.Name, isBold ? FontWeight.Bold : FontWeight.Normal,
        FontStyle.Normal,
        FontStretch.Normal,
        fontSize
      )
    );
    if (AutoSize && maxHeight < layout.Metrics.LayoutHeight)
      Height += Convert.ToInt32(Math.Round(layout.MaxHeight, 0, MidpointRounding.AwayFromZero));

    // Apply per-run formatting
    uint pos = 0;
    foreach (var (text, bold, italic) in runs) {
      var length = Convert.ToUInt32(text.Length);
      var range = new TextRange(pos, length);
      if (bold) layout.SetFontWeight(FontWeight.Bold, range);
      if (italic) layout.SetFontStyle(FontStyle.Italic, range);
      pos += length;
    }

    renderTarget!.DrawTextLayout(new Vector2(indent, y), layout, brush!);
    return layout.Metrics.Height;
  }

  private IDWriteTextLayout CreateLayout(
    string text, float size,
    bool bold = false, bool italic = false, float indent = 0,
    IDWriteTextFormat? format = null
  ) {
    return dwFactory!.CreateGdiCompatibleTextLayout(
      text,
      Convert.ToUInt32(text.Length),
      format ?? dwFactory.CreateTextFormat(
        Font.Name,
        bold ? FontWeight.Bold : FontWeight.Normal,
        italic ? FontStyle.Italic : FontStyle.Normal,
        FontStretch.Normal,
        size
      ),
      Width - indent - Padding.Right,
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
      if (inline is LiteralInline lit)
        runs.Add((lit.Content.ToString(), bold, italic));
      else if (inline is EmphasisInline em) {
        bool b = bold || em.DelimiterCount == 2;
        bool i = italic || em.DelimiterCount == 1;
        CollectRuns(em, runs, b, i);
      } else if (inline is CodeInline code)
        runs.Add((code.Content, false, false)); // could set monospace here
    }
  }

  protected override void OnHandleDestroyed(EventArgs e) {
    brush?.Dispose();
    renderTarget?.Dispose();
    dwFactory?.Dispose();
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
