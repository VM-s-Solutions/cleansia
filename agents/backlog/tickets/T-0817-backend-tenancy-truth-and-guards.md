---
id: T-0817
title: Make backend tenancy comments, contracts and test guards say what the code does
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [backend, docs]
security_touching: true
---

## Context

Findings reported by the Wave D reviews, and still true on master, per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- [BE-01] The stale-checkout and recurring auto-cancel comments say which company actually stamps the feed, the credit and the track rows, with pointers to `/flows/cross-cutting`.
- [BE-02] `GetBusyEmployeeIdsInWindowAsync`'s contract states that it is tenant-ignoring on purpose, because a cleaner's commitment with any operator occupies the same person, and that every employee id must be server-derived. The hold resolver's role page agrees.
- [BE-03] `NotifyAsync`'s summary requires a server-read user id, as `NotifyEachAsync`'s already does.
- [BE-04] `PushSubjectNamesTheEventTests` requires every `NotifyEachAsync` caller to be on the deliberately-keyed allowlist, and walks the whole AppServices tree, not only `Features/`.
- [BE-05] `BootDatabaseIoTests`' eager-probe ceiling test asserts on the probed composition alone, so a cold first leg under parallel load cannot fail it.
- [BE-07] All five request-logging middleware copies prove the oversize-body 413 on real Kestrel, through one theory over the existing host levels.
- [IOS-13] The DTO comment on a missing currency code says what both apps do.

## NOT doing

- No behaviour change in any sweep or read.
- The failure-boundary findings BE-11 to BE-14 are listed for Mike.

## Acceptance criteria (done looks like)

- [x] **AC1** — Each comment and contract matches the code it describes, with file:line in the review.
- [x] **AC2** — The strengthened guards fail when their rule is broken (RED), and pass on master's code.
- [x] **AC3** — The unit and host suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented by the backend lane. Each item was re-checked against the tree first.
  - BE-01, the two sweeps' comments (`CleanupStalePendingOrders.cs:71-74`, `:151-154`; `AutoCancelStaleRecurringOrders.cs:68-70`, `:115-118`):
    - the override stamps only the status track each group adds. `OrderStatusTrack.Create` sets no tenant, and `StampAudit` fills only empty ones;
    - the customer's feed row and push carry the account's own company;
    - the guest e-mail carries the order's company;
    - the credit return commits on its own, onto the account's company, and its key stops a rerun from returning it twice.
    - The comments' pointer target gains a customer-and-partner notice row at `/flows/cross-cutting#tenancy` (`cross-cutting.md:24`). `push-notifications.md`'s envelope sentence is corrected to match.
  - BE-02, the `GetBusyEmployeeIdsInWindowAsync` contract (`IOrderRepository.cs:166-174`):
    - it reads tenant-ignoring on purpose, as it has since `7751c5fa9`. A booking carries the operator of its address's market, so a read scoped to the customer's claim would call a cleaner free who is booked there;
    - every id it is given must be server-derived.

    These agree with it:
    - the hold resolver's role page (`:78-85`, `:161-163`);
    - the dispatch decision doc, as a departure note from ADR-0039 D3.2, which stays unedited.
  - BE-03: `INotificationProducer.cs:21-22` requires a user id the server read itself.
  - BE-04: the guard walks all of AppServices. A new fact, `Every_NotifyEachAsync_Caller_Is_On_The_Allowlist` (`PushSubjectNamesTheEventTests.cs:96`), holds every `NotifyEachAsync` caller to the allowlist, anchored on the known caller (`:105`).
  - BE-05: `BootDatabaseIoTests.cs:130`.
  - BE-07: `EveryHostCopy_ReadUpstreamOfTheExceptionHandler_Answers413AtEveryLoggerLevel` (`RequestBodyLimitPipelineOrderTests.cs:55`).
  - RED:
    - BE-05:
      - master's assertion fails against a cold first leg simulated 1.3 s slower ("gave up after 1,53s");
      - the new assertion fails against a fail-fast fixture ("took 0,16s").
    - BE-04:
      - with the known caller dropped from the allowlist, the new fact fails;
      - with a bare `order.Id,` planted in `Services/NewJobsDigestService.cs`, the widened walk fails, where master's Features-only walk passes;
      - with the caller rewritten as a method group, the fact passes vacuously without the anchor and fails with it.
    - BE-07: with the Admin copy made to swallow `BadHttpRequestException`, its 3 rows go RED.
    - BE-01 to BE-03 are comments and contracts, so they have no RED.
  - GREEN: the full suites on T-0814's line, run on the same tree.
- 2026-10-12 — Committed as `3a71c0e`, which builds on its own.
  - **IOS-13 is not in it.** The `DashboardStatsDto` comment says a client with no code shows the bare number. That was false for Android until T-0824's AND-17, so the comment moved to the native PR to land with that fix.
  - The dispatch doc's paragraph also changes its tenancy pin count from 5 to 4, which follows T-0815's deletion.

## Review

- **Correctness: approve after the fix pass.** The fix pass handled the BE-04 anchor, the BE-02 wording, the cross-cutting row and the push-notifications sentence.
- **Security: approve-with-notes on the first pass.** The re-review returned request-changes on one carried-over item, which was not a security defect: hold the patterns-backend.md hunks until the Architect's ratification is recorded, and stage BE-04 apart from BE-10. Both are done: the ratification is below, and `3a71c0e` carries only the BE-04 hunk.
- **Catalog routing, BE-04 (`patterns-backend.md:596-603`).** The Architect ratified it in the ADR-0033 lead pass of 2026-10-11 and reworded it around the anchor:
  - test 1 stays silent, because the one production caller is on the list;
  - test 2 fires;
  - `T1-CI` holds, because `PushSubjectNamesTheEventTests` asserts its anchor and runs in the blocking backend-ci job.
- **Weaker in one direction (a security note).** BE-05's lower bound now reads the whole probed composition. It catches a probe that stopped waiting only while composition takes under about 2.5 s.
- **Reported, not fixed:**
  - The role page's dated panel note still says "two public wrappers".
  - `IOrderRepository.cs`'s digest contract gives "booked through any company" as its reason. For the digest, the reason that holds is a timer with no claim.
  - The guard exempts and anchors by bare file name. Teach it the tuple shape before a second `NotifyEachAsync` caller lands.
  - Rename `GetBusyEmployeeIdsInWindowAsync`, or add it to the T-0606 roster (see T-0815).
