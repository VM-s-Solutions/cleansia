---
id: T-0709
title: docs/ still describes ExchangeRate conversion as the live multicurrency mechanism
status: todo
size: S
owner: —
created: 2026-09-10
updated: 2026-09-10
depends_on: []
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

**The architecture pages describe a column that no longer exists and a conversion that no longer happens.**

`docs/architecture/platform-expandability.md` describes `Currency.ExchangeRate` as present and the
conversion path as real, in about a dozen places. `docs/architecture/database.md` documents
`Service.BasePrice` / `PerRoomPrice` as columns. `docs/admin-app/user-management.md` describes the pay
multiplier as applying to those columns.

All three are living docs and get rewritten. ADR-0009 and ADR-0056 also cite the old shape, but
accepted ADRs are immutable — they get dated correction banners, in the shape T-0695 established.

## Acceptance criteria

1. The three living pages describe per-currency price rows and the fact that nothing converts.
2. ADR-0009 and ADR-0056 carry dated correction banners rather than edits.
3. `check-catalog-claims.mjs` and `check-docs-refs.mjs` stay green.

## Notes

Per the working agreement, docs are written at the END of a feature. This is that step for the multicurrency programme.

Split out of **T-0688** by the multicurrency readiness audit of 2026-09-10. Every claim here was
re-verified against the tree that day, and the ones that did not survive verification were not filed.
