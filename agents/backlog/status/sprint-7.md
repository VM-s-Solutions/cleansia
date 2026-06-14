# Sprint 7 — Wave 5 plan (priority bugs + consistency/quality sweep)

- **Date:** 2026-06-13
- **Goal:** Sequence and execute **Wave 5**: the two folded-front production bugs **T-0245 / T-0246**
  first, then the consistency/quality sweep **T-0196…T-0206** (the original Wave-5 core), plus the 3
  Wave-4 follow-ups **T-0242 / T-0243 / T-0244** fitted into the sweep where they belong.
- **Status:** **READY — promoted 2026-06-13.** Owner gave the GO on Wave 5 with an explicit instruction:
  fold T-0245 + T-0246 to the FRONT (fix first). Batch breakdown below.
- **Branch:** all Wave-5 work goes on **one feature branch `feature/wave-5-consistency-bugs`** cut from
  current `master` (**`ee95a57f`**, Wave 4 / PR #77), committed **batch-by-batch**. PM never merges; the
  PR to `master` is the owner's call.

---

## 0. Pre-flight reconciliation (verified against the repo, 2026-06-13)

- **Wave 4 merged:** `master` tip = **`ee95a57f`** ("Feature/wave 4 tests a11y (#77)"). All Waves 0–4
  are `done`. Every Wave-5 ticket's `depends_on` resolves against merged reality (table in §1).
- **Wave-4 left a green safety net Wave 5 leans on hard:**
  - **T-0212** (CreateOrder characterization, 20 cases) — the **acceptance gate** for T-0199/AUD-06.
  - **T-0210** (webhook integration suite, `Cleansia.IntegrationTests/Features/Payments/Webhooks/`) — the
    home for T-0245's new **non-null-tenant** webhook test.
  - **T-0215** (cross-tenant/cross-user write-path, `Cleansia.HostTests` `Ac*` family) — the suite that
    reproduced T-0246; its `Ac14` happy-leg can finally go end-to-end once T-0246 lands.
  - **T-0211** (`CancellationFeeRateBoundaryTests`) — pins the behavior T-0242 must re-flip.
  - **T-0213** (`EmployeeInvoiceEntityTests`) — pins the within-run determinism T-0244 extends to
    cross-invocation.
- **ID-collision fixed at intake:** two ticket files both claimed `id: T-0200` — the AUD-07 order-wizard
  ticket (`T-0200-aud-07.md`, created 2026-06-01) and the dispute-guard `check-consistency` rule
  (`T-0200-da-2-followup.md`, created 2026-06-09). The later follow-up was **re-id'd `T-0200 → T-0247`**
  (file slug unchanged). The AUD-07 file keeps the canonical `T-0200`.
- **`sprint:` frontmatter** re-tagged to `5` on the swept tickets (was `3` on the T-0196…T-0206 set, the
  original audit numbering).
- **Carried owner items still standing (unchanged):** T-0159 rotate-mapbox-token (live exposure);
  customer nswag-regen (`DisputeReason.Chargeback` + device endpoints) — **this one likely also unblocks
  T-0202**; IMP-1 Google OAuth ClientId; DE/AT/ES fiscal go-live gates; T-0236 (multi-tenant token-revoke
  asymmetry) **must land before multi-tenant onboarding, alongside T-0245**.

---

## 1. Scope — the Wave-5 tickets (16: 2 bugs + 11 sweep + 3 follow-ups)

L-sized epics are **NOT promoted to `ready`** — they stay `draft` and the PM splits them at batch
dispatch (the lifecycle "no L runs" rule). Their child S/M tickets get reviewer-per-developer like any
other.

| ID | Title (short) | Size | Batch | Layers | depends_on (✓=done) | status | sec gate | manual_step |
|----|---------------|------|-------|--------|---------------------|--------|----------|-------------|
| **T-0245** ⚠️ GO-LIVE BLOCKER | Multi-tenant webhook validator/handler tenant-scope mismatch | M | **5A** | backend | T-0210✓ | **ready** | **YES** | — |
| **T-0246** | StartOrder handler NRE→500 on load divergence | S | **5A** | backend | T-0215✓ | **ready** | no | — |
| **T-0243** | CheckoutSession `nameof(Command)`→`nameof(userId)` B5 | XS | **5B** | backend | T-0179✓ | **ready** | no | — |
| **T-0244** | `GenerateVariableSymbol` deterministic stable hash | S | **5B** | backend | T-0213✓ | **ready** | no (money-adv.) | ef-migration *(only if persist-path chosen)* |
| **T-0205** | Remove dead/unsafe code (Handlebars/SendGrid/FCM/scrap) | S | **5B** | backend, mobile | T-0144✓ | **ready** | no | — |
| **T-0206** | S6 logging hygiene (no PII/secrets in logs) | S | **5B** | backend, functions | — | **ready** | no (sec-advisory) | — |
| **T-0203** | LG/DA/IA long tail (B5/B1/CQRS/magic-strings/swallowed catch) | M | **5B** | backend, frontend | T-0142✓(children) | **ready** | no | nswag-regen *(verify)* |
| **T-0242** | Cancellation-fee Plus free-window override direction | S | **5B** | backend | T-0211✓ | **blocked** (Q-W5-1) | no (money-adv.) | — |
| **T-0196** | Mechanical consistency canonicalization sweep (A*/B1/B3/C*/E1/E2) | **L** | **5C** | backend, frontend, android | — | **in_progress (SPLIT → T-0248..T-0252)** | no | nswag-regen *(B1/T-0249 watch)* |
| **T-0198** | De-triplicate Dispute/SavedAddress/Auth controllers + login/forgot facades | M | **5E** | backend, frontend | — | **ready** | no (sec-advisory) | — |
| **T-0201** | Decompose AddSavedAddress god-method + B9 mapper de-triplication | M | **5E** | backend | T-0150✓ | **ready** | no | — |
| **T-0199** | AUD-06: Decompose CreateOrder god-handler | **L** | **5D** | backend | T-0118✓, T-0212✓ | **in_progress (SPLIT → T-0253..T-0255)** | no | — |
| **T-0200** | AUD-07: Split order-wizard god-facade + C3 pipe | **L** | **5F** | frontend | T-0196 | **in_progress (SPLIT → T-0256..T-0258)** | no | — |
| **T-0202** | Customer disputes → own client + cleansia-table/form/error archetype | M | **5F** | frontend | T-0196 | **blocked** (T-0196 + regen-verify) | no | nswag-regen *(verify)* |
| **T-0204** | PERF cluster: indexes, tracked reads, eager Includes, projection-before-order | M | **5G** | backend, db | T-0142✓, T-0196 | **blocked** (T-0196) | no (optimizer) | **ef-migration** |
| **T-0247** | check-consistency rule: direct Dispute.Close/Escalate/Resolve allowlist | S | **5G** | backend, tooling | T-0172✓, T-0174✓ | **ready** | yes (guards state machine) | — |
| **T-0197** | Migrate customer-app repos to `ApiResult<T>` (mobile) | **L** | **5H** (defer-candidate) | architect, android, ios | — | **draft (split, ADR-first)** | no | — |

**Counts:** ready **9** (T-0245, T-0246, T-0243, T-0244, T-0205, T-0206, T-0203, T-0198, T-0201, T-0247 →
**10** actually), draft-split-required **4** (T-0196, T-0199, T-0197 + the AUD-07 file's L-split),
blocked **3** (T-0242 on Q-W5-1; T-0200/T-0202/T-0204 on T-0196). (T-0200 is L *and* blocked.)

---

## 2. Batches, parallelism, shared-file lanes

**Strict wave-internal ordering only where a real edge or shared file forces it.** The two bugs run
first and alone-enough to ship a hotfix; the sweep then fans out by shared-file lane.

### Batch 5A — the two folded-front production bugs. **FIRST. 2 tickets, parallel.**
T-0245 and T-0246 touch **disjoint files** — no collision, run concurrently.

| Ticket | Dev | Reviewer | Extra gates | Lane / files |
|---|---|---|---|---|
| **T-0245** (M, GO-LIVE BLOCKER) | backend | yes (concurrent) | **Security gate (mandatory)** + qa | `HandlePaymentNotification.Validator` + `IOrderRepository`/`BaseRepository.ExistsAsync` (add tenant-ignoring existence read) + new **non-null-tenant** test in `Cleansia.IntegrationTests/.../Webhooks/`. **Lane I1** (shared IntegrationTests fixtures) — but T-0246 is in `HostTests`/unit, no overlap. |
| **T-0246** (S) | backend | yes | qa | `StartOrder.cs` (null-guard + reconcile handler load with validator) + regression test. Disjoint from T-0245. |

Recommended dispatch: **5A first**, both in parallel, each with its reviewer; T-0245 also gets the
Security gate (adversarial: the tenant-ignoring read must bind the write to the **order's own** tenant,
never widen the surface). A hotfix PR for 5A alone is the owner's option after both are green.

### Batch 5B — independent backend/functions micro-fixes + the long tail. Fan out, with 2 serialized lanes.
All can run after (or alongside) 5A. **Two intra-batch serialization lanes:**
- **Lane M-Membership** — `CreateMembershipCheckoutSession.cs` is edited by **both T-0243** (B5 rename)
  and **T-0203** (LG-05 B5 across the membership handlers). **Serialize T-0243 → T-0203** (or fold the
  T-0243 one-liner into T-0203's membership pass — PM's call at dispatch); never concurrent.
- **Lane BookingPolicy** — **T-0242** (when unblocked) is the sole editor of `BookingPolicy.cs` +
  `CancellationFeeRateBoundaryTests.cs`. No other 5B ticket touches it.

| Ticket | Dev | Reviewer | Extra gates | Parallel with |
|---|---|---|---|---|
| T-0243 (XS) | backend | yes | qa-light | 5B (Lane M-Membership: before T-0203) |
| T-0203 (M) | backend (+ promo facade) | yes | qa; **nswag-regen verify** (AC4 SendSitewidePromo Response, device/membership error shapes) | 5B (Lane M-Membership: after T-0243) |
| T-0244 (S) | backend | yes | **adversarial money review** (fiscal ref); ef-migration *only if persist-path* | all of 5B (edits `EmployeeInvoice.cs`) |
| T-0205 (S) | backend ∥ mobile | yes | qa-light | all of 5B (4 disjoint surfaces) |
| T-0206 (S) | backend (+ functions) | yes | **Security advisory** (S6) + qa | all of 5B |
| T-0242 (S) | backend | yes | **adversarial money review** | **HELD on Q-W5-1**; unblocks into 5B Lane BookingPolicy |

### Batch 5C — the mechanical consistency sweep (T-0196, **L → split first**). The base layer.
T-0196 is the **dependency for 5D-adjacent consumers** (T-0200, T-0202, T-0204 build on its C1 base /
B1 `UpdateDisputeStatus` wrap). The PM splits it into independent cluster sub-streams, run as disjoint
lanes with a reviewer per developer:
- **5C.A** — A* backend paged-query (PromoCodes/Referrals/PayConfigs/Services) — disjoint Features folders.
- **5C.B** — B1 backend Response-wrap (CreateDispute/UpdateDisputeStatus/DeleteSavedAddress). **NSwag
  watch:** prefer wire-compatible Response shapes; if a generated response *type* changes, add
  `manual_step: nswag-regen` and hold consumers.
- **5C.C** — B3 backend validator-base (PayConfig/PayPeriod/Employee/CurrentUser validators).
- **5C.D** — C* customer frontend facades (order-wizard/recurring/rewards/fiscal-failures/invoices/
  partner-orders) — **excludes `disputes.facade.ts`** (owned by T-0202).
- **5C.E** — E1/E2 Android sealed states (partner + customer ViewModels).
The 5 sub-streams touch disjoint files and run **concurrently**; serialize only if any two land in the
same file. Each is S/M with its own characterization test (behavior-unchanged net).

### Batch 5D — AUD-06 CreateOrder decomposition (T-0199, **L → split**). **RUNS ALONE on `CreateOrder.cs`.**
**The big, risky, lane-isolated ticket.** Split into (a) address-resolution+serviced-area collaborator,
(b) promo-application collaborator, (c) payment-side-effect dispatcher + late-referral + handler
slim-down — each landing under the **T-0212 characterization net**. **Acceptance gate: T-0212 stays green
unchanged through every sub-step.** **No other ticket touching `CreateOrder.cs` runs concurrently** (and
T-0204's loyalty read-variant, if it needs a call-site touch in `CreateOrder.cs`, serializes behind 5D).
5D can run in parallel with 5B/5C/5E since none of those edit `CreateOrder.cs`.

### Batch 5E — de-triplication + AddSavedAddress decomposition. 2 tickets, separate lanes.
- **T-0198** — exclusive surface (3 login handlers, 5 AuthControllers, Dispute/SavedAddress controllers
  ×3, 4 login/forgot facades). **Security advisory** (auth surface + admin-password-complexity
  alignment + 2 swallowed-error bug fixes). Must NOT touch host auth registration (BSP-1/T-0100) or
  `disputes.facade.ts` (T-0202).
- **T-0201** — SavedAddress **feature handlers** (AddSavedAddress/UpdateSavedAddress/GetSavedAddresses +
  new Mappers). **Lane SavedAddress:** T-0198 touches the SavedAddress **controllers**, T-0201 the
  **handlers/mappers** — disjoint files, but both in the SavedAddress area; keep them in one lane and do
  not let them race on a shared file. T-0203's device/GDPR handlers are disjoint from both.

### Batch 5F — the two frontend god-unit/archetype rebuilds. **After 5C (T-0196) lands.** 2 tickets.
- **T-0200** (AUD-07, L → split) — sole editor of `libs/cleansia-customer-features/order-wizard/**`.
- **T-0202** (M) — sole editor of `libs/cleansia-customer-features/disputes/**`; **gated on the customer
  nswag-regen verify** (the dev confirms the customer client emits the dispute DTOs/enums; if missing,
  hold on the owner regen — likely the same outstanding Wave-3 customer regen).
Disjoint feature folders → T-0200 ∥ T-0202 once both are unblocked.

### Batch 5G — the perf cluster + the tooling rule. **After 5C (T-0196) lands** (PERF-D2 rebases on the
B1 `UpdateDisputeStatus`). 2 tickets.
- **T-0204** (M) — **carries `ef-migration`** (4 indexes, owner builds CONCURRENTLY; held at the
  migration boundary). **Optimizer gate** (PERF-IDA-02 critical + LG-PERF-01 booking hot path). AC6
  GdprRequest order-before-page is a **latent-correctness fix → flag to owner**. Internal fan-out: one
  dev+reviewer per repo group (dispute / loyalty-referral / user-identity / address+membership-index).
- **T-0247** (S) — `check-consistency.mjs` rule (Dispute state-machine writer allowlist).
  `security_touching: true` (guards the T-0172 transition graph). Tooling-only; parallel with T-0204.

### Batch 5H — the mobile `ApiResult<T>` migration (T-0197, **L → split, ADR-first**). **DEFER-CANDIDATE.**
Lowest priority: a large cross-app mobile refactor, **no go-live/money pressure**, needs an architect ADR
+ a `:core` type move + one serial child per customer-app repo. **Recommend deferring to a Wave-6 mobile
slice** (owner sequencing call, §4). If kept in Wave 5, it runs last and entirely in its own lane.

### Commit cadence
One commit per batch on `feature/wave-5-consistency-bugs` (5A may land as its own early commit / hotfix
PR option). PM never merges; the PR to `master` is the owner's call.

---

## 2.1 L-epic splits — child tickets (PM, 2026-06-13, autonomous Wave-5 run)

The three L-epics the lifecycle forbids running un-split are now **`in_progress` [SPLIT/EPIC] tracking
tickets**; each is `done` only when all its children are `done`. **11 child tickets created, T-0248…T-0258**
(highest pre-existing id was T-0247). Every child is S or M, independently shippable/reviewable, with a
reviewer per developer and the parent's AC carved in.

### T-0196 (5C consistency sweep base) → 5 children — **disjoint, concurrent**
| Child | Scope | Size | Status | depends_on | Lane / serialize | Layers | manual_step |
|---|---|---|---|---|---|---|---|
| **T-0248** | 5C.A A* canonical paged-query (PromoCodes/Referrals/PayConfigs/Services) | M | ready | — | disjoint Features folders | backend | — |
| **T-0249** | 5C.B B1 Response-wrap (CreateDispute/UpdateDisputeStatus/DeleteSavedAddress) — `blocks: [T-0202]` | S | ready | — | Disputes/SavedAddresses folders; SavedAddress area = one lane (vs T-0198/T-0201) | backend | **nswag-regen — conditional** (wire-compatible default; flag only if response *type* changes) |
| **T-0250** | 5C.C B3 validator-base composition (PayConfig/PayPeriod/Employee/CurrentUser) | S | ready | — | disjoint Features folders | backend | — |
| **T-0251** | 5C.D C* customer/partner/admin facades — **EXCL `disputes.facade.ts`** — `blocks: [T-0200]` | M | ready | — | facade `.ts` in disjoint feature folders; **must NOT touch `disputes.facade.ts`** (T-0202 lane) | frontend | — |
| **T-0252** | 5C.E E1/E2 sealed Android UiState + shared ActionState | M | ready | — | disjoint ViewModel files; shared `:core` `ActionState.kt` edit (if any) serializes | android | — |

### T-0199 (5D AUD-06 CreateOrder) → 3 children — **SERIAL a→b→c, lane-isolated on `CreateOrder.cs`**
Each child's AC pins **T-0212's CreateOrder characterization suite green and UNMODIFIED**. The Cash-branch
enqueue is preserved as the **post-commit dispatch / outbox seam (T-0118 / ADR-0002)**, not a raw
`IQueueClient.SendAsync` (explicit in T-0255 AC4).
| Child | Scope | Size | Status | depends_on | Lane | Layers | manual_step |
|---|---|---|---|---|---|---|---|
| **T-0253** | AUD-06a address-resolution + serviced-area collaborator | M | ready | T-0118✓, T-0212✓ | `CreateOrder.cs` (sole writer) + Orders DI | backend | — |
| **T-0254** | AUD-06b promo preview/apply collaborator | M | blocked (T-0253) | T-0118✓, T-0212✓, T-0253 | `CreateOrder.cs` (serial after a) | backend | — |
| **T-0255** | AUD-06c payment-side-effect dispatcher + late-referral + slim handler (**preserves outbox seam**) | M | blocked (T-0254) | T-0118✓, T-0212✓, T-0254 | `CreateOrder.cs` (serial after b) | backend | — |

### T-0200 (5F AUD-07 order-wizard) → 3 children — **SERIAL a→b→c, sole editor of `order-wizard/**`**
The C1 `UnsubscribeControlDirective` base dependency rides the **first** child via **T-0251** (5C.D). Each
child is under a characterization Jest spec written first; behavior-identical → no nswag-regen.
| Child | Scope | Size | Status | depends_on | Lane | Layers | manual_step |
|---|---|---|---|---|---|---|---|
| **T-0256** | AUD-07a quote/pricing collaborator + C3-migrate stream | M | blocked (T-0251) | T-0251 | `order-wizard/**` (sole editor) | frontend | — |
| **T-0257** | AUD-07b promo+referral + city-serviced collaborators + drop `firstValueFrom` | M | blocked (T-0251, T-0256) | T-0251, T-0256 | `order-wizard/**` (serial after a) | frontend | — |
| **T-0258** | AUD-07c saved-address + slim facade (step-nav + submit) + C1/C3 submit branches | M | blocked (T-0251, T-0257) | T-0251, T-0257 | `order-wizard/**` (serial after b) | frontend | — |

---

## 2.2 Dependency-ordered DISPATCH PLAN for the remaining wave (revised post-split)

5A (T-0245/T-0246) is **DONE / committed `3df53ab2`**. Remaining batches in dispatch order. "∥" = parallel,
"→" = serial. Reviewer-per-developer on **every** ticket (omitted per-row for brevity).

| # | Batch | Tickets (agent · gates) | Parallelism / lanes | Depends on | manual_step |
|---|---|---|---|---|---|
| 1 | **5B — backend micro-fixes + long tail** | **T-0243** (backend · qa-light) → **T-0203** (backend+promo-facade · qa, **nswag-regen verify**) *[Lane M-Membership, SERIAL — both edit `CreateMembershipCheckoutSession.cs`]* · **T-0244** (backend · **adversarial money review**) · **T-0205** (backend ∥ mobile · qa-light) · **T-0206** (backend+functions · **Security advisory S6** + qa) · **T-0242** (backend · adversarial money review — **HELD**) | Fan out; 2 serial lanes: **M-Membership** (T-0243→T-0203), **BookingPolicy** (T-0242 sole editor). T-0244 edits `EmployeeInvoice.cs`; T-0205 = 4 disjoint surfaces. | none (5A done) | T-0203 nswag-regen* (verify); T-0244 ef-migration* (only if persist-path); **T-0242 BLOCKED on Q-W5-1** |
| 2 | **5C — consistency sweep base** | **T-0248** (backend) ∥ **T-0249** (backend) ∥ **T-0250** (backend) ∥ **T-0251** (frontend) ∥ **T-0252** (android) | **All 5 concurrent** (disjoint files). T-0251 must NOT touch `disputes.facade.ts`. SavedAddress area shared lane note (T-0249 vs T-0198/T-0201). | none | T-0249 nswag-regen* (conditional — flag only on response-type change) |
| 3 | **5D — AUD-06 CreateOrder (ALONE on `CreateOrder.cs`)** | **T-0253** → **T-0254** → **T-0255** (backend · optimizer-eligible hot path) | **SERIAL a→b→c.** No other `CreateOrder.cs` writer. **Runs ∥ 5B/5C/5E** (none edit `CreateOrder.cs`). | T-0118✓, T-0212✓ (both done) — **can start immediately** | none |
| 4 | **5E — de-triplication + AddSavedAddress** | **T-0198** (backend+frontend · **Security advisory** — auth surface) ∥ **T-0201** (backend) | Separate lanes. **SavedAddress area = one lane** (T-0198 controllers vs T-0201 handlers/mappers vs T-0249 DeleteSavedAddress command — disjoint files, do not race). T-0198 must NOT touch host auth registration (BSP-1/T-0100) or `disputes.facade.ts`. | none (independent) — **can start immediately** | none |
| 5 | **5F — frontend rebuilds (AFTER 5C)** | **[T-0256→T-0257→T-0258]** (frontend, AUD-07 serial) ∥ **T-0202** (frontend · **regen-verify**) | T-0200 children serial on `order-wizard/**`; **∥ T-0202** (disjoint `disputes/**`). Both downstream of 5C. | **T-0251** (5C.D) for the AUD-07 chain; **T-0249** (5C.B) + customer-client regen-verify for T-0202 | T-0202 nswag-regen* (customer client — verify first; likely the outstanding Wave-3 customer regen) |
| 6 | **5G — perf cluster + tooling (AFTER 5C)** | **T-0204** (backend+db · **Optimizer gate**, internal fan-out per repo group) ∥ **T-0247** (backend+tooling · **Security advisory** — guards state machine) | Parallel. T-0204 rebases its dispute `GetForUpdateAsync` on the **T-0249 B1 `UpdateDisputeStatus`** wrap. | **T-0249** + **T-0251** (the T-0196 C1/B1 base for PERF-D2 rebase); T-0142✓ | **T-0204 ef-migration** (4 indexes, CONCURRENTLY — owner; held at AC8 boundary) |
| 7 | **5H — mobile `ApiResult<T>` (T-0197, L→split, ADR-first) — DEFER-CANDIDATE** | **T-0197** (architect ADR → `:core` type move → one serial child per customer-app repo) | Own lane, last. | — | — (recommend **defer to Wave 6** per §4.2) |

**Batch dependency edges (which blocks which):**
- **5C must complete before 5F and 5G.** Specifically: **T-0251 → the AUD-07 chain (T-0256/7/8)**;
  **T-0249 → T-0202** (rebase on `UpdateDisputeStatus` Response) **and → T-0204** (PERF-D2 rebase);
  **T-0251 → T-0204** (C1 base, where a frontend touch is needed — backend-only PERF parts can start once
  T-0249 lands).
- **5B / 5C / 5D / 5E are mutually independent** and can all be dispatched concurrently (disjoint
  file surfaces: micro-fixes/long-tail vs consistency clusters vs `CreateOrder.cs` vs auth/saved-address
  controllers+handlers). The only cross note is the **SavedAddress area lane** spanning T-0249 (5C),
  T-0198 + T-0201 (5E) — disjoint files, kept in one lane so no two race on a shared file.
- **5D is internally serial** (T-0253→T-0254→T-0255) and **lane-isolated**; it parallelizes with all of
  5B/5C/5E at the batch level.
- **T-0242 stays BLOCKED on Q-W5-1** (owner product decision) — it does not gate any other ticket; the
  rest of 5B proceeds.

**Recommended concurrent dispatch wave-internal:** {5B, 5C, 5D, 5E} fan out together → then {5F, 5G} once
their 5C predecessors (T-0249/T-0251) are `done` → 5H deferred.

---

## 3. Stale-ticket-text deltas (merged Waves-1…4 reality the implementing agents MUST be told)

The sweep ticket bodies were written 2026-06-01 (the bug + follow-up tickets 2026-06-13). Corrections:

- **T-0245:** (1) `HandlePaymentNotification.cs` was changed by **T-0174** (chargeback
  `LinkStripeDispute`) — cited line refs are stale; re-derive. (2) The dispatch substrate is the
  **durable outbox** (T-0157/T-0158) + **ADR-0010** in force alongside ADR-0002 — AC2's "effects fire
  once" must assert at the **current** post-commit dispatch seam (outbox rows), not a raw
  `IQueueClient.SendAsync`. (3) The **T-0210** suite **exists** and deliberately seeds single-tenant to
  dodge this bug — extend it to seed a **non-null-tenant** order; consume `PostgresContainerFixture`/
  `BaseIntegrationTest`. (4) citext columns need `EnableUnmappedTypes` — already fixed in
  `DbContextBindingExtensions`; the container suite exercises it. (5) The fix is the **memory pattern**
  `tenant-ignoring-read-on-webhook-paths.md`: tenant-ignoring existence read, then set the tenant override
  from the resolved order before the confirm/paid write.
- **T-0246:** `StartOrder.cs:137` / `:45` line refs may have drifted (Waves 1–4 touches) — re-derive
  against current master. T-0215's `Ac14` (in `Cleansia.HostTests`) proved the bug on the Mobile partner
  host with tenant-consistent seed; its happy-leg currently routes through `TakeOrder` to dodge the NRE —
  the regression test home is the same suite family.
- **T-0196:** the consistency baseline + canonical forms are unchanged, but Waves 2–3 **added** handlers
  in the touched Features folders (refund/dispute/payroll/catalog) — re-derive the exact A*/B1/B3 hit
  list from current source before splitting; `disputes.facade.ts` is **excluded** (T-0202 owns it).
- **T-0198:** `BusinessErrorMessage.cs` grew in Waves 2–3 (refund/chargeback/payroll/catalog/lockout) —
  the IA-6 password/email single-source-of-truth must be derived from **current** validators; the admin
  weak-password divergence is still the target. Host auth registration is BSP-1/T-0100's surface — do NOT
  touch it. The 5 AuthControllers gained Wave-3 endpoints; the shared base must preserve every host's
  current attributes verbatim.
- **T-0199 (AUD-06):** line numbers drifted (Wave-1/2 touches). The Cash-branch enqueue is the
  **post-commit dispatch / outbox** seam (T-0118), **not** a raw `IQueueClient.SendAsync` in the handler —
  keep T-0118's shape; rebase on post-Wave-4 master. **T-0212 is the green net (Wave 4 shipped it) — it
  must stay green unchanged.**
- **T-0200 (AUD-07):** the god-facade is **977 lines** today (title says 1048 — drifted post-Wave-0).
  T-0218 (Wave-4 a11y) already touched the order-wizard **markup/components** — verify no merge surprise,
  but the facade is this ticket's surface and was not refactored by T-0218.
- **T-0201:** depends on **T-0150✓ (done)** — consume the named `"CZE"`/geo/length constants; do NOT
  re-introduce them. The triplicated inline `SavedAddressDto` projection is in AddSavedAddress /
  UpdateSavedAddress / GetSavedAddresses — verify the line refs against current master.
- **T-0202:** the **customer** generated client must already emit the dispute DTOs/enums — **verify FIRST**;
  if missing, hold on the owner regen (the outstanding Wave-3 customer regen for `DisputeReason.Chargeback`
  + device endpoints is likely the same unblock). Depends on T-0196's C1 base.
- **T-0203:** T-0148 (tier-threshold `Reason`) and T-0176 (referral wiring) touched `RevokePointsManually.cs`
  /loyalty in different regions — sequence after them (done) and rebase. The device handlers
  (`RegisterDevice`/`UnregisterDevice`) were soft-deleted by T-0142's children (done) — touch only the
  exception/magic-string smell, not delete semantics. The new `LoyaltyEarnSource.ManualRevoke` is an int
  enum value → **no migration**.
- **T-0204:** the dispute write handlers were changed by T-0172 (transition guard) and T-0173 (admin
  management) — PERF-D2's `GetForUpdateAsync` rebases on the **post-DA-2 / post-T-0196 B1** handler. The
  Wave-0 perf indexes (User.Email etc., T-0124) are done — don't re-touch them. `database.md` is flagged
  stale — index per the real columns.
- **T-0205:** **T-0144 (done)** routed Stripe/SendGrid through `IHttpClientFactory` — confirm whether it
  already collapsed the dead `SendGridClientFactory.SendTemplateEmailAsync`; if so, reconcile rather than
  re-delete (AC2). The 13-file Android scrap tree + Handlebars engine + FCM log-spam are independent.
- **T-0206:** the Functions consumers were reshaped across Waves 0–3 (T-0118/0157/0181/0182/0184) — the
  `messageText`-logging call-sites may have moved; re-derive the exact lines. Assert the log **event +
  scalar keys**, never the message string.
- **T-0242/0243/0244:** current (filed 2026-06-13). T-0242 is **held on Q-W5-1** (owner product decision).
  T-0244: confirm no current code recomputes the variable symbol from stored invoices before choosing the
  persist-and-never-recompute path (which would add a migration).
- **T-0247:** filed 2026-06-09 (re-id'd from T-0200). Deps T-0172✓/T-0174✓ done — the three sanctioned
  writers (`Dispute.UpdateStatus`, `ResolveDispute.Handler`, `HandlePaymentNotification.ReflectChargebackStatus`)
  are the current allowlist; verify the rule is green on current master before wiring it into the run.

---

## 4. Owner items

### 4.1 Blocking question (gates ONE ticket; not the wave)
- **Q-W5-1 (blocking: yes)** — Plus-membership free-cancellation-window direction. **Gates T-0242 only.**
  T-0242 is held `blocked`; every other Wave-5 ticket proceeds. Recommended resolution: path (b) (invert
  override semantics in `BookingPolicy`), but the owner's product intent decides. See `questions/open.md`.

### 4.2 Sequencing call (PM recommendation)
- **Defer T-0197** (mobile `ApiResult<T>` migration, L) to a **Wave-6 mobile slice**. It is a large
  cross-app refactor with no go-live/money pressure and needs its own architect ADR; keeping it in Wave 5
  stretches the wave for low marginal value. Confirm or keep-in-scope.

### 4.3 Manual steps flagged this wave (owner-only — PM never runs)
- **T-0204:** `ef-migration` — 4 new indexes (Addresses composite; UserMembership `(Status,
  CurrentPeriodEnd)`; GdprRequest `CreatedOn`; Devices `(IsActive, LastActiveAt)`) in one migration, built
  `CONCURRENTLY` on populated tables. Ticket held at the migration boundary (AC8).
- **T-0244:** `ef-migration` **only if** the dev chooses AC1 path-(b) persist-and-never-recompute; the
  default stable-hash path needs none.
- **T-0202:** `nswag-regen` (customer client) **if** the dispute DTOs/enums are missing from the generated
  client — likely satisfied by the still-outstanding Wave-3 customer regen.
- **T-0196 (B1) / T-0203 (SendSitewidePromo Response + device/membership error shapes):** `nswag-regen`
  **only if** the OpenAPI response type actually changes — prefer wire-compatible shapes; the dev confirms
  the OpenAPI diff at review and the PM adds the flag + holds consumers only if it changes.

### 4.4 Latent-correctness fixes to surface to the owner
- **T-0204 / PERF-IDA-06** — `GetAllGdprRequests` currently orders **after** paging (wrong page contents
  on an Article-30 compliance surface). The fix corrects it; called out as a behavior-correcting change,
  not a silent one.

### 4.5 Standing carry-forwards (unchanged, owner-tracked)
T-0159 rotate-mapbox-token (live exposure) · customer nswag-regen (Wave-3 residual) · IMP-1 Google OAuth
ClientId · CZ Stripe-fee figures · DE/AT/ES fiscal go-live gates (Q-REFUND-01/ADR-0009 D7) · Q-REFUND-03
per-bundle weights · Q-W3-4 dispute-resolve-on-refund-failure UX · **T-0236 (multi-tenant token-revoke
asymmetry) must land before multi-tenant onboarding — alongside T-0245.**

---

## 5. Definition of "Wave 5 done"

All in-scope tickets `done`: every AC has named-test / migration-metadata / log-event / reviewer
evidence; reviewer approved each (Security gate reconciled on **T-0245** + advisory on T-0198/T-0206/
T-0247; adversarial money review on T-0244 and T-0242-when-unblocked; optimizer on T-0204); QA recorded;
`dotnet build` + `dotnet test` (Cleansia.Tests, IntegrationTests, HostTests) green on the branch, the
touched `nx` projects build/lint/test green, **T-0212 stays green through T-0199**, and
`check-consistency.mjs` reports no new violation for every touched area; every owner manual-step is
flagged-and-confirmed (no migration/regen run by an agent); INDEX.md + this doc match reality. The two
L-epics (T-0196, T-0199) and the AUD-07 L-file are `done` only via their split children. T-0242 is `done`
only after Q-W5-1; T-0197 is `done`, deferred, or descoped per §4.2. PR to `master` is the owner's call.

---

## 6. Explicitly NOT in Wave 5

- **T-0233/T-0234** (lockout-DoS, ChangeOwnPassword bound), **T-0236** (multi-tenant token-revoke — must
  precede multi-tenant onboarding), **T-0237/0238/0239/0240/0241** — Wave-3 security/quality follow-ups,
  stay `draft`; sequence at a later wave (T-0236 before multi-tenant onboarding, with T-0245).
- The owner manual steps themselves (migrations, regens) — flagged, never run.
- Any new feature behavior — Wave 5 is bug-fix + consistency/quality only (plus the two named behavior
  fixes inside T-0203 and the T-0242 product-correction once unblocked).

---

## Status log
- 2026-06-13 — Wave-5 plan drafted + promoted (PM). Verified master `ee95a57f` (Wave 4 / PR #77); every
  Wave-5 ticket's `depends_on` resolved against merged Waves 0–4. **Folded T-0245 + T-0246 to the FRONT
  as Batch 5A** (parallel, disjoint files; T-0245 Security-gated + non-null-tenant integration test in the
  T-0210 suite; T-0246 null-guard + regression). **Fixed an id collision** (two files claimed `T-0200`;
  re-id'd the dispute-guard follow-up `T-0200 → T-0247`). Sequenced the sweep into 5B (independent
  micro-fixes + long tail) → 5C (T-0196 L-split base) → 5D (T-0199 AUD-06, **lane-isolated on
  `CreateOrder.cs`, gated by T-0212 staying green**) → 5E (de-triplication + AddSavedAddress) → 5F (the two
  frontend rebuilds, after T-0196) → 5G (perf cluster + the tooling rule, after T-0196) → 5H (T-0197 mobile
  migration, **defer-candidate**). Promoted **10 ready**, held **4 draft (L-split required: T-0196/T-0199/
  T-0197 + the AUD-07 file)**, **4 blocked** (T-0242 on Q-W5-1; T-0200/T-0202/T-0204 on T-0196). Opened
  **Q-W5-1 (blocking, gates T-0242 only)**. Stale-text deltas in §3 for the implementing agents; owner
  items (Q-W5-1, defer-T-0197 recommendation, 3 manual-step flags, the GDPR latent-correctness fix) in §4.
  No code/commits/branch ops by the PM (backlog bookkeeping only).
- 2026-06-13 — **Batch 5A DONE / committed `3df53ab2`** (T-0245 webhook tenant-scope + T-0246 StartOrder
  NRE — verified). Owner approved driving the rest of Wave 5 autonomously. **PM split the three L-epics**
  (T-0196/T-0199/T-0200) into **11 child tickets T-0248…T-0258** (§2.1); the epics became `in_progress`
  [SPLIT/EPIC] tracking tickets (each `done` only when its children are `done`). **5C** → 5 disjoint
  concurrent children (T-0248 A*, T-0249 B1 [conditional nswag-regen; `blocks` T-0202], T-0250 B3, T-0251
  C* [EXCL `disputes.facade.ts`; `blocks` T-0200], T-0252 E1/E2). **5D** → 3 serial children
  (T-0253→T-0254→T-0255) lane-isolated on `CreateOrder.cs`, each pinning **T-0212 green+unmodified**, the
  Cash-branch outbox seam preserved (T-0255 AC4). **5F (AUD-07)** → 3 serial children (T-0256→T-0257→T-0258)
  sole-editor of `order-wizard/**`, the C1 base dependency riding T-0256→T-0251. Revised dependency-ordered
  **dispatch plan in §2.2**: {5B, 5C, 5D, 5E} fan out concurrently → {5F, 5G} after their 5C predecessors
  (T-0249/T-0251) land → 5H deferred. **5C must complete before 5F/5G.** **T-0242 stays BLOCKED on Q-W5-1.**
  INDEX.md child rows added. No code/commits by the PM (backlog only).
- 2026-06-14 — **WAVE 5 CLOSED.** All in-scope work landed orchestrator-verified green and committed +
  pushed; the 3 parent epics (T-0196/T-0199/T-0200) reconciled to `done`; 4 close follow-ups filed
  (T-0259…T-0262); T-0242 carried (blocked Q-W5-1); T-0197 deferred to Wave 6. INDEX.md banner flipped to
  **WAVE 5 COMPLETE**. Close-out detail in **§7** below. No code/commits/branch ops by the PM (backlog only).

---

## 7. Close-out (Wave 5 CLOSED, 2026-06-14)

**Status: WAVE 5 CLOSED.** Functionally complete, committed + pushed on `feature/wave-5-consistency-bugs`
(commits **`3df53ab2`** [5A], **`79b0153c`**, **`226bc928`**, **`9be1f8ee`**). PR to `master` is the owner's call.

### 7.1 Verified suite counts (orchestrator, clean rebuild, real Postgres)
- **Cleansia.Tests 1472/1472** · **IntegrationTests 66/66** · **HostTests 51/51**
- **frontend order-wizard 119/119 + customer-disputes 41/41 Jest**
- **all 3 web apps build production** · **S6 logging 9/9**
- **T-0212 CreateOrder characterization gate held 20/20 unchanged** through the entire AUD-06 decomposition.

### 7.2 What landed, per batch (21 tickets DONE)
- **5A — priority bugs (committed `3df53ab2`):** **T-0245** multi-tenant Stripe webhook validator/handler
  tenant-scope mismatch **FIXED** (the GO-LIVE BLOCKER; tenant-ignoring existence read bound to the order's
  own tenant + non-null-tenant integration test in the T-0210 suite; Security gate reconciled). **T-0246**
  StartOrder handler NRE→500 on load divergence **FIXED** (null-guard + handler/validator query reconciled +
  regression test).
- **5B — backend micro-fixes + long tail:** **T-0243** (CheckoutSession `nameof` B5 rename), **T-0203**
  (LG/DA/IA long tail — B5/B1/CQRS/magic-strings/swallowed-catch; **admin nswag-regen owed**), **T-0244**
  (`GenerateVariableSymbol` deterministic stable hash — stable-hash path chosen → **no migration**),
  **T-0205** (dead/unsafe code removed — Handlebars/SendGrid/FCM/Android scrap), **T-0206** (S6 logging
  hygiene — event+scalar keys, never message strings; 9/9).
- **5C — consistency sweep base (T-0196 epic → all 5 children done):** **T-0248** A* paged-query, **T-0249**
  B1 Response-wrap (wire-compatible — no regen needed), **T-0250** B3 validator-base, **T-0251** C*
  customer/partner/admin facades (excl. `disputes.facade.ts`), **T-0252** E1/E2 Android sealed states.
- **5D — AUD-06 (T-0199 epic → all 3 children done):** **T-0253**→**T-0254**→**T-0255**. CreateOrder
  god-handler **decomposed** into address-resolution + serviced-area, promo preview/apply, and
  payment-side-effect-dispatcher + late-referral collaborators; the slim handler reads as orchestration.
  Cash-branch **outbox/post-commit dispatch seam preserved** (T-0255 AC4). **T-0212 held 20/20 unmodified.**
- **5E — de-triplication + AddSavedAddress:** **T-0201** (AddSavedAddress god-method + B9 mapper), **T-0198**
  (de-triplicated Dispute/SavedAddress/Auth controllers + login/forgot facades) — **fixed 2 real bugs**:
  the **weak admin password** path and the **swallowed login/forgot-password errors**.
- **5F — frontend rebuilds (T-0200/AUD-07 epic → all 3 children done) + disputes:** **T-0256**→**T-0257**→
  **T-0258** order-wizard god-facade **decomposed** onto focused collaborators with C1/C3 paradigm
  (behavior-identical, 119/119); **T-0202** customer disputes moved to its own client + cleansia-table/form/
  error archetype (41/41; **customer nswag-regen owed**).
- **5G — perf cluster + tooling:** **T-0204** PERF cluster — **4 indexes** + tracked-read/eager-Include
  cleanup + the **GDPR paging correctness fix** (PERF-IDA-06: `GetAllGdprRequests` now orders **before**
  paging — a behavior-correcting fix on an Article-30 surface). **T-0247** check-consistency Dispute
  state-write allowlist tooling (lives in `tickets/T-0200-da-2-followup.md`).

### 7.3 AUD-06 / AUD-07 decomposition outcomes
- **AUD-06 (T-0199):** the 484-line, 15-dep `CreateOrder.Handler` god-handler is decomposed; each
  collaborator independently unit-tested; **no contract/wire change** → no nswag-regen, no migration; the
  acceptance gate (T-0212 20-case characterization suite **green and unmodified**) held through every
  serial sub-step.
- **AUD-07 (T-0200):** the 977-line `OrderWizardFacade` god-facade is split into focused collaborators with
  the slim facade retaining step-nav + submit orchestration; C1 (`UnsubscribeControlDirective`) + C3
  (`takeUntil → catchError → finalize`) paradigm migrated; **behavior-identical** (characterization Jest
  spec held) → no nswag-regen.

### 7.4 Real bugs fixed this wave
- **T-0245** — multi-tenant webhook tenant-scope mismatch (silent money/lifecycle failure on non-null-tenant
  paid `checkout.session.completed`) — the **GO-LIVE BLOCKER**, now fixed.
- **T-0246** — StartOrder handler NRE→500 on validator/handler load divergence — fixed.
- **T-0198** — **weak admin password** path + **swallowed login/forgot-password errors** — both fixed.
- **T-0204** — **GDPR paging correctness** (order-before-page on `GetAllGdprRequests`) — fixed (behavior-correcting).

### 7.5 MANUAL_STEPS owner queue (PM never runs these)
1. **nswag-regen — admin client** (T-0203 SendSitewidePromo Response / device+membership error shapes that
   surfaced a generated-type change).
2. **nswag-regen — customer client** (T-0202 dispute DTOs/enums; also clears the residual Wave-3 customer
   regen for `DisputeReason.Chargeback` + device endpoints).
3. **ef-migration — DONE in this wave** (T-0204's 4 indexes were applied). **For PROD:** apply the 4 indexes
   `CONCURRENTLY` by hand (`CREATE INDEX CONCURRENTLY` outside the migration transaction) on the populated
   tables — the EF migration creates them in-transaction, which locks; the CONCURRENTLY hand-edit is the
   prod-safe application. (Indexes: Addresses composite; UserMembership `(Status,CurrentPeriodEnd)`;
   GdprRequest `CreatedOn`; Devices `(IsActive,LastActiveAt)`.)

### 7.6 Follow-ups filed (Wave-6 candidates, all `draft`)
- **T-0259** — frontend nx-lib test-infra scaffolding (tags + jest/eslint/tsconfig.spec targets for
  loyalty-promo-codes + customer login/forgot + partner-forgot libs). Source: T-0203 + T-0198.
- **T-0260** — funnel `HandleChargeback` dispute-terminal write through the T-0172 `CanTransitionTo` guard
  (not direct `Escalate`); safe today (`Pending→Escalated` legal) but a defense-in-depth gap. `sec`.
  Source: T-0247.
- **T-0261** — UserMembership partial-index gap: the renewal-arm partial index doesn't cover the
  cancellation-reminder sweep arm. DB optimization; `ef-migration` (CONCURRENTLY). Source: T-0204.
- **T-0262** — remove dead `BusinessErrorMessage.EmailNotSentError` constant (zero consumers). Source: T-0205.

### 7.7 Carried / deferred
- **T-0242** (cancellation-fee Plus free-window direction) — **still BLOCKED on Q-W5-1** (owner product
  decision, unanswered in `questions/open.md`). Carried to whenever the owner answers; then S, money-adjacent
  (adversarial review), edits `BookingPolicy.cs` + `CancellationFeeRateBoundaryTests.cs`.
- **T-0197** (mobile `ApiResult<T>` L-migration) — **DEFERRED to Wave 6** per §4.2. Stays `draft`
  (SPLIT-REQUIRED + ADR-FIRST); re-tag `sprint: 6` and promote at Wave-6 planning when the owner opens a
  mobile slice.

### 7.8 Consolidated OWNER ACTION LIST for Wave 5
1. **nswag-regen — admin client** (T-0203). 2. **nswag-regen — customer client** (T-0202; clears Wave-3
   residual too). 3. **PROD index application — apply the 4 T-0204 indexes `CONCURRENTLY` by hand** (the EF
   migration itself is done). 4. **Answer Q-W5-1** to unblock T-0242. 5. **Confirm defer-T-0197-to-Wave-6.**
   6. **Open the PR `feature/wave-5-consistency-bugs` → `master`** (PM never merges). Standing carry-forwards
   unchanged: T-0159 rotate-mapbox-token (live exposure) · IMP-1 Google OAuth ClientId · CZ Stripe-fee
   figures · DE/AT/ES fiscal go-live gates · Q-REFUND-03 per-bundle weights · Q-W3-4 dispute-resolve-on-
   refund-failure UX · **T-0236 (multi-tenant token-revoke asymmetry) must land before multi-tenant
   onboarding, alongside the now-fixed T-0245.**
