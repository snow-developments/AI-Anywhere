# TODO

## Application

- [ ] Ensure `Colors.Background` is the default background color in all `Form`s
      and that it is used as the BG color for form frames, i.e. forms with
      visible captions. Always use the app's current theme. For context, this
      to-do task is referring to caption background colors in form non-client
      areas.
- [ ] Make light/dark theme user-configurable (force Light/Dark over the
      OS-detected default). Plan:
      `docs/superpowers/plans/2026-09-05-theme-override.md`.
- [ ] Bug: Markdown hyperlinks are not rendered in chat bubbles. Inline links
      (`[text](url)`) should render as blue, clickable text with the usual
      link affordances (pointer cursor, hover state, opens in the default
      browser) in `MarkdownLabel` / `ChatTranscriptPanel`.

## Branding

- [ ] Run a USPTO wordmark search for "Anywhere" in Class 009 (computer
      software/hardware) using the `.agents/skills/uspto-wordmark-search` skill,
      to confirm trademark availability — the project is already named
      `Anywhere` in the spec/plan pending this check.

  See
  [github.com/api-evangelist/uspto-trademark-search-api](https://github.com/api-evangelist/uspto-trademark-search-api)

## Future Versions

- [ ] v3: add a macOS client. `Anywhere.Design` (the framework-agnostic
      design-token library split out in v1) is meant to be reused there rather
      than rebuilt.
