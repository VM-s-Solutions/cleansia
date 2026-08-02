---
id: T-0530
title: Two false "mirrors X" comments — and the three-way status divergence behind one of them
status: draft
size: S
owner: pm
created: 2026-08-02
updated: 2026-08-02
depends_on: []
blocks: []
stories: []
adrs: []
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

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
