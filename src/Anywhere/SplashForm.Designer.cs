using Anywhere.Design;

namespace Anywhere;

partial class SplashForm {
  private readonly Label titleCaption = new();
  private readonly TableLayoutPanel titleBar = new();
  private readonly Button closeButton = new();
  private readonly ToolTip toolTip = new();
  private readonly Label wordmark = new();
  private readonly Button newConversationButton = new();
  private readonly TableLayoutPanel recentSection = new();
  private readonly Label recentHeader = new();
  private readonly ListView recentList = new();

  private void InitializeComponent() {
    SuspendLayout();

    //
    // titleBar
    //
    titleBar.Dock = DockStyle.Top;
    titleBar.Height = 32;
    titleBar.BackColor = Color.FromArgb(0x14, 0x14, 0x18);
    titleBar.Padding = new Padding(Spacing.Tiny);
    titleBar.ColumnCount = 2;
    titleBar.RowCount = 1;
    titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
    titleBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
    titleBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
    titleBar.MouseDown += TitleBar_MouseDown;

    //
    // titleCaption
    //
    titleCaption.Text = "Conversations";
    titleCaption.Font = new Font("Segoe UI", SystemFonts.CaptionFont.Size);
    titleCaption.ForeColor = Color.White;
    titleCaption.Dock = DockStyle.Fill;
    titleCaption.TextAlign = ContentAlignment.MiddleLeft;
    titleCaption.MouseDown += TitleBar_MouseDown;

    //
    // closeButton
    //
    closeButton.Text = "✕"; // ✕
    closeButton.FlatStyle = FlatStyle.Flat;
    closeButton.FlatAppearance.BorderSize = 0;
    closeButton.ForeColor = Color.Gainsboro;
    closeButton.BackColor = Color.FromArgb(0x14, 0x14, 0x18);
    closeButton.Size = new Size(Spacing.Large, Spacing.Medium);
    closeButton.Dock = DockStyle.Fill;
    closeButton.Cursor = Cursors.Hand;
    closeButton.Click += (_, _) => Close();
    titleBar.Margin = new Padding(0);
    titleBar.Padding = new Padding(0);
    toolTip.SetToolTip(closeButton, "Close");

    titleBar.Controls.Add(titleCaption, 0, 0);
    titleBar.Controls.Add(closeButton, 1, 0);

    //
    // wordmark
    //
    wordmark.Text = "Anywhere";
    wordmark.Font = new Font("Segoe UI Light", 24F, FontStyle.Regular);
    wordmark.ForeColor = Color.White;
    wordmark.AutoSize = true;
    wordmark.Location = new Point(Spacing.Medium - Spacing.Tiny, 48);
    wordmark.TextAlign = ContentAlignment.MiddleLeft;

    //
    // newConversationButton
    //
    newConversationButton.Text = "+ New conversation";
    newConversationButton.Size = new Size(200, 36);
    newConversationButton.Location = new Point(Spacing.Medium, 108);
    newConversationButton.FlatStyle = FlatStyle.Flat;
    newConversationButton.FlatAppearance.BorderSize = 0;
    newConversationButton.BackColor = Color.FromArgb(0x4F, 0x6B, 0xFF);
    newConversationButton.ForeColor = Color.White;
    newConversationButton.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
    newConversationButton.Cursor = Cursors.Hand;
    newConversationButton.Click += (_, _) => OpenConversation(null);

    //
    // recentSection
    //
    // Flows the header above the list and lets the list fill all remaining
    // space, flush with the form's right/bottom edges, without manual size math.
    recentSection.Location = new Point(Spacing.Medium, 168);
    recentSection.Size = new Size(504, 192);
    recentSection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
    recentSection.ColumnCount = 1;
    recentSection.RowCount = 2;
    recentSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
    recentSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
    recentSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

    //
    // recentHeader
    //
    recentHeader.Text = "Recent conversations";
    recentHeader.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
    recentHeader.ForeColor = Color.Gainsboro;
    recentHeader.AutoSize = true;
    recentHeader.Dock = DockStyle.Top;

    //
    // recentList
    //
    recentList.View = View.Details;
    recentList.FullRowSelect = true;
    recentList.GridLines = false;
    recentList.HeaderStyle = ColumnHeaderStyle.None;
    recentList.BorderStyle = BorderStyle.None;
    recentList.BackColor = Color.FromArgb(0x1F, 0x1F, 0x23);
    recentList.ForeColor = Color.Gainsboro;
    recentList.Font = new Font("Segoe UI", 9F);
    recentList.Dock = DockStyle.Fill;
    recentList.Columns.Add("Title", recentList.ClientSize.Width, HorizontalAlignment.Left);
    recentList.DoubleClick += RecentList_DoubleClick;
    recentList.Resize += RecentList_Resize;

    recentSection.Controls.Add(recentHeader, 0, 0);
    recentSection.Controls.Add(recentList, 0, 1);

    //
    // SplashForm
    //
    // Window chrome: borderless, fixed, centered, present in the taskbar so
    // the user can restore it after minimizing the whole app.
    FormBorderStyle = FormBorderStyle.None;
    ClientSize = new Size(520, 360);
    StartPosition = FormStartPosition.CenterScreen;
    ShowInTaskbar = true;
    MinimumSize = new Size(520, 360);
    MaximizeBox = false;
    MinimizeBox = false;
    Text = "Conversations - Anywhere";

    // Single accent color literal — Phase 4 styling is out of scope for this
    // plan (see AGENTS.md "Visual styling"). Do not introduce a WinForms.Fluent
    // dependency from the app project; Anywhere.Controls owns that.
    BackColor = Color.FromArgb(0x1F, 0x1F, 0x23);

    Controls.Add(recentSection);
    Controls.Add(newConversationButton);
    Controls.Add(wordmark);
    Controls.Add(titleBar);

    ResumeLayout(performLayout: false);
    PerformLayout();
  }
}
