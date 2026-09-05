# Anywhere

Native, hackable WinForms desktop client for the
[Agent Client Protocol](https://agentclientprotocol.com/get-started/introduction)
(ACP); works with any ACP-compliant coding agent.

## Why This Exists

Claude's official desktop app is Electron based, hence it's _slow_ and hard to
extend if you want custom UI or behavior. Anywhere is native Windows software
instead: it launches your agent of choice as a subprocess and talks ACP to it
over stdio, the same protocol Zed uses for editor/agent communication. Swap in
Claude Code, Zed's agent, or anything else that speaks ACP, just by editing an
agent profile. No fork or rebuild required to point it at a different agent.

## Status

> [!WARNING]
> This is pre-alpha software. Expect crashes, missing features, and breaking
> changes without notice. Data loss is possible, so don't rely on it for
> anything you can't afford to lose. Clone and build from source; no binary
> releases yet.

## What is it?

- **🔌 Talks to any ACP agent.** Configure display name, launch command, args,
  working directory, and env vars per agent profile. Nothing is hardcoded to a
  specific vendor.
- **💬 Chat transcript.** Send prompts, watch responses stream in, rendered as
  markdown.
- **🔐 Inline permissions, keeping you in flow.** When the agent asks for
  permission (`session/request_permission`), it appears as a docked panel right
  above the input box, so the chat, the diff, and the approval controls stay in
  one view. Allow once, allow always, or deny, without losing context or digging
  through modal dialogs.
- **📝 Diffs always in reach.** If the pending permission is for a file write or
  edit, the panel renders the diff right there, old vs. new, colored line by
  line.
- **💾 Nothing disappears on restart.** Chat history and agent profiles are
  persisted to a local SQLite database.
- **🚨 Crashes are visible, not silent.** If the agent subprocess dies, you get
  a system message and a restart button. Malformed JSON-RPC traffic goes to a
  debug log instead of vanishing.

## What it _deliberately_ doesn't do, yet...

Version 1 is scoped to daily power-user needs, not a feature-complete editor
plugin replacement:

- No named presets for specific agents (Claude Code, Zed, Antigravity,
  OpenCode), just the generic "arbitrary command" profile. Presets are planned
  for v1.1.
- No multiple concurrent session tabs, slash-command UI, MCP server config UI,
  attachments, plan-mode UI, or permission-request timeouts (ACP itself doesn't
  have timeouts, so neither do we; a pending permission just waits).

## Tech Stack

- **.NET 10**, WinForms
- **ACP transport:** [`acp-csharp`](https://github.com/nuskey8/acp-csharp), an
  unofficial C# SDK. We contribute fixes upstream rather than forking.
- **Markdown rendering:** a custom `MarkdownLabel` control (Markdig for parsing,
  Vortice/Direct2D+DirectWrite for drawing)
- **Visual style:** [`WinForms.Fluent.UI`](https://github.com/) for a
  native-feeling Windows 11 look on the custom controls; everything else keeps
  the default Win32 look
- **Persistence:** SQLite via EF Core, schema managed by EF Core Migrations,
  stored under the local (non-roaming) `%APPDATA%\Anywhere\`

## Project Structure

Four projects, dependencies flow one way:

```
Anywhere  ->  Anywhere.Controls  ->  Anywhere.Design
Anywhere  ->  Anywhere.Models
```

- **`Anywhere.Design`**: design tokens only (colors, spacing, typography). No UI
  framework dependency, so a future macOS client can reuse it.
- **`Anywhere.Models`**: the EF Core data layer: entities, `AnywhereDbContext`,
  migrations. Also framework-agnostic.
- **`Anywhere.Controls`**: WinForms control library: `MarkdownLabel`,
  `ChatTranscriptPanel`, `PermissionDiffPanel`, `DebugLogPanel`.
- **`Anywhere`**: the app itself. A protocol layer that wraps `acp-csharp`, an
  agent registry backed by concrete repository classes over `AnywhereDbContext`,
  and the `MainForm` UI that ties it together.

See [docs/Architecture.md](docs/Architecture.md) for the architecture writeup,
[AGENTS.md](AGENTS.md) for build/test commands, and
[docs/superpowers/specs/Design.md](docs/superpowers/specs/Design.md) for the
original design spec.

## Building

```bash
dotnet tool restore
dotnet build Anywhere.sln
dotnet test src/Anywhere.Tests/Anywhere.Tests.csproj
dotnet run --project src/Anywhere/Anywhere.csproj
```

## License

BSD 3-Clause. See [LICENSE](LICENSE).
