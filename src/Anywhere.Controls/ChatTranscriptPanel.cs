using Anywhere.Design;

namespace Anywhere.Controls;

public sealed class ChatTranscriptPanel : FlowLayoutPanel {
  private MarkdownLabel? currentAgentBubble;
  private string currentAgentText = string.Empty;

  public ChatTranscriptPanel() {
    FlowDirection = FlowDirection.TopDown;
    AutoScroll = true;
    WrapContents = false;
    Dock = DockStyle.Fill;
    ClientSizeChanged += (_, _) => ResizeBubbles();
  }

  public void AppendMessage(string role, string markdown) {
    var bubble = new MarkdownLabel {
      Text = markdown,
      Width = ClientSize.Width - Spacing.Medium,
      Margin = new Padding(Spacing.Small),
    };
    Controls.Add(bubble);
    ScrollControlIntoView(bubble);
  }

  public void StartAgentMessage() {
    currentAgentText = string.Empty;
    currentAgentBubble = new MarkdownLabel {
      Text = string.Empty,
      Width = ClientSize.Width - Spacing.Medium,
      Margin = new Padding(Spacing.Small),
    };
    Controls.Add(currentAgentBubble);
    ScrollControlIntoView(currentAgentBubble);
  }

  public void AppendToCurrentAgentMessage(string chunk) {
    if (currentAgentBubble is null) StartAgentMessage();
    currentAgentText += chunk;
    currentAgentBubble!.Text = currentAgentText;
    ScrollControlIntoView(currentAgentBubble);
  }

  private void ResizeBubbles() {
    foreach (Control control in Controls) {
      control.Width = ClientSize.Width - Spacing.Medium;
    }
  }
}
