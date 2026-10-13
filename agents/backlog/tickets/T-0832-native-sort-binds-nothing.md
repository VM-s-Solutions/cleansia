---
id: T-0832
title: Make the native apps' sort reach the server
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-13
depends_on: []
blocks: []
stories: []
adrs: [0075]
layers: [backend, android, ios, docs]
security_touching: false
---

## Context

Found on 2026-10-12. Neither native app's sort parameter binds on the server, so every native sorted list gets the endpoint's default order instead of the one it asked for.
- Android serialises the generated model through `toString`: `Sort=SortDefinition(field=cleaningDateTime, direction=0)`, captured from the generated Retrofit client.
- iOS sends base64 JSON: `Sort=ewog…`.
- `DataRangeRequest.Sort` (`DataRangeRequest.cs:13`) has no binder or type converter, and nothing in `src/` adds a value provider.
- A throwaway ASP.NET Core 10 app with the same request shape bound both strings to an empty list, with ModelState valid. The web client's indexed form, `Sort[0].field=…&Sort[0].direction=0` (generated, `admin-client.ts:112`; the keys bind case-insensitively), bound one entry.
- An empty sort falls back to each endpoint's default. For orders that is `SortMapper.ForOrderPage`: newest created first, then `Id`.

Six native call sites send a sort:
- Android: `OrdersRepository.kt:300` (the Orders list's chosen field and direction), `DashboardRepository.kt:267` (upcoming, by cleaning date ascending) and `InvoicesRepository.kt:125` (created descending).
- iOS: `PartnerOrderClient.swift:107`, `PartnerDashboardClient.swift:27` and `PartnerPayrollClient.swift:51`.

Effects:
- The Orders list's sort choice does nothing on either app.
- The dashboard's upcoming page is the ten most recently created orders from today. So once a cleaner has more than ten orders from today on, the next job can fall off the page (whenever ten newer-created ones exist), and the today line can undercount (T-0831).
- The two created-descending calls get whatever their endpoint's default is, which this finding did not check.

## Doing (proposed, for Mike)

- Both native clients send the indexed form the server already binds (`Sort[0].Field`, `Sort[0].Direction`). This goes through each app's generator configuration or one query-encoding seam. A wire test on each app pins the form.
- The alternative is a server binder that accepts the native forms too. Choosing between the two is the Architect's call.

## NOT doing

- No change to the web client, which already binds.
- No change to `SortMapper`'s defaults.

## Acceptance criteria (done looks like)

- [x] **AC1** — Every native sorted request carries a form the server binds. On each app, the wire tests go RED, then GREEN.
- [x] **AC2** — On a device, the Orders list's sort choice changes the order.
  - Met on both apps in the third device pass (Work record). The total-price order itself cannot be read on a device, because a card shows no total price.
- [x] **AC3** — The full suites pass.

## Work record

- 2026-10-12 — Filed from the correctness re-review of the native PR (T-0831). Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.
- 2026-10-12 — Mike: "You can fix those" (2026-10-12), his answer to the item covering T-0832 to T-0836.
- 2026-10-12 — Ruled by item 1 of the native design ruling (off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`). It reached consensus. The drafted decision stands, challenges 1a, 1b and 1d were conceded and 1c in part, and two amendments, A and B, were added.
  - The decision: "One new Swashbuckle IOperationFilter, `IndexedSortOperationFilter` (src/Cleansia.Config/Swagger/), registered only by the two mobile hosts." It states `DataRangeRequest.Sort` as the two scalar query parameters the server already binds. Both committed mobile specs are re-dumped, and both generators then emit `sort0Field` and `sort0Direction`.
  - The Doing's routes, answered: "(a) Generator configuration. It cannot do this." A query seam in each app and a server binder that accepts the native forms are both rejected.
  - Its note for Mike opens "None for the decision itself." It rests on a reading of Mike's 2026-10-12 answers, not on a separate answer.
  - The ruling named the record ADR-0071 and said to "take the next free one at commit time". It shipped as ADR-0075, because ADR-0071 to ADR-0074 went to T-0844 and T-0833 on the P5 branch.
- 2026-10-12 — Implemented on `fix/post-e-p6-native-followups`, off master `6952149`. Evidence paths below are relative to the off-tree Wave F folder, `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/`. The result files are in `p5/impl/`: `backend_t0832-spec-filter.json`, `android_native-items.json`, `ios_native-items.json`, `docs_native.json` and `native_fix-pass.json`. Every run was local; nothing was pushed. The evidence files and the reviews quoted here name this branch's commits by the ids they had before its commit messages were reworded before push; each has the same tree as the commit cited here, and `p5/sha-map-p6.md` in the Wave F folder maps them.
  - `f9771b2` — the server and spec half. ADR-0075 is accepted in this commit, with its index row and sidebar entry.
    - `IndexedSortOperationFilter` (`src/Cleansia.Config/Swagger/`) replaces each query-bound sort with `Sort[0].Field` (a string) and `Sort[0].Direction` (`$ref SortDirection`).
    - Only the two mobile hosts register it: `Cleansia.Web.Mobile.Partner/Extensions/ServiceExtensions.cs:87` and `Cleansia.Web.Mobile.Customer/Extensions/ServiceExtensions.cs:90`. The server's runtime binding does not change.
    - Both committed specs were re-dumped through `src/cleansia_ios/scripts/refresh-mobile-spec.sh`. A scratch harness, never committed, booted the two hosts in Development against a throwaway Postgres (`p5/t0832-server-spec/ScratchMobileSpecDump.cs`).
    - The dump taken before the filter was byte-identical to the committed specs, so there is no drift commit (ADR-0075 D3). The dump taken after it changes only the eight `Sort` parameters, and undoing them gives back the earlier documents exactly.
    - At `6952149` each spec had four query parameters that are arrays of objects, all `Sort`. At the head neither spec has one, and each has four `Sort[0].Field` parameters.
    - RED: `MobileSpecSortParameterTests` failed 5 of 5 with the filter unregistered (`p5/t0832-server-spec/red-hosttests.txt`), and again on the final test code (`red-hosttests-final.txt`).
    - GREEN: 5 of 5 in Debug (`green-hosttests-focused.txt`), and in Release by the result file's account; no Release log was kept. `MobileSpecEnumGuardTests` passed 7 of 7 on the re-dumped specs.
    - Its round-trip case reads the two names out of the partner host's document. `GET /api/Order/GetPaged` with them comes back by ascending cleaning date.
    - Full suites on this tree: unit 8647/8647 and host 508/508 in Debug, integration 850/850 in Release (`full-unit.txt`, `full-hosttests.txt`, `full-integration.txt`).
  - `1fca12e` — `agents/architecture/decisions/mobile-wire-contract.md` gains a sentence in §Scope and the section "The request side: the query sort". As a named addition (challenge 1c), the four places where the page called the re-dump owner-only no longer do: the diagram says the re-dump is run and reported, and the new section says it is ordinary work. Its quotation of `generated-client-contract.md` stays verbatim.
  - `9c1433c` — the Android half.
    - `OrdersRepository.kt:299-300` sends the list's chosen field, with direction 0 when ascending and 1 when descending. `DashboardRepository.kt:266-267` sends `cleaningDateTime` and 0. `InvoicesRepository.kt:124-125` sends `createdOn` and 1.
    - `DashboardWireTest` and `InvoicesWireTest` each gain a sort test. The new `OrdersWireTest` covers `/api/Order/GetPaged` in both directions.
    - All three use `assertSortedBy` (`SortQueryAssertions.kt`), which requires exactly `Sort[0].Field` and `Sort[0].Direction` and no bare `Sort` key.
    - RED in the worktree: a compile failure only, "No parameter with name 'sort' found" at the three call sites (`p5/android-native/t0832-red-worktree-compile.txt`).
    - RED on the assertions, in a scratch copy of `src/cleansia_android` holding the specs from `6952149`: 58 tests, 4 failed (`p5/android-native/old-spec-red/t0832-red-old-spec.txt`). Each failed with "expected:<[Sort[0].Field, Sort[0].Direction]> but was:<[Sort]>" (the JUnit reports in `p5/android-native/t0832-red-old-spec-results/`).
    - GREEN: the three wire classes and the three repository test classes pass (`t0832-green-targeted.txt`; 74 tests by the result file's count, which the text log does not print).
  - `ce9240f` — the iOS half.
    - `PartnerOrderClient.swift:113-114`, `PartnerDashboardClient.swift:27-28` and `PartnerPayrollClient.swift:51-52` pass the same fields and directions.
    - `UpcomingOrdersWireTests` gains a sort test. The new `OrderPageWireTests` (both directions) and `InvoicesWireTests` go through the production spine.
    - RED against clients generated from the specs at `6952149`: 3 tests, 12 failures (InvoicesWireTests 3, OrderPageWireTests 6, UpcomingOrdersWireTests 3). Every request carried `Sort=ewog…` (`p5/ios-native/logs/partner-red-186.log`).
    - Against clients generated from the committed specs, the old call sites do not compile: "extra argument 'sort' in call" (`logs/partner-compile-red-newclient.log`, `partner-compile-red-newclient-2.log`).
    - GREEN on iOS 18.6 and on the iOS 16.4 simulator (`logs/partner-full-186.log`, `partner-full-164.log`).
    - Gate 8.5 evidence for challenge 1a: the three wire classes pass on 16.4 inside the partner suite, 1113/1113 (`p5/ios-native/logs/partner-full-164.log`, bundle `p5/ios-native/evidence/partner-full-164.xcresult`). They pass on 16.4 again at `6672db1` (`p5/native-fixpass/logs/full-CleansiaPartner-164.log`).
  - Docs, in the review fix pass (Review):
    - `4345b7b` — `docs/architecture/frontend.md`, Partner dashboard: the sort goes out as `Sort[0].Field` and `Sort[0].Direction` and binds, so the page is the ten soonest of the cleaner's orders from the start of today. The commit's other half is T-0836's.
    - `625740d` — `docs/mobile-app/api-integration.md`: a re-dump is ordinary work, run and reported. Generated output is never hand-edited, drift lands first, and the mobile hosts flatten the sort.
    - `5dc23df` — the two catalog entries the design ruling ratified (Review).
    - `4dbad83` — ADR-0075's status line says 1c was conceded in part. The wire contract's Pinned-by line says the test reads each host's live document.
  - `6672db1` — the CHANGELOG entry (Gate 7).
  - The filing commit — files T-0847, for the questions the design ruling left open: the invoices endpoint's default order and tie-break, the `createdOn` name, and the decision records that still call regeneration owner-only.
  - Suites, each on the build named:
    - Backend: unit 8647/8647, integration 850/850 and host 508/508, on the tree committed as `f9771b2`. The correctness review ran the same totals on a `git archive` of `23ba6e3`.
    - No backend file changed after `f9771b2`, and no backend suite ran after the correctness review's run.
    - Android: core 296, partner 839 and customer 1683 tests, 0 failures, on the `6672db1` tree (`p5/native-fixpass/logs/summary.txt`, `android-rerun-tests.log`).
    - iOS: CleansiaCore 776/776, CleansiaPartner 1113/1113 and CleansiaCustomer 1780 (1779 passed, 1 skipped), on iOS 18.6 and on 16.4, on the `6672db1` tree (`p5/native-fixpass/logs/summary.txt`). SwiftFormat: 0 of 960 files to format. SwiftLint `--strict`: 0 violations in 959 files.
    - Checks on the `6672db1` tree: check-docs-refs, 377 references with 0 unresolved; check-catalog-claims, C1 0 · C2 0 · C3 0 with 157 advisories; the docs site builds with no dead links.
    - No code changed after `6672db1`. The filing commit and the records change only `agents/backlog/`; `3bd7b2b` changes docs and catalog pages and one test comment; `8c26004` changes the CHANGELOG. No suite ran after `6672db1`.
  - Intermediate commits that do not build on their own. The correctness review named them (N3), and the fix pass did not rebase reviewed commits to fix them:
    - `f9771b2` and `1fca12e`: both partner apps fail to compile. The regenerated clients take `sort0Field` and `sort0Direction`, and the six call sites still pass `sort` (`p5/t0832-server-spec/android-partner-compile.txt`; for iOS, `p5/ios-native/logs/partner-compile-red-newclient.log`).
    - `9c1433c`, `b629ff5` and `8f67dbb`: the iOS partner app still fails the same way, until `ce9240f`.
    - `23ba6e3`: everything builds, but the iOS customer suite fails one test until `6d6bf94` (T-0839).
    - `ce9240f`, `d3b0890`, `7ca3c2e` and `109d76a` were each exported and built for the app they touch: TEST BUILD SUCCEEDED (`p5/ios-native/logs/build-commit-*.log`). No record shows each Android commit built on its own.
  - **Ruled differently from Doing:**
    - The route. The Doing proposed each app's generator configuration or a query-encoding seam, or else a server binder. The design ruling changed the mobile API description instead, as above. Neither app gained a seam, and the server gained no binder.
    - The round-trip fixture. The ruling's fixture (cleaning dates opposite to creation order, expected ascending) matches the server's default order, so it could not tell a bound sort from an unbound one. The test first asserts the default with a request that sends no sort, then asserts ascending order with the names read from the document. The correctness review agreed with the change.
    - The record's number is ADR-0075, not ADR-0071 (above).
    - Visible changes the Context did not name: the invoice lists go from no `ORDER BY` to newest generated first, because `createdOn` falls back to `GeneratedAt`. The customer specs' four sorts are flattened too, though the customer apps send none.
  - Ran, as CLAUDE.md asks to say: the mobile spec re-dump, committed in `f9771b2`. The iOS clients were generated with `src/cleansia_ios/scripts/generate-api-clients.sh` at the pinned 7.10.0, into the gitignored `CleansiaPartnerApi/` and `CleansiaCustomerApi/`. Android generates its clients on every compile. `xcodegen generate` ran in both iOS app directories; the projects are gitignored.
  - Not run: no NSwag regeneration, because no web DTO or endpoint changed and the web hosts do not register the filter. No migration. Nothing ran against DEV or PRO.
  - NOT doing held: `git diff --stat 6952149..HEAD` shows no change under `src/Cleansia.App` or `src/Cleansia.Core.AppServices`, where `SortMapper` lives.
- 2026-10-13 — AC2, the on-device check: three render attempts, the first begun late on 2026-10-12. All three were set up for the owned guests, Android AVD `CleansiaWaveC35` (API 35) and iOS simulator `ED225002` (iOS 18.6), against the local disposable render fixture. None touched DEV or PRO. Evidence: `render-p6/raw/`.
  - First attempt (`receipt.json`, `prepare-manifest.json`). It built the four Android APKs from an export of the filing commit, then stopped at step 4. Gradle had regenerated 7 API-client files from the head's specs, so the build tool's own guard failed: "arm inputs changed during build". No guest was booted, no app was installed or opened, and no iOS app was built. Verdict: NOT OBSERVED.
  - Second attempt (`receipt-002.json`, `prepare-manifest-002.json`). It used a fresh build tree without the pinned Wave C clients: Swift regenerated at 7.10.0, Kotlin by Gradle. Four APKs were built, inspected and installed, and both iOS apps were built and installed.
    - The fixed hosts were Wave C's pinned builds. The first receipt found the four sort-binding source files byte-identical to this head's.
    - iOS: the partner app opened signed in on its Dashboard. The Orders list and its sort were not captured, and the observer saw no `GET /api/Order/GetPaged`. Verdict: NOT OBSERVED.
    - Android: the partner app rested on its splash retry screen. The guest could not connect to the observer at `10.0.2.2:16002` (okhttp `ConnectException`); the cause was not established, and nothing was retried. Verdict: NOT OBSERVED.
  - Third attempt (`render-p6/raw/receipt-003.json` (sha256 `900967f1…`, verified by its own check), `prepare-manifest-003.json`). It reused the second attempt's products, unchanged and still installed, and the same fixed hosts.
    - Android: a TCP probe from the guest reached `10.0.2.2:16002` before any app opened, and the partner app opened signed in. The second attempt's network failure did not recur; its cause is still not known.
    - Android, a01: PASS. Each choice on the Orders list's sort control (Earnings high → low, Soonest first, Total price high → low) changed the list, each list rebuilt from screen dumps with 50 cards. The fixed host's request log shows 9 `GetPaged` requests from Android, each carrying `Sort[0].Field` and `Sort[0].Direction` (`raw/captures-003/android/`).
    - iOS, i01: PASS. Each choice changed the order. The host log shows 5 `GetPaged` requests from iOS, each with the indexed sort and none in base64 (`raw/captures-003/ios/`).
    - The host log holds 14 `GetPaged` requests in all and no bare `Sort=`, the observer's count. The observer logs paths only, so the query strings come from the fixed host's log.
    - Not observable on either device: the total-price order itself, because a card shows no total price.
- 2026-10-13 — Not yet: the P6 pull request's exact-head CI and its merge.

## Review

- **Correctness review** of `fix/post-e-p6-native-followups` at `23ba6e3` against `6952149` (`p5/impl/review_native.json`): "## Verdict: CHANGES REQUESTED". "The code is correct on all three stacks and each fix has a test that catches its removal."
  - Its runs used a `git archive` of `23ba6e3`. Backend: unit 8647/8647, integration 850/850, host 508/508, and `MobileSpecSortParameterTests` "5/5 pass; 5/5 fail with the filter's body stubbed out; 5/5 pass again once restored". Android: core 296, partner 839 and customer 1683 tests, 0 failures. CleansiaPartner: 1113/1113 on 18.6 and on 16.4. Its logs are in a temporary directory outside the Wave F folder, named in its result file.
  - Its deletion test for this ticket: `OrdersWireTest.anAscendingChoiceIsSentAsItsFieldAtDirectionZero` "fails when no sort is sent".
  - It agreed with the change to the round-trip fixture.
  - Blocking items for this ticket, and what became of them:
    - Item 2, "**`docs/architecture/frontend.md:568-576` is now false.**" Fixed in `4345b7b`.
    - Item 3, "**`docs/mobile-app/api-integration.md:82-86` still says the spec re-dump is owner-only**". Fixed in `625740d`.
    - Item 4, "**Three ratified catalog entries are not written.**" This ticket's two are in `5dc23df`.
    - Item 5, "**No changelog entry for changes cleaners will notice (Gate 7):**". Fixed in `6672db1`.
    - Item 8: AC2, the on-device check that each Orders sort changes the order, "was not run by any lane". The third device pass met it (receipt-003: a01 PASS, i01 PASS) on the owned emulator and simulator, against the local render fixture served by Wave C's `18a2e5ac` host build, whose sort-binding files are byte-identical to this head's; the first two attempts were NOT OBSERVED. The review's route, "a signed-in cleaner on DEV, or a waiver from you on record", was not used.
  - Should fix, for this ticket:
    - "**N3:** several commits do not build on their own, which matters only if you want every commit to build". Not done, because fixing it means rebasing reviewed commits. The commits are listed in the Work record.
    - "**N4:** `adr-0075.md:9-10` says "1a–1d were conceded", but 1c was conceded only in part (line 186 says so)." Fixed in `4dbad83`.
    - "**N5:** the new "Pinned by" line in `mobile-wire-contract.md` says "either mobile spec". The test reads each host's live document, not the committed JSON files." Fixed in `4dbad83`.
- **Security review** of the branch against `6952149` (`p5/impl/security_native.json`): "**VERDICT: PASS. Nothing blocks the merge.**"
  - On this ticket: "## 2. Mobile spec change — PASS (no server behaviour change, no new bindings)" and "## 3. Generated clients — PASS".
  - "The only difference is the `Sort` parameter on four operations per host, now stated as `Sort[0].Field` and `Sort[0].Direction`."
  - Its non-blocking item 1, low and pre-existing: "A cleaner can sort the order board by customer e-mail or name". Filed as T-0850.
  - It names only its base, `6952149`. Its line citations match the tree at `23ba6e3` (and at `6d6bf94`, which changes only a test it does not cite), and it calls the docs edits uncommitted, so it read the branch before `9952094`.
- **Correctness review of the later commits**, `6d6bf94` to `6672db1` (`p5/impl2/review_native_followups.md`): "**Verdict: CHANGES REQUESTED.**" For this ticket:
  - "**B2 — one clause in b5e3d76c7's message is false**": the message said "the features page already said otherwise", but the text that does is `4345b7b`'s own hunk. The message is reworded before push, with its tree unchanged.
  - Its N2: the list of pages that still call the spec re-dump owner-only, in `625740d`'s message as first written (`85d7b357d`), missed three. The reworded message names them, now among others; they are also under Reported, below.
  - Its N7: `5dc23df`'s backend entry says "the web hosts do not register it" under an enforcer that boots only the mobile hosts. It is true today; whether the entry should name the residual is a design call (Reported).
- **Security review of the later commits** (`p5/impl2/security_native_followups.md`): "**VERDICT: PASS-WITH-NOTES.**" On the sort: "## 3. d00e470f0, 85d7b357d, c9783e67b and ADR-0075 — the mobile sort: PASS, nothing widened, no new field or behaviour". Its F1 finds T-0850 wider than it was filed; T-0850 records it.
- **AC1's reading.** "Every native sorted request" is the six partner call sites; the customer apps send no sort. The wire tests pin the form at all six. The host round trip proves the server binds it for `GET /api/Order/GetPaged`. The dashboard's upcoming call binds the same `GetPagedOrders.Request`, and the invoices call binds `GetPagedInvoices.Request`, which is also a `DataRangeRequest`.
- **AC2** is met on both apps by the third device pass. The total-price order itself cannot be read on a device, because a card shows no total price.
- **Catalog-edit routing** (the routing test in `agents/knowledge/conventions.md`), for `5dc23df`:
  - The entries: `patterns-backend.md`, "A sort on the mobile hosts is two scalar query parameters (ADR-0075)", and `patterns-mobile.md`, "A paged call sends its sort through the generated `sort0Field` / `sort0Direction` (ADR-0075)".
  - Both were ratified by the design ruling ("Catalog, ratified here."), with its routing: "Test 1 sweep: every query array in both committed specs. Only the eight Sort params are object arrays; the rest are int enums. Zero violations once the filter lands." Then "Test 2 fires (the entry forbids a form), so it is priced." and "Test 3 does not fire."
  - Re-checked at the head: neither spec has a query parameter that is an array of objects, and the six call sites send `sort0Field` and `sort0Direction`.
  - Two wordings were tightened against the ruling's text, and the commit names both: the enforcer reads the hosts' documents, and the iOS wire tests are named instead of "two new siblings". The ruling's text names ADR-0071; the entries name ADR-0075.
  - The enforcer, `MobileSpecSortParameterTests`, is in `Cleansia.HostTests`, which backend-ci runs (`backend-ci.yml:108`).
- **Gate 7:** the CHANGELOG entry under Fixed in `6672db1`, "the order lists, the dashboard and your invoices come in the order they should" (T-0832).
- **Reported, not fixed:**
  - These still call client regeneration or the mobile spec re-dump owner-run or owner-only, outside T-0847's list. None is on a ticket:
    - `agents/knowledge/patterns-mobile.md:1016`, `:1028` and `:1033`;
    - `docs/mobile-app/overview.md:712`;
    - `agents/knowledge/patterns-backend.md:434` and `agents/templates/ticket.md:15` (NSwag regeneration, owner-only);
    - `src/cleansia_ios/MAC-HANDOVER.md:160`;
    - `.claude/commands/sync.md:4` and `:27` (client regeneration, owner-only);
    - `src/cleansia_ios/openapi/README.md:83`;
    - `src/cleansia_ios/scripts/generate-api-clients.sh:76`;
    - `agents/architecture/decisions/ios-app-architecture.md:47`.

    T-0847 covers `generated-client-contract.md` and `ios-app-architecture.md:929` (T-0847 cites it as `:928`; `3bd7b2b` added a line above it).
  - `MobileSpecSortParameterTests` boots only the two mobile hosts, so `5dc23df`'s "the web hosts do not register it" is true but not enforced (the follow-up review's N7). Not on a ticket.
  - The `SortDefinition` schema stays in both specs' components with nothing referencing it, so both generators still emit the model. The design ruling called it harmless (its risk 5). Not on a ticket.
  - Merge order with the P5 branch: this branch's decisions index says "71 records" (`docs/decisions/index.md:5`), and P5's says 74. Whichever merges second sets the count to 75 and merges the sidebar entries in `docs/.vitepress/config.ts`. T-0844's record on the P5 branch notes the same. Not on a ticket.
  - ADR-0075 keeps the deliberation wording "by consensus of the panel" and "the lead added amendments A and B" (`docs/decisions/adr-0075.md:9-11`). Whether the rule against role names in the docs covers that convention is Mike's call; T-0844 and T-0833 record the same question for ADR-0071 and ADR-0074.
  - check-consistency over the whole repository, which no CI workflow runs, reports 30 violations (26 of them B10) and 126 advisory warnings. All are outside this branch's diff. Over the changed folders it reports OK, 271 files (`p5/native-fixpass/logs/final-consistency.txt`, `final-consistency-changed.txt`). Not on a ticket.
  - Already filed, so not repeated here: T-0847 (the invoices endpoint's default order and tie-break, the `createdOn` name, and two decision records' owner-only wording) and T-0850 (a cleaner's sort by customer e-mail or name, which the follow-up security review found wider than filed).
