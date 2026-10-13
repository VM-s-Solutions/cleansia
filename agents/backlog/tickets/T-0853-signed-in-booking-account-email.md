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

- [ ] **AC1** — Given a real handler fixture with session e-mail A and valid request e-mail B, the CreateOrderInput sent to the factory contains A; a guest contains B, with name/phone unchanged. The authenticated assertion is recorded RED then GREEN; the fixture must capture actual input rather than a mock order that ignores contact fields.
- [ ] **AC2** — Given a draft address B and later signed-in profile A, web uses A and renders it read-only. A guest keeps an editable field and its supplied address.
- [ ] **AC3** — Given a valid account e-mail longer than 50 and no longer than 150 characters, both advance validation and real rendered fieldError accept it; the same guest address retains the cap refusal. Applicable assertions go RED then GREEN.
- [ ] **AC4** — Customer ordering-flow and business-rules describe the rule, guest cap, and earlier guest bookings matching the account e-mail in first-booking/oops decisions. Separate S1/money review and backend/Jest/final head checks have recorded results.

## Dependencies and execution

Phase: **PR 2**. Dependencies: none beyond the applicable merged foundations and phase sequence. Parent: none. T-0844/ADR-0072 foundations already shipped in #327; its remaining AC2/DEV evidence is not falsely claimed satisfied by this filing.

Canonical shape: Existing CreateOrder handler + facade-owned web state; S1 server-derived session identity.

Behavior changes require recorded RED/GREEN evidence, byte-exact restoration of scratch mutations, separate implementation review and a separate applicable security review. Run the full relevant suites in CI order on the final combined head, then docs references/catalog claims/VitePress build; run native parity/symbol gates when their trees change. Save commands, exit codes, counts, head identity and genuine verification limits. Generated artifacts are assessed before dependent work and never hand-edited. Do not infer a completed gate from a before-implementation source report.

Generation assessment: no DTO/endpoint/schema regeneration is implied by this ticket’s planned scope. If a concrete required contract/model change is discovered, record it within authorized scope and join the wave’s generation/migration sequence; shared DEV reset still waits until the end.

## Work record

- 2026-10-13 — Owner authority quoted above; the off-tree scope was filed on the backend branch after PR #329 merged, with its closure committed first. All acceptance boxes remain evidence obligations; no implementation, review approval, test, CI, generation or deployment is asserted here.

## Review

Pending independent review of the eventual filed scope and implementation where applicable.
