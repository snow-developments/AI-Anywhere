namespace Anywhere;

partial class SplashForm {
  private readonly Panel titleBar = new();
  private readonly Label titleLabel = new();
  private readonly Button closeButton = new();
  private readonly Label wordmark = new();
  private readonly Button newConversationButton = new();
  private readonly Label recentHeader = new();
  private readonly ListView recentList = new();

  private void InitializeComponent() {
    SuspendLayout();

    // Window chrome: borderless, fixed, centered, present in the taskbar so
    // the user can restore it after minimizing the whole app.
    FormBorderStyle = FormBorderStyle.None;
    ClientSize = new Size(520, 360);
    StartPosition = FormStartPosition.CenterScreen;
    ShowInTaskbar = true;
    MinimumSize = new Size(520, 360);
    MaximizeBox = false;
    MinimizeBox = false;
    Text = "Anywhere";

    // Single accent color literal — Phase 4 styling is out of scope for this
    // plan (see AGENTS.md "Visual styling"). Do not introduce a WinForms.Fluent
    // dependency from the app project; Anywhere.Controls owns that.
    BackColor = Color.FromArgb(0x1F, 0x1F, 0x23);

    BuildTitleBar();
    BuildContent();
    BuildRecentList();

    ResumeLayout(performLayout: false);
    PerformLayout();
  }

  private void BuildTitleBar() {
    titleBar.Dock = DockStyle.Top;
    titleBar.Height = 32;
    titleBar.BackColor = Color.FromArgb(0x14, 0x14, 0x18);
    titleBar.Padding = new Padding(12, 0, 8, 0);

    titleLabel.Text = "Anywhere";
    titleLabel.ForeColor = Color.Gainsboro;
    titleLabel.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
    titleLabel.AutoSize = true;
    titleLabel.Dock = DockStyle.Left;
    titleLabel.TextAlign = ContentAlignment.MiddleLeft;

    closeButton.Text = "✕"; // ✕
    closeButton.FlatStyle = FlatStyle.Flat;
    closeButton.FlatAppearance.BorderSize = 0;
    closeButton.ForeColor = Color.Gainsboro;
    closeButton.BackColor = Color.FromArgb(0x14, 0x14, 0x18);
    closeButton.Size = new Size(32, 32);
    closeButton.Dock = DockStyle.Right;
    closeButton.Cursor = Cursors.Hand;
    closeButton.Click += (_, _) => Close();

    titleBar.Controls.Add(titleLabel);
    titleBar.Controls.Add(closeButton);
    titleBar.MouseDown += TitleBar_MouseDown;
    titleLabel.MouseDown += TitleBar_MouseDown;
    closeButton.MouseDown += TitleBar_MouseDown;

    Controls.Add(titleBar);
  }

  private void BuildContent() {
    wordmark.Text = "Anywhere";
    wordmark.Font = new Font("Segoe UI Light", 24F, FontStyle.Regular);
    wordmark.ForeColor = Color.White;
    wordmark.AutoSize = true;
    wordmark.Location = new Point(24, 48);
    wordmark.TextAlign = ContentAlignment.MiddleLeft;

    newConversationButton.Text = "+ New conversation";
    newConversationButton.Size = new Size(200, 36);
    newConversationButton.Location = new Point(24, 108);
    newConversationButton.FlatStyle = FlatStyle.Flat;
    newConversationButton.FlatAppearance.BorderSize = 0;
    newConversationButton.BackColor = Color.FromArgb(0x4F, 0x6B, 0xFF);
    newConversationButton.ForeColor = Color.White;
    newConversationButton.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
    newConversationButton.Cursor = Cursors.Hand;
    newConversationButton.Click += (_, _) => OpenConversation(null);

    recentHeader.Text = "Recent conversations";
    recentHeader.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
    recentHeader.ForeColor = Color.Gainsboro;
    recentHeader.AutoSize = true;
    recentHeader.Location = new Point(24, 168);

    Controls.Add(wordmark);
    Controls.Add(newConversationButton);
    Controls.Add(recentHeader);
  }

  private void BuildRecentList() {
    recentList.View = View.Details;
    recentList.FullRowSelect = true;
    recentList.GridLines = false;
    recentList.HeaderStyle = ColumnHeaderStyle.None;
    recentList.BorderStyle = BorderStyle.None;
    recentList.BackColor = Color.FromArgb(0x1F, 0x1F, 0x23);
    recentList.ForeColor = Color.Gainsboro;
    recentList.Font = new Font("Segoe UI", 9F);
    recentList.Location = new Point(24, 192);
    recentList.Size = new Size(472, 144);
    recentList.Columns.Add("Title", -2, HorizontalAlignment.Left); // -2 = auto-size
    recentList.DoubleClick += RecentList_DoubleClick;

    Controls.Add(recentList);
  }
}
