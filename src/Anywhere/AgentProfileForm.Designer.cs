namespace Anywhere;

partial class AgentProfileForm {
  private System.ComponentModel.IContainer components = null;

  private System.Windows.Forms.ListBox profileList;
  private System.Windows.Forms.TableLayoutPanel fieldGrid;
  private System.Windows.Forms.Label nameLabel;
  private System.Windows.Forms.Label commandLabel;
  private System.Windows.Forms.Label argsLabel;
  private System.Windows.Forms.Label workingDirLabel;
  private System.Windows.Forms.TextBox nameBox;
  private System.Windows.Forms.TextBox commandBox;
  private System.Windows.Forms.TextBox argsBox;
  private System.Windows.Forms.TextBox workingDirBox;
  private System.Windows.Forms.FlowLayoutPanel buttonRow;
  private System.Windows.Forms.Button newButton;
  private System.Windows.Forms.Button saveButton;
  private System.Windows.Forms.Button deleteButton;

  protected override void Dispose(bool disposing) {
    if (disposing && (components != null))
      components.Dispose();
    base.Dispose(disposing);
  }

  #region Windows Form Designer generated code

  private void InitializeComponent() {
    components = new System.ComponentModel.Container();
    profileList = new System.Windows.Forms.ListBox();
    fieldGrid = new System.Windows.Forms.TableLayoutPanel();
    nameLabel = new System.Windows.Forms.Label();
    commandLabel = new System.Windows.Forms.Label();
    argsLabel = new System.Windows.Forms.Label();
    workingDirLabel = new System.Windows.Forms.Label();
    nameBox = new System.Windows.Forms.TextBox();
    commandBox = new System.Windows.Forms.TextBox();
    argsBox = new System.Windows.Forms.TextBox();
    workingDirBox = new System.Windows.Forms.TextBox();
    buttonRow = new System.Windows.Forms.FlowLayoutPanel();
    newButton = new System.Windows.Forms.Button();
    saveButton = new System.Windows.Forms.Button();
    deleteButton = new System.Windows.Forms.Button();

    //
    // profileList
    //
    profileList.Dock = System.Windows.Forms.DockStyle.Left;
    profileList.Width = 180;
    profileList.IntegralHeight = false;
    profileList.SelectedIndexChanged += OnProfileSelected;
    //
    // nameLabel
    //
    nameLabel.Text = "Name";
    nameLabel.AutoSize = true;
    nameLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
    //
    // commandLabel
    //
    commandLabel.Text = "Command";
    commandLabel.AutoSize = true;
    commandLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
    //
    // argsLabel
    //
    argsLabel.Text = "Args (comma-separated)";
    argsLabel.AutoSize = true;
    argsLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
    //
    // workingDirLabel
    //
    workingDirLabel.Text = "Working directory";
    workingDirLabel.AutoSize = true;
    workingDirLabel.Anchor = System.Windows.Forms.AnchorStyles.Left;
    //
    // nameBox
    //
    nameBox.Dock = System.Windows.Forms.DockStyle.Fill;
    //
    // commandBox
    //
    commandBox.Dock = System.Windows.Forms.DockStyle.Fill;
    //
    // argsBox
    //
    argsBox.Dock = System.Windows.Forms.DockStyle.Fill;
    //
    // workingDirBox
    //
    workingDirBox.Dock = System.Windows.Forms.DockStyle.Fill;
    //
    // fieldGrid
    //
    fieldGrid.Dock = System.Windows.Forms.DockStyle.Fill;
    fieldGrid.ColumnCount = 2;
    fieldGrid.RowCount = 5;
    fieldGrid.Padding = new System.Windows.Forms.Padding(12);
    fieldGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 150F));
    fieldGrid.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
    for (int i = 0; i < 4; i++)
      fieldGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
    fieldGrid.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
    fieldGrid.Controls.Add(nameLabel, 0, 0);
    fieldGrid.Controls.Add(nameBox, 1, 0);
    fieldGrid.Controls.Add(commandLabel, 0, 1);
    fieldGrid.Controls.Add(commandBox, 1, 1);
    fieldGrid.Controls.Add(argsLabel, 0, 2);
    fieldGrid.Controls.Add(argsBox, 1, 2);
    fieldGrid.Controls.Add(workingDirLabel, 0, 3);
    fieldGrid.Controls.Add(workingDirBox, 1, 3);
    //
    // newButton
    //
    newButton.Text = "New";
    newButton.AutoSize = true;
    newButton.Click += OnNewClicked;
    //
    // saveButton
    //
    saveButton.Text = "Save";
    saveButton.AutoSize = true;
    saveButton.Click += OnSaveClicked;
    //
    // deleteButton
    //
    deleteButton.Text = "Delete";
    deleteButton.AutoSize = true;
    deleteButton.Enabled = false;
    deleteButton.Click += OnDeleteClicked;
    //
    // buttonRow
    //
    buttonRow.Dock = System.Windows.Forms.DockStyle.Bottom;
    buttonRow.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
    buttonRow.AutoSize = true;
    buttonRow.Padding = new System.Windows.Forms.Padding(8);
    buttonRow.Controls.Add(saveButton);
    buttonRow.Controls.Add(deleteButton);
    buttonRow.Controls.Add(newButton);
    //
    // AgentProfileForm
    //
    Controls.Add(fieldGrid);
    Controls.Add(buttonRow);
    Controls.Add(profileList);

    AutoScaleMode = AutoScaleMode.Font;
    ClientSize = new Size(640, 360);
    FormBorderStyle = FormBorderStyle.FixedDialog;
    MinimizeBox = false;
    MaximizeBox = false;
    StartPosition = FormStartPosition.CenterParent;
    Text = "Agent Profiles";
  }

  #endregion
}
