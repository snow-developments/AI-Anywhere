# Style Guide

Rules for this repo's C# code, enforced where possible by `./.editorconfig` (repo root) and otherwise by review. Applies to all four `Anywhere.*` projects and `Anywhere.Tests`.

## Markdown Headings

All markdown headings across the repo (this file included) use title case: capitalize the first, last, and every major word; lowercase articles, conjunctions, and short prepositions (`a`, `an`, `the`, `for`, `and`, `of`, `in`, `to`, ...) unless they're the first or last word. `## DRY in Symbol Naming`, not `## DRY in symbol naming`.

## C#

### Naming

- `PascalCase` for types, methods, properties, public fields, and constants.
- `camelCase` for parameters and locals.
- `camelCase` (no leading underscore) for private instance fields, same as parameters and locals.
- Interfaces (on the rare occasion one is justified; see [[Abstractions]]) are prefixed `I`. Don't create an interface as a rename-proof wrapper around a single concrete type "just in case."
- Async methods are suffixed `Async` (`InsertAsync`, `SendPromptAsync`), with no exceptions, including for `void`-returning fire-and-forget methods (rare; prefer `Task`-returning even then).
- Boolean-returning members read as a question or assertion: `IsVisible`, `HasPendingRequest`, not `Visible` (unless it's a WinForms property you don't own) or `PendingRequestFlag`.

#### Don't Repeat Yourself (DRY)

The most common DRY violation in this codebase's plans isn't duplicated logic; it's **the same concept spelled with a different name in different places**. That's still a DRY violation: a reader has to work out that `db`, `context`, and `dbContext` all mean the same thing before they can trust that they *do* all mean the same thing.

- **Pick one canonical name per concept, project-wide, and never deviate.** Concrete list for this repo:
  - `AnywhereDbContext` parameters/fields are always named `db`, never `context`, `dbContext`, or `_context`.
  - A repository's backing context field is always `db` (private, readonly).
  - `AgentProfile`/`Session`/`Message` instances are named after their type in camelCase (`profile`, `session`, `message`), never abbreviated (`p`, `sess`, `msg`) except in short LINQ lambdas where the parameter's scope is a single line (`p => p.Id == id` is fine; a multi-line method body is not).
  - The ACP subprocess wrapper is always `AgentProcess`/`agentProcess`, never `process`, `agent`, or `client` (the last is especially confusable with `ClientSideConnection`, which is `acp-csharp`'s type, not ours).
  - Cancellation tokens, when added, are always named `cancellationToken` in full, never `ct`, `token`, or `cancelToken`.
- **Don't introduce a second name for something that already has one.** If a type, field, or method already exists under a name, reuse that name, don't add a locally-scoped synonym because it reads better in one call site. If the existing name genuinely doesn't fit a new use, that's a signal to rename it everywhere (one PR, one meaning), not to grow a second name alongside it.
- **Don't repeat the type name inside a variable name when the type is already obvious from context** (`AgentProfile agentProfile` inside a method already named `InsertAsync(AgentProfile profile)` should just be `profile`), but *do* keep the type-derived name when there's more than one thing of a similar shape in scope (a method juggling both a `Session session` and a `SessionSummary summary` needs the distinction).

### Formatting Baseline (See `.editorconfig` for the Enforced Subset)

- File-scoped namespaces always: `namespace Anywhere.Models;` on its own line, never the braced block form.
- 2-space indentation, spaces not tabs, no trailing whitespace, final newline at EOF; see `.editorconfig`.
- One blank line between members; no blank line at the top of a block immediately after its opening brace.
- `var` when the right-hand side makes the type obvious (`var profile = new AgentProfile { ... }`, `var results = await db.Profiles.ToListAsync()`); the explicit type when it doesn't (`int id = ExecuteScalar(...)` if `ExecuteScalar`'s return type isn't visible at a glance).

### Prefer One-Liners and Bare Blocks for Simple, Singular Statements

If a method body, `if`, `for`, `foreach`, or `while` controls exactly one simple statement, don't wrap it in a block. This isn't a stylistic nicety here; it's already the pattern the implementation plans use (`if (currentRequestId is null) return;`, `if (profile is null) return;`) and it should be applied consistently rather than only where it happened to get written that way first.

```csharp
// Preferred: single simple statement, no braces:
if (profile is null) return;
if (toolCallJson is null) return null;
foreach (var chunk in chunks) AppendToCurrentAgentMessage(chunk);

// Preferred: single-expression method body:
public Task<AgentProfile?> GetAsync(int id)
    => db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

// Not preferred: braces around one simple statement:
if (profile is null)
{
    return;
}
```

"Simple and singular" is the boundary: the moment a branch needs two statements, a comment, or spans more than one logical action, it gets braces:

```csharp
// Two statements: braces required, no exceptions:
if (currentRequestId is null)
{
    debugLogPanel.AppendLine("Ignored outcome with no pending request.");
    return;
}
```

Don't omit braces to cram multiple statements onto one line with semicolons, and don't nest a brace-less `if` directly inside another brace-less `if`/`for` (the classic dangling-`else` hazard); add braces the moment there's any nesting, even if each individual level would qualify alone.

## Relevance to This Repo

This guide exists because the `Anywhere` implementation plans ([[Style Guide]] applies to all of `docs/superpowers/plans/2026-09-05-anywhere-phase*.md`) drifted in exactly the ways this document rules out; inconsistent naming for the same concept across sibling files being the main one. Apply it going forward rather than retrofitting the plan documents themselves, since plan prose isn't compiled code; enforce it in actual `.cs` files as they're written.
