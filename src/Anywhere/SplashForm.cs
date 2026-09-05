using System.Diagnostics;
using Anywhere.Controls;
using Anywhere.Models;

namespace Anywhere;

/// <summary>
/// App-shell window shown at startup. Borderless; offers a "New conversation"
/// button and a list of recent conversations. Owns the lifetime of
/// <see cref="ConversationForm"/> windows opened from it.
/// </summary>
public partial class SplashForm : Form {
  internal SplashForm() {
    InitializeComponent();
    Shown += SplashForm_Shown;
  }

  private void TitleBar_MouseDown(object? sender, MouseEventArgs e) {
    if (e.Button != MouseButtons.Left) return;
    WindowDrag.Begin(Handle);
  }

  private void RecentList_DoubleClick(object? sender, EventArgs e) {
    if (recentList.SelectedItems.Count == 0) return;
    int sessionId = Convert.ToInt32(recentList.SelectedItems[0].Tag);
    OpenConversation(sessionId);
  }

  private async void SplashForm_Shown(object? sender, EventArgs e) {
    try {
      await LoadRecentSessionsAsync();
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

  /// <summary>
  /// Opens a <see cref="ConversationForm"/> for the given session, or for a
  /// brand-new session if <paramref name="sessionId"/> is null.
  /// </summary>
  // FIXME: `sessionId` is unused
  internal void OpenConversation(int? sessionId) {
    var form = new ConversationForm { Owner = this };
    form.Show();
    Hide();
  }

  protected override void OnFormClosing(FormClosingEventArgs e) {
    base.OnFormClosing(e);
    Application.Exit();
  }
}
