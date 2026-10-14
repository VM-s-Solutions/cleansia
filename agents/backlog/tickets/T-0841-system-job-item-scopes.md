---
id: T-0841
title: Give every system job that continues past a failed commit one scope per item, SendRecurringOrderReminders and RedrivePendingRefunds first, and make CancelUnfilledOrders re-read the order it cancels
size: L
owner: pm
created: 2026-10-12
updated: 2026-10-14
depends_on: [T-0856, T-0857, T-0858, T-0859, T-0860, T-0861, T-0862, T-0863, T-0864, T-0865, T-0866, T-0867]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

**The rule.** T-0844's job-units decision record is ADR-0072, D2. It states the rule:
- a job whose items must fail independently gives each item its own scope;
- a loop never continues past a failed commit, or past staging that a later commit in the same scope will save, with or without `Rollback()`.

**Why it matters.** A failed `CommitAsync` leaves the tracked entries as they were (`CleansiaDbContext.cs:84-120`). So a failed item's leftovers ride the next item's commit, and a fault that repeats fails every later commit in the run (BE-13).

**Where this ticket comes from.**
- T-0844 fixes the recurring auto-cancel and lists the remaining cases in a deviating-forms entry in `consistency.md`. This ticket retires that entry.
- The 2026-10-12 design ruling on BE-11 to BE-14 reported these as items 3 and 4, and recommended filing this ticket. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_be-lead.json`.

**The deviations** were read from the tree by the ruling, with a search for `catch (Exception` and `Rollback()` under `src/Cleansia.Core.AppServices`. Lines are as of `207d2f319`.
- **A commit inside the try, or `Rollback()` and continue:**
  - `SendRecurringOrderReminders.cs:62-92`;
  - `RedrivePendingRefunds.cs:120-218`, with the catch at `:208-218`. This job moves money;
  - `FiscalRetryService.cs:40-112`;
  - `PayPeriodBackgroundService.cs:255-279`, with `:455-476`;
  - `SendMembershipLifecycleNotifications.cs:67-89` and `:105-127`;
  - `NewJobsDigestService.cs:110-244`, which commits at `:376`;
  - `DataRetentionBackgroundService.cs:93-105`.
- **A catch over staging only, then a group commit.** These are milder:
  - `SendPreCleaningReminders.cs:87-112`;
  - `NotifyLapsedPreferredOffers.cs:88-102`;
  - `SendCleanerJobReminders.cs:175-201`;
  - `SendTomorrowJobDigest.cs:138-153`;
  - `PayPeriodBackgroundService.cs:156-172`.

**SendRecurringOrderReminders goes first.** Once T-0844's `PaymentStatus` token lands, a payment that arrives during this job's run fails every later reminder in that run. This is rare: the run takes seconds, at 02:30 UTC.

**CancelUnfilledOrders' re-check sees nothing new.**
- Its comment (`CancelUnfilledOrders.cs:131-133`) says the in-memory re-check catches a seat filled after the read. It cannot, because tracked entities are never refreshed.
- The `CurrentStatus` token catches only a take that moves New to Confirmed. A take onto a Confirmed order with no crew appends no status track (`TakeOrder.cs:406-412`), so nothing catches it.
- So the sweep can cancel a job a cleaner has just taken. This is rare, because a Confirmed order with no crew is itself rare (ADR-0067).

**If not built:** in each listed job, a failing item holds back every later item in the same run until the next run. For `RedrivePendingRefunds`, the next run is an hour later.

## Doing — approved split, 2026-10-13

This is the aggregate evidence record for twelve children, not one L implementation job.

- T-0856: recurring reminder item scopes; PR 2 with T-0840.
- T-0857: refund re-drive item scopes; remaining-jobs PR after T-0851/M10.
- T-0858: unfilled-order fresh re-read; after T-0854.
- T-0859: membership notification fail-tick boundary.
- T-0860: new-jobs digest cleaner scopes.
- T-0861: fiscal retry receipt scopes.
- T-0862: employee invoicing plus period-close staging.
- T-0863: retention company/task scopes and child archive gate.
- T-0864: pre-cleaning reminder fail-tick boundary.
- T-0865: await lapsed-offer notice before stamp and fail unsafe staging.
- T-0866: cleaner reminder fail-tick boundary.
- T-0867: tomorrow digest fail-tick boundary.

Each child owns its observable failure boundary and evidence. The remaining-jobs PR is explicitly allowed. Prefer no catch where the existing window supports retry; the four notification staging sites and payroll close get no new types. A scoped unit resolves all dependencies afresh, re-reads its row, and takes tenancy from that row. Retention opens its archive gate in each child. CancelUnfilledOrders re-reads status/crew immediately before cancellation and retains the final read-to-claim race residual. Product windows, attempt caps, stale-checkout company commits and refund rules remain unchanged. Do not claim a next-tick guarantee at a finite window edge. Fiscal e-mail reconciliation is separately reported, outside this scope.

Remove only each verified job’s deviating-forms row; the final child reconciles the aggregate entry and ADR-0072’s temporary statements. Full combined suites and independent reviews are final-head obligations.

## NOT doing

- The stale-checkout sweep keeps one commit per company. It is the reference shape, and its two-operator tests pin it.
- No attempt caps. The job-units decision record accepts their absence.
- The reminder sweep's window and status term (T-0840).

## Acceptance criteria (done looks like)

- [x] **AC1** — Each listed job’s child proves its chosen independent-scope or fail-tick boundary RED then GREEN. A fail-tick shape need not finish later unsaved items in that tick; its retry eligibility and finite-window limitations are recorded.
- [x] **AC2** — T-0858’s PostgreSQL two-connection test proves the final fresh read skips cancellation after a take committed before that read. The read-to-claim residual remains explicit; no serializability claim.
- [x] **AC3** — All twelve child results are linked, the verified deviations and temporary records match the final tree, and the full combined suites and reviews pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The approved split replaces the earlier aggregate proposal. T-0844’s foundations shipped in #327; its remaining AC2/final DEV evidence is not supplied by this filing. The corrected source reports are `p2/correctness-verifier-before.md` and `remaining-jobs/verifier-before.md` under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. No acceptance result is claimed here.

## Work record

- 2026-10-12 — Filed from the BE-11 to BE-14 design ruling's reported items 3 and 4, and its recommendation under BE-13. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work, and when.

- 2026-10-13 — First child T-0856 is implemented locally with T-0840: ordered reminder IDs, a new scope/command for each unit, fresh tenant/status/window/stamp/account reread, failed count and host-cancellation propagation. Actual isolation/poison-repeat and timing RED→GREEN evidence is in that child's Work record. The remaining eleven children are still future work in the explicitly permitted following PR; RedrivePendingRefunds remains after T-0851. This parent is a cross-reference/aggregate closure record, not an L implementation job. No remaining-job acceptance criterion or final catalog retirement is claimed here.

- 2026-10-13 — All eleven remaining source children now have frozen implementation receipts; T-0857's extra direct dispatch/disposal proof now passes locally and is in fresh review. Scoped staging and money implementation/security verdicts approve only their exact input groups. Digest/payroll/retention implementation review approved the frozen 44-file set (own183unit/80PostgreSQL and five private no-op failures followed by rebuilt green); their separate security verification is underway; final combined gates, record refutation, CI and merge remain pending. This parent has no source implementation and does not add these overlapping focused runs into a distinct-test total. T-0863 is the planned last source child: its exact row and exhausted temporary catalog roster are retired in the proposed source change, with current flow wording and a dated ADR-0072 mechanism note; the later attribution sweep remains in the prose phase.

| Child | Current implementation evidence (separate from publication) |
|---|---|
| [T-0856](./T-0856-recurring-reminder-item-scopes.md) | #330 merged; exact-head CI9/9 |
| [T-0857](./T-0857-refund-redrive-item-scopes.md) | 43 PostgreSQL/119 unit; direct identity/disposal proof 2/2; updated fixture independently approved |
| [T-0858](./T-0858-unfilled-order-fresh-reread.md) | 34 PostgreSQL/63 unit; real mediator take race |
| [T-0859](./T-0859-membership-notification-failure-boundary.md) | 15 PostgreSQL/36 unit; scoped review/security approved |
| [T-0860](./T-0860-new-jobs-digest-item-scopes.md) | 7 PostgreSQL/56 unit; repeated leading refusal and B watermark |
| [T-0861](./T-0861-fiscal-retry-receipt-scopes.md) | 23 PostgreSQL/74 wider unit; scoped review/security approved |
| [T-0862](./T-0862-pay-period-failure-boundaries.md) | 33 PostgreSQL/95 unit; five genuine persisted failures |
| [T-0863](./T-0863-retention-company-task-scopes.md) | 40 PostgreSQL/36 unit; three genuine persisted/effect failures |
| [T-0864](./T-0864-pre-cleaning-reminder-fail-tick.md) | 7 PostgreSQL/54 unit; scoped review/security approved |
| [T-0865](./T-0865-lapsed-preferred-offer-notice-before-stamp.md) | 6 PostgreSQL/31 unit; scoped review/security approved |
| [T-0866](./T-0866-cleaner-job-reminder-fail-tick.md) | 11 PostgreSQL/40 unit; scoped review/security approved |
| [T-0867](./T-0867-tomorrow-job-digest-fail-tick.md) | 10 PostgreSQL/52 unit; scoped review/security approved |

- 2026-10-13 — #330's master push CI subsequently passed **7/7 executed checks** on first attempts; the configured push-only e2e job was skipped, with annotations and the explicit no-log reason retained. Receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/master-ci-evidence-receipt.json` SHA256 `eff33741f95fdabafa2f84fdfbc69a2f7a424b57b13c7c632b8576ea2bd95186` binds the saved polling and evidence summary. This supplements the earlier bounded closure's pending-at-capture statement; it adds no deploy/reset.
- 2026-10-13 — All three scoped implementation groups and their applicable separate security passes now approve the frozen changes. The updated T-0857 fixture has its additional fresh implementation approval and direct security execution. These approvals cover their exact group inputs and the one-file override, not the final cumulative 77-path/full-suite tree. Per-job independently buildable trees, final combined suites, record/head checks, CI and merge remain pending. The authoritative INDEX notes now distinguish completed scoped approvals from these remaining gates.

- 2026-10-13 — Independent final source verification passed all eleven cumulative source prefixes: each separately forced Release Rebuild of Tests and IntegrationTests exited 0 from a clean complete source archive. The final frozen **77-source-path** composition, manifest SHA256 `b230b7b00b7d22d424b865fdc1f9e5505894cf3f124beedf99a1efa3ccba7876`, then passed the unfiltered full CI-order backend suites: **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped. All 69 added integration cases and the six final composed PreferredHold cases ran. These are executed results for the final frozen composition; each partial commit has its separate source-prefix build proof. The raw broader consistency scan exited 1 on six inherited byte-identical B10 test-fixture hits, with no new violation. Actual Rebuild argv does not contain an explicit UseSharedCompilation=false option. Source/DLL/TRX identities, source-exact output cleanup and the eleven prefix rows are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/verifier/final-source-verification.md` and `verifier/prospective-build-results.json`.

Full frontend validation passed **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, all three builds and **5 customer + 1 partner + 1 administrator** browser smokes. The first full-unit attempt exited 1 when Nx could not write its terminal cache because the disk was full; retained attempt 001 is an environment failure. Fresh full retry 002 passed uncached with task-owned cache/workspace-data. Android then passed forced compile followed by all **2,818 cases / 304 XML suites**, zero failures/errors/skips; all 875 tracked Android inputs remain unchanged. Aggregated commands, exits, log hashes and the environmental recovery are in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/local/full-local-gates-before-commit-001.json`; Android's independently measured source/class/XML identities are in `android/gate-summary.json`. No DTO, endpoint, model or generated client changed in this phase, so no regeneration was needed. Actual per-head docs, final affected lint/history secret checks, record closure, exact-head CI and merge remain pending.

## Review

Scoped implementation/security and final source/object/record verification passed. The master failure and remaining assertion repair are recorded below.

- 2026-10-13 — PR [#330](https://github.com/VM-s-Solutions/cleansia/pull/330) merged at2026-10-13T17:48:26Z as `6c396f013d79e7b62958fe3b09e2a503a724008c`. Exact-head `5490b331d7f7e8be549b44b24ff21f98a8c35a2f` passed9/9 checks in8 workflows on first attempts; no rerun. The SHA-guarded PUT merged the tested head, and both trees are `87f7b48db03700f48b27074ba32890144489fe10`. The PR has319 changed files and3 commits: #329 closure first, approved filings, then the combined implementation/records. Saved snapshots, all9 job logs and annotations are under Wave F `raw/ci/nw-p2-pr330-poll-001` and `raw/ci/nw-p2-pr330-5490b331d7f7/evidence`; off-tree closure `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-p2-pr330-ci-merge-closure-001.json` SHA256`bad0ae9478bd9cf0b2efb14946ccb4400387b036c43351a48be8ab4f5ecdcfb1` hashes the CI snapshots, saved annotation/log summary, premerge read, guarded merge and four deployment-run reads. Their bounded GETs found0 deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. This is that observed window, not a claim about all history or future dispatches. No phase reset/deploy was dispatched. Master push CI is tracked separately and remains unclaimed at this closure's capture.
- 2026-10-13 — Final committed-head local checks on5490b331 passed: uncached lint64 projects, docs references/catalog/VitePress and secret-history2446 commits/no leaks (`p2/local/committed-head-gates.json` under the wave root). Earlier filing-head lint/check attempts are nonproof for this source head. The implementation/full-suite/command-only and residual limits above remain; no real-mail/device/DEV proof is added by hosted CI. This closure is prepared as the first commit of the fresh remaining-jobs branch, directly after the merged P2 base.

- 2026-10-13 — T-0856 is merged. The other eleven child changes are now the remaining-jobs branch scope; this parent remains in progress.

Full local source gates PASS for the final frozen composition and all eleven independently buildable prefixes, as recorded above. Final object/record/docs checks and PR publication are complete in #331. Master Backend has the one subsequently observed assertion failure below; it remains a completion gate for T-0860 and the parent. The inherited six B10 test-fixture hits remain unchanged; the broader raw checker did not pass.

- 2026-10-13 — PR [#331](https://github.com/VM-s-Solutions/cleansia/pull/331) merged at `2026-10-13T21:38:51Z` as `26883f31c2cba99881ffe2140ae197e46d438670`. Tested head `4929ed84967d154b8ee3f90a13cf091dde822688` and the merge have tree `7d1e079731705440c78e733b93503e771217747c`. All **6/6 exact-head checks in five workflows** passed on first attempts, with no rerun; the SHA-guarded merge published that head. The PR contains 110 changed files and 13 commits: #330 closure first, eleven separate child implementations, then a forward record-label correction leaving the complete source tree unchanged. Saved annotations/job logs and CI/merge/deployment-metadata reads are bound by `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-remaining-pr331-ci-merge-closure-001.json` SHA256 `3db0c81a36eccd6a5aebda2bede326fe2919117160d0ea2613343b37dbd8f96f`. Its bounded GETs found no deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. No phase reset or deployment was dispatched.

- 2026-10-13 — The eleven actual child commit objects passed **33/33 docs gates** (references, catalog claims and VitePress at each head). The final forward head passed all three docs gates and the history secret check (2,459 scanned commits, no leak); affected lint selected zero projects and adds no positive lint coverage. Independent actual-object verification checked complete archive bytes/modes and the cumulative source identities; final record refutation was CLEAN. Receipts under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/`: `head-gates/actual-commit-docs-gates.json` SHA256 `bae651def190f5daa4460d46aad7524c041d468349f2f4faff061558ed898493`, `local/precision-committed-head-gates-001.json`, and `verifier/actual-commit-gates-verification.json` SHA256 `86ecfca9ead2ad53bc45e515f5768f99170d8a801fb04444ed48875fafd1c65b`.

- 2026-10-13 — Master push attempt 1 at `26883f31c2cba99881ffe2140ae197e46d438670` passed Docs and Secret scan, but Backend failed: **8,731 unit passed; 974/975 PostgreSQL integration passed; host tests did not execute after that failure**. The sole failed test is T-0860's `Every_Child_Uses_One_Clock_While_Notices_Follow_Their_Recipient`: both saved outbox-key tick suffixes were `639275250997934015`, while the reloaded database stamp was `639275250997934010`. The exact diagnostic and all three job logs/annotation responses are retained by `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-ci-evidence-attempt1-receipt-001.json` SHA256 `28ee98b87f224974941c6ab8cd11fecfe28eb7525b10b42f8b5683ac0245a350`. No rerun was dispatched. T-0860 and the parent remain in progress pending a focused assertion repair and green master; no green-master claim or native runtime proof is made. This observed failure supplements the earlier PR/local success record rather than replacing its evidence.

- 2026-10-13 — A focused forward repair addresses this child's newly observed master assertion failure. The test now requires one **exact distinct original tick suffix** across both push messages, then compares its microsecond-normalized value with the one persisted watermark. The per-message suffix, recipient/operator, count and non-null checks remain; production clocks, message keys, handlers, schema and clients are unchanged. Exact source SHA256 `b9d82c4da15b093bdf69adc528333d08e520060f47644607289b6485cc2400bc`. The failure diagnosis was independently confirmed; the failed master job was not rerun.

- 2026-10-13 — Command-only precision evidence uses a distinct exact-image loopback PostgreSQL **16.15** container and typed Npgsql parameters: all ten submicrosecond remainders round-trip under the repaired assertions, whereas the original comparison rejects nine valid round-trips. Independent checks also reject differing raw clocks within one microsecond, shifted stamps, wrong tenants and malformed/noncanonical keys. The macOS pre-fix class passed **2/2**, explicitly a characterization; the saved Linux master failure is the actual RED. The repaired class passed **2/2**, followed by five independent runs with **10/10** cases; the ordinary inherited `postgres:latest` fixture was actually **18.6**, separate from the pinned PG16 probe. The first private probe readiness attempt stopped before any .NET probe ran, and its own container/credentials were cleaned; the corrected TCP readiness attempt passed.

- 2026-10-13 — Exact-source implementation review returned **APPROVED** and separate security verification returned **PASS**, no material finding. Own implementation verification executed two PostgreSQL cases, ten precision cases and fifteen negative controls. Security verification performed 23 controls and recomputed evidence hashes; it inspected the separate executed runtime proof and did not claim its own runtime run. Under `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-failure-001/`: `developer/developer.json` SHA256 `4fd9389c3096510aab26531247f46f5b042f63582835be47179cc0cb6bbc9b72`, `reviewer.json` SHA256 `d98638e485c949f27306722a5de29624ae9b6e0e1109b281d33bf96c19e2af09`, and `security.json` SHA256 `efa15abdca5326e52dd236ccd397340dff60b35b9593b72d703cc5294a0375ba` bind the exact source and immutable evidence.

- 2026-10-13 — The complete repair source passed fresh CI-order local gates: forced Release Rebuild, **8,731 unit → 975 PostgreSQL integration → 509 host**, zero failed/skipped; frontend **5,041 cases / 408 suites / 71 tested projects plus three empty projects**, eight typecheck-guard tests, all three uncached builds and **5 customer + 1 partner + 1 administrator** browser smokes; Android forced compile followed by **2,818 cases / 304 XML suites**, zero failures/errors/skips. Requested compile/test tasks executed; lifecycle-only UP-TO-DATE lines add no coverage. Full receipt `full-local-gates-001.json` SHA256 `68175bc188dba25c562c851589a2485daf858da4378e9e7992f45b33a8eb4830` retains all 17 commands/exits/log hashes and source-before/after equality. Affected lint selected zero projects, so no positive lint coverage is claimed. No new iOS run is asserted for this backend-test-only diff.

- 2026-10-13 — The branch carries #331's truthful closure first at `efe6e765ab7acd76e99fa6037c025e408daeb810`; its records passed fresh refutation and all three docs gates against the exact committed source (`closure-docs-gates-003.json` SHA256 `d153ce372ca00324bb99edf38e3f94b92e1a38c87c881b2934c8631ee924847b`). Two earlier archive-preparation assertions stopped before docs execution: tar permission normalization and gradlew.bat line-ending conversion were corrected by reconstructing exact Git blobs/modes. No migration or client regeneration was needed. Final committed-head docs/history checks, exact-head CI, guarded merge and green master remain due; this repair adds no device, real-mail, DEV or PRO proof.

- 2026-10-13 — Fresh final evidence verification returned **PASS_FOR_ALLOCATED_LOCAL_REPAIR_EVIDENCE**, no material finding. It independently checked 1,152 evidence hashes, 7,525 source/SQL inputs, all actual suite counts and produced-DLL/TRX paths. The 29 produced DLL timestamps lie inside the forced rebuild interval. No root before-DLL snapshot was collected, so unchanged root DLL hashes during those suites are not claimed. Receipt `final-gate-verifier.json` under the same repair folder, SHA256 `c20084aa4c5f8f9825784c0c6c408ef486c23424abba5cb7f6a46a91502d5fb7`. Final committed-head gates and publication remain separate obligations.

- 2026-10-14 — Focused repair PR [#332](https://github.com/VM-s-Solutions/cleansia/pull/332) merged at `2026-10-13T23:08:35Z` as `093af7f89301a9c7838be6b0d11023d401e3b6c8` after all **6/6 exact-head checks in five workflows** passed on first attempts, with no rerun. Tested head `44120a3e0ce1d24403b9b48384881b9ba4c2df7f` and merge share tree `da8e9e7b21904997fdf2f76392b2fafdffbdbd41`. The SHA-guarded merge carries #331's truthful closure first and the one-file digest timestamp assertion repair. Production source, clocks, message keys, schema and clients are unchanged. Saved annotations, six job logs, guarded merge and bounded deployment metadata are bound by `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-clock-proof-pr332-ci-merge-closure-001.json` SHA256 `2d15f0b6fbcb949a0263ba0545d5953d7fdc2af5aed77960752164563cc2c754`. No phase reset or deployment was dispatched.

- 2026-10-14 — New master push CI at `093af7f89301a9c7838be6b0d11023d401e3b6c8` passed **3/3 executed checks in three workflows**, first attempts, no skipped job and no rerun. Retained hosted logs, annotation responses and polling snapshots are bound by `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-failure-001/master-ci-evidence-receipt-001.json` SHA256 `cc8aec520dd76c0ae8bc6b34d848e458b4c37daa4e43ee2f97d25fb723974975`. The original #331 master attempt at `26883f31c2cba99881ffe2140ae197e46d438670` remains a recorded failure and was not rerun; this new revision supplies the green completion gate. No iOS, device, real-mail or DEV proof is added by the repair.

- 2026-10-14 — Final repair head passed references, catalog claims, VitePress, affected lint and the history secret scan; affected lint selected zero projects and adds no positive lint coverage. Exact head gate receipt `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/remaining-jobs/master-failure-001/actual-head-gates-002.json` SHA256 `b99681905915c0d9b131f4f46b355d2860bd4fdce0243c2bc3a659a8bbc64372`. Final record refutation was CLEAN103/103. Existing local suite, PostgreSQL-version, precision-probe and inherited-checker qualifications above remain. This closure rides first on the native branch.
