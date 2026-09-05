using Anywhere.Design;

namespace Anywhere.Controls;

/// <summary>
/// Toggle-visible, read-only log of raw agent stderr / malformed-traffic
/// diagnostics. Fed from <c>AgentProcess.OnProtocolWarning</c>.
/// </summary>
public sealed class DebugLog : TextBox {
  public DebugLog() {
    Multiline = true;
    ReadOnly = true;
    ScrollBars = ScrollBars.Vertical;
    Dock = DockStyle.Fill;
    Visible = false;
    Font = Typography.Monospace();
    Margin = new Padding(Spacing.Small);
  }

  public void AppendLine(string line) => AppendText(line + Environment.NewLine);
}
