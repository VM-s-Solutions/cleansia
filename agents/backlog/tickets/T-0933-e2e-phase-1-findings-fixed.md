---
id: T-0933
title: "The E2E phase 1 findings fixed: the multi-service job card, three app and API defects, On my way on the partner web, and the docs"
size: M
owner: Michael Chaban
created: 2026-11-15
updated: 2026-11-15
depends_on: [T-0932]
blocks: [T-0934]
stories: []
adrs: []
layers: [ios, android, frontend, backend, docs, qa]
security_touching: false
---

## Context

Mike, 15 November 2026, on the T-0932 report (`e2e/runs/2026-11-15/findings.md`): fix all of the findings, then run
phase 2 of the tests. His rulings: F-066 adds *On my way* to the partner web; one DEV deploy after this merges.

## Doing

1. F-035 (P2): both partner apps name every package and service of a job on its detail, and the board card adds
   *+N more*. The partner web already lists them all.
2. F-022: the Android customer app skips the order map when the Mapbox token is blank instead of crashing.
3. F-046: the Android partner dashboard shows no trend when last month had no job, as the web does.
4. F-068: `CancelInvoice` answers the shared `common.max_length` for a reason over 1000 characters, not a dispute key.
5. F-066: the partner web job page offers *On my way*, as the apps do and the docs say.
6. The docs: the 17 docs faults (F-036, F-048 to F-052, F-055, F-058, F-061 to F-063, F-065, F-067, F-069 to F-071,
   F-073), and F-028, F-053 and F-074 written as the code does them (the web hides *Complete* until an after photo,
   as the apps do; a repeat grant of the same document is refused; *Ready to book* reads what the form holds), with
   the `e2e/cases` rows that cite a changed rule.

## Not doing

- The by-design and environment rows: there is nothing to fix.
- F-020 (accessibility ids), F-008, F-009 and F-014 (harness warnings), F-018 (Package.resolved).
- Anything on PRO.

## Done

One PR merged with CI green; every item above is fixed or documented.

## Work record

- 2026-11-15 — Filed and started on `fix/e2e-findings-t0933`.
- 2026-11-15 — F-022 has no guard test: the gate reads the `BuildConfig.MAPBOX_ACCESS_TOKEN` constant, which a unit test cannot vary, and a blank token occurs only on developer builds (P3).
