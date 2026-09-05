using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Anywhere.Design;

namespace Anywhere.Controls;

/// <summary>Which side of a conversation a <see cref="ChatBubble"/> belongs to.</summary>
public enum ChatRole { User, Agent, System }

/// <summary>
/// One chat message: a Markdown body inset by <see cref="Control.Padding"/> on a
/// role-tinted rounded-rectangle background. The host owns geometry — call
/// <see cref="Measure"/> to find the size for a width cap, then set
/// <see cref="Control.Width"/> and call <see cref="LayoutBody"/>. The bubble
/// does not auto-size.
/// </summary>
public sealed class ChatBubble : Panel {
  private const int cornerRadius = Spacing.Small;

  private readonly MarkdownLabel body = new();
  private ChatRole role = ChatRole.Agent;

  public ChatBubble() {
    DoubleBuffered = true;
    // The rounded fill is size-dependent — a grow must repaint the whole
    // control, not just the newly exposed strip, or old edges pile up.
    SetStyle(ControlStyles.ResizeRedraw, true);
    Padding = new Padding(Spacing.Medium, Spacing.Small, Spacing.Medium, Spacing.Small);
    Controls.Add(body);
    ApplyRoleColors();
  }

  // Set in code by the transcript, never the VS designer.
  [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
  public ChatRole Role {
    get => role;
    set {
      role = value;
      ApplyRoleColors();
      Invalidate();
    }
  }

  [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
  public string Markdown {
    get => body.Text;
    set => body.Text = value;
  }

  /// <summary>Bubble size needed to show the current text within <paramref name="maxWidth"/>.</summary>
  public Size Measure(int maxWidth) {
    var inner = Math.Max(0, maxWidth - Padding.Left - Padding.Right);
    var content = body.MeasureContent(inner);
    return new Size(
      content.Width + Padding.Left + Padding.Right,
      content.Height + Padding.Top + Padding.Bottom);
  }

  /// <summary>Position and size the body for the bubble's current width; returns the height it needs.</summary>
  public int LayoutBody() {
    var inner = Math.Max(0, Width - Padding.Left - Padding.Right);
    var content = body.MeasureContent(inner);
    body.Location = new Point(Padding.Left, Padding.Top);
    body.Size = new Size(inner, content.Height);
    return content.Height + Padding.Top + Padding.Bottom;
  }

  private Color FillColor => role switch {
    ChatRole.User => Colors.Accent,
    ChatRole.System => SystemColors.Control,
    _ => SystemColors.ControlLight,
  };

  private Color TextColor => role == ChatRole.User ? Color.White : SystemColors.ControlText;

  private void ApplyRoleColors() {
    // The Direct2D body clears to its own BackColor, so match it to the bubble
    // fill — otherwise a rectangular seam shows through the rounded corners.
    body.BackColor = FillColor;
    body.ForeColor = TextColor;
  }

  protected override void OnParentChanged(EventArgs e) {
    base.OnParentChanged(e);
    // Area outside the rounded fill blends into the transcript.
    if (Parent is not null) BackColor = Parent.BackColor;
  }

  protected override void OnPaint(PaintEventArgs e) {
    base.OnPaint(e);
    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
    using var path = RoundedRect(new Rectangle(0, 0, Width - 1, Height - 1), cornerRadius);
    using var fill = new SolidBrush(FillColor);
    e.Graphics.FillPath(fill, path);
  }

  private static GraphicsPath RoundedRect(Rectangle r, int radius) {
    var d = radius * 2;
    var path = new GraphicsPath();
    if (d <= 0 || r.Width <= 0 || r.Height <= 0) {
      path.AddRectangle(r);
      return path;
    }
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
  }
}
