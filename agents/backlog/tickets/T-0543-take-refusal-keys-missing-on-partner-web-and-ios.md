---
id: T-0543
title: The two new take-refusal keys landed on Android only — partner web and iOS show the generic error
status: in_progress
size: S
owner: frontend
created: 2026-08-04
updated: 2026-08-04
depends_on: []
blocks: []
stories: []
adrs: [0037]
layers: [frontend, ios]
security_touching: false
manual_steps: []
sprint: 15
source: PM sprint-15 reconciliation, 2026-08-04. **Not in any commit message** — found by diffing
  `BusinessErrorMessage.cs` across `37440bbc..HEAD` and checking the three partner-facing catalogs.
---

## Context

`077b7e8a` split the take gate's two terminal-state refusals off the customer keys, and the reasoning in
the constant's own doc comment is right:

> ADR-0037 CH-X1: *if two personas need different sentences for one key, the backend emits two keys.*
> ADR-0037's gate made the customer keys newly reachable by a cleaner, and iOS resolves every `error.*`
> string from the one shared `CleansiaCore` catalog — so a cleaner tapping a dead job read the
> customer's sentence (*"This booking is already cancelled"*), in a register and with a noun that are
> wrong for them.

So `BusinessErrorMessage.cs` gained:

```csharp
public const string TakeOrderAlreadyCancelled = "order.take.already_cancelled";
public const string TakeOrderAlreadyCompleted = "order.take.already_completed";
```

**At HEAD — i.e. in committed state — only one of the three partner clients can render them.** Verified
2026-08-04 against the committed tree:

| client | state **at HEAD (committed)** | state **in the working tree** |
|---|---|---|
| Android partner | ✅ `error_order_take_already_cancelled` + `_completed` in **all five** locales (`values`, `values-cs`, `values-sk`, `values-uk`, `values-ru`) | ✅ unchanged |
| **partner web** | ❌ `api.order` had no `take` node at all (31 keys, none of them `take`) | ✅ `api.order.take.{already_cancelled, already_completed}` present in **all five** bundles |
| **iOS `CleansiaCore`** | ❌ zero occurrences of `error.order.take.*` (`error.order.already_cancelled` / `_completed` and `error.order.not_takeable` were all present, so the catalog was otherwise current) | ✅ both keys present |

> **Read this before starting.** The gap is real **at HEAD** and the fix is **already in the working
> tree, uncommitted**, from a live web lane. That is why this ticket is `in_progress` rather than
> `ready` — the work has an owner and starting a second instance on it would collide on the same five
> i18n bundles. What this ticket adds is the **ACs that lane was not briefed on**: the verbatim-from-
> Android rule (AC3), the register check (AC4), the parity-guard question (AC5), and iOS partner
> coverage (AC6). The PM's job here is to make sure the diff is gated, not to re-do it.

The consequence is precisely the one the split was made to prevent, one layer over: a cleaner on partner
web or on iOS who taps a job that has just been cancelled or completed gets the interceptor's **generic**
message — *"An error occurred. Please try again"* — and tries again, and fails again.

This is the same defect `26c5274` fixed for `order.weekly_limit_reached` twelve days into the same
sprint: a key thrown by `TakeOrder` and absent from all five partner-web locales, swapped by the
interceptor for the generic message. The partner parity guard that commit added exists precisely to
catch this class — **it should have caught this**, and part of this ticket is finding out why it did not.

## Acceptance criteria

- [ ] **AC1 — partner web renders both keys in all five locales.** Given
      `apps/cleansia-partner.app/src/assets/i18n/{en,cs,sk,uk,ru}.json`, When they are read, Then
      `api.order.take.already_cancelled` and `api.order.take.already_completed` exist in each.
      ⚠️ **Under `api.*`, not `errors.*`** — the shared `HttpErrorInterceptorFn` resolves
      `` `api.${dotValue}` `` and all three apps register it (established in `8ff9dfb4`; `errors.*` is a
      legacy admin path read only by per-feature key maps).
- [ ] **AC2 — iOS renders both keys in all five locales.** Given the catalog APNs/error resolution uses,
      When the keys are added, Then every unit state is `translated` — not `new`, not `needs_review`.
- [ ] **AC3 — no copy is invented.** Given Android partner already ships cleaner-facing wording for both
      keys in all five languages, When web and iOS are written, Then they match it **verbatim per
      locale**. This is the rule `26c5274` and `c968cbf9` both followed, and `c968cbf9` shows the cost of
      not following it: an interrupted iOS lane wrote 70 values in its own voice and **30 of them had to
      be re-sourced on a second pass**.
- [ ] **AC4 — the sentence is a cleaner's, not a customer's.** Given the whole point of the split, When
      the copy is read, Then it addresses someone who tried to **take** a job — not someone whose
      **booking** was cancelled. If Android's own wording fails that test, fix Android too and say so.
- [ ] **AC5 — the guard that missed this is fixed or its blind spot is recorded.** Given the partner
      `error-contract-parity.spec.ts` added by `26c5274`, When it is run against HEAD, Then it either
      **fails on these two keys** (in which case this ticket makes it pass) or it does not — in which
      case the status log records **why**, because a parity guard that cannot see a brand-new backend key
      is worth less than the number of keys it does cover. **Do not add the keys to a
      `PENDING_TRANSLATION` list** — that list is a ratchet for keys we have decided not to translate
      yet; these are being translated now.
- [ ] **AC6 — iOS has no partner-side twin gap.** Given `error.order.not_takeable` is already in the
      shared catalog, When the two new keys are added, Then confirm no other `order.take.*` key exists
      server-side and is missing. One grep, recorded.
- [ ] **AC7 — web Jest suites and the three builds pass; `swiftformat` then `swiftlint` are clean.**
      ⚠️ Order matters and a red "SwiftLint" in CI is often SwiftFormat.

## Out of scope

- The customer-facing `order.already_cancelled` / `order.already_completed` keys. They are correct and
  still used by `CancelOrder`, `AdminCancelOrder` and `AdminOverrideOrderStatus`.
- The three wider keys `26c5274` reported and did not fix (`country.not_serviced`,
  `gdpr.consent_not_found`, `gdpr.consent_already_granted`) — still on the PENDING_TRANSLATION ratchet,
  still rendering generic, still reachable from partner web. **Not widened into here**; they need their
  own ticket if they are not already on one.
- Admin. There is no admin take path.

## Implementation notes

**Backend source of truth:** `src/Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs` — the doc
comment above the two constants names `TakeOrder` as the **sole intended emitter** and explains that the
`order.take.*` segment is what keeps that true.

**Android source of copy:** `src/cleansia_android/partner-app/src/main/res/values*/strings.xml:1102-1103`
and their four locale siblings.

**Archetype:** `agents/knowledge/consistency.md` — the error-key/translation contract, one namespace per
client (see also the memory note on backend error keys → client namespaces: NSwag throws `ProblemDetails`
**bare**, so reading `.result` alone resolves nothing).

**No-decision note:** the key names, the persona split and the wording register were all decided by
ADR-0037 CH-X1 and shipped in `077b7e8a`. This is applying them to the two clients that were missed. No
panel.

## Status log
- 2026-08-04 — created by pm during the sprint-15 reconciliation. **PM-found, not carried from a commit
  message:** `077b7e8a` is the last backend commit on the branch and its i18n consequence landed **after**
  the sprint's two i18n sweeps (`8ff9dfb4` web, `befbb7af` Android) had already run — so web and iOS were
  never given the keys.
- 2026-08-04 — **filed at `in_progress`, not `ready`, after a second check.** The finding was established
  against **HEAD**; re-checking the **working tree** showed a live web lane has already added all five web
  bundles and the iOS `CleansiaCore` catalog. Filing it `ready` would have invited a second instance into
  the same five files. **The distinction is deliberate and is the point of the reconciliation: a citation
  is only true against the tree state you name.** Remaining and unverified either way: **iOS partner**
  bundle coverage (AC6) and the parity-guard blind spot (AC5).

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
