---
id: T-0818
title: Correct the docs that drifted from the code: backend pages, request logging, local test commands, owner rulings
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-15
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
---

## Context

Docs findings still true on master, per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- [BE-06/WCD-20] `infrastructure.md`: DEV ships the five middleware categories' redacted request and response lines at Information, as `main.bicep` says → `/architecture/request-logging`.
- [BE-08] The digest's freshness docs say that only the hold-expiry and released-window sources are bounded at sweep start. This is the zero-behaviour fix: the Wave D handover ruled "no new sweep-start upper bound".
- [BE-09] The hold resolver's role page and `security-rules.md` cite the current lines.
- [BE-10/WCD-30] `patterns-backend.md` states ADR-0039 D3.2 as shipped, and adds the one-entity-many-windows shape: one commitment read, a pure Domain twin, and a PostgreSQL agreement test.
- [WCD-19] The README and local-orchestration pages show CI's exact `dotnet test` form.
- Owner rulings [D4]: the overview's F11 paragraph records that asking after sign-in was declined on 2026-10-11, and the F10 note says the bar-less hero is final.

## NOT doing

- No ADR edit.
- No sweep of the 68 old citation misses (a separate decision).

## Acceptance criteria (done looks like)

- [x] **AC1** — Every edited sentence matches the code, the infrastructure template or Mike's ruling.
- [x] **AC2** — The repository checkers and the docs build pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented: BE-06/WCD-20, WCD-19 and D4 by the docs lane; BE-08, BE-09 and BE-10/WCD-30 by the backend lane.
  - BE-06/WCD-20:
    - DEV ships the five hosts' redacted request and response lines at Information, through `main.bicep`'s `Cleansia` category.
    - On prod, only a 4xx or 5xx line ships.
    - The Sentry item is scoped to prod. It names DEV's protection: redaction and sensitive-path suppression.
  - BE-08, with no behaviour change:
    - the digest's summary, `ApplyFreshness` and `offerability-and-take.md` now say only the hold-expiry and released-window sources stop at the sweep's start, and the role page's invariant 11 bounds only the hold-expiry term;
    - so a status change during a sweep can count twice.
  - BE-09:
    - the role page cites `NewJobsDigestService.cs:202-209`, and security-rules cites `UserRepository.cs:157-204` and `:206-213`;
    - check-catalog-claims' C3B miss on `security-rules.md:114` clears, taking advisories from 155 to 154.
  - BE-10/WCD-30: `patterns-backend.md` states ADR-0039 D3.2 as shipped and adds the one-entity-many-windows shape.
  - WCD-19:
    - the README, `local-orchestration.md` and the `ci-cd.md` steps block show CI's form: one Release build, then `--no-build -- xUnit.parallelizeTestCollections=false`;
    - the README block, run under `zsh -f` in a copy of HEAD, builds and passes the unit suite 8,639/8,639.
  - D4: the F11 and F10 sentences record the 2026-10-11 rulings.
  - RED/GREEN:
    - the docs lane's assertion script fails these claims on HEAD and passes them on the edited tree;
    - the docs build passes, and so do check-docs-refs, check-catalog-claims and check-consistency.
- 2026-10-12 — Committed as `da2aa77`, with the `ci-cd.md` steps-block hunk. It builds on its own; its one code file, `NewJobsDigestService.cs`, changes comments only.
- 2026-10-12 — Shipped in [#322](https://github.com/VM-s-Solutions/cleansia/pull/322). Exact-head CI on `1136e16` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `6840882`. No deploy ran. Evidence: `raw/ci/p1-pr322-ci-merge-closure-001.json` (sha256 `d31e29ba…`) in the off-tree Wave F folder.

### P6 documentation and comment follow-ups — 2026-10-14

- Authority, dated 2026-10-13 — Mike: “If rules improve the workflow in a better way then yes”; “Explain more, I don't remember what they're about”; “Fix the findings as well”; “Go with defaults (recommended)”. Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The relevant decided scope is D6 process/living/source attribution umbrella; T-0818 three shared-filter passages; FF-09–13.
- Decision 6 changes ADR-0071–0075, the ADR template, deliberation process, all 13 charters and the inventoried living pages to record Mike's ruling or the design review as the decider. Functional responsibilities, protocol User-Agent text and quoted owner words remain. The runbook contributor credit was already removed in P4 and receives no new edit here. ADR-0001–0070 remain byte-preserved except the separately recorded 705-byte additive ADR-0039 head note on T-0833; no historical ADR body is reworded.
- The source comments comprise 21 D6 attribution sites in 17 files, 41 mandatory wipe sites in 33 files, and four FF sites. The additional `UserRevocationEnforcementTests` U1 attribution comment is a deliberate inventory addition within D6; U1 remains a functional evidence identifier. W05's two replacements are one wipe site. Across the 54 changed code files, executable tokens and literal tokens are unchanged. These are frozen diff controls, not suite execution.
- Decision 2 changes exactly three `patterns-backend.md` passages to the decision record's phrase, “one shared filter method, never two predicates written to the same rule”. `LiveCommitmentsInWindow` remains an unterminated queryable shared by four public terminal wrappers: the scoped bool gate for TakeOrder/AdminReassignOrder, the every-company employee set for picker/hold resolver, the every-company candidate set for digest, and the scoped reservation list for TakeOrder. Picker and resolver keep the identical set method; the write gate keeps `AnyAsync` early exit. The batch keeps each candidate's Domain floor and agreement-test requirement. No predicate or behavior changes.
- FF-09–13 correct the listed redaction, CI, local-command, backend-test and request-logging references. The production logging warning remains. The 28 FF dispositions are 22 documentation-candidate items, two already-current items (FF-17/24), three separately composed source-comment items (FF-18/25/26, four sites), and the dropped optional FF-27 addition. These categories are items, not 22 newly shipped behaviors. FF-16 is recorded on T-0847; FF-28 on T-0833. No general accuracy sweep or additional work is absorbed.
- Static integration verdict `APPROVED_SCOPED` is in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/composition002-integration-review001/verdict.json` (SHA256 `b214343fe865847c117f149cd1c1d7ea4f35dda26ab80a4fdc25f6d643e701e1`, seal `055592f029a88260321201f1ae4e3693c07511dd7be857ac385884b2337d5c83`). It covers the frozen 106-path composition, unchanged original afterimages and separately approved E9 clause. Actual-source pre-application factual refutation is `CLEAN`, with zero material findings, in `p6/actual-source-refuter001/verdict.json` (SHA256 `ef1205b6d95dec726aec86a3a51f0bbe08258b872c689484c3ece377fae55025`). Those verdicts cover their stated static/source scopes; neither supplies P6 local suites or hosted CI.
- No CHANGELOG entry is owed for these documentation/process/comment corrections under the existing entry policy. No contract, model, client/spec or migration regeneration is required. Graphify is not run under the agreed exclusion.
- Source: `51a66fe807400ce9dc628e29936333aa0cf43507`, parent `dccf996787ed52e14b7a8e0305bebab3fa3ca0ea`, after the first P5 closure on P6. The 106 approved paths match the frozen afterimages and Git modes; the other 8,152 tracked paths are unchanged. Application receipt: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/source-commit001.json` (SHA256 `02ccb3119f4c30a6cd8d51b42a4ef641ada01cac06e3d533180fe3d5f5fa542d`). This is source binding, not a new SDK, device, provider, CI or deployment result.

#### Actual P6 source-head local gates — 2026-10-14

- On source `51a66fe807400ce9dc628e29936333aa0cf43507`, tree `c6e7ac4fee9ba1a9334ac4cae7a23a784c4e19a4`, the 11 documentation/checker commands all exited 0: documentation references and catalog checks with their self-tests, the VitePress build, and the iOS-symbols, booking-policy and Available-status scans with their self-tests. These source-head results are separate from the first P5 closure-head checks. Receipt: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/source-head-docs001/final.json` (SHA256 `2ea68d9f1b6811e6c8a9f25b60a5bc01ab6ed3dae5d93e690b8b4b25e7f2b5e7`).
- The 25 backend/frontend commands all exited 0. The fresh Release Rebuild recorded 220 warnings and 0 errors; backend unit/integration/host suites passed 8,731/975/509 with zero failed or skipped cases. The actual frontend delta contains one comment-only spec; affected lint selected nine projects and exited 0. The separate 77-project full lint was informational, also exit 0. The typecheck guard passed 8 self-tests and the three app spec programs included 73 specs; frontend Jest passed 5,041 tests in 408 suites across 74 targets (71 nonempty, three empty). All three production builds ran without Nx cache hits. Seven localhost network-stubbed Playwright smokes passed (customer/partner/admin 5/1/1). Receipt: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/full-backend-frontend001/final.json` (SHA256 `33ae2a4702469d6b184726849dcc0cecf53543c1278370233871f1539c315173`); sealed packet SHA256 `1fcae90b147df798eefda7738386a5580061379125c6406caf54928d75559c71`.
- Exact argv/cwd, exits, raw receipt/log hashes, complete 8,258-row source snapshot bindings and retained product-map bindings are recorded in `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/actual-gate-records-preparation003/actual-gate-facts.json` and the original sealed packets. These are local macOS/Docker results; hosted Node20/Ubuntu26, provider behavior and live DEV acceptance remain separate. The retained `gradlew.bat` working bytes use CRLF and its raw Git blob uses LF; each identity is qualified, with no generic normalization claim. Actual mobile local results follow below. No device, hosted-CI, merge or deployment PASS is inferred from these docs/backend/frontend receipts.

#### Actual P6 mobile local gates — 2026-10-14

- The combined 30-command full mobile receipt is `PASS` at source `51a66fe807400ce9dc628e29936333aa0cf43507`. Android's fresh compile invocation executed all 56 actionable tasks and the JVM unit invocation all 96; all three modules compiled with `--rerun-tasks --no-build-cache`. The full unfiltered JVM suites passed 2,873 cases (core/partner/customer 298/881/1,694), zero failed/errors/skipped, in 309 retained XML files. Fresh iOS 18.6 build/test results are Core 776 Passed, Partner 1,133 Passed and Customer 1,779 Passed plus one Skipped. The exact known skip is `ProfileEditChipTests/testVoiceOverAnnouncesTheWholeLabelWhileTheChipIsTruncated()` because it requires the simulator accessibility server; it proves no VoiceOver announcement.
- The original run completed 24 commands and stopped before Customer at `2026-10-14T22:41:20.123952+00:00` on the unchanged 2.5 GB capacity guard. The stopped receipt is retained unchanged (SHA256 `c3b3862d8c6733f26bd54fd19a66091ad99eb25aee7f531369c175d16f3513cf`). A separately reviewed and admitted Customer-only continuation supplied six further commands, using this same run's fresh DerivedData and private cloned package cache. Android/Core/Partner suites were not rerun, and no previous-phase Products were used. This is one capacity pause and continuation, not a failed-test retry.
- The local mobile gate ran both iOS API-client generations through `./scripts/generate-api-clients.sh`, XcodeGen in both app directories, SwiftFormat lint before strict SwiftLint, and the three build/test schemes. Android generation tasks also executed within the fresh compile/unit invocations. The complete source proofs retain all 8,258 tracked working-byte/Git-blob/mode identities, with the known working-CRLF/raw-Git-LF `gradlew.bat` qualification. No contract/model/migration change is added. Full mobile final: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/full-mobile001/final.json` (SHA256 `cb493f835abdc65afaffec7dc3c94070c83818a6144cbfc0e8808ccca7a6e701`); sealed packet SHA256 `4e969d0ac98da4446f8dec65eb41c8ac372fbb1f0d217eb6fca0b6987deb38a7`. Exact commands, raw maps, source/product bindings and the stop/continuation remain in the retained receipts and `actual-gate-records-preparation003/actual-gate-facts.json`.
- These are local command results. There is no new iOS-floor, device, provider, PaymentSheet, R8 or VoiceOver acceptance. Hosted CI, publication and END execution remain separate. All local source suites are now recorded; actual publication closure is still owed.

### P6 publication closure — 2026-10-15

- The P6 follow-up for D6 wording, the three shared-filter passages, FF-09–13 and the recorded FF dispositions was published in [#337](https://github.com/VM-s-Solutions/cleansia/pull/337), head `da03f005d99c60e3acbae7fef5b5f0bc16a17156`, merged as `cfeed43c1ca3defd0b85ca500e109a8ead9b7e6e`. The actual PR/master outcomes, expected skipped job, annotation qualifications, zero reruns and UTC capture timing are retained in the [PR publication closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/pr337-ci-merge-closure001.json) and [master CI closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/master-ci-complete001.json).


Shared publication evidence: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/end/final-records-preparation001/publication-facts.json` (SHA256 `8d709a85b776d2c014c68bf0cf76d7d71c9ccccbe90bfb3505a293a3bd63616f`), with exact retained PR/merge/master-CI, annotation and local/rebind receipt pins.

## Review

- **Catalog routing, BE-10 (`patterns-backend.md:1414-1441`).** Test 2 fires, because the hunk replaces a governing sentence. So the Architect ratified it in the ADR-0033 lead pass of 2026-10-11, and made these adjustments himself (`:1418-1428`, `:1439-1441`):
  - "lands on every shape" is narrowed, because time terms also live in `Order.OccupiesWindow`;
  - the shared filter is named as the "same call" of the read-path/write-path bullet, which the D3.2 shape would otherwise contradict;
  - a Gate 4 `T3-HUMAN` enforcer line is added, with its not-mechanizable reason;
  - the reverse-shape bullet gets its justification.
- **The security re-review objected to that "same call" sentence**, reading it as a relaxation of the "SAME CALL, not the same rule" bullet.
  - It is left as the Architect ratified it. What shipped is ADR-0039 D3.2 as amended ("sharing the *predicate* rather than the *terminal operation*", `adr-0039.md:450-452`), and a shared filter is one code path, not two kept in step by documentation.
  - Listed for Mike, who can overrule it.
- **Two deliberate additions, both named in the commit:**
  - the dispatch doc's snapshot sentence, from BE-10's fix sketch and the Wave D architect review;
  - invariant 11, scoped to the hold-expiry term. That follows the Wave D handover's "no new sweep-start upper bound" ruling, recorded in this ticket's Doing.
- **Guard duty:** no mechanical guard. `infrastructure.md` is outside check-catalog-claims' corpus, and the rest is prose that the docs build and the checkers cover.
- **Reported, not fixed:**
  - `infrastructure.md:843-845` overstates what a redacted record carries.
  - In `ci-cd.md`:
    - the DEV/PRO table says `Environment (default)`, but DEV runs in `dev-weu`;
    - the resource-group and app names are stale;
    - the Backend tip says "Both run with …";
    - the Secret scan's "fails it" is ambiguous.
  - CLAUDE.md's docs-table row sends request logging to `local-orchestration`.
  - The ADRs' Q-CI-01 mentions.
  - The trailing comments in the README's Jest block.
  - Stale `backend-ci.yml:69-71` and `:69-74` citations, which now land on the NuGet cache step (the unit tests are at `:87`). They are in security-rules, enforcement, patterns-backend, consistency and three ADRs (0043, 0049 and 0051).
