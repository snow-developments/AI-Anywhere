# Constraints (from the spec/plan — do not violate silently)

Inviolable project rules. Each one has a source — a spec line, a plan section,
a review finding — but the rule itself stands without the source. Violating
one of these silently is a bug.

- Dependency direction `Anywhere` → `Anywhere.Controls` → `Anywhere.Design`, and
  `Anywhere` → `Anywhere.Models`, is one-way. `Anywhere.Design` and
  `Anywhere.Models` must stay UI-framework-agnostic, and neither is ever
  referenced by `Anywhere.Controls`.
- ACP transport goes through the `acp-csharp` NuGet package (nuskey8/acp-csharp)
  — do not hand-roll JSON-RPC framing. Contribute fixes upstream rather than
  forking.
- Markdown rendering is `MarkdownLabel` (Markdig +
  Vortice/Direct2D+DirectWrite), adapted from an external source file, not a
  WebView2-based renderer.
- Persistence goes through EF Core (`Microsoft.EntityFrameworkCore.Sqlite`)
  against `AnywhereDbContext` in `Anywhere.Models` — not raw
  `Microsoft.Data.Sqlite`/ADO.NET. Schema changes go through
  `dotnet ef migrations add` (never hand-edited `CREATE TABLE`), applied via
  `Database.Migrate()` at startup.
- No repository interfaces (`IProfileRepository`, etc.) — concrete classes over
  `AnywhereDbContext` only. See
  [Architecture → Design Decisions → No repository interfaces](Architecture.md#no-repository-interfaces)
  for the rationale before adding one.
- Visual styling: `WinForms.Fluent.UI` (an "add new controls" library referenced
  only by `Anywhere.Controls`), not a full app-wide skin override. The
  superseded single-file plan's Task 10 had drifted to `MaterialSkin.2` instead,
  contradicting the spec — this was fixed when the plan was split into phases;
  Phase 4's Task 10 uses `WinForms.Fluent.UI`.
- No permission-request timeouts — the permission/diff panel waits indefinitely,
  matching editor behavior (ACP itself has no timeout).
- v1 scope excludes: agent-specific presets, multiple concurrent session tabs,
  slash-command UI, MCP server config UI, attachments, plan-mode UI, WinForms UI
  automation. See the spec's "Out of scope" section before adding any of these.

## Cross-references

- The agent-rule version of these (where the constraint is enforced as an
  inviolable agent rule, not just documentation): `AGENTS.md` → Constraints.
- Project structure that several of these constraints flow from:
  [`Architecture.md`](Architecture.md).
- Each phase plan carries a "Global Constraints" section that re-states the
  subset binding to that phase; treat those as plan-time copies rather than
  authoritative sources — this file and `AGENTS.md` are the source of truth.
