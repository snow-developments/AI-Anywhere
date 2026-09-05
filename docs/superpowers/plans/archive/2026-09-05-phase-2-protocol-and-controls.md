# ACP WinForms Client — Phase 2: Markdown Control + ACP Protocol Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the reusable `MarkdownLabel` WinForms control and the
`AgentProcess` wrapper that drives an ACP agent subprocess over stdio JSON-RPC
via `acp-csharp` — the two building blocks Phase 3's chat UI wires together.

**Architecture:** `MarkdownLabel` lives in `Anywhere.Controls` (Markdig for
parsing, Vortice/Direct2D+DirectWrite for hardware-accelerated custom drawing)
and has no dependency on the protocol layer. `AgentProcess` lives in `Anywhere`
and wraps a `System.Diagnostics.Process` plus an `acp-csharp`
`ClientSideConnection`; it depends on `AgentProfile` (`Anywhere.Models`,
Phase 1) for launch config and on two small types (`PermissionRequest`,
`PermissionOutcome`) that live in `Anywhere.Controls` rather than
`Anywhere/Agents/`, specifically so Phase 3's `PermissionDiffPanel` can consume
them without a circular project reference.

**Tech Stack:** .NET 10, WinForms, Markdig,
Vortice.Direct2D1/Vortice.DirectWrite, `acp-csharp` (NuGet, MIT), xUnit
(integration test drives a real fake-agent subprocess over real stdio JSON-RPC,
not mocked library internals).

**Spec:**
[docs/superpowers/specs/2026-09-04-design.md](../specs/2026-09-04-design.md)

**Plan series:** This is Phase 2 of 4. Requires Phase 1 complete (project
skeleton + `Anywhere.Models`). See also:

- Phase 1 —
  [2026-09-05-anywhere-phase1-foundation.md](2026-09-05-anywhere-phase1-foundation.md)
  (scaffolding, EF Core persistence, canonical file structure)
- Phase 3 —
  [2026-09-05-anywhere-phase3-core-ui.md](2026-09-05-anywhere-phase3-core-ui.md)
  (chat transcript UI, permission/diff panel)
- Phase 4 —
  [2026-09-05-anywhere-phase4-polish.md](2026-09-05-anywhere-phase4-polish.md)
  (crash recovery, profile management UI, visual styling)

## Global Constraints

Full project-wide constraints are listed in
[Phase 1's Global Constraints](2026-09-05-anywhere-phase1-foundation.md#global-constraints).
The ones that bind this phase specifically:

- Project dependency direction: `Anywhere` → `Anywhere.Controls` →
  `Anywhere.Design`, and `Anywhere` → `Anywhere.Models`. Never reverse either
  arrow. `AgentProcess` (in `Anywhere`) may freely reference `Anywhere.Controls`
  types since `Anywhere` already depends on `Anywhere.Controls`.
- ACP transport dependency: `acp-csharp` NuGet package — do not hand-roll
  JSON-RPC framing. This project will contribute fixes/gaps upstream as they're
  found rather than forking.
- Markdown rendering: adapt `MarkdownLabel.cs` from `family-lock-out`, living in
  `Anywhere.Controls` — do not add a WebView2-based renderer.
- No permission-request timeouts — this phase defines
  `PermissionRequest`/`PermissionOutcome` and the event that raises them; Phase
  3's panel is the one that actually waits indefinitely on them.

## File Structure

Files this phase adds, within the canonical structure defined in
[Phase 1](2026-09-05-anywhere-phase1-foundation.md#file-structure):

```
src/
  Anywhere.Controls/
    MarkdownLabel.cs           (adapted from family-lock-out)
    PermissionRequest.cs
    PermissionOutcome.cs
  Anywhere/
    Agents/
      AgentProcess.cs
      PromptResult.cs
  Anywhere.Tests/
    MarkdownLabelTests.cs
    FakeAgent/
      fake_agent.py
    AgentProcessIntegrationTests.cs
```

**Interfaces summary added by this phase (in addition to Phase 1's):**

- `MarkdownLabel : Control` — markdown-source property confirmed from the source
  file in Task 4 Step 1 (spec guesses `Text`; do not assume).
- `PromptResult` (`Anywhere.Agents`, record): `string Content`
- `PermissionRequest` (`Anywhere.Controls`, record):
  `string RequestId, string ToolName, string Description, string? OldContent, string? NewContent`
- `PermissionOutcome` (`Anywhere.Controls`, enum): `Allow, AllowAlways, Deny`
- `AgentProcess` (`Anywhere.Agents`): `Task StartAsync()`,
  `Task<PromptResult> SendPromptAsync(string text)`,
  `event Action<string> OnResponseChunk`,
  `event Action<Anywhere.Controls.PermissionRequest> OnPermissionRequested`,
  `event Action<string> OnAgentExited`,
  `Task RespondToPermissionAsync(string requestId, Anywhere.Controls.PermissionOutcome outcome)`
  — consumed by Phase 3 (UI wiring, `PermissionDiffPanel`, incremental
  transcript append) and Phase 4 (crash recovery, protocol debug log).

**2026-09-06 fix (review finding #1):** the spec
(`docs/superpowers/specs/2026-09-04-design.md:13`) requires chat to "stream
agent responses." The original draft of this task had `AgentProcess` expose only
a single awaited `Task<PromptResult>`, with no way to observe partial output
before the turn completes — a real gap given ACP streams `session/update`
notifications (e.g. `agent_message_chunk`) ahead of the final result.
`OnResponseChunk` was added to close that gap; `SendPromptAsync` still returns
the final `PromptResult` once the turn completes, so callers that don't care
about incremental output can ignore the event.

---

## Task 4: Adapt MarkdownLabel control

**Files:**

- Create: `src/Anywhere.Controls/MarkdownLabel.cs` (adapted from
  `D:\Users\enigm\GitHub\family-lock-out\Controls\MarkdownLabel.cs`)
- Test: `src/Anywhere.Tests/MarkdownLabelTests.cs`

**Interfaces:**

- Consumes: nothing new.
- Produces: `MarkdownLabel : Control` with a `string Markdown { get; set; }`
  property (renamed/generalized if the source used a different property name),
  living in the `Anywhere.Controls` project — consumed by Phase 3 (chat
  transcript panel).

- [x] **Step 1: Read the source control**

Read `D:\Users\enigm\GitHub\family-lock-out\Controls\MarkdownLabel.cs` in full
to understand its exact public API (property names, constructor, `OnPaint`
override, Markdig/Vortice usage) before copying — do not assume the property
names guessed in the spec.

- [x] **Step 2: Add Markdig and Vortice package references**

```bash
dotnet add src/Anywhere.Controls/Anywhere.Controls.csproj package Markdig
dotnet add src/Anywhere.Controls/Anywhere.Controls.csproj package Vortice.Direct2D1
dotnet add src/Anywhere.Controls/Anywhere.Controls.csproj package Vortice.DirectWrite
```

(Adjust exact Vortice package names to match whichever `Vortice.*` namespaces
the source file actually imports — confirm from Step 1's read.)

- [x] **Step 3: Copy the control into the new project, renaming the namespace**

Copy the file to `src/Anywhere.Controls/MarkdownLabel.cs`, change
`namespace FamilyLockout.Controls` to `namespace Anywhere.Controls`, and fix any
using-directives that referenced the old project.

- [x] **Step 4: Write a smoke test that constructs the control
      off-UI-thread-safely**

```csharp
// src/Anywhere.Tests/MarkdownLabelTests.cs
using Anywhere.Controls;
using Xunit;

public class MarkdownLabelTests
{
    [WinFormsFact]
    public void Setting_markdown_text_does_not_throw()
    {
        using var label = new MarkdownLabel();
        label.Text = "**bold** and _italic_ and a [link](https://example.com)";
        Assert.Equal("**bold** and _italic_ and a [link](https://example.com)", label.Text);
    }
}
```

(If the source control exposes a different property than `Text` for the markdown
source, use that property name here instead — confirmed in Step 1.)

- [x] **Step 5: Add the WinForms test SDK for `[WinFormsFact]`**

```bash
dotnet add src/Anywhere.Tests/Anywhere.Tests.csproj package WinForms.UITest.Foundation
```

If `WinForms.UITest.Foundation` isn't available/needed, replace `[WinFormsFact]`
with plain `[Fact]` (WinForms controls can be constructed off-thread in a
headless test as long as no message loop is required) — try plain `[Fact]` first
since it avoids an extra dependency.

- [x] **Step 6: Run the test to verify it passes**

Run:
`dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter MarkdownLabelTests`
Expected: PASS.

- [x] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: adapt MarkdownLabel control from family-lock-out"
```

---

## Task 5: Agent process wrapper over acp-csharp

**Files:**

- Create: `src/Anywhere/Agents/AgentProcess.cs`
- Create: `src/Anywhere/Agents/PromptResult.cs`
- Create: `src/Anywhere.Controls/PermissionRequest.cs`
- Create: `src/Anywhere.Controls/PermissionOutcome.cs`
- Test: `src/Anywhere.Tests/FakeAgent/fake_agent.py`
- Test: `src/Anywhere.Tests/AgentProcessIntegrationTests.cs`

**Note on project placement:** `PermissionRequest`/`PermissionOutcome` are
created here in `Anywhere.Controls` rather than `Anywhere/Agents/`, even though
`AgentProcess` (which uses them) lives in `Anywhere`. This is intentional: Phase
3's `PermissionDiffPanel` lives in `Anywhere.Controls`, and `Anywhere` →
`Anywhere.Controls` is the only allowed reference direction (never the reverse)
— see Global Constraints. `AgentProcess` can reference `Anywhere.Controls` types
freely since `Anywhere` already depends on `Anywhere.Controls` (Phase 1, Task 1
Step 6).

**Interfaces:**

- Consumes: `AgentProfile` (`Anywhere.Models`) from Phase 1, Task 2.
- Produces: `AgentProcess.StartAsync()`,
  `SendPromptAsync(string) : Task<PromptResult>`,
  `event Action<string> OnResponseChunk`,
  `event Action<Anywhere.Controls.PermissionRequest> OnPermissionRequested`,
  `event Action<string> OnAgentExited`,
  `RespondToPermissionAsync(string requestId, Anywhere.Controls.PermissionOutcome outcome)`
  — consumed by Phase 3 (UI wiring, `PermissionDiffPanel` and incremental
  transcript append, which consume the
  `PermissionRequest`/`PermissionOutcome`/`OnResponseChunk` members created here
  rather than redefining them) and Phase 4 (crash recovery, protocol debug log).

- [x] **Step 1: Add the acp-csharp package**

```bash
dotnet add src/Anywhere/Anywhere.csproj package AcpCSharp
```

(Confirm exact NuGet package id from https://github.com/nuskey8/acp-csharp's
README/NuGet listing — adjust the id above if it differs, e.g. it may be
published as `Acp.CSharp` or similar.)

- [x] **Step 2: Write a minimal fake ACP agent for integration testing**

```python
# src/Anywhere.Tests/FakeAgent/fake_agent.py
import sys, json

def send(msg):
    data = json.dumps(msg)
    sys.stdout.write(f"Content-Length: {len(data)}\r\n\r\n{data}")
    sys.stdout.flush()

def read_message():
    line = sys.stdin.readline()
    length = int(line.split(":")[1].strip())
    sys.stdin.readline()  # blank line
    body = sys.stdin.read(length)
    return json.loads(body)

while True:
    msg = read_message()
    if msg.get("method") == "initialize":
        send({"jsonrpc": "2.0", "id": msg["id"], "result": {"protocolVersion": "1"}})
    elif msg.get("method") == "session/prompt":
        # Emit an intermediate session/update notification (a streamed chunk) before
        # the final result, so AgentProcess.OnResponseChunk is actually exercised —
        # a fake agent that only ever sends one complete response would let a
        # non-streaming AgentProcess pass this test by accident.
        send({"jsonrpc": "2.0", "method": "session/update",
              "params": {"update": {"sessionUpdate": "agent_message_chunk", "content": "fake agent "}}})
        send({"jsonrpc": "2.0", "id": msg["id"], "result": {"content": "fake agent response"}})
```

(This uses ACP's `Content-Length`-framed stdio transport per the spec, and a
`session/update` shape based on the public ACP schema's `agent_message_chunk`
update kind. If `acp-csharp`'s actual wire framing or update-notification shape
differs, adjust `send`/`read_message` and the `session/update` payload to match
after reading the library's transport implementation in Step 3.)

- [x] **Step 3: Read acp-csharp's public API for `ClientSideConnection`**

Before writing `AgentProcess`, read the actual
`ClientSideConnection`/`IAcpClient` API from the installed `acp-csharp` package
(via NuGet cache or its GitHub source) to get exact method/event names — do not
guess signatures.

- [x] **Step 4: Write the failing integration test**

```csharp
// src/Anywhere.Tests/AgentProcessIntegrationTests.cs
using System.Collections.Generic;
using System.Threading.Tasks;
using Anywhere.Agents;
using Anywhere.Models;
using Xunit;

public class AgentProcessIntegrationTests
{
    [Fact]
    public async Task SendPromptAsync_returns_the_fake_agents_response()
    {
        var profile = new AgentProfile
        {
            Name = "Fake",
            Command = "python",
            Args = new[] { "src/Anywhere.Tests/FakeAgent/fake_agent.py" },
            Env = new System.Collections.Generic.Dictionary<string, string>(),
            WorkingDir = System.IO.Directory.GetCurrentDirectory(),
        };

        var process = new AgentProcess(profile);
        await process.StartAsync();

        var result = await process.SendPromptAsync("hello");

        Assert.Equal("fake agent response", result.Content);
    }

    [Fact]
    public async Task SendPromptAsync_raises_OnResponseChunk_before_completing()
    {
        var profile = new AgentProfile
        {
            Name = "Fake",
            Command = "python",
            Args = new[] { "src/Anywhere.Tests/FakeAgent/fake_agent.py" },
            Env = new System.Collections.Generic.Dictionary<string, string>(),
            WorkingDir = System.IO.Directory.GetCurrentDirectory(),
        };

        var process = new AgentProcess(profile);
        await process.StartAsync();

        var chunks = new List<string>();
        process.OnResponseChunk += chunks.Add;

        await process.SendPromptAsync("hello");

        Assert.Contains("fake agent ", chunks);
    }
}
```

- [x] **Step 5: Run test to verify it fails**

Run:
`dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter AgentProcessIntegrationTests`
Expected: FAIL (compile error — `AgentProcess`, `PromptResult` not defined).

- [x] **Step 6: Implement `PromptResult`, `PermissionRequest`,
      `PermissionOutcome`**

```csharp
// src/Anywhere/Agents/PromptResult.cs
namespace Anywhere.Agents;

public record PromptResult(string Content);
```

```csharp
// src/Anywhere.Controls/PermissionRequest.cs
namespace Anywhere.Controls;

public record PermissionRequest(string RequestId, string ToolName, string Description, string? OldContent, string? NewContent);
```

```csharp
// src/Anywhere.Controls/PermissionOutcome.cs
namespace Anywhere.Controls;

public enum PermissionOutcome { Allow, AllowAlways, Deny }
```

- [x] **Step 7: Implement `AgentProcess`, including `OnResponseChunk`**

Implement `AgentProcess` in `src/Anywhere/Agents/AgentProcess.cs`, wrapping a
`System.Diagnostics.Process` (launching `Profile.Command` with
`Profile.Args`/`Profile.Env`/`Profile.WorkingDir`, redirecting stdin/stdout) and
an `acp-csharp` `ClientSideConnection` bound to those streams, using the exact
API confirmed in Step 3. Add `using Anywhere.Controls;` for the
`PermissionRequest`/`PermissionOutcome` types. Wire `OnPermissionRequested` to
the library's `session/request_permission` callback, and `OnAgentExited` to the
underlying `Process.Exited` event.

Add `public event Action<string>? OnResponseChunk;`, raised from whichever
`acp-csharp` callback surfaces `session/update` notifications of kind
`agent_message_chunk` (confirmed from Step 3's API read) — invoke it with that
chunk's text content each time one arrives, before `SendPromptAsync`'s awaited
`Task<PromptResult>` completes with the final response.

- [x] **Step 8: Run the test to verify it passes**

Run:
`dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj --filter AgentProcessIntegrationTests`
Expected: PASS (both tests).

- [x] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: wrap acp-csharp ClientSideConnection in AgentProcess"
```

---

## Self-Review Notes

- **Spec coverage:** markdown rendering (Task 4) and the agent-agnostic,
  _streaming_ ACP protocol wrapper (Task 5) are both covered per spec. Chat UI
  wiring and the permission panel UI itself are Phase 3's responsibility — this
  phase only produces the building blocks (`MarkdownLabel`, `AgentProcess`,
  `PermissionRequest`/`PermissionOutcome`).
- **Independently testable:** `MarkdownLabelTests` verifies rendering doesn't
  throw; `AgentProcessIntegrationTests` drives a real fake-agent subprocess
  through `initialize` → `session/prompt` (including an intermediate
  `session/update` chunk) over real stdio JSON-RPC, not mocked library internals
  — consistent with the project's testing philosophy.
- **2026-09-06 fix from `2026-09-05-anywhere-phases.review.md`, finding #1
  (High):** the original draft of Task 5 had `AgentProcess` expose only a
  single-shot `Task<PromptResult>`, contradicting the spec's explicit "stream
  agent responses" requirement
  (`docs/superpowers/specs/2026-09-04-design.md:13`). Added
  `event Action<string> OnResponseChunk`, wired to `acp-csharp`'s
  `session/update`/`agent_message_chunk` notifications, plus a fake-agent update
  and a dedicated integration test
  (`SendPromptAsync_raises_OnResponseChunk_before_completing`) so the streaming
  path is actually exercised rather than assumed. Phase 3, Task 6 was amended to
  consume this event incrementally.
- **Unconfirmed external API surfaces** (flagged inline rather than guessed,
  each task instructs reading the real source before coding against it): exact
  `acp-csharp` NuGet package id and its `ClientSideConnection` method/event
  names, including the exact shape of `session/update`/`agent_message_chunk`
  notifications (Task 5 Steps 1 & 3), `MarkdownLabel`'s real public property
  name (Task 4 Step 1).

## Implementation Notes (2026-09-05)

Notes from the actual implementation session; recorded here so the next phase
inherits them and the next plan author doesn't re-discover the same gotchas.

### `acp-csharp` (nuskey8/acp-csharp)

- **NuGet package id is `AgentClientProtocol`**, not `AcpCSharp`. Current
  version is `0.1.5`. The `.csproj` uses
  `<PackageReference Include="AgentClientProtocol" Version="0.1.5" />`.
- **Wire framing is newline-delimited JSON**, one JSON object per line — read by
  `TextReader.ReadLine()` inside `JsonRpcEndpoint.ReadMessagesAsync`. It is
  **not** the `Content-Length`-framed LSP-style framing the canonical ACP
  TypeScript reference implementation uses. The fake agent and any third-party
  agent we shell out to must terminate every message with `\n`.
- **`ClientSideConnection` constructor:**
  `(Func<IAcpAgent, IAcpClient> toClient,
  TextReader reader, TextWriter writer)`.
  The callback receives the `IAcpAgent` (the connection itself, since it
  implements that interface for the other side) and must return an `IAcpClient`
  implementation that the connection will invoke when the agent sends
  notifications / requests back.
- **Required handshake sequence:** `Open()` (background read loop) →
  `InitializeAsync(InitializeRequest { ProtocolVersion = 1, ClientCapabilities = new() })`
  → `NewSessionAsync(NewSessionRequest { Cwd, McpServers = [] })` →
  `PromptAsync(PromptRequest { SessionId, Prompt = [new TextContentBlock { Text }] })`.
  We must stash `NewSessionResponse.SessionId` for use in `PromptRequest`.
- **`PromptResponse` only carries `StopReason`**, not content. Content must be
  assembled from `IAcpClient.SessionNotificationAsync`
  `AgentMessageChunkSessionUpdate` notifications with `Content` of type
  `TextContentBlock`.
- **Streaming chunks:** `AgentMessageChunkSessionUpdate.Content` is a
  `ContentBlock` (abstract). Switch on runtime type — only `TextContentBlock` is
  text; `ImageContentBlock`/`AudioContentBlock`/`ResourceLinkContentBlock`/
  `ResourceContentBlock` are not (yet) surfaced to the UI in v1.
- **Permissions:** the agent invokes `session/request_permission` by calling
  `IAcpClient.RequestPermissionAsync(RequestPermissionRequest)`. That returns a
  `ValueTask<RequestPermissionResponse>` — the C# side can block as long as it
  wants (no timeout from the library). The `ToolCall` field is typed as
  `object`; at runtime it's a `JsonElement` whose `title`/`name`/`description`
  we surface into `PermissionRequest`.
- **`PermissionOptionKind` enum** has four values: `AllowOnce`, `AllowAlways`,
  `RejectOnce`, `RejectAlways`. We map `PermissionOutcome.Allow` → `AllowOnce`,
  `AllowAlways` → `AllowAlways`, `Deny` → `RejectOnce` or `RejectAlways`
  (whichever the agent offered, else return
  `CancelledRequestPermissionOutcome`).
- **Terminal / fs methods** on `IAcpClient` are unused in v1 — throw
  `NotImplementedException("Terminal support is out of scope for v1.")` from
  each. `ExtMethodAsync`/`ExtNotificationAsync` likewise.
- **`Connection.Open()` starts the background read loop in a fire-and-forget
  task** (`Task.Run(async () => await endpoint.ReadMessagesAsync(cts.Token))`).
  The `CancellationTokenSource` is internal; `Dispose()` on the connection
  cancels it. `AgentProcess` is `IDisposable` and disposes the connection plus
  kills the subprocess (`Process.Kill(entireProcessTree: true)` followed by
  `WaitForExit(2000)`) so tests using `using var process = ...` don't leak
  Python processes.

### `MarkdownLabel` (adapted from `family-lock-out/Controls/MarkdownLabel.cs`)

- **Source property is `Text`** (WinForms standard `Control.Text`), confirmed
  from the source — the plan's note that the property name should be confirmed
  before copy is satisfied; no rename needed.
- **Packages:** only `Vortice.Direct2D1` is needed; `Vortice.DirectWrite` does
  not exist as a separate NuGet package — `using Vortice.DirectWrite;` works
  because `Vortice.Direct2D1` brings in the DirectWrite types as a transitive
  dependency. The plan's Step 2 listing both was wrong; only `Vortice.Direct2D1`
  is referenced.
- **WinForms source-generator errors:** the source had two properties
  (`AutoSizeMode` and `BlockSpacing`) that triggered `WFO1000` ("does not
  configure code serialization") when built against .NET 10. Fix: add
  `[DefaultValue(AutoSizeMode.GrowOnly)]` and `[DefaultValue(6f)]` on the
  respective properties. `dotnet format` will also reorder usings and insert a
  missing indent on the `AutoSize` setter — both are auto-fixed.
- **Two pre-existing CS8765 warnings** on the `Font` and `Text` setters
  (nullability mismatch with `Control`'s nullable overrides). Source had them
  too on .NET 8; they're harmless warnings in .NET 10 as well.
- **Smoke test uses plain `[Fact]`**, not `[WinFormsFact]` from
  `WinForms.UITest.Foundation`. The control's `Handle` is lazy, so constructing
  it off-thread never triggers `OnHandleCreated`/`OnPaint`, and the test only
  round-trips `Text` — no message loop required.

### Integration test pitfalls (resolved)

- **Fake agent script path must be resolved via `AppContext.BaseDirectory`**,
  not relative to `Directory.GetCurrentDirectory()`. `dotnet test` runs tests
  with cwd set to the bin directory, not the repo root. Adding
  `<None Include="FakeAgent\**\*.py" CopyToOutputDirectory="PreserveNewest" />`
  to `Anywhere.Tests.csproj` copies the script into `bin/.../FakeAgent/` so
  `Path.Combine(AppContext.BaseDirectory, "FakeAgent", "fake_agent.py")`
  resolves.
- **Python stdout is block-buffered when piped** (not attached to a TTY). Even
  with `sys.stdout.flush()` after every write, the buffered bytes never reach
  the C# `TextReader.ReadLine()` until the buffer fills or the process exits —
  at which point `acp-csharp` writes fail with
  `IOException: The pipe
  is being closed`. Fix: call
  `sys.stdout.reconfigure(line_buffering=True)` at script startup. This avoids
  needing `python -u` or `PYTHONUNBUFFERED=1` in the agent profile's
  `Args`/`Env`.
- **`[Fact(Timeout = 30000)]` on both integration tests.** Without this, a hang
  in `StartAsync` (e.g. while debugging the buffering issue above) silently
  hangs the test host forever — the user has to kill the runner manually. With
  the timeout, the test fails cleanly after 30 s and the suite exits. Pair with
  `using var process = new AgentProcess(...)` in each test so `Dispose` runs
  even on timeout, killing the orphan Python subprocess.
- **Test ordering:** xUnit runs tests in non-deterministic order by default, but
  if a previous test left an orphaned Python subprocess, the next test can fail
  with confusing errors. `AgentProcess.Dispose` handles teardown; if a hang
  still escapes, `taskkill /F /IM python.exe` cleans up.

### Plan author should also know

- **Missing assets regression:** the original Phase 1 scaffold left
  `assets/icons/Anywhere.ico` (and friends) on disk but never committed them.
  `Anywhere.csproj` references the icon via `<ApplicationIcon>` and
  `<EmbeddedResource>`, so the build only worked locally; CI / fresh clones
  would have failed. The Phase 2 commit history now includes a separate
  `Add chat-compass icon assets` commit before the MarkdownLabel and
  AgentProcess commits to fix this. Future plans that touch `Anywhere.csproj`
  should assume `assets/` exists in HEAD.
- **AGENTS.md GitHub-queries rule was strengthened** in this session after the
  implementation agent (me) silently used the `fetch` tool against
  `api.github.com` instead of `gh api` for several rounds of acp-csharp source
  queries. The rule now explicitly forbids `fetch`/`curl` against
  `api.github.com` and `raw.githubusercontent.com`, with examples of the
  `gh api` equivalents.
