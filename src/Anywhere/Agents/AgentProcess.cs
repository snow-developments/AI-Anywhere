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
  private readonly AgentProfile _profile;
  private Process? _process;
  private ClientSideConnection? _connection;
  private string? _sessionId;
  private readonly AcpClientAdapter _adapter;
  private long _nextRequestId;

  public event Action<string>? OnResponseChunk;
  public event Action<PermissionRequest>? OnPermissionRequested;
  public event Action<string>? OnAgentExited;

  public AgentProcess(AgentProfile profile) {
    _profile = profile;
    _adapter = new AcpClientAdapter(this);
  }

  public async Task StartAsync() {
    _process = new Process {
      StartInfo = new ProcessStartInfo {
        FileName = _profile.Command,
        WorkingDirectory = _profile.WorkingDir,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardInput = true,
        RedirectStandardError = true,
      },
    };
    foreach (var arg in _profile.Args) _process.StartInfo.ArgumentList.Add(arg);
    foreach (var (key, value) in _profile.Env) {
      _process.StartInfo.Environment[key] = value;
    }
    if (!_process.Start()) {
      throw new InvalidOperationException("Failed to start agent process.");
    }
    _process.EnableRaisingEvents = true;
    _process.Exited += (_, _) => OnAgentExited?.Invoke(
      $"exit code {_process.ExitCode}");

    _connection = new ClientSideConnection(
      _ => _adapter,
      _process.StandardOutput,
      _process.StandardInput);
    _connection.Open();

    var initResult = await _connection.InitializeAsync(new InitializeRequest {
      ProtocolVersion = 1,
      ClientCapabilities = new ClientCapabilities(),
    });

    if (initResult.ProtocolVersion != 1) {
      throw new InvalidOperationException(
        $"Unsupported protocol version: {initResult.ProtocolVersion}");
    }

    var sessionResult = await _connection.NewSessionAsync(new NewSessionRequest {
      Cwd = _profile.WorkingDir,
      McpServers = [],
    });
    _sessionId = sessionResult.SessionId;
  }

  public async Task<PromptResult> SendPromptAsync(string text) {
    if (_connection is null || _sessionId is null) {
      throw new InvalidOperationException("StartAsync must complete before SendPromptAsync.");
    }

    _adapter.BeginTurn();

    var promptRequest = new PromptRequest {
      SessionId = _sessionId,
      Prompt = [new TextContentBlock { Text = text }],
    };

    await _connection.PromptAsync(promptRequest);

    return new PromptResult(_adapter.EndTurnText());
  }

  public Task RespondToPermissionAsync(string requestId, PermissionOutcome outcome) {
    _adapter.ResolvePermission(requestId, outcome);
    return Task.CompletedTask;
  }

  public void Dispose() {
    _connection?.Dispose();
    if (_process is not null && !_process.HasExited) {
      try {
        _process.Kill(entireProcessTree: true);
        _process.WaitForExit(2000);
      } catch {
        // best-effort cleanup; the process may already be exiting on its own
      }
    }
    _process?.Dispose();
  }

  // ---- Adapter: implements IAcpClient so acp-csharp can call back into us ----

  private sealed class AcpClientAdapter : IAcpClient {
    private readonly AgentProcess _owner;
    private readonly StringBuilder _turnBuffer = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PermissionOutcome>> _permissionWaiters = new();

    internal AcpClientAdapter(AgentProcess owner) => _owner = owner;

    internal void BeginTurn() {
      // Discard any leftover text from a prior turn — chunks accumulate into
      // _turnBuffer until EndTurnText reads it out.
      _turnBuffer.Clear();
    }

    internal string EndTurnText() => _turnBuffer.ToString();

    internal void ResolvePermission(string requestId, PermissionOutcome outcome) {
      if (_permissionWaiters.TryRemove(requestId, out var tcs)) {
        tcs.TrySetResult(outcome);
      }
    }

    public ValueTask<RequestPermissionResponse> RequestPermissionAsync(
        RequestPermissionRequest request, CancellationToken cancellationToken = default) {
      var requestId = Interlocked.Increment(ref _owner._nextRequestId).ToString();
      var toolCallElement = (JsonElement)request.ToolCall;
      var toolName = TryGetString(toolCallElement, "title")
                     ?? TryGetString(toolCallElement, "name")
                     ?? "tool";
      var description = TryGetString(toolCallElement, "description")
                        ?? TryGetString(toolCallElement, "title")
                        ?? toolName;

      var tcs = new TaskCompletionSource<PermissionOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
      _permissionWaiters[requestId] = tcs;

      _owner.OnPermissionRequested?.Invoke(new PermissionRequest(
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
        _turnBuffer.Append(text.Text);
        _owner.OnResponseChunk?.Invoke(text.Text);
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
