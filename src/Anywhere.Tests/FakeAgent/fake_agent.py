"""Minimal fake ACP agent used by AgentProcessIntegrationTests.

Speaks JSON-RPC over stdio to AgentClientProtocol.ClientSideConnection.
NOTE: the acp-csharp library (nuskey8/acp-csharp) uses *newline-delimited JSON*
(one JSON object per line) — not the `Content-Length`-framed LSP-style framing
that the canonical ACP TypeScript reference implementation uses. The agent
script was originally drafted in the plan with Content-Length framing; this
file is the corrected version that matches what ClientSideConnection.Open()
actually reads via `TextReader.ReadLine()`.
"""

import json
import sys

# acp-csharp's ClientSideConnection reads JSON-RPC messages via
# TextReader.ReadLine(), so the agent must flush stdout after every newline.
# Python defaults to block-buffered stdout when not attached to a TTY (which is
# the case when launched via Process.RedirectStandardOutput), so without this
# the library would block forever waiting for the first message and our
# initialization handshake would deadlock. `-u` / `PYTHONUNBUFFERED=1` would
# work too, but configuring the stream here keeps the script self-contained
# and lets `python` rather than `python -u` work in the AgentProfile.Args.
sys.stdout.reconfigure(line_buffering=True)


def send(msg):
    sys.stdout.write(json.dumps(msg) + "\n")
    sys.stdout.flush()


def read_message():
    # JSON-RPC framing is line-delimited: one JSON object per line.
    line = sys.stdin.readline()
    if not line:
        raise EOFError("stdin closed")
    return json.loads(line)


while True:
    msg = read_message()
    method = msg.get("method")
    msg_id = msg.get("id")

    if method == "initialize":
        # The test only asserts the final response's `content`, so anything
        # protocolVersion-shaped is fine here.
        send({"jsonrpc": "2.0", "id": msg_id, "result": {"protocolVersion": 1}})
    elif method == "session/new":
        # NewSessionResponse requires a sessionId.
        send({"jsonrpc": "2.0", "id": msg_id, "result": {"sessionId": "fake-session-id"}})
    elif method == "session/prompt":
        # Emit two streamed chunks ("fake agent " + "response") BEFORE the final
        # result, so AgentProcess.OnResponseChunk is actually exercised — a fake
        # agent that only ever sent the final response would let a non-streaming
        # AgentProcess pass the first test by accident.
        for text in ("fake agent ", "response"):
            send({
                "jsonrpc": "2.0",
                "method": "session/update",
                "params": {
                    "sessionId": "fake-session-id",
                    "update": {
                        "sessionUpdate": "agent_message_chunk",
                        "content": {"type": "text", "text": text},
                    },
                },
            })
        # PromptResponse only requires stopReason.
        send({"jsonrpc": "2.0", "id": msg_id, "result": {"stopReason": "end_turn"}})
