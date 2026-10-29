---
Anthropic_Feedback_ID: a7f5e69b-bc8f-429e-9aab-2967f665fa41
---
# 0004 — Questionnaire Epic PRD: Four Rule Violations Polishing One Document

Date: 2026-10-29
Area: `.agents/plans/questionnaire/epic.md` (via `breakdown-epic-pm`)

## What broke

One `/breakdown-epic-pm` run amending the Interactive Questionnaire MCP epic.
Every failure below was surfaced by the user, not by the agent's own checks.

### 1. Stopped grilling with two open questions still open

The skill's own instruction said to grill the user "relentlessly" until the
plan was settled. The agent ran two rounds of `AskUserQuestion` (15
questions), then reported the epic as amended with two questions still listed
as open. The user: "Rule violation! You ought have grilled me, relentlessly,
until _all_ open questions were resolved and folded back into the plan!" A
third round closed the remaining two.

### 2. Numbered every top-level heading

`breakdown-epic-pm`'s own PRD Structure template numbered every section
(`#### 1. Epic Name`, `#### 2. Goal`, …), and the agent copied that numbering
into the generated document. The user: "Why is each high-level section
numbered? How stupid!" Fixed both the skill's template and the generated
document.

### 3. Subsection headings left in sentence case

Across the edits for (1) and (2), several subsection headings (`Agent author
or integrator`, `End user`, `Journey A: Labeled yes/no decision`) were
touched or added without checking them against the standing Title Case rule
(`title-case-headings.md`). The user flagged it as a separate violation after
the numbering fix, rather than it being caught in the same pass.

### 4. Bolted a "Decisions Captured" log and an "Open Questions" tombstone onto the document

Instead of folding each resolved decision into its matching requirement,
journey, or NFR, the agent appended a running numbered log of every grilled
decision plus an empty "Open Questions" section once nothing remained open.
This is exactly the pattern the design-docs rule already forbids (a doc
records current state, not a run-log of how it got there). The user: "Rule
violation! ... 'Decisions Captured' section is bullshit. Also, total bullshit
empty open questions section. You're supposed to fold everything into the
document!" Two decisions (the server-to-UI bridge choice, the stdio-only
security boundary) existed *only* in that log and had to be added to the real
body before the log could be deleted.

## Lesson

A skill's stated stop condition ("grill until resolved," "fold everything in")
is the actual stop condition, not a point that looks close enough. Heading
case and numbering are part of finishing a document, checked in the same pass
as the content edit, not after a separate complaint. A generated document is
never its own changelog: every fact belongs in the section that already
covers it, with nothing left over to log.

## Guardrail

- `skill-checklist-discipline.md` and its `.csv`: added the "grill until
  resolved" incidence.
- `title-case-headings.md` and its `.csv`: added the "heading touched during
  an unrelated edit pass" incidence.
- `breakdown-epic-pm/SKILL.md`: PRD Structure headings no longer numbered.
