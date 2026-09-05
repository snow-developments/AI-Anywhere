using Anywhere.Agents;
using Anywhere.Controls;
using Anywhere.Models;
using Anywhere.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Anywhere;

public partial class ChatForm : Form {
  private AnywhereDbContext? db;
  private AgentProcess? agent;
  private MessageRepository? messages;
  private ProfileRepository? profiles;
  private SessionRepository? sessions;
  private int sessionId;
  private CancellationTokenSource? sendCts;

  private readonly AgentProfile? startingProfile;
  private bool populatingPicker;

  /// <summary>
  /// The conversation's working directory — a per-conversation runtime choice,
  /// not a profile property. Picked when the conversation starts and
  /// changeable at any point via <see cref="OnChangeDirClicked"/>.
  /// </summary>
  private string? currentWorkingDir;

  public ChatForm() : this(null, null) { }

  public ChatForm(AgentProfile? profile) : this(profile, null) { }

  public ChatForm(AgentProfile? profile, string? workingDirectory) {
    startingProfile = profile;
    currentWorkingDir = workingDirectory;
    InitializeComponent();
    permissionPanel.OutcomeChosen += OnPermissionOutcomeChosen;
    inputPanel.SendRequested += OnSendRequested;
    inputPanel.CancelRequested += OnCancelRequested;
    changeDirButton.Click += OnChangeDirClicked;
    Load += OnLoad;
    FormClosed += OnFormClosed;
  }

  private async void OnLoad(object? sender, EventArgs e) {
    try {
      db = new AnywhereDbContext(AnywhereDbContext.DefaultDbPath());
      db.Database.Migrate();

      profiles = new ProfileRepository(db);
      sessions = new SessionRepository(db);
      messages = new MessageRepository(db);

      await ReloadProfilesAsync(startingProfile?.Id);

      // The working directory is always an explicit per-conversation choice.
      // The splash flow supplies one; if it didn't (e.g. ChatForm opened
      // directly), prompt now rather than guessing a cwd.
      currentWorkingDir ??= PromptForDirectory(SelectedProfile?.WorkingDir);
      if (currentWorkingDir is null) {
        transcript.AppendMessage("system", "No working directory chosen. Pick one to start the agent.");
        UpdateChangeDirButton();
        return;
      }

      await StartAgentAsync();
    } catch (Exception ex) {
      transcript.AppendMessage("system", $"Failed to start agent: {ex.Message}");
    }
  }

  /// <summary>
  /// Repopulate the profile picker from the DB. Seeds a local dev/fake profile
  /// the first time the table is empty so the app stays runnable before the
  /// user has configured a real agent.
  /// </summary>
  private async Task ReloadProfilesAsync(int? selectId = null) {
    if (profiles is null) return;

    var all = await profiles.ListAllAsync();
    if (all.Count == 0) {
      await profiles.InsertAsync(DevFakeProfile());
      all = await profiles.ListAllAsync();
    }

    populatingPicker = true;
    profilePicker.ComboBox.DisplayMember = nameof(AgentProfile.Name);
    profilePicker.Items.Clear();
    foreach (var p in all) profilePicker.Items.Add(p);

    var target = selectId is { } id
      ? all.FindIndex(p => p.Id == id)
      : -1;
    profilePicker.SelectedIndex = target >= 0 ? target : (all.Count > 0 ? 0 : -1);
    populatingPicker = false;
  }

  private AgentProfile? SelectedProfile => profilePicker.SelectedItem as AgentProfile;

  /// <summary>
  /// (Re)start the agent subprocess for the currently selected profile,
  /// opening a fresh session row. Safe to call repeatedly — a prior agent is
  /// disposed first.
  /// </summary>
  private async Task StartAgentAsync() {
    if (sessions is null) return;

    agent?.Dispose();
    agent = null;

    var profile = SelectedProfile;
    if (profile is null) {
      transcript.AppendMessage("system", "No agent profile configured. Use Agent ▸ Manage Profiles…");
      return;
    }

    if (currentWorkingDir is null) return;

    sessionId = await sessions.InsertAsync(profile.Id, currentWorkingDir);

    var next = new AgentProcess(profile, currentWorkingDir);
    next.OnResponseChunk += chunk =>
      transcript.BeginInvoke(() => transcript.AppendToCurrentAgentMessage(chunk));
    next.OnPermissionRequested += request =>
      permissionPanel.BeginInvoke(() => permissionPanel.ShowRequest(request));
    next.OnAgentExited += reason =>
      BeginInvoke(() => {
        transcript.AppendMessage("system", $"Agent exited ({reason}).");
        restartBar.Visible = true;
      });
    next.OnProtocolWarning += line =>
      BeginInvoke(() => debugLog.AppendLine(line));

    // Publish to `agent` only after the handshake completes — otherwise a
    // prompt sent during the await races an agent whose connection/session are
    // still null and fails with "StartAsync must complete before SendPromptAsync".
    await next.StartAsync();
    agent = next;
    restartBar.Visible = false;
    UpdateChangeDirButton();
  }

  /// <summary>
  /// Change the conversation's working directory. ACP fixes a session's
  /// <c>cwd</c> at <c>session/new</c>, so this tears down the current agent and
  /// starts a fresh session in the new directory; the persisted transcript
  /// carries over, the agent's in-memory context does not.
  /// </summary>
  private async void OnChangeDirClicked(object? sender, EventArgs e) {
    var picked = PromptForDirectory(currentWorkingDir);
    if (picked is null || picked == currentWorkingDir) return;

    var hadSession = agent is not null;
    currentWorkingDir = picked;
    try {
      if (sessions is not null && hadSession) {
        await sessions.UpdateWorkingDirAsync(sessionId, picked);
      }
      transcript.AppendMessage("system",
        hadSession ? $"Working directory changed to {picked}." : $"Working directory set to {picked}.");
      await StartAgentAsync();
    } catch (Exception ex) {
      transcript.AppendMessage("system", $"Failed to change working directory: {ex.Message}");
    }
  }

  /// <summary>
  /// Show a folder picker seeded with <paramref name="seed"/> (falling back to
  /// the OS current directory). Returns null if the user cancels.
  /// </summary>
  private string? PromptForDirectory(string? seed) {
    using var dialog = new FolderBrowserDialog {
      Description = "Working directory for this conversation",
      UseDescriptionForTitle = true,
      SelectedPath = Directory.Exists(seed) ? seed! : Environment.CurrentDirectory,
    };
    return dialog.ShowDialog(this) == DialogResult.OK ? dialog.SelectedPath : null;
  }

  private void UpdateChangeDirButton() {
    changeDirButton.Text = currentWorkingDir is null
      ? "(no directory)"
      : new DirectoryInfo(currentWorkingDir).Name;
    changeDirButton.ToolTipText = currentWorkingDir ?? "Choose a working directory";
  }

  private async void OnRestartAgentClicked(object? sender, EventArgs e) {
    restartBar.Visible = false;
    try {
      await StartAgentAsync();
    } catch (Exception ex) {
      transcript.AppendMessage("system", $"Restart failed: {ex.Message}");
      restartBar.Visible = true;
    }
  }

  private void OnToggleDebugLog(object? sender, EventArgs e) =>
    debugLog.Visible = debugLogMenuItem.Checked;

  private async void OnProfilePickerChanged(object? sender, EventArgs e) {
    if (populatingPicker || agent is null) return;
    try {
      transcript.AppendMessage("system", $"Switched to profile “{SelectedProfile?.Name}”.");
      await StartAgentAsync();
    } catch (Exception ex) {
      transcript.AppendMessage("system", $"Failed to switch profile: {ex.Message}");
    }
  }

  private async void OnManageProfilesClicked(object? sender, EventArgs e) {
    if (profiles is null) return;
    using var form = new AgentProfileForm(profiles);
    form.ShowDialog(this);
    await ReloadProfilesAsync(SelectedProfile?.Id);
  }

  private async void OnSendRequested() {
    if (agent is null || messages is null) return;

    var text = inputPanel.InputBox.Text;
    if (string.IsNullOrWhiteSpace(text)) return;
    inputPanel.InputBox.Clear();

    transcript.AppendMessage("user", text);
    await messages.InsertAsync(sessionId, "user", text, null);

    transcript.StartAgentMessage();
    sendCts = new CancellationTokenSource();
    inputPanel.SetBusy(true);
    try {
      var result = await agent.SendPromptAsync(text, sendCts.Token);
      await messages.InsertAsync(sessionId, "agent", result.Content, null);
    } catch (OperationCanceledException) {
      transcript.AppendMessage("system", "Cancelled.");
    } catch (Exception ex) {
      transcript.AppendMessage("system", $"Agent error: {ex.Message}");
    } finally {
      inputPanel.SetBusy(false);
      sendCts.Dispose();
      sendCts = null;
    }
  }

  private void OnCancelRequested() => sendCts?.Cancel();

  private async void OnPermissionOutcomeChosen(string requestId, PermissionOutcome outcome) {
    if (agent is null) return;
    try {
      await agent.RespondToPermissionAsync(requestId, outcome);
    } catch (Exception ex) {
      transcript.AppendMessage("system", $"Failed to respond to permission request: {ex.Message}");
    }
  }

  private void OnFormClosed(object? sender, FormClosedEventArgs e) {
    agent?.Dispose();
    db?.Dispose();
  }

  private static AgentProfile DevFakeProfile() => new() {
    Name = "fake (dev)",
    Command = "python",
    Args = [Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory, "..", "..", "..", "..",
      "Anywhere.Tests", "FakeAgent", "fake_agent.py"))],
    // WorkingDir intentionally unset — the directory is picked per conversation.
  };
}
