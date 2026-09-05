using Anywhere.Design;

namespace Anywhere.Controls;

/// <summary>
/// Docked prompt shown when the agent requests permission for a tool call.
/// Hugs its content: a one-line description, an optional old/new diff (only
/// when the tool call carries diff content), and the action buttons. Collapses
/// to zero height when nothing is pending.
/// </summary>
public sealed class PermissionDiffPanel : TableLayoutPanel {
  private const int diffHeight = 120;

  public event Action<string, PermissionOutcome>? OutcomeChosen;

  private readonly Label description = new() {
    AutoSize = true,
    Margin = new Padding(Spacing.Tiny, Spacing.Tiny, Spacing.Tiny, Spacing.Small),
  };

  private readonly TableLayoutPanel diffHost = new() {
    Dock = DockStyle.Fill,
    ColumnCount = 2,
    RowCount = 1,
    Height = diffHeight,
    Margin = new Padding(0, 0, 0, Spacing.Small),
    Visible = false,
  };

  private readonly TextBox oldContent = new() {
    Multiline = true,
    ReadOnly = true,
    Dock = DockStyle.Fill,
    ScrollBars = ScrollBars.Vertical,
    WordWrap = false,
  };

  private readonly TextBox newContent = new() {
    Multiline = true,
    ReadOnly = true,
    Dock = DockStyle.Fill,
    ScrollBars = ScrollBars.Vertical,
    WordWrap = false,
  };

  // Distinct labels: "once" applies to this call only; "always" whitelists the
  // tool for the rest of the session. Sharing the word "Allow" with no
  // qualifier (the old "Allow" / "Allow Always" pair, which also clipped to
  // "Allow" / "Allow" at the default button width) gave the user two
  // identical-looking buttons.
  private readonly Button allowOnce = MakeButton("Allow once");
  private readonly Button allowAlways = MakeButton("Allow always");
  private readonly Button deny = MakeButton("Deny");

  private string? currentRequestId;

  public PermissionDiffPanel() {
    Dock = DockStyle.Bottom;
    AutoSize = true;
    AutoSizeMode = AutoSizeMode.GrowAndShrink;
    ColumnCount = 1;
    RowCount = 3;
    Padding = new Padding(Spacing.Small);
    Visible = false;

    ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
    diffHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
    diffHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
    diffHost.Controls.Add(oldContent, 0, 0);
    diffHost.Controls.Add(newContent, 1, 0);

    var buttonRow = new FlowLayoutPanel {
      AutoSize = true,
      AutoSizeMode = AutoSizeMode.GrowAndShrink,
      Margin = new Padding(0),
      Dock = DockStyle.Fill,
    };
    buttonRow.Controls.AddRange(new Control[] { allowOnce, allowAlways, deny });

    Controls.Add(description, 0, 0);
    Controls.Add(diffHost, 0, 1);
    Controls.Add(buttonRow, 0, 2);

    allowOnce.Click += (_, _) => Choose(PermissionOutcome.Allow);
    allowAlways.Click += (_, _) => Choose(PermissionOutcome.AllowAlways);
    deny.Click += (_, _) => Choose(PermissionOutcome.Deny);
  }

  public void ShowRequest(PermissionRequest request) {
    currentRequestId = request.RequestId;
    description.Text = $"{request.ToolName}: {request.Description}";

    var hasDiff = !string.IsNullOrEmpty(request.OldContent) || !string.IsNullOrEmpty(request.NewContent);
    oldContent.Text = request.OldContent ?? string.Empty;
    newContent.Text = request.NewContent ?? string.Empty;
    diffHost.Visible = hasDiff;

    Visible = true;
  }

  protected override void OnSizeChanged(EventArgs e) {
    base.OnSizeChanged(e);
    // Let the description wrap instead of forcing the panel wider.
    var usable = ClientSize.Width - Padding.Horizontal - description.Margin.Horizontal;
    if (usable > 0) description.MaximumSize = new Size(usable, 0);
  }

  private void Choose(PermissionOutcome outcome) {
    if (currentRequestId is null) return;
    OutcomeChosen?.Invoke(currentRequestId, outcome);
    currentRequestId = null;
    Visible = false;
  }

  private static Button MakeButton(string text) => new() {
    Text = text,
    AutoSize = true,
    AutoSizeMode = AutoSizeMode.GrowAndShrink,
    Margin = new Padding(0, 0, Spacing.Small, 0),
    Padding = new Padding(Spacing.Small, Spacing.Tiny, Spacing.Small, Spacing.Tiny),
  };
}
