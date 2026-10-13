---
id: T-0847
title: Give the invoices endpoint a default order and a tie-break, name the native invoice sort by the field it means, and stop the decision records calling regeneration owner-only
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0832]
blocks: []
stories: []
adrs: []
layers: [backend, android, ios, docs]
security_touching: false
---

## Context

This comes from open questions 1 to 3 of the 2026-10-12 native design ruling's item on T-0832, and from surprise 2 of the native ground truth. Both are off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/`, files `architect_native-lead.json` and `gt_native.json`. Lines are as of `207d2f319`.

- **The invoices query has no order at all.**
  - With an empty sort, `GetPagedInvoices.cs:58-63` reaches `BaseRepository.cs:88-96`. That code loops over zero sort definitions, so the query has no `ORDER BY`.
  - Pages therefore come back in database order. Past 50 invoices (the native lists ask for 50), which ones a page shows is arbitrary, and paging can skip or repeat rows.
  - `EmployeeInvoiceSort.DefaultSort` (`:10`, `GeneratedAt`) is used only for an unknown field (`:42`), never for an empty sort.
  - Even when sorted, invoices with the same `GeneratedAt` have no tie-break.
  - Orders, by contrast, fall back to newest created, then `Id` (`SortMapper.cs:22-38`).
  - Until T-0832 lands, both native invoice lists send a sort that binds to nothing, so they get the unordered query.
- **The native sort field has a misleading name.**
  - Android `InvoicesRepository.kt:125` and iOS `PartnerPayrollClient.swift:51` sort by `createdOn`, which is not an `EmployeeInvoiceSort` field. It falls back to `GeneratedAt` (`:42`).
  - The order comes out the same, but the name says otherwise.
- **Two decision records still call regeneration owner-only.**
  - Client regeneration and the mobile re-dump are ordinary work (CLAUDE.md, owner ruling 2026-09-07).
  - `agents/architecture/decisions/generated-client-contract.md:95-96`, `:128-130` and `:557` still call them owner-only. The ruling cited these as `:38-39`, `:71-73` and `:500`; the W01 note added in `207d2f319` moved them.
  - `agents/architecture/decisions/ios-app-architecture.md:928` says the same.
  - The first page has five more mentions, in its round records (`:318`, `:445`, `:492`, `:550`, `:597`).
  - T-0832 corrects the same claim in `mobile-wire-contract.md` and `docs/mobile-app/api-integration.md`.

**If not built:** a cleaner with more than 50 invoices may see an arbitrary page of them. Nothing else breaks.

## Doing — approved 2026-10-13

1. **A default order and a tie-break.** An empty invoice sort orders the newest generated first, and every invoice sort ends with `Id` as a tie-break, the same shape as the orders page. A paged read with no sort is stable across pages; the test is RED today.
2. **The native sort field.** Both native call sites ask for `generatedAt` instead of `createdOn`, through the parameters T-0832 generates. T-0832's wire tests follow.
3. **The docs.** The four lines say regeneration is ordinary work, run and reported. The round records stay as dated history.

## NOT doing

- T-0832's own re-dump and docs, and the web clients' sort.

## Acceptance criteria (done looks like)

- [ ] **AC1** — `GetPagedInvoices` with no sort returns the newest generated first, with a stable tie-break. The test goes RED then GREEN.
- [ ] **AC2** — No native call site sorts invoices by `createdOn`.
- [ ] **AC3** — Outside dated records, the decision records no longer call regeneration or the re-dump owner-only, and the full suites pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

The default GeneratedAt descending order belongs before WithinCurrencyWhenSortedBy; the Id tie-break belongs after it, with an explicit Id resolver and local invariant normalization. No one-caller SortMapper helper. The approved regeneration wording includes the eleven outside lines (including .claude/commands/sync.md) and the current-facing generated-client passages at baseline lines 550 and 596–597; genuinely dated rounds remain history. The two native generatedAt literals land in PR 3; AC2 remains open until then.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the native design ruling's open questions on T-0832. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
