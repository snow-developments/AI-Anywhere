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

  private bool busy;

  /// <summary>Raised when the user clicks Send or presses Enter.</summary>
  public event Action? SendRequested;

  /// <summary>Raised when the user clicks Stop while a prompt is in flight.</summary>
  public event Action? CancelRequested;

  public TextBox InputBox => inputBox;

  /// <summary>
  /// Toggle the in-flight state: disables the text box, swaps Send for Stop,
  /// and shows a caption. The host clears this in a <c>finally</c> on every
  /// outcome (success, error, cancel).
  /// </summary>
  public void SetBusy(bool value) {
    busy = value;
    inputBox.Enabled = !value;
    sendButton.Text = value ? "Stop" : "Send";
    Text = value ? "Working…" : string.Empty;
  }

  public ChatInputPanel() {
    Text = string.Empty;
    Padding = new Padding(Spacing.Small);

    // Docking resolves last-added-first: Send pins right, spacer sits to its
    // left, the text box fills the rest.
    Controls.Add(inputBox);
    Controls.Add(spacer);
    Controls.Add(sendButton);

    sendButton.Click += (_, _) => {
      if (busy) {
        CancelRequested?.Invoke();
      } else {
        SendRequested?.Invoke();
      }
    };
    inputBox.KeyDown += (_, e) => {
      if (e.KeyCode != Keys.Enter || e.Shift || busy) return;
      e.SuppressKeyPress = true;
      SendRequested?.Invoke();
    };
  }
}
