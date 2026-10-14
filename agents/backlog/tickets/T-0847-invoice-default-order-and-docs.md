---
id: T-0847
title: Give the invoices endpoint a default order and a tie-break, name the native invoice sort by the field it means, and stop the decision records calling regeneration owner-only
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-14
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

- [x] **AC1** — `GetPagedInvoices` with no sort returns the newest generated first, with a stable tie-break. The test goes RED then GREEN.
- [x] **AC2** — No native call site sorts invoices by `createdOn`.
- [x] **AC3** — Outside dated records, the decision records no longer call regeneration or the re-dump owner-only, and the full suites pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

The default GeneratedAt descending order belongs before WithinCurrencyWhenSortedBy; the Id tie-break belongs after it, with an explicit Id resolver and local invariant normalization. No one-caller SortMapper helper. The approved regeneration wording includes the eleven outside lines (including .claude/commands/sync.md) and the current-facing generated-client passages at baseline lines 550 and 596–597; genuinely dated rounds remain history. The two native generatedAt literals land in PR 3; AC2 remains open until then.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the native design ruling's open questions on T-0832. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

- 2026-10-13 — Backend half implemented inside GetPagedInvoices: materialize the supplied sort; locally normalize explicit Id invariantly, default to GeneratedAt descending only when empty before WithinCurrencyWhenSortedBy, and append Id descending afterward unless already explicit. EmployeeInvoiceSort resolves Id ordinal-ignore-case. Currency remains first for unfiltered amount sorting. No SortMapper helper or web sort change.
- 2026-10-13 — PostgreSQL valid RED18 executed:11 new invoice assertions failed,7 existing/seed controls passed; fresh Release rebuild GREEN27/27 (14 invoice,4 seed,8 AppLogin,1 development-first-boot), zero skipped (`p2/data/t0847-valid-red-and-t0842-clean-model.json`, `t0847-green-and-t0842-fixtures.json`, associated TRXs/fingerprints). Seed order d,a,c,b differs from desired timestamp/id order. Tests cover null/empty default, explicit generatedAt directions with paging/ties, Turkish id/Id/ID and equal amounts within currency. The earlier missing-FK fixture/build/disk failures are retained as nonproof, not behavioral RED.
- 2026-10-13 — Approved eleven outside regeneration passages and linked current guidance now say ordinary work, run and reported: sync command, backend/mobile catalogs, mobile overview, ticket template, iOS handover/openapi README/generator, generated-client-contract and iOS architecture. Genuinely dated rounds remain historical. Shell syntax/diff checks pass; sorting changes require no DTO/spec/client regeneration. The iOS helper was executed only to reproduce the local CI workspace from committed specs; its outputs are ignored. Full backend8722→906→509 all pass. Two native generatedAt literals and AC2 remain PR3 work; this ticket remains in progress until that evidence/closure.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

### Native follow-up — 2026-10-14

- Authority, dated 2026-10-13: Mike, "Analyze and Fix those" and "Go with defaults (recommended)"; `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. This is the two native `generatedAt` literals in PR 3. All #330 backend/default/tie-break/regeneration-wording history remains intact.
- Evidence root for this dated follow-up: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/`. Android evidence ids resolve through `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/records-android-preparation-002/evidence-hash-manifest.json` (SHA256 `6b433bd7c839ac2a2e12a777a6070914ac23f785c746ec8a3ee9e467e12eb21f`), whose `evidence_ids` maps each Android id below to its exact path and SHA256. The preparation draft is retained unchanged; the actual assessment verdicts are quoted below. Base `31c10f5caac57ed17dfde0b6e07a8007bb9c310f` contains the preceding phase's first closure commit. These native source overlays are local, with commit, exact-head hosted CI and guarded merge still pending.
- Android `InvoicesRepository.kt:124` and iOS `PartnerPayrollClient.swift:51` now send `generatedAt` descending through existing generated scalar parameters. Current employee filter, offset 0, limit 50 and response `createdOn` fields are unchanged. No endpoint/DTO/spec/client contract change or tracked regeneration is needed.
- Android `InvoicesWireTest.theListRequestAsksForTheNewestInvoicesFirst` fails against compiled baseline with `expected:<[generatedAt]> but was:<[createdOn]>`; final and independently executed socket-request suite pass 32/32 (`AP-COMMANDS`, `AP-REVIEW`). iOS `InvoicesWireTests.testTheInvoiceListSortTravelsInTheFormTheServerBinds` is in the retained original baseline RED matrix, showing `Sort[0].Field=createdOn`, and passes with the same method id on both final floors. These are actual encoded requests; no invoice device acceptance is claimed or added.
- `A-COMBINED` is the actual combined Android receipt, SHA256 `79723d63889bad7b0b7b7aaf83cc7c79a2f8e07191486229aa5e1b8a07f9ecb8`: forced compile 56/56 tasks, full Core 298 → Partner 881 → Customer 1694, **2873 passed, zero failed/error/skipped**, and 96/96 test tasks executed. Both Debug APK builds executed 69/69 tasks each; partner release R8 executed 55/55. All five commands exited 0. The 41 Android overlays match frozen manifest SHA256 `c6066079399fbff4f9a620f40d785ef7bbe5a6c4a2236d249c4339bd2fa9b2b9`; these are measured combined results, not an expected total or a device result.
- `ios/developer-final-packet-001.json` (SHA256 `470115f5e483f4af7f835d3b66221bbae638315771d4296f791eab51b448c031`) binds all 27 iOS overlays to `ios/developer-final-source-001-source-manifest.json` (SHA256 `1cc89799ea912d3d650ce8fc33c2a2a96aca8b308d813d261c6610ece2ba09cb`). On **each** of 16.4 and 18.6: Core 776 passed; Partner 1133 passed; Customer 1780 reported, 1779 passed and one unchanged VoiceOver guard skipped; no failures. Focused Partner 133/133 passed on each floor. All nine final attempts are retained, including the second Customer 16.4 run: the accessibility preference was already 1, reapplying 1 still left `UIAccessibility.isVoiceOverRunning` false, and readback restored the same 1. There is no spoken VoiceOver proof. The actual Dashboard source split is 24 main-file methods plus 10 barrier-file methods, all 34 passing on both floors; the older 25+9 preparation split is stale. The final RED/GREEN matrix has 23 entries with the same method ids passing on both floors. Its 19 original failed methods include an invalid assumed default-phone-grouping assertion; the epoch attempt's three failures include the still-unlabelled Menu and a wrong radius fixture expectation, both explicitly qualified. Harness errors are not relabelled as behavior RED.
- AC2's two native literals are locally complete. Prior backend/docs AC1/AC3 and #330 approval remain history. Final native iOS approval is sealed below; current doc/parity/symbol gates and commit/CI/merge remain pending; no phase deploy/reset occurs.

- Final combined-source cross-stack gates PASS is recorded centrally in T-0839's dated native Work follow-up (`cross-stack-001/final.json` and `count-receipt-001.json`); final docs/parity/symbol and publication obligations remain separate.


### Native publication closure — 2026-10-14

- 2026-10-14 — Native follow-up shipped in [#333](https://github.com/VM-s-Solutions/cleansia/pull/333). Exact-head CI on `e6039a9acca40e22047a2bf4ba56a4c76a1abf63` passed all 8 check runs across 7 triggered workflows on their first attempt, with no rerun. Merged with the head SHA guard as `a667f62b1c7157c5ca0104837dbe8e6c4dc45045` at `2026-10-14T12:55:03Z`. Exact merge-head master CI on `a667f62b1c7157c5ca0104837dbe8e6c4dc45045` passed all 6 check runs across 6 triggered workflows on their first attempt, with no skipped job or rerun. Evidence: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-native-pr333-ci-merge-closure-001.json` (SHA256 `ca11540ebb8c221b5b6abe1af2160e107afd801ce360adba66c7275a0692e179`) and `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/master-ci-evidence-receipt-001.json` (SHA256 `1005f55da28ab299d259aea4772f2253ee42eff213314291e8d215ce4c9abde8`).
- The two native generatedAt literals and their wire proofs complete AC2. The historical #330 backend/default/tie-break/wording closure remains. No tracked DTO/spec/client regeneration was required for this native follow-up.
- The source/full-suite and actual committed-head gate binding is recorded in T-0839’s native publication closure below its Work record.
- The captured deployment metadata queries beginning at the merge timestamp reported zero runs for deploy-dev, deploy-pro, deploy-docs and execute-sql. The authorized wave-end DEV reset, fresh deployment and docs deployment remain due. Earlier dated pending statements record their capture time.

## Review

2026-10-13 — Backend/docs implementation verdict **approve-with-notes**. Command-only provider checks pass; final combined gates/hosted CI remain due, and AC2 belongs to PR3. Evidence: `p2/reviewer-round1.md`, `p2/verifier-round1.md` under the wave root.

- 2026-10-13 — PR [#330](https://github.com/VM-s-Solutions/cleansia/pull/330) merged at2026-10-13T17:48:26Z as `6c396f013d79e7b62958fe3b09e2a503a724008c`. Exact-head `5490b331d7f7e8be549b44b24ff21f98a8c35a2f` passed9/9 checks in8 workflows on first attempts; no rerun. The SHA-guarded PUT merged the tested head, and both trees are `87f7b48db03700f48b27074ba32890144489fe10`. The PR has319 changed files and3 commits: #329 closure first, approved filings, then the combined implementation/records. Saved snapshots, all9 job logs and annotations are under Wave F `raw/ci/nw-p2-pr330-poll-001` and `raw/ci/nw-p2-pr330-5490b331d7f7/evidence`; off-tree closure `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-p2-pr330-ci-merge-closure-001.json` SHA256`bad0ae9478bd9cf0b2efb14946ccb4400387b036c43351a48be8ab4f5ecdcfb1` hashes the CI snapshots, saved annotation/log summary, premerge read, guarded merge and four deployment-run reads. Their bounded GETs found0 deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. This is that observed window, not a claim about all history or future dispatches. No phase reset/deploy was dispatched. Master push CI is tracked separately and remains unclaimed at this closure's capture.
- 2026-10-13 — Final committed-head local checks on5490b331 passed: uncached lint64 projects, docs references/catalog/VitePress and secret-history2446 commits/no leaks (`p2/local/committed-head-gates.json` under the wave root). Earlier filing-head lint/check attempts are nonproof for this source head. The implementation/full-suite/command-only and residual limits above remain; no real-mail/device/DEV proof is added by hosted CI. This closure is prepared as the first commit of the fresh remaining-jobs branch, directly after the merged P2 base.

- 2026-10-13 — Backend order/regeneration wording is merged; both native generatedAt literals/AC2 remain PR3 work.

### Native follow-up assessment — 2026-10-14

- Android saved `AP-REVIEW` actual verdict **APPROVED** within frozen source/command scope, including invoice wire 32/32. iOS prior applicable `APPROVED_SCOPED_LOGIC` input rebind 29 PASS/0 failed retains the logic/literal assessment; final native iOS approval is sealed below. #330's backend/docs verdict is unchanged. No device/provider result is inferred from these wire assertions.
- Final independent `ios/final-review-001/verdict.json` (SHA256 `0b26a7a469e2478c76ffdcd4665f7d6ab593225ec63e12abbbe4713d4b0f2769`): **APPROVED_SCOPED_NATIVE_IOS27**. Allocated native source/SDK/runtime scope and the own 18/18 copied-product SDK run (no own compile) are qualified in T-0839's dated Work follow-up. This does not close records, hosted CI, merge/deploy or whole S11.
