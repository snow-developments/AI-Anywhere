namespace Anywhere;

partial class ChatForm {
  /// <summary>
  ///  Required designer variable.
  /// </summary>
  private System.ComponentModel.IContainer components = null;

  private Anywhere.Controls.ChatTranscriptPanel transcript;
  private Anywhere.Controls.PermissionDiffPanel permissionPanel;
  private Anywhere.Controls.ChatInputPanel inputPanel;
  private Anywhere.Controls.DebugLog debugLog;
  private System.Windows.Forms.Panel restartBar;
  private System.Windows.Forms.Button restartButton;
  private System.Windows.Forms.MenuStrip menuStrip;
  private System.Windows.Forms.ToolStripMenuItem viewMenu;
  private System.Windows.Forms.ToolStripMenuItem debugLogMenuItem;
  private System.Windows.Forms.ToolStripMenuItem agentMenu;
  private System.Windows.Forms.ToolStripMenuItem manageProfilesMenuItem;
  private System.Windows.Forms.ToolStripComboBox profilePicker;
  private System.Windows.Forms.ToolStripButton changeDirButton;

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
    debugLog = new Anywhere.Controls.DebugLog();
    restartBar = new System.Windows.Forms.Panel();
    restartButton = new System.Windows.Forms.Button();
    menuStrip = new System.Windows.Forms.MenuStrip();
    viewMenu = new System.Windows.Forms.ToolStripMenuItem();
    debugLogMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    agentMenu = new System.Windows.Forms.ToolStripMenuItem();
    manageProfilesMenuItem = new System.Windows.Forms.ToolStripMenuItem();
    profilePicker = new System.Windows.Forms.ToolStripComboBox();
    changeDirButton = new System.Windows.Forms.ToolStripButton();

    //
    // debugLog
    //
    debugLog.Dock = System.Windows.Forms.DockStyle.Bottom;
    debugLog.Height = 160;
    debugLog.Visible = false;
    //
    // restartButton
    //
    restartButton.Text = "Restart agent";
    restartButton.Size = new Size(120, 28);
    restartButton.Dock = System.Windows.Forms.DockStyle.Left;
    restartButton.Click += OnRestartAgentClicked;
    //
    // restartBar
    //
    restartBar.Dock = System.Windows.Forms.DockStyle.Top;
    restartBar.Height = 36;
    restartBar.Padding = new System.Windows.Forms.Padding(4);
    restartBar.Visible = false;
    restartBar.Controls.Add(restartButton);
    //
    // profilePicker
    //
    profilePicker.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
    profilePicker.AutoSize = false;
    profilePicker.Width = 200;
    profilePicker.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
    profilePicker.SelectedIndexChanged += OnProfilePickerChanged;
    //
    // changeDirButton
    //
    changeDirButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
    changeDirButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
    changeDirButton.Text = "(no directory)";
    changeDirButton.ToolTipText = "Choose a working directory";
    // Click handler wired in ChatForm.cs.
    //
    // debugLogMenuItem
    //
    debugLogMenuItem.Text = "&Debug Log";
    debugLogMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D;
    debugLogMenuItem.CheckOnClick = true;
    debugLogMenuItem.Click += OnToggleDebugLog;
    //
    // viewMenu
    //
    viewMenu.Text = "&View";
    viewMenu.DropDownItems.Add(debugLogMenuItem);
    //
    // manageProfilesMenuItem
    //
    manageProfilesMenuItem.Text = "&Manage Profiles…";
    manageProfilesMenuItem.Click += OnManageProfilesClicked;
    //
    // agentMenu
    //
    agentMenu.Text = "&Agent";
    agentMenu.DropDownItems.Add(manageProfilesMenuItem);
    //
    // menuStrip
    //
    menuStrip.Items.Add(agentMenu);
    menuStrip.Items.Add(viewMenu);
    menuStrip.Items.Add(profilePicker);
    menuStrip.Items.Add(changeDirButton);
    //
    // inputPanel
    //
    inputPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
    inputPanel.Height = 72;
    //
    // ChatForm
    //
    Controls.Add(transcript);
    Controls.Add(restartBar);
    Controls.Add(menuStrip);
    Controls.Add(debugLog);
    Controls.Add(permissionPanel);
    Controls.Add(inputPanel);
    MainMenuStrip = menuStrip;

    AutoScaleMode = AutoScaleMode.Font;
    ClientSize = new Size(800, 450);
    // FIXME: Regression: App icon is missing
    StartPosition = FormStartPosition.CenterScreen;
    Text = "Conversation";
  }

  #endregion
}
