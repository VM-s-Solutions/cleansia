---
id: T-0853
title: Use the account e-mail for every signed-in booking
size: S
owner: backend
created: 2026-10-13
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend, docs]
security_touching: true
---

# Use the account e-mail for every signed-in booking

## Context

The approved account-contact fix is mike-4. CreateOrder currently passes the body's CustomerEmail to CreateOrderInput even with a session subject (`CreateOrder.cs:1014,1109-1113`). Customer web preserves an already typed address on profile fetch. Native apps already send the account address. The fresh contact report confirms the defect and two independent guest-only 50-character checks; it does not execute their tests.

## Owner decision — 2026-10-13

Mike, answer 4: "I think that not since it's using the account's by default".

Mike, default choice: "Go with defaults (recommended)".

The governing verbatim record is `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`.

This implements answer 4 and the record’s mike-4 correction: signed-in e-mail is session-derived; guests stay unchanged.

## Baseline and evidence limits

These references describe the `737bdb4` source tree or the fresh read-only reports named below. PR #329 merged as `155e14067322262d17d5224346621da55860a19c`; the fresh backend branch begins there, with its closure first at `b568804d60f8983265139dca87c2a252f0ec6dcb`. PR #329 changed booking test waits and records, not backend behavior. Each implementation re-ground-truths its item at the current head. This filing records source evidence and proposed tests: no implementation, build, test result or deployment is claimed.

Original analysis/verifier entries: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/analysis-results.json`.

Fresh source reports:
- `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p2/contact-verifier-before.md`

## Doing

- At the existing contact assignment, take IUserSessionProvider.GetUserEmail() for a nonempty session subject; keep command.CustomerEmail for a guest. Keep name and phone as supplied.
- On customer web, replace the draft e-mail with the account value after authenticated profile load; keep name/phone prefill behavior and show e-mail read-only when authenticated.
- Make both facade step validation and the rendered field-error path retain the 50-character cap only for guests, preserving required/format rules. Replace the existing contrary signed-in prefill expectation.
- Document account-authoritative contact e-mail and the first-booking/oops matching consequence, with active Plus precedence preserved.

## NOT doing

- No body fallback for an absent signed-in e-mail claim; issued tokens already carry it.
- No native e-mail change, contact-name/phone rule change, existing-order backfill or guest-cap policy change.
- No DTO, endpoint, client/spec regeneration, model/schema or max100-versus150 mismatch fix.
- No operation against PRO; no shared DEV reset/deploy in this ticket. The approved reset/fresh deployment remains at the end of the complete wave.
- No graphify rebuild, owner-checkout edit, stash or checkout of the owner’s iOS key files.
- No unrelated defect fix, generic job framework, attempt cap or extra abstraction.

## Acceptance criteria

- [x] **AC1** — Given a real handler fixture with session e-mail A and valid request e-mail B, the CreateOrderInput sent to the factory contains A; a guest contains B, with name/phone unchanged. The authenticated assertion is recorded RED then GREEN; the fixture must capture actual input rather than a mock order that ignores contact fields.
- [x] **AC2** — Given a draft address B and later signed-in profile A, web uses A and renders it read-only. A guest keeps an editable field and its supplied address.
- [x] **AC3** — Given a valid account e-mail longer than 50 and no longer than 150 characters, both advance validation and real rendered fieldError accept it; the same guest address retains the cap refusal. Applicable assertions go RED then GREEN.
- [ ] **AC4** — Customer ordering-flow and business-rules describe the rule, guest cap, and earlier guest bookings matching the account e-mail in first-booking/oops decisions. Separate S1/money review and backend/Jest/final head checks have recorded results.

## Dependencies and execution

Phase: **PR 2**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: none. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Existing CreateOrder handler + facade-owned web state; S1 server-derived session identity.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

- 2026-10-13 — Implemented at the existing CreateOrder contact assignment: a nonempty session subject uses `GetUserEmail()` with no body fallback when that claim is missing; guests keep the request address, and name/phone remain supplied. Customer web replaces an earlier draft address on account load and renders the signed-in field read-only; both step and rendered blur-error paths keep the 50-character cap guest-only. Required/format checks remain. Ordering-flow/business-rules record account-authoritative matching for first-booking/oops, with active Plus precedence unchanged.
- 2026-10-13 — Command-only RED/GREEN: `p2/contact/implementation-receipt.md` and `final-proof-fingerprints.json` bind actual commands, logs, source and assemblies. Account input RED2 failed/2 controls passed, then16/16 GREEN; web prefill/cap RED3 failed/400 passed and field/readonly RED2 failed/92 passed, then404/404 across11 suites. Final combined contact/slot142/142 unit and29/29 PostgreSQL pass. Full backend Release, rebuilt from current source, passed8722 unit→906 integration→509 host, all executed/zero skipped (`p2/local/backend-*-full-00*.json`/TRXs). Full web/native/docs gates are separately recorded when complete.
- 2026-10-13 — No DTO, endpoint, client/spec, native contact behavior or schema change was required by this contact fix. The existing account/validator150 versus CustomerEmail model100 mismatch remains; Initial emits citext without a length. No backfill or real-mail/DEV proof is claimed. This follow-up was not in #329 or an earlier merge.

- 2026-10-13 — Combined-source local release gates completed before publication: rebuilt Release unit8722→integration906→host509, zero skipped; uncached web5041 tests/408 suites across71 tested projects (three empty projects), all three production builds and seven Playwright smokes (5/1/1); Android three Debug compilations then core296→partner839→customer1683 JVM tests, every task forced to execute, zero skipped; iOS18.6 Core776→partner1113→customer1779 passed, with one existing VoiceOver case skipped. Docs refs/catalog/VitePress, iOS symbols/booking-policy parity and their self-tests pass. Exact commands, counters and receipt/TRX/summary hashes are in `p2/local/full-gates-before-commit.json` and `p2/verifier/android-gates-final.json` under the wave root. Android ordinary ignored OpenAPI generation and ignored iOS workspace client generation from committed specs ran for local CI; no tracked contract dump/NSwag change was required. These are command-only proofs, not real mail, native behavior/device or deployment checks. First unit source-guard failure was corrected with four actual token-bearing invocations and passed the full rerun; first partner iOS ENOSPC attempt is nonproof. The initial affected lint selected no targets on filing-only HEAD and is not source proof; actual affected lint and the three Docs checks will run at the committed source head before push. Hosted exact-head CI/guarded merge and final DEV remain due.

## Review

2026-10-13 — Implementation verdict: **approve-with-notes**; separate security verdict: **APPROVED for the reviewed P2 security implementation**. Their own executed checks and limits are in `p2/reviewer-round1.md`, `p2/security-round1.md` and `p2/verifier-round1.md` under the evidence root. Hosted exact-head CI and guarded merge remain due; no deployment is claimed.
