using Anywhere.Design;

namespace Anywhere.Controls;

public sealed class PermissionDiffPanel : TableLayoutPanel {
  private const int expandedHeight = 200;

  public event Action<string, PermissionOutcome>? OutcomeChosen;

  private readonly Label description = new() { AutoSize = true, Dock = DockStyle.Fill };
  private readonly TextBox oldContent = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill };
  private readonly TextBox newContent = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill };
  private readonly Button allow = new() { Text = "Allow" };
  private readonly Button allowAlways = new() { Text = "Allow Always" };
  private readonly Button deny = new() { Text = "Deny" };

  private string? currentRequestId;

  public PermissionDiffPanel() {
    Dock = DockStyle.Bottom;
    ColumnCount = 2;
    RowCount = 2;
    Height = 0;
    Visible = false;
    Padding = new Padding(Spacing.Small);

    Controls.Add(description, 0, 0);
    Controls.Add(oldContent, 0, 1);
    Controls.Add(newContent, 1, 1);

    var buttonRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
    buttonRow.Controls.AddRange(new Control[] { allow, allowAlways, deny });
    Controls.Add(buttonRow, 0, 2);

    allow.Click += (_, _) => Choose(PermissionOutcome.Allow);
    allowAlways.Click += (_, _) => Choose(PermissionOutcome.AllowAlways);
    deny.Click += (_, _) => Choose(PermissionOutcome.Deny);
  }

  public void ShowRequest(PermissionRequest request) {
    currentRequestId = request.RequestId;
    description.Text = $"{request.ToolName}: {request.Description}";
    oldContent.Text = request.OldContent ?? string.Empty;
    newContent.Text = request.NewContent ?? string.Empty;
    Visible = true;
    Height = expandedHeight;
  }

  private void Choose(PermissionOutcome outcome) {
    if (currentRequestId is null) return;
    OutcomeChosen?.Invoke(currentRequestId, outcome);
    currentRequestId = null;
    Visible = false;
    Height = 0;
  }
}
