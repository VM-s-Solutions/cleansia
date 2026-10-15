---
id: T-0832
title: Make the native apps' sort reach the server
size: M
owner: pm
created: 2026-10-12
updated: 2026-10-15
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
    - This branch's own commits after `f9771b2` change no backend file. The master merge `753b98b` brings #327's backend code, which #327's exact-head CI ran on `b47e6ce`. No backend suite ran on this branch after the correctness review's run, and none ran on the merged tree.
    - Android: core 296, partner 839 and customer 1683 tests, 0 failures, on the `6672db1` tree (`p5/native-fixpass/logs/summary.txt`, `android-rerun-tests.log`).
    - iOS: CleansiaCore 776/776, CleansiaPartner 1113/1113 and CleansiaCustomer 1780 (1779 passed, 1 skipped), on iOS 18.6 and on 16.4, on the `6672db1` tree (`p5/native-fixpass/logs/summary.txt`). SwiftFormat: 0 of 960 files to format. SwiftLint `--strict`: 0 violations in 959 files.
    - Checks on the `6672db1` tree: check-docs-refs, 377 references with 0 unresolved; check-catalog-claims, C1 0 · C2 0 · C3 0 with 157 advisories; the docs site builds with no dead links.
    - No native code changed after `6672db1`. The filing commit and the records change only `agents/backlog/`. `3bd7b2b` changes docs and catalog pages and one test comment, and `8c26004` changes the CHANGELOG. The master merge `753b98b` brings #326's workflow and Bicep changes and #327's backend code, which #327's exact-head CI ran on `b47e6ce`. No suite ran after `6672db1`, and none ran on the merged tree.
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
  - NOT doing held: this branch's own commits change nothing under `src/Cleansia.App` or `src/Cleansia.Core.AppServices` (`git diff --stat 6952149..fbc98cd` lists nothing there, and the commits after the merge change only `agents/backlog/`). At the head, `src/Cleansia.App` and `SortMapper.cs` are unchanged. The eight `src/Cleansia.Core.AppServices` files that `git diff --stat 6952149..HEAD` lists are #327's, brought by the master merge `753b98b`.
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
- 2026-10-13 — Shipped in [#328](https://github.com/VM-s-Solutions/cleansia/pull/328).
  - Exact-head CI on `fc24e7e` passed all 9 check runs, iOS CI after one re-run. Its first attempt failed one customer test this branch did not touch, `BookingViewModelTests.testSelectionsTheMarketDoesNotOfferArePrunedWithANotice`, which waited for an asynchronous catalogue reload with five `Task.yield()` calls (`BookingViewModelTests.swift:448-452` at `fc24e7e`); the re-run passed. Backend, Frontend build and e2e-smoke, Android, iOS Symbols, Docs, booking-policy parity and Secret scan passed on their first attempt.
  - Merged normally with the SHA guard as `737bdb4`. No deploy ran: deploy-dev, deploy-pro, deploy-docs and execute-sql have no run since the merge.
  - Evidence: `raw/ci/p6-pr328-ci-merge-closure-001.json` (sha256 `5557de97…`) in the off-tree Wave F folder.

### Native follow-up — 2026-10-14

- Authority, dated 2026-10-13: Mike, "Fix the findings as well" and "Go with defaults (recommended)"; `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`, finding (e). #328's sort/spec closure remains intact. Finding (f)'s commit-subject correction is reserved for PR 6; no master history or subject correction is made here.
- Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/`. Finding (e) changes exactly one production modifier, `OrdersListContent.swift:147`, `.accessibilityLabel(OrdersFormat.sortLabel(sort))`, on the existing **Menu**. `TextInkTests.testAvailableSortMenuNamesItsCurrentVisibleSelection` fails against unmodified source and passes in final SDK tests on both floors. No extra accessibility modifier is added.
- The initial `runtime-preparation/ios-original-menu-001-164.json` and `ios-original-menu-001-186.json` attempts used the wrong child-Button metric and remains retained as nonproof. Proper unmodified base31 app replay, `runtime-preparation/ios-original-popup-scroll-004-186.json` (SHA256 `03b6313f9cf846e1f9292278b6109ad62e7ee415a1c9e9ec9d7ed10df00e6421`), shows the **parent `AXPopUpButton.AXLabel`** becoming nil after scrolling offscreen and back while its visible descendant text stays named. This device RED was captured **after** the source modifier had already been added, using the preserved unmodified normal product; it is not claimed to precede implementation. The single 16.4 original-app cycle, retained under `ios/menu-device-verifier-001.json`'s `baseline164_one_cycle_non_reproduction` raw `ios-original-popup-scroll-005-164` references, did not reproduce the parent-label defect; no two-floor RED is invented.
- Final `runtime-preparation/ios-final-menu-choices-007-164.json` and `ios-final-menu-choices-007-186.json` qualify all three choices after scrolling away and back: Earnings high → low, Total price high → low and Soonest first. Six of six parent labels equal the visible selection; installed main/debug-dylib/source hashes bind the product. The nil direct child Button remains an irrelevant observation, not an unclosed criterion.
- Independent read-only `ios/menu-device-verifier-001.json` (SHA256 `84161de7cd38583e76195ed365dc725de779181525c03ba7ac65049e509a7322`) reports PASS for exact Menu modifier and six post-scroll cases. It preserves the late baseline chronology and 16.4 non-reproduction, and is not a full ticket approval. `runtime-preparation/ios-runtime-acceptance-final-001.json` (SHA256 `40501b1b8af9dc2ef62bba9c33b57d8a358b8f99e801e01abe4befcb557c7628`) seals the final two-floor runtime receipts and their product/source bindings. It records PASS within the stated device scope, not a full ticket or whole-wave approval.
- `ios/developer-final-packet-001.json` (SHA256 `470115f5e483f4af7f835d3b66221bbae638315771d4296f791eab51b448c031`) binds all 27 iOS overlays to `ios/developer-final-source-001-source-manifest.json` (SHA256 `1cc89799ea912d3d650ce8fc33c2a2a96aca8b308d813d261c6610ece2ba09cb`). On **each** of 16.4 and 18.6: Core 776 passed; Partner 1133 passed; Customer 1780 reported, 1779 passed and one unchanged VoiceOver guard skipped; no failures. Focused Partner 133/133 passed on each floor. All nine final attempts are retained, including the second Customer 16.4 run: the accessibility preference was already 1, reapplying 1 still left `UIAccessibility.isVoiceOverRunning` false, and readback restored the same 1. There is no spoken VoiceOver proof. The actual Dashboard source split is 24 main-file methods plus 10 barrier-file methods, all 34 passing on both floors; the older 25+9 preparation split is stale. The final RED/GREEN matrix has 23 entries with the same method ids passing on both floors. Its 19 original failed methods include an invalid assumed default-phone-grouping assertion; the epoch attempt's three failures include the still-unlabelled Menu and a wrong radius fixture expectation, both explicitly qualified. Harness errors are not relabelled as behavior RED.
- The prior past-job observation is addressed by T-0854's merged slot-end rule and T-0855's native D9 follow-up. CHANGELOG adds the visible accessibility behavior. Final source commit, remaining final gates and exact-head CI/merge are pending; the older T-0832 closure is not rewritten.

- Final combined-source cross-stack gates PASS is recorded centrally in T-0839's dated native Work follow-up (`cross-stack-001/final.json` and `count-receipt-001.json`); final docs/parity/symbol and publication obligations remain separate.


### Native publication closure — 2026-10-14

- 2026-10-14 — Native follow-up shipped in [#333](https://github.com/VM-s-Solutions/cleansia/pull/333). Exact-head CI on `e6039a9acca40e22047a2bf4ba56a4c76a1abf63` passed all 8 check runs across 7 triggered workflows on their first attempt, with no rerun. Merged with the head SHA guard as `a667f62b1c7157c5ca0104837dbe8e6c4dc45045` at `2026-10-14T12:55:03Z`. Exact merge-head master CI on `a667f62b1c7157c5ca0104837dbe8e6c4dc45045` passed all 6 check runs across 6 triggered workflows on their first attempt, with no skipped job or rerun. Evidence: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/ci/nw-native-pr333-ci-merge-closure-001.json` (SHA256 `ca11540ebb8c221b5b6abe1af2160e107afd801ce360adba66c7275a0692e179`) and `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/master-ci-evidence-receipt-001.json` (SHA256 `1005f55da28ab299d259aea4772f2253ee42eff213314291e8d215ce4c9abde8`).
- The historical #328 sort/spec closure remains. Finding (e) publishes the Menu label and its six final two-floor parent-label checks; the late 18.6 baseline RED, one-cycle 16.4 non-reproduction and three historical nil-parent dumps retain their recorded limits. Finding (f) commit-subject corrections remain in PR 6.
- The source/full-suite and actual committed-head gate binding is recorded in T-0839’s native publication closure below its Work record.
- The captured deployment metadata queries beginning at the merge timestamp reported zero runs for deploy-dev, deploy-pro, deploy-docs and execute-sql. The authorized wave-end DEV reset, fresh deployment and docs deployment remain due. Earlier dated pending statements record their capture time.

### P6 documentation and comment follow-ups — 2026-10-14

- Authority, dated 2026-10-13 — Mike: “If rules improve the workflow in a better way then yes”; “Fix the findings as well”; “Go with defaults (recommended)”. Authority: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The relevant decided scope is D6 ADR-0075; finding (f) f9771b2 correction; FF-16 cross-reference.
- Decision 6 changes ADR-0075's attribution wording with the original decision, concessions and amendments retained. FF-16's remaining regeneration wording is recorded on T-0847. The #328 sort/spec and #333 Menu/accessibility histories remain; this follow-up adds no client generation, device check or new sort acceptance.
- 2026-10-14 — Finding (f), correction of the actual subject: `f9771b281efa212c42a76e98372929d077a7a787` has the exact subject “fix(mobile-api): the mobile specs state a paged sort as Sort[0].Field and Sort[0].Direction, the form the server binds, instead of an array of objects no generator can send”. Its body and nine-file diff establish indexed `Sort[0].Field`/`Sort[0].Direction` in the two committed mobile specs, the two mobile-host filter registrations and the named host tests. Each spec had four object-array Sort query parameters before, then four indexed Field/Direction pairs. This supports the indexed contract and inspected Android/iOS client behavior; it proves no universal inability of every generator. The original commit/body, intermediate compile limitations and all prior records remain unchanged. Exact subject/body and diff evidence: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/records-preparation001/evidence/f9771b2-message.txt` and `finding-f-indexed-contract-evidence.json`. Master history is not rewritten.
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

- The P6 follow-up for ADR-0075 wording and the dated f9771b2 subject qualification was published in [#337](https://github.com/VM-s-Solutions/cleansia/pull/337), head `da03f005d99c60e3acbae7fef5b5f0bc16a17156`, merged as `cfeed43c1ca3defd0b85ca500e109a8ead9b7e6e`. The earlier device findings and wave-end DEV debt remain as recorded. The actual PR/master outcomes, expected skipped job, annotation qualifications, zero reruns and UTC capture timing are retained in the [PR publication closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/pr337-ci-merge-closure001.json) and [master CI closure](/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/p6/master-ci-complete001.json).


Shared publication evidence: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/end/final-records-preparation001/publication-facts.json` (SHA256 `8d709a85b776d2c014c68bf0cf76d7d71c9ccccbe90bfb3505a293a3bd63616f`), with exact retained PR/merge/master-CI, annotation and local/rebind receipt pins.

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
  - The enforcer, `MobileSpecSortParameterTests`, is in `Cleansia.HostTests`, which backend-ci runs (`backend-ci.yml:111`).
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
  - Merge order with the P5 branch: done in `753b98b`, which keeps both sides, sets the index to 75 records and lists ADR-0071 to ADR-0075 in the sidebar.
  - Two observations from device pass -003 (`render-p6/raw/receipt-003.json`, off-tree in the Wave F folder), neither investigated. Not on a ticket:
    - On both apps the Available board lists jobs dated before today, on devices dated Tue 13 Oct: Soonest first opens on jobs dated Thu 8 Oct, and Android's Total-price list also has jobs dated 10, 11 and 12 Oct. On iOS each has a Take Order button. It may be the fixture's seeding; whether a past cleaning time should be offerable is a question for Mike.
    - In three iOS dumps (i01g9, i01h, i01i7) the sort control's parent PopUpButton had an empty accessibility label while its child text carried the visible one. *(Count/element corrected 2026-10-14: this line originally said two and named only i01g9/i01i7; `ios/menu-device-verifier-001.json` rechecks all 25 historical dumps. The direct inner Button is nil in every visible capture and is not the acceptance metric.)*
  - ADR-0075 keeps the deliberation wording "by consensus of the panel" and "the lead added amendments A and B" (`docs/decisions/adr-0075.md:9-11`). Whether the rule against role names in the docs covers that convention is Mike's call; T-0844 and T-0833 record the same question for ADR-0071 and ADR-0074.
  - check-consistency over the whole repository, which no CI workflow runs, reports 30 violations (26 of them B10) and 126 advisory warnings. All are outside this branch's diff. Over the changed folders it reports OK, 271 files (`p5/native-fixpass/logs/final-consistency.txt`, `final-consistency-changed.txt`). Not on a ticket.
  - Already filed, so not repeated here: T-0847 (the invoices endpoint's default order and tie-break, the `createdOn` name, and two decision records' owner-only wording) and T-0850 (a cleaner's sort by customer e-mail or name, which the follow-up security review found wider than filed).

### Native follow-up assessment — 2026-10-14

- Finding (e) Menu source pin and final two-floor post-scroll **parent-label acceptance PASS** in the saved independent device assessment. The three historical nil-parent dumps are corrected in Reported above. Final native iOS approval is sealed below; docs/symbol/parity and current publication remain separate; #328's prior approval and merge evidence are retained.
- Final independent `ios/final-review-001/verdict.json` (SHA256 `0b26a7a469e2478c76ffdcd4665f7d6ab593225ec63e12abbbe4713d4b0f2769`): **APPROVED_SCOPED_NATIVE_IOS27**. Allocated native source/SDK/runtime scope and the own 18/18 copied-product SDK run (no own compile) are qualified in T-0839's dated Work follow-up. This does not close records, hosted CI, merge/deploy or whole S11.
