namespace Anywhere;

partial class ChatForm {
  /// <summary>
  ///  Required designer variable.
  /// </summary>
  private System.ComponentModel.IContainer components = null;

  private Anywhere.Controls.ChatTranscriptPanel transcript;
  private Anywhere.Controls.PermissionDiffPanel permissionPanel;
  private Anywhere.Controls.ChatInputPanel inputPanel;

  /// <summary>
  ///  Clean up any resources being used.
  /// </summary>
  /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
  protected override void Dispose(bool disposing) {
    if (disposing && (components != null))
      components.Dispose();
    base.Dispose(disposing);
  }

  #region Windows Form Designer generated code

  /// <summary>
  ///  Required method for Designer support - do not modify
  ///  the contents of this method with the code editor.
  /// </summary>
  private void InitializeComponent() {
    components = new System.ComponentModel.Container();
    transcript = new Anywhere.Controls.ChatTranscriptPanel();
    permissionPanel = new Anywhere.Controls.PermissionDiffPanel();
    inputPanel = new Anywhere.Controls.ChatInputPanel();

    inputPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
    inputPanel.Height = 72;

    Controls.Add(transcript);
    Controls.Add(permissionPanel);
    Controls.Add(inputPanel);

    AutoScaleMode = AutoScaleMode.Font;
    ClientSize = new Size(800, 450);
    // FIXME: Regression: App icon is missing
    StartPosition = FormStartPosition.CenterScreen;
    Text = "Conversation";
  }

  #endregion
}
