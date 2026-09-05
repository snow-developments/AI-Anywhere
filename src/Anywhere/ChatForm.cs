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
  private int sessionId;
  private CancellationTokenSource? sendCts;

  public ChatForm() {
    InitializeComponent();
    permissionPanel.OutcomeChosen += OnPermissionOutcomeChosen;
    inputPanel.SendRequested += OnSendRequested;
    inputPanel.CancelRequested += OnCancelRequested;
    Load += OnLoad;
    FormClosed += OnFormClosed;
  }

  private async void OnLoad(object? sender, EventArgs e) {
    try {
      db = new AnywhereDbContext(AnywhereDbContext.DefaultDbPath());
      db.Database.Migrate();

      var profiles = new ProfileRepository(db);
      var sessions = new SessionRepository(db);
      messages = new MessageRepository(db);

      var profile = new AgentProfile {
        Name = "fake",
        Command = "python",
        Args = [Path.GetFullPath(Path.Combine(
          AppContext.BaseDirectory, "..", "..", "..", "..",
          "Anywhere.Tests", "FakeAgent", "fake_agent.py"))],
        WorkingDir = Environment.CurrentDirectory,
      };
      var profileId = await profiles.InsertAsync(profile);
      sessionId = await sessions.InsertAsync(profileId, profile.WorkingDir);

      agent = new AgentProcess(profile);
      agent.OnResponseChunk += chunk =>
        transcript.BeginInvoke(() => transcript.AppendToCurrentAgentMessage(chunk));
      agent.OnPermissionRequested += request =>
        permissionPanel.BeginInvoke(() => permissionPanel.ShowRequest(request));
      agent.OnAgentExited += reason =>
        transcript.BeginInvoke(() => transcript.AppendMessage("system", $"Agent exited: {reason}"));

      await agent.StartAsync();
    } catch (Exception ex) {
      transcript.AppendMessage("system", $"Failed to start agent: {ex.Message}");
    }
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
}
