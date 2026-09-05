using Anywhere.Agents;
using Anywhere.Models;
using Anywhere.Persistence;

namespace Anywhere;

/// <summary>
/// Modal add / edit / delete UI over <see cref="ProfileRepository"/>. Tracks the
/// currently-loaded profile's <c>Id</c> in <see cref="editingId"/> — null while
/// composing a new profile.
/// </summary>
public partial class AgentProfileForm : Form {
  private readonly ProfileRepository profiles;
  private int? editingId;
  private AgentProfile? loaded;

  public AgentProfileForm(ProfileRepository profiles) {
    this.profiles = profiles;
    InitializeComponent();
    Load += async (_, _) => await RefreshListAsync();
  }

  private async Task RefreshListAsync(int? selectId = null) {
    var all = await profiles.ListAllAsync();
    profileList.DisplayMember = nameof(AgentProfile.Name);
    profileList.DataSource = null;
    profileList.DataSource = all;

    if (selectId is { } id) {
      var idx = all.FindIndex(p => p.Id == id);
      if (idx >= 0) {
        profileList.SelectedIndex = idx;
        return;
      }
    }
    ClearFields();
  }

  private void OnProfileSelected(object? sender, EventArgs e) {
    if (profileList.SelectedItem is not AgentProfile p) return;
    loaded = p;
    editingId = p.Id;
    nameBox.Text = p.Name;
    commandBox.Text = p.Command;
    argsBox.Text = string.Join(", ", p.Args);
    workingDirBox.Text = p.WorkingDir;
    deleteButton.Enabled = true;
  }

  private void OnNewClicked(object? sender, EventArgs e) => ClearFields();

  private void ClearFields() {
    editingId = null;
    loaded = null;
    profileList.ClearSelected();
    nameBox.Clear();
    commandBox.Clear();
    argsBox.Clear();
    workingDirBox.Clear();
    deleteButton.Enabled = false;
  }

  private async void OnSaveClicked(object? sender, EventArgs e) {
    var name = nameBox.Text.Trim();
    var command = commandBox.Text.Trim();
    var workingDir = workingDirBox.Text.Trim();
    if (name.Length == 0 || command.Length == 0 || workingDir.Length == 0) {
      MessageBox.Show(this, "Name, Command, and Working directory are required.",
        "Incomplete profile", MessageBoxButtons.OK, MessageBoxIcon.Warning);
      return;
    }

    var profile = new AgentProfile {
      Name = name,
      Command = command,
      Args = AgentProfileParser.ParseArgs(argsBox.Text),
      WorkingDir = workingDir,
      // Env has no UI in v1 — carry the existing value through on edit.
      Env = loaded?.Env ?? new(),
    };

    try {
      if (editingId is { } id) {
        profile.Id = id;
        profile.CreatedAt = loaded?.CreatedAt ?? profile.CreatedAt;
        await profiles.UpdateAsync(profile);
      } else {
        await profiles.InsertAsync(profile);
      }
      await RefreshListAsync(profile.Id);
    } catch (Exception ex) {
      MessageBox.Show(this, ex.Message, "Save failed",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  private async void OnDeleteClicked(object? sender, EventArgs e) {
    if (editingId is not { } id) return;
    try {
      await profiles.DeleteAsync(id);
      await RefreshListAsync();
    } catch (Exception ex) {
      MessageBox.Show(this, ex.Message, "Delete failed",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }
}
