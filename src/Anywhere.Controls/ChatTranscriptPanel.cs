using Anywhere.Design;

namespace Anywhere.Controls;

public sealed class ChatTranscriptPanel : FlowLayoutPanel {
  private ChatBubble? currentAgentBubble;
  private string currentAgentText = string.Empty;

  public ChatTranscriptPanel() {
    FlowDirection = FlowDirection.TopDown;
    AutoScroll = true;
    WrapContents = false;
    Dock = DockStyle.Fill;
    ClientSizeChanged += (_, _) => ResizeBubbles();
  }

  public void AppendMessage(string role, string markdown) =>
    AddBubble(RoleOf(role), markdown);

  public void StartAgentMessage() {
    currentAgentText = string.Empty;
    currentAgentBubble = AddBubble(ChatRole.Agent, string.Empty);
  }

  public void AppendToCurrentAgentMessage(string chunk) {
    if (currentAgentBubble is null) StartAgentMessage();
    currentAgentText += chunk;
    currentAgentBubble!.Markdown = currentAgentText;
    LayoutBubble(currentAgentBubble);
    ScrollControlIntoView(currentAgentBubble);
  }

  private ChatBubble AddBubble(ChatRole role, string markdown) {
    var bubble = new ChatBubble { Role = role, Markdown = markdown };
    Controls.Add(bubble);
    LayoutBubble(bubble);
    ScrollControlIntoView(bubble);
    return bubble;
  }

  private static ChatRole RoleOf(string role) => role switch {
    "user" => ChatRole.User,
    "system" => ChatRole.System,
    _ => ChatRole.Agent,
  };

  // Bubbles shrink-wrap their content up to ~3/4 of the transcript width; user
  // messages hug the right edge (via left Margin), everyone else the left.
  private void LayoutBubble(ChatBubble bubble) {
    var avail = Math.Max(0, ClientSize.Width - Spacing.Medium);
    var cap = Math.Max(Spacing.ExtraLarge, avail * 3 / 4);

    bubble.Width = Math.Min(bubble.Measure(cap).Width, cap);
    bubble.Height = bubble.LayoutBody();

    var side = Spacing.Small;
    var offset = bubble.Role == ChatRole.User
      ? Math.Max(side, avail - bubble.Width)
      : side;
    bubble.Margin = new Padding(offset, Spacing.Tiny, side, Spacing.Tiny);
  }

  private void ResizeBubbles() {
    foreach (Control control in Controls)
      if (control is ChatBubble bubble) LayoutBubble(bubble);
  }
}
