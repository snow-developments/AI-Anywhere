using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentClientProtocol;
using Anywhere.Controls;
using Anywhere.Models;

namespace Anywhere.Agents;

/// <summary>
/// Drives one ACP agent subprocess over stdio JSON-RPC via acp-csharp's
/// <see cref="ClientSideConnection"/>. Owns the agent process, the
/// initialize/new-session handshake, and the streaming text accumulation that
/// produces <see cref="PromptResult.Content"/>.
/// </summary>
/// <remarks>
/// Streaming: <see cref="OnResponseChunk"/> is raised from
/// <c>IAcpClient.SessionNotificationAsync</c> each time the agent emits an
/// <c>agent_message_chunk</c> whose content is a <c>TextContentBlock</c>, BEFORE
/// the awaited <see cref="SendPromptAsync"/> completes. Final
/// <see cref="PromptResult.Content"/> is the concatenation of every chunk
/// raised for that turn (ACP's <c>PromptResponse</c> only carries
/// <c>StopReason</c>, not content — see the acp-csharp Schema folder).
///
/// Permissions: <see cref="OnPermissionRequested"/> is raised with a
/// <see cref="PermissionRequest"/> whenever the agent invokes
/// <c>session/request_permission</c>; the request hangs until the host calls
/// <see cref="RespondToPermissionAsync"/> with the matching <c>requestId</c>.
/// Per spec, there is no timeout — the panel waits indefinitely.
/// </remarks>
public sealed class AgentProcess : IDisposable {
  private readonly AgentProfile profile;
  private Process? process;
  private ClientSideConnection? connection;
  private string? sessionId;
  private readonly AcpClientAdapter adapter;
  private long nextRequestId;
  private bool disposing;

  public event Action<string>? OnResponseChunk;
  public event Action<PermissionRequest>? OnPermissionRequested;
  public event Action<string>? OnAgentExited;

  /// <summary>
  /// Raised for each non-empty line the agent writes to stderr. acp-csharp
  /// swallows JSON-RPC parse failures internally (its <c>errorWriteFunc</c> is
  /// wired to a no-op and not reachable without forking), so the agent's own
  /// stderr is the only host-visible channel for malformed-traffic / startup
  /// diagnostics. Consumed by the debug log panel.
  /// </summary>
  public event Action<string>? OnProtocolWarning;

  public AgentProcess(AgentProfile profile) {
    this.profile = profile;
    adapter = new AcpClientAdapter(this);
  }

  public async Task StartAsync() {
    process = new Process {
      StartInfo = new ProcessStartInfo {
        FileName = profile.Command,
        WorkingDirectory = profile.WorkingDir,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardInput = true,
        RedirectStandardError = true,
      },
    };
    foreach (var arg in profile.Args) process.StartInfo.ArgumentList.Add(arg);
    foreach (var (key, value) in profile.Env) {
      process.StartInfo.Environment[key] = value;
    }
    if (!process.Start()) {
      throw new InvalidOperationException("Failed to start agent process.");
    }
    process.EnableRaisingEvents = true;
    process.Exited += (_, _) => {
      // A caller-initiated Dispose() kills the process (exit code -1 on
      // Windows) — that is an intentional teardown (profile switch, window
      // close), not a crash, so stay quiet. Only surface an exit the agent
      // decided on itself.
      if (!disposing) OnAgentExited?.Invoke($"exit code {process.ExitCode}");
    };

    // stderr is redirected but never consumed by acp-csharp; drain it
    // asynchronously so malformed-traffic / crash diagnostics surface in the
    // debug log. Safe alongside ClientSideConnection, which only reads stdout.
    process.ErrorDataReceived += (_, e) => {
      if (!string.IsNullOrWhiteSpace(e.Data)) OnProtocolWarning?.Invoke(e.Data);
    };
    process.BeginErrorReadLine();

    connection = new ClientSideConnection(
      _ => adapter,
      process.StandardOutput,
      process.StandardInput);
    connection.Open();

    var initResult = await connection.InitializeAsync(new InitializeRequest {
      ProtocolVersion = 1,
      ClientCapabilities = new ClientCapabilities(),
    });

    if (initResult.ProtocolVersion != 1) {
      throw new InvalidOperationException(
        $"Unsupported protocol version: {initResult.ProtocolVersion}");
    }

    var sessionResult = await connection.NewSessionAsync(new NewSessionRequest {
      Cwd = profile.WorkingDir,
      McpServers = [],
    });
    sessionId = sessionResult.SessionId;
  }

  public async Task<PromptResult> SendPromptAsync(string text, CancellationToken cancellationToken = default) {
    if (connection is null || sessionId is null) {
      throw new InvalidOperationException("StartAsync must complete before SendPromptAsync.");
    }

    adapter.BeginTurn();

    var promptRequest = new PromptRequest {
      SessionId = sessionId,
      Prompt = [new TextContentBlock { Text = text }],
    };

    // ClientSideConnection.PromptAsync's cancellationToken doesn't actually
    // abort the wait for the agent's response (confirmed via a failing test:
    // the awaited call still completed after the fake agent's artificial
    // delay even after the token was cancelled) — it only affects request
    // dispatch, not the in-flight wait. So the host-visible "Cancelled."
    // behavior is implemented here instead: stop waiting on our end and
    // surface OperationCanceledException, without waiting for (or expecting)
    // the agent to actually halt its turn server-side.
    await connection.PromptAsync(promptRequest, cancellationToken).AsTask()
      .WaitAsync(cancellationToken);

    return new PromptResult(adapter.EndTurnText());
  }

  public Task RespondToPermissionAsync(string requestId, PermissionOutcome outcome) {
    adapter.ResolvePermission(requestId, outcome);
    return Task.CompletedTask;
  }

  public void Dispose() {
    disposing = true;
    try {
      connection?.Dispose();
    } catch (ObjectDisposedException) {
      // acp-csharp's ClientSideConnection.Dispose() calls Cancel() on its internal
      // CancellationTokenSource; if the read loop already tore itself down (e.g.
      // the agent process exited/closed its streams first), that CTS is already
      // disposed and Cancel() throws. Dispose() must never throw, so swallow it.
    }
    if (process is not null && !process.HasExited) {
      try {
        process.Kill(entireProcessTree: true);
        process.WaitForExit(2000);
      } catch {
        // best-effort cleanup; the process may already be exiting on its own
      }
    }
    process?.Dispose();
  }

  // ---- Adapter: implements IAcpClient so acp-csharp can call back into us ----

  private sealed class AcpClientAdapter : IAcpClient {
    private readonly AgentProcess owner;
    private readonly StringBuilder turnBuffer = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PermissionOutcome>> permissionWaiters = new();

    internal AcpClientAdapter(AgentProcess owner) => this.owner = owner;

    internal void BeginTurn() {
      // Discard any leftover text from a prior turn — chunks accumulate into
      // turnBuffer until EndTurnText reads it out.
      turnBuffer.Clear();
    }

    internal string EndTurnText() => turnBuffer.ToString();

    internal void ResolvePermission(string requestId, PermissionOutcome outcome) {
      if (permissionWaiters.TryRemove(requestId, out var tcs)) {
        tcs.TrySetResult(outcome);
      }
    }

    public ValueTask<RequestPermissionResponse> RequestPermissionAsync(
        RequestPermissionRequest request, CancellationToken cancellationToken = default) {
      var requestId = Interlocked.Increment(ref owner.nextRequestId).ToString();
      var toolCallElement = (JsonElement)request.ToolCall;
      var toolName = TryGetString(toolCallElement, "title")
                     ?? TryGetString(toolCallElement, "name")
                     ?? "tool";
      var description = TryGetString(toolCallElement, "description")
                        ?? TryGetString(toolCallElement, "title")
                        ?? toolName;

      var tcs = new TaskCompletionSource<PermissionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
      permissionWaiters[requestId] = tcs;

      owner.OnPermissionRequested?.Invoke(new PermissionRequest(
        RequestId: requestId,
        ToolName: toolName,
        Description: description,
        OldContent: TryGetString(toolCallElement, "oldContent"),
        NewContent: TryGetString(toolCallElement, "newContent")));

      // Hand back the response once the host calls RespondToPermissionAsync.
      // Phase 3's PermissionDiffPanel drives the resolve; we just wait.
      return AwaitPermissionAsync(request, tcs, cancellationToken);

      static async ValueTask<RequestPermissionResponse> AwaitPermissionAsync(
          RequestPermissionRequest req,
          TaskCompletionSource<PermissionOutcome> tcs,
          CancellationToken ct) {
        var outcome = await tcs.Task.WaitAsync(ct);
        var optionId = outcome switch {
          PermissionOutcome.Allow => PickOptionId(req, static k => k == PermissionOptionKind.AllowOnce),
          PermissionOutcome.AllowAlways => PickOptionId(req, static k => k == PermissionOptionKind.AllowAlways),
          PermissionOutcome.Deny => PickOptionId(req, static k => k is PermissionOptionKind.RejectOnce or PermissionOptionKind.RejectAlways),
          _ => null,
        };
        if (optionId is null) {
          return new RequestPermissionResponse {
            Outcome = new CancelledRequestPermissionOutcome(),
          };
        }
        return new RequestPermissionResponse {
          Outcome = new SelectedRequestPermissionOutcome { OptionId = optionId },
        };
      }

      static string? PickOptionId(RequestPermissionRequest req, Func<PermissionOptionKind, bool> predicate) {
        foreach (var option in req.Options) {
          if (predicate(option.Kind)) return option.OptionId;
        }
        return null;
      }
    }

    public ValueTask SessionNotificationAsync(
        SessionNotification notification, CancellationToken cancellationToken = default) {
      if (notification.Update is AgentMessageChunkSessionUpdate chunk
          && chunk.Content is TextContentBlock text) {
        turnBuffer.Append(text.Text);
        owner.OnResponseChunk?.Invoke(text.Text);
      }
      return default;
    }

    public ValueTask<WriteTextFileResponse> WriteTextFileAsync(
        WriteTextFileRequest request, CancellationToken cancellationToken = default)
      => new(new WriteTextFileResponse());

    public ValueTask<ReadTextFileResponse> ReadTextFileAsync(
        ReadTextFileRequest request, CancellationToken cancellationToken = default)
      => new(new ReadTextFileResponse { Content = string.Empty });

    public ValueTask<CreateTerminalResponse> CreateTerminalAsync(
        CreateTerminalRequest request, CancellationToken cancellationToken = default)
      => throw new NotImplementedException("Terminal support is out of scope for v1.");

    public ValueTask<TerminalOutputRequest> TerminalOutputAsync(
        TerminalOutputRequest request, CancellationToken cancellationToken = default)
      => throw new NotImplementedException("Terminal support is out of scope for v1.");

    public ValueTask<ReleaseTerminalResponse> ReleaseTerminalAsync(
        ReleaseTerminalRequest request, CancellationToken cancellationToken = default)
      => throw new NotImplementedException("Terminal support is out of scope for v1.");

    public ValueTask<WaitForTerminalExitResponse> WaitForTerminalExitAsync(
        WaitForTerminalExitRequest request, CancellationToken cancellationToken = default)
      => throw new NotImplementedException("Terminal support is out of scope for v1.");

    public ValueTask<KillTerminalCommandResponse> KillTerminalCommandAsync(
        KillTerminalCommandRequest request, CancellationToken cancellationToken = default)
      => throw new NotImplementedException("Terminal support is out of scope for v1.");

    public ValueTask<JsonElement> ExtMethodAsync(
        string method, JsonElement request, CancellationToken cancellationToken = default)
      => throw new NotImplementedException();

    public ValueTask ExtNotificationAsync(
        string method, JsonElement notification, CancellationToken cancellationToken = default)
      => default;

    private static string? TryGetString(JsonElement element, string propertyName) {
      if (element.ValueKind != JsonValueKind.Object) return null;
      if (!element.TryGetProperty(propertyName, out var prop)) return null;
      return prop.ValueKind switch {
        JsonValueKind.String => prop.GetString(),
        _ => prop.ToString(),
      };
    }
  }
}
