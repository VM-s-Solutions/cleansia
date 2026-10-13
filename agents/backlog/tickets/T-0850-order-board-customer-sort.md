---
id: T-0850
title: Stop a cleaner sorting the order board by customer e-mail or name, which tells them which open offer belongs to a customer they know
size: XS
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: true
---

## Context

Found on 2026-10-12 by the security review of the native follow-ups (T-0832, T-0835, T-0836). It is low and pre-existing; that branch's apps never send these fields. The review is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/impl/security_native.json`.

- **The two fields are protected everywhere but the sort.**
  - As filters, `CustomerName` and `CustomerEmail` reach the query only for an administrator (`GetPagedOrders.cs:96-97`).
  - On the board, a browsing cleaner sees both blanked (`OrderPiiRedaction.cs:16-25`).
  - But `OrderSort` accepts both as sort keys (`OrderSort.cs:22-29`), and `GetPagedOrders` applies the request's sort for every caller (`:120-121`).
- **How a cleaner uses it.**
  - They send `GET /api/Order/GetPaged?Filter.CleaningDateFrom=2020-01-01&Sort[0].Field=customerEmail&Sort[0].Direction=0`, then the same request with `Direction=1`.
  - Their own past jobs come back with the customer's e-mail visible, and open offers come back blanked.
  - Ties are broken by `Id` (`SortMapper.cs:14`, `BaseRepository.cs:88-94`), so equal e-mails keep their relative order in both calls, while different ones swap places.
  - So an open offer that never swaps places with a known customer's job belongs to that customer.
- **Who can do it:** any signed-in cleaner, from a hand-written request.

## Doing — approved 2026-10-13

- For a non-admin open-board read (`!isAdmin && employeeIdFilter is null`), drop every `CustomerName`/`CustomerEmail` sort entry before ForOrderPage, using resolver-equivalent normalization. Preserve other safe entries; if none remain, the normal default applies. Administrator and cleaner own-order sorting stay unchanged.
- A test proves it: a cleaner's two opposite customer-e-mail sorts return the same order, and an administrator's do not.

## NOT doing

- No change to the redaction or to the filters.
- No change to the native apps, which never send these fields.

## Acceptance criteria (done looks like)

- [x] **AC1** — A cleaner's customer-name or customer-e-mail sort no longer changes the board's order. The test goes RED, then GREEN.
- [x] **AC2** — An administrator's sort by either field still works.
- [x] **AC3** — Security review passes (S3/S4), and the full suites pass.

## Owner decision — 2026-10-13

Mike, answer 8: "Analyze and Fix those".

Mike, default choice: "Go with defaults (recommended)".

Mike, answer 4: "I think that not since it's using the account's by default".

Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

Drop every customerName/customerEmail sort entry only for a non-admin open-board read (!isAdmin && employeeIdFilter is null), using the resolver’s normalization before ForOrderPage. Administrator and cleaner own-order sorting stay. Record the own-order redacted-PII sort residual in docs/api/orders.md and the separate security verdict. T-0853 resolves answer 4’s account e-mail question in the same PR.

Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/`. Source checks are pre-implementation findings; no test, implementation review, CI or deployment result is asserted by this approval record.

## Work record

- 2026-10-12 — Filed from the native PR's security review. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.
- 2026-10-13 — The security review of the native branch's later commits found this wider than filed (`p5/impl2/security_native_followups.md`, its F1, off-tree in the Wave F folder), and proposes Medium in place of Low:
  - A cleaner needs no past customer: a signed-in booking stamps whatever contact e-mail and name the request sends (`CreateOrder.cs:1111-1112`), so a cleaner can book one cash visit under a guessed e-mail and use it as the reference order.
  - `GET /api/Dashboard/GetUpcomingOrders` on both partner hosts reaches the same handler, and every sort entry is applied, not only the first (`BaseRepository.cs:88-94`). So the fix belongs in `GetPagedOrders` (`:120-121`), dropping both fields from every entry.
  - A question for Mike (S1): should a signed-in booking be able to name a contact e-mail other than the account's? The order's status e-mails go to it (`TakeOrder.cs:484-485`).

- 2026-10-13 — Inline sanitization now drops every customerName/customerEmail sort entry only on `!isAdmin && employeeIdFilter is null`, with resolver-equivalent current-culture normalization before ForOrderPage. Safe entries retain priority/direction, and an empty remainder gets the normal default. Administrator and pinned own-order sorts remain. `docs/api/orders.md` records the own-order stored-contact inference residual after PII redaction; no claim of a universal contact-sort removal is made.
- 2026-10-13 — Actual PostgreSQL RED3 failed/9 passed/12; GREEN12/12, later29/29 combined provider includes16 scope cases. Opposite contact sorts, both protected fields, Turkish casing, safe-primary ties with later protected terms, pagination, admin/own controls and foreign employee pinning are covered (`p2/contact/implementation-receipt.md` and fingerprints). The signed-in guessed-contact reference path is closed by T-0853 in the same change. Full backend8722→906→509 all pass/zero skipped; command-only verification and remaining full gates are separately named. No DTO/client/schema/native sort changes.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

2026-10-13 — Implementation verdict: **approve-with-notes**; separate security verdict: **APPROVED for the reviewed P2 security implementation**. Their own executed checks and limits are in `p2/reviewer-round1.md`, `p2/security-round1.md` and `p2/verifier-round1.md` under the evidence root. Hosted exact-head CI and guarded merge remain due; no deployment is claimed.

- 2026-10-13 — PR [#330](https://github.com/VM-s-Solutions/cleansia/pull/330) merged at2026-10-13T17:48:26Z as `6c396f013d79e7b62958fe3b09e2a503a724008c`. Exact-head `5490b331d7f7e8be549b44b24ff21f98a8c35a2f` passed9/9 checks in8 workflows on first attempts; no rerun. The SHA-guarded PUT merged the tested head, and both trees are `87f7b48db03700f48b27074ba32890144489fe10`. The PR has319 changed files and3 commits: #329 closure first, approved filings, then the combined implementation/records. Saved snapshots, all9 job logs and annotations are under Wave F `raw/ci/nw-p2-pr330-poll-001` and `raw/ci/nw-p2-pr330-5490b331d7f7/evidence`; off-tree closure `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-p2-pr330-ci-merge-closure-001.json` SHA256`bad0ae9478bd9cf0b2efb14946ccb4400387b036c43351a48be8ab4f5ecdcfb1` hashes the CI snapshots, saved annotation/log summary, premerge read, guarded merge and four deployment-run reads. Their bounded GETs found0 deploy-dev/deploy-pro/deploy-docs/execute-sql runs created at or after the merge timestamp. This is that observed window, not a claim about all history or future dispatches. No phase reset/deploy was dispatched. Master push CI is tracked separately and remains unclaimed at this closure's capture.
- 2026-10-13 — Final committed-head local checks on5490b331 passed: uncached lint64 projects, docs references/catalog/VitePress and secret-history2446 commits/no leaks (`p2/local/committed-head-gates.json` under the wave root). Earlier filing-head lint/check attempts are nonproof for this source head. The implementation/full-suite/command-only and residual limits above remain; no real-mail/device/DEV proof is added by hosted CI. This closure is prepared as the first commit of the fresh remaining-jobs branch, directly after the merged P2 base.
