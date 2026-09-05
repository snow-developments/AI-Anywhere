using System;
using System.Windows.Forms;
using Anywhere.Design;

namespace Anywhere.Controls;

/// <summary>
/// Chat input area: a multiline text box with a Send button, wrapped in a
/// standard <see cref="GroupBox"/> frame. No custom painting — WinForms draws
/// the border and respects the OS light/dark color mode.
/// </summary>
public sealed class ChatInputPanel : GroupBox {
  private readonly TextBox inputBox = new() {
    Multiline = true,
    Dock = DockStyle.Fill,
  };

  private readonly Button sendButton = new() {
    Text = "Send",
    Dock = DockStyle.Right,
    AutoSize = true,
    AutoSizeMode = AutoSizeMode.GrowAndShrink,
    Padding = new Padding(Spacing.Medium, 0, Spacing.Medium, 0),
    Cursor = Cursors.Hand,
  };

  private readonly Panel spacer = new() {
    Dock = DockStyle.Right,
    Width = Spacing.Small,
  };

  /// <summary>Raised when the user clicks Send or presses Enter.</summary>
  public event Action? SendRequested;

  public TextBox InputBox => inputBox;

  public ChatInputPanel() {
    Text = string.Empty;
    Padding = new Padding(Spacing.Small);

    // Docking resolves last-added-first: Send pins right, spacer sits to its
    // left, the text box fills the rest.
    Controls.Add(inputBox);
    Controls.Add(spacer);
    Controls.Add(sendButton);

    sendButton.Click += (_, _) => SendRequested?.Invoke();
    inputBox.KeyDown += (_, e) => {
      if (e.KeyCode != Keys.Enter || e.Shift) return;
      e.SuppressKeyPress = true;
      SendRequested?.Invoke();
    };
  }
}
