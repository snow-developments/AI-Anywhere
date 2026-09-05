---
name: uspto-wordmark-search
description: Use when checking whether a proposed product/project name is available as a US wordmark trademark, before naming a product, package, or company, especially in Class 009 (software/hardware).
---

# USPTO Wordmark Search

## Overview

Checks a candidate name against existing US trademark registrations/applications using USPTO's public Trademark Search system (tmsearch.uspto.gov, successor to TESS), scoped to a specific Nice Classification class — most often Class 009 (computer software, hardware, downloadable apps).

This is a manual web-UI workflow, not a live API integration (no stable public API endpoint has been confirmed as of this writing — see Future Enhancement below).

## When to Use

- Before committing to a product, app, or company name
- Specifically relevant for software/apps: Class 009 covers "computer software" and related goods
- NOT a substitute for legal advice — this surfaces conflicts for you to evaluate, it doesn't clear a mark

## How to Search

1. Open `https://tmsearch.uspto.gov/search/search-basic` (Basic Search).
2. Enter the candidate wordmark in the search field (e.g. `Anywhere`).
3. In the filters panel, filter by **International Class** = `009`.
4. Review results:
   - **Live/registered marks** with an exact or confusingly similar wordmark in class 009 → high conflict risk.
   - **Dead/abandoned/cancelled marks** → lower risk, but note them in case of common-law rights.
   - Pay attention to marks that are phonetically or visually similar, not just exact string matches (e.g. "Anywhere" vs "AnywareApp").
5. For a promising candidate, open the individual record to check: status, owner, goods/services description, and filing date.

## Reporting Findings

Summarize as: candidate name → list of conflicting/similar live marks in class 009 (if any) → a plain risk read (clear / some risk / likely conflict). Always recommend the user consult a trademark attorney before filing or committing to a name — this search is informational only.

## Future Enhancement

No confirmed public JSON API for tmsearch.uspto.gov was found during initial research (only third-party paid wrappers exist, e.g. RapidAPI/parse.bot listings — these are NOT the official USPTO system and shouldn't be treated as authoritative). A future revision of this skill could automate the search via direct network inspection of the tmsearch.uspto.gov frontend (browser devtools) to find its real backing endpoint, if one exists.
