using System.Diagnostics;
using Anywhere.Controls;
using Anywhere.Models;

namespace Anywhere;

/// <summary>
/// App-shell window shown at startup. Borderless; offers a "New conversation"
/// button and a list of recent conversations. Owns the lifetime of
/// <see cref="ChatForm"/> windows opened from it. Closing the window
/// hides it to the tray rather than exiting the app; the user exits via the
/// tray icon's Exit menu item.
/// </summary>
public partial class SplashForm : Form {
  internal SplashForm() {
    InitializeComponent();
    Shown += SplashForm_Shown;
  }

  private void TitleBar_MouseDown(object? sender, MouseEventArgs e) {
    if (e.Button != MouseButtons.Left) {
      return;
    }
    WindowDrag.Begin(Handle);
  }

  private void RecentList_Resize(object? sender, EventArgs e) {
    recentList.Columns[0].Width = recentList.ClientSize.Width;
  }

  private void RecentList_DoubleClick(object? sender, EventArgs e) {
    if (recentList.SelectedItems.Count == 0) {
      return;
    }
    int sessionId = Convert.ToInt32(recentList.SelectedItems[0].Tag);
    OpenConversation(sessionId);
  }

  private async void SplashForm_Shown(object? sender, EventArgs e) {
    try {
      await LoadRecentSessionsAsync();
      await LoadProfilesAsync();
    } catch (Exception ex) {
      // Don't crash the splash if the DB isn't ready or anything else goes
      // wrong. Show the empty state and surface the error in a placeholder
      // row so it's visible during smoke testing.
      recentList.Items.Add(new ListViewItem("(failed to load recent)"));
      Debug.WriteLine($"SplashForm: failed to load recent chats: {ex}");
    }
  }

  private async Task LoadRecentSessionsAsync() {
    using var db = new AnywhereDbContext(AnywhereDbContext.DefaultDbPath());
    var repo = new Persistence.SessionRepository(db);
    var sessions = await repo.ListAllAsync();
    if (sessions.Count == 0) {
      recentList.Items.Add(new ListViewItem("No recent conversations."));
      return;
    }
    foreach (var s in sessions) {
      var item = new ListViewItem($"#{s.Id}  {s.WorkingDir}  ({s.CreatedAt:yyyy-MM-dd HH:mm})") {
        Tag = s.Id
      };
      recentList.Items.Add(item);
    }
  }

  private async Task LoadProfilesAsync() {
    using var db = new AnywhereDbContext(AnywhereDbContext.DefaultDbPath());
    var all = await new Persistence.ProfileRepository(db).ListAllAsync();
    profilePicker.DisplayMember = nameof(AgentProfile.Name);
    profilePicker.DataSource = null;
    profilePicker.DataSource = all;
  }

  private async Task ManageProfilesAsync() {
    using (var db = new AnywhereDbContext(AnywhereDbContext.DefaultDbPath())) {
      using var form = new AgentProfileForm(new Persistence.ProfileRepository(db));
      form.ShowDialog(this);
    }
    await LoadProfilesAsync();
  }

  /// <summary>
  /// Opens a <see cref="ChatForm"/> for the given session, or for a
  /// brand-new session if <paramref name="sessionId"/> is null. The profile
  /// chosen in the splash picker (if any) seeds the new conversation.
  /// </summary>
  // FIXME: `sessionId` is unused
  internal void OpenConversation(int? sessionId) {
    var form = new ChatForm(profilePicker.SelectedItem as AgentProfile) { Owner = this };
    form.Show();
    Hide();
  }

  protected override void OnFormClosing(FormClosingEventArgs e) {
    // Closing the splash only hides it — the app stays alive in the tray so
    // the user can bring the window back. Real shutdown goes through the
    // tray icon's Exit menu item, which calls Application.Exit().
    if (e.CloseReason == CloseReason.ApplicationExitCall) {
      base.OnFormClosing(e);
      return;
    }
    Hide();
    e.Cancel = true;
  }
}
