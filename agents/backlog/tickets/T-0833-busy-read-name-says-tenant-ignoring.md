---
id: T-0833
title: Rename GetBusyEmployeeIdsInWindowAsync so its name says it reads every company (S8)
size: XS
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: [T-0815]
blocks: []
stories: []
adrs: [0074]
layers: [backend, docs]
security_touching: true
---

## Context

Found on 2026-10-11. `GetBusyEmployeeIdsInWindowAsync` reads `GetQueryableIgnoringTenant()` (`OrderRepository.cs:416`), but its name sounds scoped. Request paths call it: `GetMyServingCleaners.cs:158`, and `PreferredCleanerHoldResolver.cs:108` through `CreateOrder`, `ChoosePreferredCleaner` and `ConfirmRecurringOrder`. So do the Stripe payment webhook (`HandlePaymentNotification.cs:426`, through `PreferredOfferNotifier`) and the recurring-materializer timer (`OrderFactory.cs:316`, through `MaterializeRecurringBookingTemplate`). That mix of request path and background job is exactly what S8's third form is about.

It is not exploitable today:
- Every caller's ids are server-derived, or proven by a validator.
- The contract (`IOrderRepository.cs:166-174`) states the caller's obligation.

It is still the IntelliSense trap that T-0815 deleted a method to close. S8's third form (`security-rules.md:705-714`) asks a method like this to name its world.

## Doing (proposed, for Mike)

- Rename it to `GetBusyEmployeeIdsInWindowIgnoringTenantAsync`, or add it to the roster that `(gate pending: T-0606)` names (`security-rules.md:652`). The choice is the Architect's call.
- Update its callers, tests and docs.

## NOT doing

- No change to its behaviour or its callers' ids.

## Acceptance criteria (done looks like)

- [x] **AC1** — The name, or the roster, says that it reads every company. S8's text agrees.
- [x] **AC2** — The unit, integration and host suites pass.

## Work record

- 2026-10-12 — Filed from the security review of T-0815 and T-0817. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.
- 2026-10-12 — Mike: "You can fix those", his answer to item 7 of the decisions put to him, which listed T-0832 to T-0836 (off-tree, in the Wave F folder: `p5/phase0/owner-decisions-2026-10-12.md`).
- 2026-10-12 — Ruled by item 2 of the native design ruling (off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`): "CONSENSUS on the rename. The S8 sentence, the supersession and the verification grep are amended per 2a–2d."
  - The Doing's choice goes to the rename: "Exactly one method, no scoped sibling." and "Not the roster route: the roster does not exist".
  - The ruling added these to the Doing:
    - S8 text with a `T3-HUMAN` tier;
    - a decision record that partly supersedes ADR-0039, with a dated pointer at ADR-0039's head and no other edit there;
    - edits to the hold-resolver role card, the dispatch decision notes and `patterns-backend.md`;
    - T-0837, filed for `IsFirstBookingAsync`.
  - The ruling named the record ADR-0072 and said to "take the next free number at commit time". It shipped as ADR-0074, because ADR-0071 to ADR-0073 went to T-0844.
- 2026-10-12 — Implemented on `fix/post-e-p5-backend-sweep-devices`, off master `6952149`. Evidence paths below are relative to the off-tree Wave F folder, `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/`; the step's result file is `p5/impl/backend_t0833-rename.json`. Every run was local; nothing was pushed. The evidence files name this branch's commits by the ids they had when each was written. The commit messages were reworded before push with each of those commits' trees unchanged, so every earlier id has the same tree as the commit cited here; `p5/sha-map.md` in the Wave F folder maps them.
  - `a792c6f` — the rename, to `GetBusyEmployeeIdsInWindowIgnoringTenantAsync`.
    - Code: `IOrderRepository.cs:176`, its three crefs (`:116`, `:186`, `:210`), `OrderRepository.cs:400`, and the callers `GetMyServingCleaners.cs:158` and `PreferredCleanerHoldResolver.cs:108`. The contract text at `IOrderRepository.cs:166-174` is unchanged word for word. There is one method and no scoped sibling, and behaviour does not change.
    - Tests: the busy-set test class and its file are renamed `GetBusyEmployeeIdsInWindowIgnoringTenantTests`. The resolver's and the picker's mocks and two PostgreSQL suites call the new name: `OrderStatusSetPredicatePlanTests`, whose plan label names the method, and `ReservationSpendsNoCapacityTests`.
    - Docs: the hold-resolver role card, the dispatch decision notes and `patterns-backend.md`'s set-method entry use the new name. Two dated records keep the old name: the card's dated note and the notes' A1 plan row.
    - RED: a rename has no behaviour that can fail first, so the tests were written against the new name before the production rename. `Cleansia.Tests` failed to build with 8 errors, all CS1061 (`p5/t0833-busy-read-rename/red-build-unit.txt`). `Cleansia.IntegrationTests` failed with 9 (`red-build-integration.txt`): three CS1061 on the new name (`OrderStatusSetPredicatePlanTests.cs:272`; `ReservationSpendsNoCapacityTests.cs:80`, `:119`), and six cascades in the arrange and assert lambdas of the same two test calls, one CS1503, three CS1061 on `IServiceProvider` and two CS8030.
    - GREEN: the solution builds in Release with 0 errors. 41/41 targeted unit tests pass (`green-unit-targeted.txt`), the renamed class 12/12 among them, including `A_Proven_Cleaner_Is_Busy_Across_Operators_And_Unrelated_Cleaners_Stay_Excluded`, which seeds non-null tenants. 12/12 pass on PostgreSQL (`green-integration-targeted.txt`). Full suites: unit 8685/8685, integration 860/860, host 503/503 (`full-unit.txt`, `full-integration.txt`, `full-host.txt`).
  - `8145b44` — S8 and ADR-0074.
    - S8 (`docs/architecture/security-rules.md:745-762` at the head) gains the paragraph "When every caller needs every company's rows, there is one method, and its name still says so", with `GetBusyEmployeeIdsInWindowIgnoringTenantAsync` as its reference. It reaches only the third form's subject. Its ✗ line names `IOrderRepository.IsFirstBookingAsync` for T-0837. It is enforced by S8's closing caller test, walked in the security review of every `security_touching` ticket, at `T3-HUMAN`, and it cites no gate ticket.
    - That closing test (`:782-784`) gains the matching clause.
    - ADR-0074 (new, accepted 2026-10-12) has three decisions: D1 the rename, D2 the S8 paragraph, D3 ADR-0039's compliance step 2 restated. It partly supersedes ADR-0039.
    - The decisions index gains the row, the arrow and the paragraph; ADR-0039's row gets the supersession mark, and the sidebar gets the entry. ADR-0039 gains a dated pointer at its head and nothing else.
    - The hold-resolver card gains a postscript under its item 3, and the dispatch notes' departure note says the name now says so.
    - A deliberate addition, named in the commit: `patterns-backend.md`'s ADR-0039 law banner now cites `adr-0039.md:3-16` instead of `:3-9`, because the pointer moved ADR-0039's status line.
    - Checks (`final-docs-refs.txt`, `final-claims.txt`, `final-docs-build.txt`):
      - check-docs-refs: 378 references, 0 unresolved;
      - check-catalog-claims in strict mode: C1 0 · C2 0 · C3 0 over 74 ADRs, with 157 advisories. The one new advisory is a heuristic miss: the correctness review confirmed that the `CancellationPolicyResolver.cs:43` citation does point at the `IsFirstBookingAsync` call;
      - both checkers' self-tests pass, and the docs site builds and renders ADR-0074;
      - the index's mermaid arrow is not checked by the docs build; it was checked by eye.
    - Verification greps, as ruled:
      - `rg -n 'GetBusyEmployeeIdsInWindowAsync' src docs/architecture docs/domain agents/knowledge agents/architecture` returns exactly the two dated records the ruling expected, `preferred-cleaner-hold-resolver.md:90` and `preferred-cleaner-dispatch.md:807`.
      - The ruled grep for the old test-class name over `src agents docs/domain` returns one line, where the ruling said none: `agents/WAVE-B-2026-10-08.md:1957`, a dated Wave B ledger line. ADR-0074's Verification states that hit.
      - Both greps gave the same result when re-run on `1768d58` while this record was written.
  - `0e91bb3` — in the review fix pass: S8's one-method paragraph now makes the re-pin a requirement of every such method, not a property of its example, as the security review asked (`security-rules.md:748-750` at the head; `:735-737` in `0e91bb3`).
  - `6d8e10c` — files T-0837, which S8's ✗ line and ADR-0074 cite.
- 2026-10-13 — Follow-ups.
  - `49ebf55` — ADR-0074 now supersedes every passage of ADR-0039 that rules the set read's tenancy: D3's specified doc comment; D3.2's end state; D6's caller-table rows, and its "name the two variants" where every caller needs every company's rows; D9's note; and compliance step 2. The added items are dated 2026-10-13, after acceptance and before merge. ADR-0039 itself is unedited. The commit's ADR-0071 half is T-0844's.
  - `1768d58` — the decisions index's prose and its two table rows now say ADR-0074 supersedes every passage of ADR-0039 on the set read's tenancy. The prose said "two parts", and the two rows named only D3.2's set-read tenancy and compliance step 2. The commit's other two corrections are T-0844's and T-0834's.
  - Full suites on `10d6d82` (the evidence's `138ccc015`), in CI's order: unit 8689/8689, integration 867/867, host 504/504 (`p5/be-followups/suites-progress.txt`, `unit.log`, `integration.log`, `host.log`). After that run, `1768d58` changes two docs pages and one code comment, and `1b4ecb7` changes seven docs pages and three code comments; the master merge `6363aee` changes no `src`. No full suite ran after `10d6d82`.
  - Not run: no migration and no NSwag regeneration, because no schema, DTO or endpoint changed. Nothing ran against DEV or PRO.
  - Not yet: the P5 pull request's exact-head CI and its merge.
- 2026-10-13 — Shipped in [#327](https://github.com/VM-s-Solutions/cleansia/pull/327).
  - Exact-head CI on `b47e6ce` passed all 6 check runs with no re-run: Backend, Frontend build and e2e-smoke, Android, Docs and Secret scan. iOS, iOS Symbols and booking-policy parity did not run, because their path filters match none of the changed files.
  - Merged normally with the SHA guard as `2a8bd7f`. No deploy ran: deploy-dev, deploy-pro, deploy-docs and execute-sql have no run since the merge.
  - Evidence: `raw/ci/p5-pr327-ci-merge-closure-001.json` (sha256 `8885990d…`) in the off-tree Wave F folder.

## Review

- **Correctness review** of `6952149..0cbb4de` (`p5/impl/review_backend.json`): "**VERDICT: CHANGES REQUESTED.**" for the branch. For this ticket:
  - Blocking: "**1. Two tickets are cited but don't exist.**" Here that is T-0837, cited by S8's ✗ line and by ADR-0074. It was filed in `6d8e10c`.
  - Routed for a design ruling, not blocking: "**8. ADR-0039 still contradicts ADR-0074.**" Ruled in `49ebf55`: the challenge stands, and the supersession is completed. "Item 8: ADR-0074 also supersedes those rows, and three more passages that say the same thing" (`p5/impl2/architect_p5-ratify.json`). `1768d58` brings the decisions index into line. The ruling ran no commands; its citations were checked against the tree before the commit (`p5/impl2/backend_close.json`, which names the commit by its earlier id `8851d31f6`).
  - It confirmed the greps: "the old method name appears exactly twice, both in the dated records the ruling expected. The old test-class name appears once, in `agents/WAVE-B-2026-10-08.md:1957`, which ADR-0074 documents."
  - It recorded the numbering as a stated change: "T-0833's ADR is numbered 0074, per the ruling's next-free-number rule."
  - A nit, declined: S8's "Enforced by" calls the paragraph it means its "closing test", while that paragraph's heading names a role. Matching the heading would put a role name in the docs. The reference quotes the paragraph's opening words instead, so a search finds it.
- **Security review** (`p5/impl/security_backend.json`): "changes requested" for the branch, on T-0844's F1. For this ticket:
  - S8: "**T-0833.** A pure rename. Every S8 citation checks out": `IOrderRepository.cs:166-174` and `:10-18`, `OrderRepository.cs:20`, `CancellationPolicyResolver.cs:43` and `SendEmailHandler.cs:290`.
  - "**C3 — T-0837 must exist before merge.** S8's ✗ line cites it, and the backlog index still says "Next id: T-0837"." Filed in `6d8e10c`.
  - Non-blocking: "**The new S8 paragraph** should make the re-pin a requirement of this kind of method. As written, "server-derived ids" reads as a property of the example only." Fixed in `0e91bb3`.
- **Security re-review** of `e0de775` and every commit after `0cbb4de` up to `1768d58` (`p5/impl2/security_rereview.md`): "## Verdict: approve-with-notes" for the branch. For this ticket's commits it found `0e91bb3` (its own C1 ruling's S8 text) and `49ebf55` right, and `1768d58` right "except N4", which is T-0844's citation. S8: PASS. `0f9d0d1` and `1b4ecb7` came after it, and no review re-read them; neither touches this ticket's rename or its S8 paragraph.
- **Catalog-edit routing** (the routing test in `agents/knowledge/conventions.md`): `patterns-backend.md`'s set-method entry takes the new name inline, because a rename narrows nothing.
  - Test 1: `rg GetBusyEmployeeIdsInWindow src` shows every call site renamed.
  - Test 2: a search of `agents/knowledge/*.md` for `GetBusyEmployeeIdsInWindow` returns one hit, the entry itself.
  - S8's new paragraph is a law. The design ruling ratified it, at `T3-HUMAN` on S8's closing caller test (its challenge 2b).
- **Gate 7:** no CHANGELOG entry. The change is a rename with no behaviour change, a refactor, which CHANGELOG.md's "What does NOT get an entry" excludes (`agents/process/quality-gates.md:184-187`).
- **Reported, not fixed:**
  - ADR-0039's dated head pointer (`docs/decisions/adr-0039.md:9-14`) still names only D3.2's end state and step 2, while ADR-0074 now supersedes five passages. A reader is not misled on substance: the pointer says the set read covers every company and links to ADR-0074. A dated record-only closure would be allowed (`agents/knowledge/consistency.md:762-766`; `:758-762` at `2a8bd7f`), but ADR-0039 was left unedited.
  - ADR-0039 still uses the old name at `:297`, `:479`, `:508`, `:1044` and `:1271`. Those passages are about the name, not the tenancy, and stay as dated records.
  - ADR-0074 keeps the deliberation wording "Provenance, checked by the lead" (`docs/decisions/adr-0074.md:226`) and "the lead read every passage" (`:239`). Whether the rule against role names in the docs covers that convention is Mike's call; T-0844 records the same question for ADR-0071.
  - Already filed, so not repeated here: T-0837, which covers `IsFirstBookingAsync`'s name, the unswept repository files, `GetEmployeeIdsCurrentlyOnAJobAsync` and the unfiled T-0606.
