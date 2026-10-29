# Interactive Questionnaire MCP

## Epic Name

Interactive Questionnaire MCP

## Goal

### Problem

Agents need a reliable way to ask users for structured decisions during a task. Plain-text prompts are easy to miss, difficult to answer consistently, and unable to express common interaction patterns such as a labeled yes/no decision, a single choice with a write-in answer, or selecting several suggestions while adding an option not anticipated by the agent. The existing `Anywhere.Controls` foundation can provide a native questionnaire experience, but agents need an MCP-facing contract and a distributable server that can invoke it.

The epic also needs a clear boundary between UI behavior, shared questionnaire data contracts, and MCP transport. Without shared contracts in `Anywhere.Models`, the UI and server can drift in how they represent choices, write-ins, cancellation, and results.

### Solution

Deliver an agent-facing MCP server, distributed as a separate process or package, that uses the `Anywhere.Controls` foundation to present native interactive questionnaires. The first release will support yes/no questions with customizable labels, single-select multiple choice with an optional write-in answer, and multi-select suggestions with an optional `Other` write-in answer.

The shared questionnaire contracts and models will live in `Anywhere.Models` so the separately distributed MCP server and the Anywhere application can exchange framework-agnostic request and response data. The server will translate MCP tool calls into native questionnaire interactions and return structured user outcomes to the calling agent.

### Impact

- Give agents a dependable, discoverable interaction mechanism for user decisions.
- Reduce ambiguous or malformed answers during planning and implementation workflows.
- Reuse the native controls foundation instead of introducing a second questionnaire UI technology.
- Establish a stable questionnaire contract that can support additional MCP clients and UI hosts later.

## User Personas

### Agent Author or Integrator

A developer configuring or building an MCP-compatible agent workflow. They need a small, explicit tool contract, predictable result values, and clear behavior for cancellation, write-ins, and invalid requests.

### End User

A person collaborating with an agent inside the Anywhere ecosystem. They need to understand the question, choose one or more answers quickly, provide a custom answer when permitted, and know whether their response was accepted, skipped, or cancelled.

### Anywhere Maintainer

A developer maintaining the native controls and shared models. They need contracts that remain independent of WinForms and MCP implementation details, with behavior that can be tested without requiring a live UI for every model-level test.

## High-Level User Journeys

### Journey A: Labeled Yes/No Decision

1. An agent decides that it needs an explicit user decision.
2. The agent calls the questionnaire MCP server with a question, a yes label, and a no label. Example labels include `Begin Implementation` and `Keep Planning`.
3. The server validates the request and opens the native questionnaire UI using `Anywhere.Controls`.
4. The user selects one labeled action.
5. The server returns the selected value to the agent in the shared questionnaire result shape.
6. The agent continues according to the selected answer.

### Journey B: Single-Choice Question with Write-In

1. An agent supplies a question and a list of suggested answers.
2. The server displays the suggestions as mutually exclusive choices and provides a write-in path when enabled.
3. The user selects one suggestion or enters a custom answer.
4. The server returns the selected option or normalized write-in value, along with the completion status.
5. The agent uses the result without parsing presentation-specific text.

### Journey C: Multi-Select Suggestions with Optional `Other`

1. An agent supplies a question and several suggestions.
2. The server displays the suggestions as independently selectable options.
3. If enabled, the UI provides an `Other` write-in answer.
4. The user selects one or more suggestions, or adds a custom answer with no suggestions checked.
5. The server returns all selected suggestions and the optional custom answer in a structured result.
6. The agent decides whether the returned set is sufficient and may ask a follow-up question.

### Journey D: Interrupted or Invalid Interaction

1. The server rejects malformed requests before opening UI, with an MCP error that identifies the invalid field.
2. If the user skips or closes the questionnaire, the server returns a successful result with `Status: Cancelled` rather than treating it as an answer.
3. The agent can decide whether to retry, continue with a fallback, or stop.

## Business Requirements

### Functional Requirements

#### MCP Server and Distribution

- Deliver an MCP server, distributed as a self-contained .NET executable, that agents launch as a local subprocess over stdio.
- Expose both a single-question tool per question kind and a batch tool accepting an array of questions, returning an array of answers.
- Validate required fields, choice counts, mutually exclusive options, write-in configuration, and answer cardinality at the MCP boundary.
- Return a successful result with `Status` of `Answered` or `Cancelled` only, distinguishing a selected answer (stable ID only, not label) from a write-in answer and from a cancelled interaction. Validation failures and server-side failures surface as MCP protocol errors, not result statuses. No timeout status in v1; agents enforce their own timeout by cancelling the request.
- Avoid leaking native UI or WinForms types into the MCP protocol contract.
- Allow at most one in-flight questionnaire per MCP connection; a second request on the same connection while one is outstanding is rejected.
- Launch a standalone question-window process per questionnaire rather than requiring Anywhere.exe to already be running or bridging to a shared host over IPC.
- Communicate only over stdio with the process that spawned it, with no network listener; the OS process boundary is the trust boundary. A project opts into the server implicitly by registering it in its own `.mcp.json`, the same convention already used for the `interactive-choice` server.

#### Supported Question Types

- Support yes/no questions with independently customizable labels for both outcomes.
- Support single-choice multiple choice questions.
- Support optional write-in answers for single-choice questions.
- Support multi-select suggestion questions.
- Support an optional `Other` write-in answer for multi-select questions.
- Preserve the order of choices supplied by the agent unless the product decision explicitly permits sorting.
- Make the selected values unambiguous when labels are changed for presentation.
- Reject a multi-select submission with zero checked suggestions and no `Other` text; require at least one of either.
- Trim write-in text and treat blank-after-trim as not provided, with no maximum length in v1. Define behavior for duplicate labels and duplicate stable values.

#### Native Questionnaire Experience

- Build the interaction on the existing `Anywhere.Controls` foundation.
- Present the question, available choices, write-in affordance, and submission/cancellation actions clearly.
- Support keyboard and mouse interaction for all supported question types.
- Prevent submission of invalid combinations, such as selecting multiple answers in a single-choice question.
- Clearly distinguish an optional `Other` answer from an agent-provided suggestion.
- Ensure the UI can report cancellation and skip outcomes without conflating them with empty answers.

#### `Anywhere.Models` Support

- Add framework-agnostic shared request, question, choice, write-in, completion-status, and result models to `Anywhere.Models`:
  - `QuestionnaireRequest`: `QuestionKind` (`YesNo` | `SingleChoice` | `MultiSelect`), prompt text, ordered `Choices` list, `AllowWriteIn` flag, and for `YesNo` the two customizable labels.
  - `Choice`: stable `Value` (agent-supplied ID) and display `Label`, kept separate so a label can be customized without changing the ID a `Result` returns.
  - `QuestionnaireResult`: `Status` (`Answered` | `Cancelled`), `SelectedValues` (zero, one, or many `Choice.Value`s depending on kind), and `WriteInText` (trimmed, `null` when not provided). Validation and server-side failures are MCP protocol errors, not a `Result` status.
  - All models are plain POCOs with no WinForms or MCP SDK references, serializable with the same JSON settings the MCP server and the standalone question-window process both use.
- Keep shared models usable by both the separately distributed MCP server and the standalone question-window process it launches.
- Represent question kind and answer cardinality explicitly on `QuestionnaireRequest` rather than inferring them from UI controls.
- No EF Core entities, `DbSet`, or migrations for questionnaires in v1 — in-memory only, per the persistence decision above.

#### Operational Behavior

- Correlate the one in-flight questionnaire per MCP connection to the request that created it; reject a second concurrent request on the same connection.
- Propagate MCP client disconnect or cancel as immediate window-process termination, returning `Cancelled`.
- Avoid indefinite orphaned window processes when the calling client disappears (process exits with its parent, or is killed on disconnect detection).
- Provide actionable errors for invalid requests and server shutdown.

### Non-Functional Requirements

- **Framework independence:** `Anywhere.Models` must remain independent of WinForms and other UI frameworks.
- **Protocol safety:** All external input must be validated before it reaches UI code.
- **Deterministic results:** Equivalent user actions must produce equivalent serialized results.
- **Accessibility:** WCAG 2.1 AA baseline. Choices and write-in controls must be keyboard reachable, screen-reader labeled, have visible focus and sufficient contrast, and expose clear selected and disabled states.
- **Localization:** Server/UI chrome strings are stored in `.resx` resource files for future localization. Agent-supplied question text, choice labels, and write-in content are shown as-is and never localized.
- **Responsiveness:** The UI must remain responsive while waiting for a user decision.
- **Reliability:** A cancelled, skipped, or disconnected interaction must complete the MCP request deterministically.
- **Privacy:** Questionnaire content and answers must not be logged by default when they may contain user-provided text.
- **Compatibility:** The shared model contract is unversioned in v1 and must not require MCP clients to reference UI assemblies.
- **Testability:** Model validation and serialization must be testable without a desktop UI; UI behavior must have a documented manual smoke-test path.

## Success Metrics

- An MCP client can launch the separate server and complete each supported question type without manual protocol workarounds.
- In acceptance testing, at least 95% of users who begin a questionnaire can submit a response without support or retry.
- Invalid requests receive actionable validation errors and do not open a questionnaire window.
- Users can complete a question with keyboard-only input for all supported question types.
- Cancellation and client disconnect tests do not leave orphaned questionnaire windows or hanging MCP calls.
- Shared model serialization tests cover all supported question kinds and terminal result statuses.
- No UI framework dependency is introduced into `Anywhere.Models`.

## Out of Scope

- Persistent questionnaire history, response analytics, or audit reporting.
- Saved questionnaire templates or a template-management UI.
- Agent-specific questionnaire presets.
- A general-purpose form builder or arbitrary field types such as dates, files, sliders, or rich text.
- Multiple concurrent conversation tabs or unrelated conversation-management UI.
- MCP server configuration UI inside Anywhere.
- Attachments or file uploads as questionnaire answers.
- Plan-mode UI beyond the ability to ask a supported structured question.
- Replacing the existing ACP transport or hand-rolling ACP JSON-RPC framing.
- A web-based questionnaire client.
- Automatic answer inference from free-form agent text.

## Business Value

**High.** Interactive structured decisions are a foundational capability for agent workflows that need user approval, clarification, or prioritization. The epic improves the reliability of agent-human collaboration while creating a reusable contract between an MCP server and native UI components. The value is highest if the first release keeps the supported question types focused and makes cancellation, validation, and result semantics dependable.
