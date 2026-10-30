# War Stories

Post-mortems of real failures on this repo — each one exists because an agent
(or a human) burned time on something avoidable. Read the index before doing
work in the same area.

Format per file (`NNNN-short-slug.md`): what broke, the root cause, what it
cost, the lesson, and the concrete guardrail that came out of it (a rule added
to `AGENTS.md` / a guidance doc / a skill). Keep them factual and short — this
is not a diary.

An `Anthropic_Feedback_ID` frontmatter field is added only when the user
actually submitted that feedback to Anthropic and gave you its ID. Never
invent one, copy one from another file, or reuse this session's own
transcript/session ID: those are not feedback IDs, and writing one in means
claiming a submission that never happened. Most files have no such field.

## Index

- [0001 — Custom theming rewrite, then a from-memory diagnostic ID](0001-theme-rewrite-and-wfo5001.md)
- [0002 — Chat bubbles: invisible text, then ghost rectangles](0002-chat-bubble-render-bugs.md)
- [0003 — Fluent styling: two dead libraries, a duplicate-row flood, a chatty teardown](0003-fluent-library-thrash-and-profile-dupes.md)
- [0004 — Questionnaire epic PRD: four rule violations polishing one document](0004-questionnaire-epic-prd-polish-misses.md)
- [0005 — Questionnaire arch/epic: an unverified claim, then two shallow writing passes](0005-questionnaire-arch-unverified-claim-and-shallow-writing-pass.md)
