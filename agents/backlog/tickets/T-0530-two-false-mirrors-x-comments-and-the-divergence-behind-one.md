---
id: T-0530
title: Two false "mirrors X" comments — and the three-way status divergence behind one of them
status: done
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-04
depends_on: []
blocks: []
stories: []
adrs: [0037]
layers: [architect, backend]
security_touching: false
manual_steps: []
sprint: 15
source: challenger round on ADR-0034/0035/0036 — `adr/challenges/0036-C-digest.md` CH-Q10.1.
  Owner-verified 2026-08-02. A defect that belongs to no ADR.
---

## Context

**A comment that asserts an invariant which does not hold is worse than no comment — a reviewer reads it
and stops checking.** Two such comments were found in the challenger round. This ticket owns the first
and the divergence hiding behind it; **T-0527 AC11** owns the second, because that file is being rewritten
there and two instances must not edit it at once.

**The comment.** `src/Cleansia.Core.AppServices/Services/NewJobsDigestService.cs:48-53`:

```csharp
/// <summary>
/// Status set considered "available" for a cleaner to take. Mirrors
/// <c>DashboardSpecifications.CreateAvailableOrdersSpec</c>.
/// </summary>
private static readonly OrderStatus[] AvailableStatuses =
    { OrderStatus.New, OrderStatus.Pending, OrderStatus.Confirmed };
```

`DashboardSpecifications.CreateAvailableOrdersSpec` passes
`orderStatuses: new[] { OrderStatus.Pending, OrderStatus.Confirmed }`
(`src/Cleansia.Core.AppServices/Features/Dashboard/DashboardSpecifications.cs:24`). **`New` is in one and
not the other.** The comment asserting they match is false.

**And the divergence is not two-way, it is three-way — this is the part that is not cosmetic.** PM-verified
while scoping:

| Surface | Which statuses a cleaner may act on |
|---|---|
| **The digest** (`NewJobsDigestService.cs:52-53`) | `New`, `Pending`, `Confirmed` |
| **The board** (`DashboardSpecifications.cs:24` → `GetPagedOrders`, `GetAvailableJobsPreview`) | `Pending`, `Confirmed` |
| **`TakeOrder`** (`TakeOrder.cs:38-60`) | **no status rule at all** — the validator checks existence, free spots, profile, approval, weekly cap and time conflict, and nothing else. A `New` order is takeable. |

So today a `New` order is: **pushed** to the cleaner, **absent** from the board they open, and **takeable**
if they somehow reach it. That is the same failure shape as a false push count — the cleaner is told about
a job they cannot find — and it burns the digest watermark for that order at the same time (see
**T-0528**). Whichever way the divergence is resolved, at least one of the three surfaces is wrong today.

## Acceptance criteria

- [ ] **AC1 — the canonical set is named.** Given the three surfaces above, When the architect rules, Then
      one status set is named canonical and the ruling is recorded in `agents/architecture/decisions/`,
      answering explicitly: **is a `New` order offerable to a cleaner?** (i.e. may a cleaner take an order
      before its payment settles?). This is a one-item ruling with a yes/no answer; it is not a panel-sized
      question, but it **is** a decision and no code moves before it exists.
- [ ] **AC2 — code and comment agree, whichever way AC1 goes.** Given the ruling, When the change lands,
      Then either the digest's `AvailableStatuses` matches `CreateAvailableOrdersSpec` **or** the comment
      stops claiming it does and states the deliberate difference **and why**. A comment amended to
      "mostly mirrors" is a fail.
- [ ] **AC3 — `TakeOrder` is made explicit either way.** Given the ruling, When the change lands, Then
      `TakeOrder`'s status posture is stated in code — either a validator rule enforcing the canonical set,
      or a comment at the validator naming the omission as deliberate and saying what protects it. Silence
      is not acceptable in the one place that actually assigns a cleaner to a job.
- [ ] **AC4 — the behaviour is pinned.** Given a `New` order in a cleaner's work country, When the sweep
      runs, Then a test asserts whether it is counted, matching AC1's ruling; And a test asserts the board
      query returns the same answer for the same order. **Evidence:** the two tests, in the same run, on
      the same fixture — the divergence exists because nothing compared them.
- [ ] **AC5 — the sibling comment is closed elsewhere, not here.** Given `CancelOrderSheet.kt:74-79` and
      `CancellationFeePreview.swift:12-15`, When this ticket is reviewed, Then the reviewer confirms
      **T-0527 AC11** carries them and this diff does **not** touch either file.

## Out of scope

- **The Android/iOS cancel-sheet comments** — T-0527 AC11 (shared-file lane).
- The digest's watermark burn (**T-0528**) and its tenancy defect (**T-0529**). All three edit
  `NewJobsDigestService.cs`; **serialize them.** Suggested order: T-0529 (5 lines) → this ticket
  (a constant + a comment) → T-0528 (the mechanism).
- The two *other* false assertions in the same file — `:118-119` (*"keeps the per-cleaner page tiny"*) and
  `:131-133` (*"bounded by how many new orders matched the country filter"*), both false for a
  never-notified cleaner whose watermark is `DateTimeOffset.MinValue` (`:90`). They are **T-0528 AC7**'s,
  because fixing the words requires fixing the bound.
- A repo-wide sweep for false "mirrors X" comments. Tempting and out of scope: two were found by
  challengers reading two specific files, and a grep-driven sweep over ~40 such comments is its own ticket
  with its own sizing. **If the reviewer wants it, file it; do not absorb it.**

## Implementation notes

`OrderStatus.New` is reachable and non-trivial: `TakeOrder.cs:191-196` writes the `Confirmed` track only
`if (currentStatus is OrderStatus.New or OrderStatus.Pending)` — i.e. the code already contemplates a
cleaner taking a `New` order. That is evidence for "the board is the wrong one", not against it. The
ruling should say which, and say it once.

**Archetype:** `agents/knowledge/consistency.md` — one rule expressed once; when a predicate exists at N
surfaces, the surfaces are diffed, not counted.

**Why this carries a ruling and not a panel:** there is no new behaviour and no new pattern — but there
*is* a fork (which of three surfaces is right), and the PM will not pick it. One architect, one item.

## Status log
- 2026-08-02 — draft (created by pm from the challenger round). Filed as the "small comment ticket" the
  challenge described; **the scoping pass found it is a three-way behavioural divergence, not a two-way
  comment error**, so it carries a ruling. Still `S`.
- 2026-08-03 — backend slice implemented against **ADR-0037 (`accepted`)**. AC1 is the ADR. AC2 taken on
  the strong branch (the digest's array and its "Mirrors" comment are **deleted**, not amended). AC3 is a
  real validator rule, not a comment. AC4 is pinned on one Postgres fixture across the board, the
  dashboard count and the take gate. AC5 respected — neither `CancelOrderSheet.kt` nor
  `CancellationFeePreview.swift` is touched. **No migration, no NSwag regen** (the new key rides the
  existing ProblemDetails channel; no DTO or endpoint shape changed).
- 2026-08-04 — **done** (PM sprint-15 reconciliation). AC1's architect ruling became **ADR-0037**
  (`343c7b8a`, accepted `182a5660`-era verdict in the file), and the implementation shipped in `37756936`
  *"feat(orders): one offerability rule, read by every surface, enforced at the take"*. **Verified at
  HEAD:** `src/Cleansia.Core.Domain/Orders/OrderAvailability.cs` exists with the two evaluation forms
  (`IsOfferableSql` / `IsOfferable`) and the coarse `OfferableStatuses` prefilter; the false *"Mirrors
  `DashboardSpecifications.CreateAvailableOrdersSpec`"* comment is gone from the digest because the
  literals it mirrored are gone; `TakeOrder` now has a status gate. The divergence really was three-way —
  `TakeOrder.Validator` had no status rule at all — and closing it also closed the one-error-per-refusal
  information leak. **The second false comment (`CancelOrderSheet.kt:74-79`) is NOT closed here** — it is
  T-0527 AC11's, per this ticket's own scoping, and T-0527 has not started.
- 2026-08-04 — **layer 2 of ADR-0037's enforcement, deferred at `37756936` as ADVISORY, has since shipped**
  (`01b21746` wrote the parity script, `e4dd27f5` committed the workflow that triggers it —
  `.github/workflows/offerability-parity.yml`). Recorded here so the ADR's escape hatch is not left
  looking open.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

**Backend hand-off note (2026-08-03).**

- **Catalog harvest.** `agents/knowledge/consistency.md`'s order-offerability entry still described
  ADR-0037 as `proposed` and quoted the **falsified draft predicate** as canonical. Updated in the same
  change to the accepted rule (both conjuncts) plus the three deviating forms.
  `patterns-backend.md` already carried the panel's law and needed nothing.
- **Test-harness defect found and fixed (not in the original scope).** Every `TestMethod` builds a fresh
  `ServiceCollection`, so `AddDbContextBindings` builds a fresh `NpgsqlDataSource`; the `ServiceProvider`
  is never disposed and an externally-created singleton instance would not be disposed by the container
  anyway, so pooled connections stayed open for the whole run. The integration suite had **zero
  headroom** — adding one test made an unrelated one fail with `53300: sorry, too many clients already`.
  `BaseIntegrationTest.BuildConfiguration` now appends `Pooling=false`. Same runtime (~59s), and the
  count no longer grows with the suite.
- **In scope for ADR-0037 but NOT done here, with reasons:**
  - **D4.1, the date floor** (`BookingPolicy.OfferableGraceHours`, the dashboard spec applying it, web
    dropping its own `?? new Date()`). `BookingPolicy.cs` is in another agent's lane this batch.
  - **Verification step 8's sweep case** (run `CleanupStalePendingOrders` over the fixture and assert the
    card order leaves the offerable set). `CleanupStalePendingOrders.cs` is another agent's file (T-0528
    lane); the property itself is pinned statically instead.
  - **D7 layer 2**, the cross-stack parity script + its repo-root workflow. Not delivered, so per the
    ADR's own escape hatch **layer 2 is ADVISORY until it is**. Layer 1 (structural) and layer 3 shipped.
  - **D7 layer 3's `check-consistency.mjs` line rule** for availability status literals.
  - **Client surfaces 5 / 9 / 10 / 11 and D6.4's web reconcile** — frontend lane.
  - **iOS partner catalog** — left to the iOS agent (see the two strings requested below).
- **iOS ask (one file, 5 languages, `CleansiaCore` shared catalog):**
  - new `error.order.not_takeable` — en: *"This job is no longer available."*
  - re-voice `error.order.no_available_spots` — en: *"Another cleaner has already taken this job."*
    (today it reads a **customer's** sentence: *"No cleaners are available for that slot. Please pick
    another time."* — untrue and unactionable for the cleaner who just lost the race).

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read at HEAD:
`src/Cleansia.Core.Domain/Orders/OrderAvailability.cs` in full, `OrderSpecification.cs:120-135`,
`OrderRepository.cs:255-330`, `.github/workflows/offerability-parity.yml` (exists and is committed —
`e4dd27f5` corrected the earlier record that reported the gate enforced while its trigger was untracked).
Commit `37756936` records four mutations plus a negative control (dropping the offerability conjunct fails
2 of 3 integration tests while the pre-existing scope suite stays green) and 2588 unit / 117 integration,
0 failed. **Residual, recorded not swept under:** `37756936` also fixed an out-of-scope harness defect
(connection-pool exhaustion, `53300`). **No `manual_steps`.**

