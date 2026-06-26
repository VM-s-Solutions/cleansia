---
id: T-0332
title: iOS booking-flow design checkpoint — send Bearer on dual-use Order/Payment endpoints when a session exists (withhold only for true guest)
status: draft
size: S
owner: pm
created: 2026-06-26
updated: 2026-06-26
depends_on: [T-0313]
blocks: []
stories: []
adrs: [0013]
layers: [ios]
security_touching: false
manual_steps: []
sprint: 12
source: AUDIT-2026-06-26-ios-phase0-foundation F2
---

> **Deferred — NOT to be implemented now.** Logged from the 2026-06-26 adversarially-verified iOS Phase 0
> foundation audit (F2). **Low / latent design checkpoint, fully dormant** — no iOS feature code calls these
> endpoints yet (only the `AnonymousAllowList` and its test reference them). This is a **design decision to
> honor when the customer booking flow is ported**, NOT a standalone bug. Its suggested home is the customer
> booking-wizard ticket **T-0313** — attach this as an explicit acceptance criterion there. **No-decision
> note (panel skipped):** the allow-list itself is intentional per ADR-0013 §D4.4 / header-parity-contract
> §3 (in force, untouched); this only records the dual-use-when-signed-in rule the booking port must honor.

## Context

The 2026-06-26 iOS Phase 0 foundation audit (`audits/AUDIT-2026-06-26-ios-phase0-foundation.md`, F2) flagged
a latent design checkpoint on the customer anonymous allow-list. The customer allow-list
(`src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Auth/AnonymousAllowList.swift:28-39`,
`customerGuestBooking`) **deliberately** adds the guest-booking surface — `/api/order/createorder`,
`/api/order/quote`, `/api/order/lookup`, `/api/payment/createorder`, `/api/referral/validate`, the
`*getoverview` catalogues — to the no-Bearer set, and `HeaderAdapter.swift:29` withholds `Authorization`
whenever the path is on the allow-list **regardless of a present token**. That is correct and intentional
per **ADR-0013 §D4.4** and **header-parity-contract §3** for the genuine **guest** path.

The catch is **dual-use**: a signed-in customer also hits the same `/api/Order/CreateOrder` and
`/api/Payment/CreateOrder` for in-app booking, and the backend `CreateOrder` reads
`GetUserId() ?? string.Empty`. A naive booking-flow port would therefore send these calls with **no Bearer
even when a session exists**, and the server would silently create a **guest / empty-`UserId` order**
instead of associating it with the signed-in user.

**DISPUTED in the audit** (one verifier: real medium contract-parity issue; the other: deliberate documented
design) → reconciled as a **design decision to make at booking-port time, not a current bug.** Fully latent:
no iOS feature code calls these endpoints yet.

## Acceptance criteria
> These attach to the booking-wizard work (T-0313). Carry them as ACs there when it is dispatched.

- [ ] **AC1 — Bearer sent on dual-use endpoints when authed.** When a non-expired session token exists, the
  customer booking flow's `/api/Order/CreateOrder` and `/api/Payment/CreateOrder` requests carry the
  `Authorization: Bearer …` header so the server associates the order with the signed-in user (not an
  empty-`UserId` guest order).
- [ ] **AC2 — True guest path still no-Bearer.** With no session token, the same endpoints are sent with
  **no** `Bearer` (the server's `[AllowAnonymous]` permits the genuine guest case) — the existing
  guest-booking behavior is preserved.
- [ ] **AC3 — Test asserts the authed customer carries Authorization.** A test asserts
  `/api/Order/CreateOrder` (and `/api/Payment/CreateOrder`) **with a stored token** carries `Authorization`
  for the **customer host**, and **without a token** does not (TC-IOS-ANON extended for the dual-use case).
- [ ] **AC4 — Decision recorded against the contract.** The chosen mechanism (e.g. a "send-Bearer-if-token,
  even on allow-list, for the dual-use order/payment paths" rule) cross-references ADR-0013 §D4.4 and
  header-parity-contract §3 so the allow-list's single-source intent is preserved and the dual-use carve-out
  is explicit (not an accidental allow-list edit).

## Out of scope
- **Do NOT blanket-remove the `customerGuestBooking` entries** — they are correct for the genuine guest
  path (ADR-0013 §D4.4 / header-parity-contract §3). This is a per-request "attach Bearer iff a session
  exists" carve-out for the dual-use endpoints, not an allow-list deletion.
- **No change to the partner allow-list** (no guest-booking surface there).
- **No `DeviceIdProvider` / Keychain change** — that is the separate F1 / T-0331.
- **No new backend contract change** — the backend already accepts both authed and anon on these endpoints.

## Implementation notes
Files: `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Auth/AnonymousAllowList.swift:28-39`
(`customerGuestBooking`) + `HeaderAdapter.swift:29` (the `isAnonymous` Bearer-withhold). The decision to
make when the booking flow is ported: **send the Bearer when a session token exists** (the server's
`[AllowAnonymous]` still permits the genuine guest no-token case), and withhold it only for true guest
calls. One clean option is a "dual-use" carve-out set the `HeaderAdapter` consults — paths that are on the
anon allow-list (so a tokenless guest sends no Bearer) **but** still attach the Bearer when a token is
present. Read ADR-0013 §D4.4 + header-parity-contract §3 (reviewer check #4 — anon allow-list complete
incl. customer host) first so the change preserves the single-source intent. Reviewer-per-developer; the
reviewer verifies check #4 is not regressed. No standalone `security` gate is added by this checkpoint, but
note the **parent booking ticket T-0313 already carries a security gate** (card/payment flow) — verify this
AC under it. No `optimizer`.

**Routing:** carried as an AC on T-0313 `[ios]` (developer + concurrent reviewer); QA = the authed-carries /
guest-withholds Authorization assertions on the customer host.

## Status log
- 2026-06-26 — draft (created by pm). Registered from `audits/AUDIT-2026-06-26-ios-phase0-foundation.md`
  F2 (adversarially-verified iOS Phase 0 foundation audit; the finding was **DISPUTED** → reconciled as a
  booking-time design checkpoint, not a current bug). Dedup-checked: not an existing INDEX ticket or prior
  audit finding. The affected booking endpoints are exercised by the **proposed** customer booking-wizard
  ticket **T-0313** (sprint-12 / Wave-10), this ticket's suggested home — `depends_on: [T-0313]` so the
  checkpoint is resolved as part of (or immediately alongside) the booking port. **Deferred — not for
  implementation now** (low/latent, dormant; no iOS feature code calls these endpoints yet). DoR deferred
  until dispatch — sized **S** (a header-layer carve-out + a test, or folded entirely into T-0313's ACs);
  `layers: [ios]`; `security_touching: false` (the carve-out adds Bearer; the gate lives on T-0313);
  `manual_steps: []`. No panel (no-decision: the allow-list is ratified per ADR-0013 §D4.4 — this records
  the dual-use rule, no new decision).

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
