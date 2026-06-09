# Sprint 5 — Wave 3 plan (the admin-feature block T-0170…T-0195)

- **Date:** 2026-06-09
- **Goal:** Sequence Wave 3 for owner approval. **PLAN ONLY — nothing promoted to `ready`, no code, no
  migrations, no NSwag.** Wave 3 is the broad **story-backed admin-feature block** the audit execution
  plan defines as T-0170…T-0195 (26 tickets): admin order ops, payroll lifecycle, disputes, membership/
  referral/GDPR/device, catalog activate/deactivate, Functions resilience, and the rate-limit fast-follows.
  The refund epic was pulled ahead of it into Wave 2 (now merged), so the two tickets that were
  **explicitly deferred to Wave 3 because they depend on the refund seam** — T-0170 (admin order ops) and
  T-0173 (admin dispute mgmt) — are now **unblocked.**
- **Status:** proposed. Awaiting owner sign-off on the batch sequence, the 5 L-splits, and the one blocking
  question (Q-W3-1, default-language policy — gates only T-0191's CC-06 sub-ticket) before any promotion to
  `ready`.

**Wave 2 closed.** The refund money-path epic + per-included-service package-pricing + fiscal go-live gates
shipped to master in **`8ff35d49` (PR #75)**: T-0160 (Refund entity/enums), T-0161 (IRefundService seam +
`IStripeClient` key param), T-0163 (proportional loyalty partial-refund clawback), T-0164 (CancelOrder/
ResolveDispute migrated onto the seam), T-0167 (admin partial-refund command + allocator + RefundPolicy +
per-country Stripe-fee config), T-0168 (admin refund UX incl. bundled-service selection), T-0231 (+T-0231b)
(PackageService.PriceWeight + even-weight backfill + PriceWeight/serviceWeights on the package DTO), T-0232
(admin package-weight UX), T-0219 (anon-catalog → platform config), T-0220 (FiscalCounter gapless allocator),
T-0221 (IFiscalService register idempotency), T-0222 (pay-split rounding) — plus two runtime fixes
(OutboxMessageRepository non-composable FromSqlRaw; AppHost pinned Postgres password) and the new backend DTO
field `PackageDetails.IncludedServiceItems [{Id,Name}]`. The 12 Wave-2 ticket files still read `draft`; the
PM reconciled them to **`done ✅`** in INDEX.md (status-only). Split epics T-0162/T-0165 stay `[SPLIT]`
tracking with all four children `done`. **The refund seam (T-0161) + seam migration (T-0164) are now `done`,
which is exactly what gated T-0170/T-0173 → they enter Wave 3 with satisfied dependencies.**

---

## 0. Pre-flight reconciliation (done as part of this planning pass)

Verified against git + every Wave-3 candidate's frontmatter under `tickets/`:

- **Wave 0** = PR #72 (`9a774435`) — all T-0100…T-0128 + T-0230 `done`. ✅
- **Wave 1** = `a4f14094` — Batch 1A (4 ADRs) + Batch 1B (14 tickets) `done`. The split epics **T-0142**
  (soft-delete: children T-0152/T-0153/T-0154 `done`) and **T-0143** (F2-FULL outbox: children
  T-0155→T-0156→T-0157→T-0158 `done`) are fully closed — this matters because **eight Wave-3 tickets list
  T-0142 or T-0143 as a dependency** and they are satisfied. ✅
- **Wave 2** = `8ff35d49` (PR #75) — 12 tickets reconciled to `done` this pass; refund seam + migration
  (T-0161/T-0164) `done`. ✅
- **No new ADR gates Wave 3.** ADR-0001 (authz, the frozen `PolicyBuilder` map), ADR-0002 (outbox/dispatch),
  ADR-0006/0009 (refund seam + policy), ADR-0004 (fiscal), ADR-0007 (soft-delete) are all `accepted`. Every
  Wave-3 ticket cites an accepted ADR; none requires architect deliberation. Architect is still invoked at
  **contract-lock** for the `L`-split children (command/DTO shapes), per `routing.md` — that is execution,
  not a new ADR.
- **Dependency audit (all confirmed `done`):** T-0100, T-0111, T-0112, T-0115, T-0140, T-0141, T-0145,
  T-0148, T-0161, T-0164, plus the T-0142/T-0143 epics. **Zero Wave-3 tickets have an open external/earlier
  dependency.** The only intra-Wave-3 ordering is the spine edges below.
- **`sprint:` frontmatter note:** the 26 candidates still carry `sprint: 2` in frontmatter (from the
  original audit plan, where this block was "Wave 2"). The **wave label in this doc governs**; the PM
  re-tags them `sprint: 3` only when each is promoted to `ready` (avoids churning 26 files in a plan-only
  pass — same convention sprint-4 §6 set).

---

## 1. Scope — the Wave-3 tickets (26), grouped by batch

Every `L` is **split before `ready`** (children authorized in §4). Sizes/layers/sec/manual_steps read from
each ticket's real frontmatter.

| ID | Title (short) | Size | Batch | Layers | depends_on (✓=done) | sec | manual_step |
|----|---------------|------|-------|--------|----------------------|-----|-------------|
| **T-0170** | Admin order ops + generalized cancel | **L→split** | 3A | backend, frontend | T-0100✓, T-0140✓, T-0161✓, T-0164✓ | **yes** | nswag-regen |
| **T-0172** | Dispute transition-guard (Close/Escalate/LinkStripe reachable) | M | 3A | backend | T-0140✓ | **yes** | — |
| **T-0174** | Wire Stripe chargeback linkage (LinkStripeDispute) | M | 3A | backend | T-0140✓ | **yes** | — |
| **T-0173** | Admin dispute mgmt + issue refund; remove dead Partner endpoints | **L→split** | 3A | backend, frontend | T-0100✓, T-0140✓, T-0161✓, T-0164✓, T-0172, T-0171 | **yes** | nswag-regen |
| **T-0171** | Payroll adjustment + settlement lifecycle + partner surface | **L→split** | 3B | backend, frontend, android | T-0100✓, T-0143✓, T-0170 | **yes** | nswag-regen, ef-migration |
| **T-0180** | Implement GenerateInvoiceFunction (revive dead queue) | S | 3B | functions | T-0143✓, T-0171 | no | — |
| **T-0175** | Admin Membership-Plan CRUD | **L→split** | 3C | backend, frontend | T-0100✓, T-0173 | **yes** | nswag-regen |
| **T-0176** | Admin referral intervention + wire by-user endpoint + sidebar | M | 3C | backend, frontend | T-0100✓, T-0148✓, T-0175 | **yes** | nswag-regen (hold-point) |
| **T-0177** | Invoke referral expiry sweep (timer) | S | 3C | backend, functions | T-0143✓ | no | — |
| **T-0178** | /r/{code} referral landing route | M | 3C | frontend | — | no | — |
| **T-0179** | Unify membership subscribe path (web/mobile) | S | 3C | backend, frontend | T-0111✓ | no | nswag-regen* |
| **T-0181** | SendSitewidePromo fan-out: resume cursor + idempotent enqueue | M | 3D | functions, backend | T-0143✓ | **yes** | — |
| **T-0182** | Idempotent push dispatch (per-message key) | M | 3D | functions, backend | T-0143✓, T-0141✓ | **yes** | — |
| **T-0183** | Fix cron cadence on 4 timers | S | 3D | functions | — | no | — |
| **T-0184** | FiscalRetryService per-receipt durability | S | 3D | backend | T-0143✓ | no | — |
| **T-0185** | Mapbox 429/rate-limit handling | M | 3D | backend | T-0141✓, T-0145✓ | no | — |
| **T-0186** | Admin GDPR back-office UI + partner GDPR self-service | **L→split** | 3E | backend, frontend | T-0100✓, T-0176 | **yes** | nswag-regen |
| **T-0187** | Customer-web notification-preferences UI (11-category) | M | 3E | frontend | — | no | — |
| **T-0188** | Device / active-session management (GetMyDevices + revoke) | M | 3E | backend, frontend, mobile | — | **yes** | nswag-regen |
| **T-0189** | LastLoginAt tracking | M | 3E | backend, db, frontend | — | no | ef-migration |
| **T-0190** | Admin self-service profile/password; BirthDate/lang | M | 3E | backend, frontend | T-0100✓, T-0172 | no | nswag-regen (hold-point) |
| **T-0191** | Catalog in-use guard + activate/deactivate; default currency/lang | L (internal a/b/c/d) | 3E | backend, frontend | T-0142✓ | **yes** | ef-migration, nswag-regen |
| **T-0192** | Customer dispute evidence+refund UI; status filter; saved-address UI | M | 3E | frontend | — | no | — |
| **T-0193** | Account-lockout / per-confirmation-code throttle | M | 3F | backend, db | T-0115✓, T-0189, T-0190 | **yes** | ef-migration |
| **T-0194** | Rate-limit coverage for uncovered money/side-effect endpoints | S | 3F | backend | T-0115✓, T-0171, T-0173, T-0179, T-0188 | **yes** | — |
| **T-0195** | Client-side Retry-After back-off jitter (SPA + mobile) | S | 3F | frontend, mobile | T-0115✓ | no | — |

\* T-0179 `nswag-regen` only if the handler doc/field touch changes the customer OpenAPI contract (likely
comment-only → no regen). T-0176/T-0190 carry the regen as a **hold-point between their backend and frontend
slices** (flagged at `in_review`), not a frontmatter migration.

---

## 2. Sequence + dependency rationale (the DAG governs, not id order)

Six batches. Within a batch, tickets fan out by `depends_on`; the **shared-file serialization clusters in
`TICKET-MAP.md`** force a few strict orders (called out per batch). The batches mirror the Wave-2 shape:
a **spine** (the refund-seam consumers) plus **parallel fast-follows.**

```
3A SPINE (refund-seam consumers)
  T-0170 (admin order ops, L→split) ──► T-0171 (payroll, 3B) ──► T-0180 (invoice fn, 3B)
  T-0172 (dispute guard) ──► T-0173 (admin dispute mgmt, L→split) ──► T-0175 (membership CRUD, 3C)
  T-0174 (chargeback linkage, parallel w/ T-0172)                       └─► (3C/3E consumers)
3C/3D/3E fan out after their spine; 3F lands last (depends on 3A/3B/3C consumers existing)
```

### Batch 3A — refund-seam consumers (the spine). FIRST, because it unblocks 3B and 3C.
This is the natural first batch: T-0170 and T-0173 are the two tickets the Wave-2 plan **deferred to Wave 3**
precisely because they consume the refund seam (T-0161) + seam migration (T-0164) — both now `done`. The
dispute-backend serialization cluster (`SEC-DSP-01 → DA-2 → D-01 bundle`, TICKET-MAP) forces
**T-0172 → T-0173** strictly.

| Ticket | depends_on | Parallel with | Why first / order |
|---|---|---|---|
| **T-0170** (L→split) | T-0100✓, T-0140✓, **T-0161✓**, **T-0164✓** | T-0172, T-0174 | The highest-value feature fix. Consumes the refund seam for admin cancel/refund. **PolicyBuilder cluster** (head T-0100, `done`): the 4 new `AdminOnly` rows serialize against any other `Policy.cs` editor — none other in 3A. Split children that edit `CancelOrder.cs` serialize against each other. |
| **T-0172** (M) | T-0140✓ | T-0170, T-0174 | Dispute transition-guard. Head of the dispute-backend cluster work for Wave 3; **must precede T-0173** (cluster order). |
| **T-0174** (M) | T-0140✓ | T-0170, T-0172 | Chargeback `LinkStripeDispute` wiring on the webhook. Edits `HandlePaymentNotification.cs` + repos — **not** in the dispute-backend cluster, so safely parallel with T-0172; coordinate AC4 status mapping with T-0172. |
| **T-0173** (L→split) | T-0100✓, T-0140✓, **T-0161✓**, **T-0164✓**, **T-0172**, **T-0171** | — (tail) | Admin dispute mgmt + the real idempotent refund. **Serializes behind T-0172** (cluster) and depends on **T-0171** (its `depends_on` lists the payroll ticket — admin-shell sidebar/route cluster `T-0173 → T-0175 → T-0176 → T-0186` is seeded here). Frontend slice held on its nswag-regen. |

> **Load-bearing edge:** the admin-shell cluster (`app.component.ts` sidebar + `app.routes.ts`) is
> **T-0173 → T-0175 → T-0176 → T-0186** (TICKET-MAP). All four add a sidebar entry to the one array; they
> must land in that order and never edit the shell concurrently. This threads 3A → 3C → 3E.

### Batch 3B — payroll lifecycle. After 3A's T-0170 (shared payroll-surface coordination).
| Ticket | depends_on | Notes |
|---|---|---|
| **T-0171** (L→split) | T-0100✓, **T-0143✓**, **T-0170** | Wires the dead settlement state machine + AUD-04 partner-surface reconciliation (remove admin endpoints off partner host). Depends on T-0170 (frontmatter) — coordinate the cross-host `EmployeePayrollController.cs` edits. **ef-migration** if adjustment-audit columns are added; **nswag-regen** for new admin/partner endpoints. |
| **T-0180** (S) | **T-0143✓**, **T-0171** | Revives the no-op `GenerateInvoiceFunction` by running the existing `GenerateInvoice.Command`. Needs T-0171 as the producer/owner of the invoice-generation step. Single new file, no cluster, no manual step. |

### Batch 3C — loyalty / membership / referral. Fans out after T-0173 (admin-shell) + T-0175.
| Ticket | depends_on | Notes |
|---|---|---|
| **T-0175** (L→split) | T-0100✓, **T-0173** | Admin Membership-Plan CRUD. New `AdminOnly` rows (PolicyBuilder cluster) + sidebar entry (admin-shell cluster, after T-0173). Backend slice = contract; frontend held on nswag-regen. |
| **T-0176** (M) | T-0100✓, T-0148✓, **T-0175** | Referral intervention (reverse/force-qualify) + wire orphaned by-user endpoint + sidebar (admin-shell cluster, after T-0175). Consumes the post-T-0148 grant/revoke signatures; **must not edit `LoyaltyService.cs`** (that cluster is `done`). |
| **T-0177** (S) | **T-0143✓** | Referral-expiry sweep timer (new Function). Fully independent of the admin-shell chain — runs any time after T-0143. |
| **T-0178** (M) | — | `/r/{code}` web landing route. Pure customer-frontend; no dep, no cluster. Can run day 1 of Wave 3. |
| **T-0179** (S) | T-0111✓ | Documents the two-path subscribe + B5 rename. Rebases on post-T-0111 `CreateMembershipSubscription.cs`. Likely comment-only → confirm no nswag-regen. |

> Parallelism: **T-0177, T-0178, T-0179 have no admin-shell dependency** and run alongside the
> T-0175 → T-0176 chain.

### Batch 3D — Functions resilience fast-follows. Fully parallel with 3C (disjoint files).
All five consume the **`done`** outbox substrate (T-0143) and/or the integration ADR (T-0141/0145). None
touches the admin-shell, PolicyBuilder, or dispute clusters.
| Ticket | depends_on | Notes |
|---|---|---|
| **T-0181** SendSitewidePromo cursor | **T-0143✓** | Resume cursor + idempotent enqueue on the fan-out. **security_touching** (mass side-effect). Serialize against any concurrent `SendSitewidePromo.cs` / fan-out Function editor (none in 3D). |
| **T-0182** Idempotent push dispatch | **T-0143✓**, T-0141✓ | Guard-first push consumer (ADR-0002 D2.2). Continuation of the consumer-idempotency line (`F11→F2→F4→F3`); do not run concurrently with another `SendPushNotificationFunction.cs` editor. **security_touching.** |
| **T-0183** Cron cadence | — | Cron/config-only on 4 timer files. No dep — can run day 1. |
| **T-0184** FiscalRetry durability | **T-0143✓** | Per-receipt commit cadence on `FiscalRetryService.cs`. No cluster. |
| **T-0185** Mapbox 429 | T-0141✓, **T-0145✓** | 429/Retry-After policy on the Mapbox client. Serialize after T-0145 on `MapboxGeocodingService.cs` (T-0145 is `done`, so the file is free). |

### Batch 3E — identity / GDPR / device / catalog. Mostly parallel; two admin-shell members.
| Ticket | depends_on | Notes |
|---|---|---|
| **T-0186** (L→split) | T-0100✓, **T-0176** | Admin GDPR UI (admin-shell cluster **tail**, after T-0176) + partner GDPR self-service (disjoint, parallel). Backend already exists — frontend wiring only; nswag-regen is contingency. |
| **T-0187** (M) | — | Customer-web notification-preferences UI. Backend + client already exist → **no manual step**. Pure frontend; no dep. |
| **T-0188** (M) | — | Device list + ownership-checked revoke (+ refresh-token kill) + mobile UI. New backend query/command → **nswag-regen**. **security_touching** (S1–S4). No cluster. |
| **T-0189** (M) | — | LastLoginAt: domain field + single write point at `TokenService` + admin surface. **ef-migration** (new column). No nswag (field already on DTO). Touches `TokenService.cs` + 4 auth handlers — one instance, no fan-out per handler. |
| **T-0190** (M) | T-0100✓, **T-0172** | Admin change-password + accept BirthDate/lang (fixes the data-loss footgun). Reuse an existing `AdminOnly` policy to avoid the PolicyBuilder cluster if possible (architect confirms at lock). nswag-regen hold-point. |
| **T-0191** (L, internal a/b/c/d) | **T-0142✓** | Catalog: CC-02 in-use guard, CC-03 activate/deactivate, CC-04 set-default-currency, CC-06 default-language. Soft-delete ADR (T-0142) is `done`. **Sub-(d) CC-06 is held on Q-W3-1** (owner decision); a/b/c proceed. **ef-migration + nswag-regen.** No cluster. |
| **T-0192** (M) | — | Customer dispute evidence/refund UI + status filter + saved-address management UI. Pure customer-frontend; backend already exists → **no manual step**. No cluster. |

> Parallelism: **T-0187, T-0188, T-0189, T-0191, T-0192 are dependency-free or only depend on `done`
> tickets** and run alongside the T-0186 admin-shell tail. T-0190 needs T-0172 (3A) done first.

### Batch 3F — rate-limit fast-follows. LAST, because two depend on Wave-3 consumers existing.
| Ticket | depends_on | Notes |
|---|---|---|
| **T-0193** account-lockout | T-0115✓, **T-0189**, **T-0190** | Per-account lockout + per-code attempt cap. Depends on T-0189/T-0190 (shared `User` entity + auth-handler edits — serialize against them). **ef-migration** (new `User` columns). **security_touching.** AC6 forbids touching `CleansiaStartupBase.cs`. |
| **T-0194** rate-limit coverage | T-0115✓, **T-0171**, **T-0173**, **T-0179**, **T-0188** | Attribute-only `[EnableRateLimiting]` on money/side-effect endpoints — **must run last** because it annotates endpoints that T-0171/T-0173/T-0179/T-0188 **create** (TICKET-MAP: "T-0194 runs last, after T-0171/T-0173/T-0179/T-0188"). **security_touching.** No startup edit. |
| **T-0195** client back-off jitter | T-0115✓ | SPA + Android `Retry-After` honoring. No Wave-3 dep beyond `done` T-0115; could start early, but grouped here as the rate-limit family. No cluster (shared SPA interceptor + 2 NetworkModule.kt). |

### Parallelism summary
- **Strictly serial (intra-Wave-3 edges):** T-0170 → T-0171 → T-0180; T-0172 → T-0173; admin-shell
  **T-0173 → T-0175 → T-0176 → T-0186**; T-0175 → T-0176 (also a dep); T-0189/T-0190 → T-0193;
  {T-0171, T-0173, T-0179, T-0188} → T-0194.
- **Safely parallel (disjoint files, deps met):** T-0174 ∥ T-0172 (3A); all of **3D** runs alongside **3C**
  and **3E**; T-0177/T-0178/T-0179 ∥ the T-0175 chain; T-0187/T-0188/T-0189/T-0191/T-0192 ∥ the T-0186 tail.
- **Serialization clusters in play (from TICKET-MAP):** PolicyBuilder (`Policy.cs`/`PolicyBuilder.cs`) —
  T-0170, T-0175, T-0176, and any T-0190 new const, each adds rows + regenerates the frozen-map snapshot in
  its own PR, never concurrently; admin-shell (`app.component.ts` + `app.routes.ts`) — T-0173 → T-0175 →
  T-0176 → T-0186 linear; dispute-backend — T-0172 → T-0173; `UserEntityConfiguration.cs`/`User.cs` —
  T-0189 → T-0193 (concurrent Users migrations conflict).
- **Reviewer-per-developer invariant holds at every scale** — each developer instance (including each
  `L`-split child) gets a concurrent reviewer; security gate is mandatory on every `security_touching: true`
  ticket (T-0170/0172/0173/0174/0171/0175/0176/0181/0182/0186/0188/0191/0193/0194).

---

## 3. External / owner-action blockers + carry-forward items

### 3.1 — Owner manual steps that gate Wave-3 tickets (Claude never runs these)
| Blocker (owner-only) | Gates | Notes |
|---|---|---|
| **ef-migration — T-0171** (adjustment-audit columns, *if* the chosen design adds them) | T-0171 close | Flag at split-design time; may be none. |
| **ef-migration — T-0189** (`User.LastLoginAt` column) | T-0189 persisted-read AC + T-0193 (shares `User` migration) | Serialize Users migrations: T-0189 before T-0193. |
| **ef-migration — T-0191** (`Language.IsDefault`, only if Q-W3-1 → path (a)) | T-0191 CC-06 sub-(d) | Not needed if owner picks path (b). |
| **ef-migration — T-0193** (`User` lockout/attempt columns) | T-0193 AC4 | After T-0189's Users migration. |
| **nswag-regen — T-0170** (admin order-ops endpoints/DTOs) | T-0170 frontend (AC8) | Hold the frontend child until owner confirms. |
| **nswag-regen — T-0173** (admin DisputeController) | T-0173 frontend slice (173b) | Hold 173b until confirmed. |
| **nswag-regen — T-0171** (admin/partner payroll endpoints) | T-0171 frontend + Android consumers | Hold consumers. |
| **nswag-regen — T-0175** (AdminMembershipController) | T-0175 frontend (175b) | Hold 175b. |
| **nswag-regen — T-0176 / T-0190** (referral intervention / admin profile commands) | each ticket's frontend slice | Hold-point between backend and frontend slices. |
| **nswag-regen — T-0186** (contingency) | T-0186 frontend | Only if a client method is found missing during wiring. |
| **nswag-regen — T-0188** (GetMyDevices + revoke + DeviceDto) | T-0188 mobile + admin-panel slices | Hold mobile/admin until confirmed. |
| **nswag-regen — T-0191** (catalog admin commands + `Language.IsDefault` if (a)) | T-0191 frontend | Hold. |
| **stripe-price-registration — T-0175** (register Stripe Product/Price out of band, replace seed placeholders) | T-0175 usable plans at runtime | Owner step; `StripePriceId` is admin-entered, platform doesn't call Stripe to create products. |

### 3.2 — Carry-forward owner items (NOT Wave-3 tickets; owner must track)
These are surfaced here so they are not lost. **None blocks starting Wave 3**, but several are launch gates.

1. **T-0159 `rotate-mapbox-token` — STILL OUTSTANDING (live exposure).** The Wave-1 code fix shipped (token
   off the request URL), but the **exposed token is in git history and remains live until rotated in the
   Mapbox account.** This is a standing security exposure — owner action, highest priority of the
   carry-forwards. (Note T-0185 in Wave 3 hardens Mapbox 429 handling but does **not** rotate the token.)
2. **Outstanding Wave-0 NSwag regens — confirm done or pending.** Four Wave-0 tickets flagged
   `nswag-regen` for owner: **T-0102** (SEC-DSP-01 IsStaffMessage), **T-0104** (SEC-EMP-01 analytics IDOR),
   **T-0111** (LG-SEC-02 mobile subscribe idempotency), **T-0112** (LG-SEC-06 admin loyalty command). If any
   were **not** regenerated, the affected frontend/mobile clients are stale against the merged backend
   contract. **Owner to confirm:** are these four regens applied? (Wave 2 shipped its own regens for the
   refund command + package DTO — independent of these four.)
3. **IMP-1 Google OAuth ClientId** — server-side verification (T-0105) is `done`; **live** Google sign-in
   needs a Google Cloud Console project + ClientId. Owner setup. Not a Wave-3 dependency.
4. **CZ Stripe-fee figure** — `RefundStripeFeeRate` / `RefundStripeFixedFee` on the **CZ**
   `CountryConfiguration` are currently **null → platform absorbs** the Stripe fee on goodwill refunds. The
   T-0167 mechanism is shipped; the owner sets the CZ figures **when CZ should start deducting the fee on
   pure change-of-mind goodwill refunds** (service-fault refunds always absorb per ADR-0009). A product/
   finance call, not code.
5. **Fiscal go-live gates (T-0220 / T-0221) — DONE in code, ACTIVATE on DE/AT/ES launch only.** The gapless
   FiscalCounter + per-provider register idempotency are merged but only take effect for **BlockingOnline**
   regimes (DE TSE / AT RKSV / ES VeriFactu). **Not** active for CZ/SK/PL. Before any DE/AT/ES go-live the
   owner must also implement the **corrective fiscal document** per Q-REFUND-01 (CONFIRMED in ADR-0009 D7:
   DE Stornobeleg / ES rectificativa-or-anulación / AT Storno) — that go-live ticket is **not** in Wave 3.

---

## 4. L-splits authorized this pass (5)

Each `L` ticket may not run whole (PM constraint). Authorized splits, contract-first per `routing.md` (lock
the command/DTO + domain-method shapes with the architect, then db → backend → frontend; reviewer per
developer; security gate on the privileged write paths). Parents become `[SPLIT]` tracking epics.

1. **T-0170 (admin order ops) → 4 children** (the split the ticket body itself proposes):
   - **T-0170a** — generalized cancellation + `CancelledBy` enum (folds **AUD-15**) + admin-cancel command
     through the refund seam (edits `CancelOrder.cs`).
   - **T-0170b** — admin status-override command + endpoint + UI.
   - **T-0170c** — admin reassign command + endpoint + UI (adds the un-assign/reassign domain method).
   - **T-0170d** — admin refund-only command (no status change) through the seam.
   - *Serialize 170a/170d against each other* (both touch `CancelOrder.cs`); 170b/170c are disjoint.
2. **T-0173 (admin dispute mgmt) → 2 children:**
   - **T-0173a (backend)** — Admin `DisputeController` + Partner-endpoint removal + idempotent dispute
     refund through the seam + transition guard (consumes T-0172). The contract.
   - **T-0173b (frontend)** — admin disputes-management feature lib. Held on T-0173a's nswag-regen.
3. **T-0171 (payroll lifecycle) → 5 children** (the ticket body's own suggested split):
   - **171a** backend invoice-adjustment + dispute/reject commands; **171b** backend pay-period MarkPaid +
     Reopen; **171c** AUD-04 partner-surface reconciliation (remove admin endpoints off partner/mobile-
     partner hosts + read-only "my period pay" query); **171d** admin UI; **171e** partner web + Android
     read-only surface. (171d/171e held on nswag-regen.)
4. **T-0175 (Membership-Plan CRUD) → 2 children:**
   - **T-0175a (backend)** — repo read methods + `Features/Memberships/Admin/*` + Policy consts/map rows +
     `AdminMembershipController` + xUnit (ends at the nswag-regen gate).
   - **T-0175b (admin frontend)** — feature lib + route + sidebar + i18n. Held on regen.
5. **T-0186 (GDPR UI) → 2 children** (persona seam, disjoint files, parallel once split):
   - **T-0186a** — admin Data-Protection feature lib (US-admin-0042); admin-shell cluster tail.
   - **T-0186b** — partner GDPR self-service wiring (US-partner-0007).

> **T-0191 is NOT formally split** (kept as one `L` id) but runs as **four internal sub-tickets** —
> (a) CC-02 in-use guard, (b) CC-03 activate/deactivate, (c) CC-04 set-default-currency, (d) CC-06
> default-language — that share no file and run as parallel sub-tickets once T-0142's gate is confirmed
> (`done`). **Sub-(d) is held on Q-W3-1.** If the owner prefers, this can be promoted to a formal split at
> intake; the PM's recommendation is to keep one tracking id with internal sequencing, as the ticket body
> already specifies the seams.

---

## 5. Open questions for the owner

> **One blocking item.** Q-REFUND-03 (Wave 2) remains open/non-blocking. The Wave-3 ForceQualifyReferral
> scope decision (T-0176 AC2) is an **architect** call at contract-lock, not an owner question — noted in §6.

- **Q-W3-1 [blocking: yes — gates ONLY T-0191 CC-06 sub-(d)] Default-language policy for catalog
  translations.** Choose **(a)** `Language.IsDefault` + `SetDefaultLanguage` + relax the
  all-5-languages-required validator to a fallback (needs an owner ef-migration), or **(b)** document
  translations as mandatory-for-all + define add-a-language behavior (no migration). The other three CC
  findings in T-0191 (in-use guard, activate/deactivate, set-default-currency) and **all of the rest of
  Wave 3 proceed regardless** — only the CC-06 sub-work waits. Default taken: hold CC-06 sub-(d) only.
  Filed in `questions/open.md`. **The PM does not invent the answer.**

---

## 6. Definition of "Wave 3 done"

Every Wave-3 ticket `done` (reviewer-reconciled; security gate green for every `security_touching: true`
ticket; qa where behavior is verifiable; optimizer only on the few hot paths); the **5 L-epics**
(T-0170/T-0173/T-0171/T-0175/T-0186) remain `[SPLIT]` tracking epics with **all** children `done`; T-0191's
four sub-tickets `done` (CC-06 only after Q-W3-1 is answered, or explicitly deferred if the owner so
decides); all owner ef-migrations + NSwag regens listed in §3.1 confirmed applied; the admin-shell and
PolicyBuilder snapshot updates landed in their PRs without cluster races. INDEX.md rows + this doc reflect
reality. Then — and only then — Wave 4 (the consistency/quality sweep T-0196…T-0206, then the test+a11y wave
T-0210…T-0218) opens.

---

## 7. Explicitly NOT in Wave 3 (deferred on purpose)

- **T-0196…T-0206** — the consistency/decomposition/quality sweep (the 187 canonicalization, god-unit
  decomposition, de-triplication, dead/unsafe code, S6 logging, perf). The next wave after this one.
- **T-0210…T-0218** — the feature-level + integration test wave + a11y (webhook/refund/invoice/Functions/
  authz/fiscal integration tests, error-contract parity, accessibility). Lands *with* its features per TDD,
  but the standalone integration-test tickets are sequenced after Wave 3.
- **CC-05** (drive money formatting from the configured default currency / remove hardcoded `'CZK'`) —
  explicitly excluded from T-0191; it is L, cross-cutting (all 3 apps), and needs its own owner question.
- **The DE/AT/ES corrective-fiscal-document go-live ticket** (Q-REFUND-01 / ADR-0009 D7) — implemented only
  when a BlockingOnline country launches; not a Wave-3 ticket.
- **T-0176 ForceQualifyReferral (AC2)** — the ticket allows the **architect** to defer it at contract-lock
  if it pushes T-0176 past `M`; the reverse/clawback (AC1) is the load-bearing, non-deferrable deliverable.
  This is an architect/PM execution decision, not an owner blocker.
- **Server-side dispute unread model (T-0192 AC5)** — Wave 3 ships the client-side last-viewed timestamp
  only; a durable server-side unread state is a flagged follow-up pending a data-model decision.

---

## Status log
- 2026-06-09 — Wave-3 plan drafted (PM). Reconciled the 12 Wave-2 tickets (`draft` → `done`) against merge
  `8ff35d49` (PR #75) in INDEX.md. Read all 26 Wave-3 candidate frontmatters; verified every dependency is
  `done` (T-0100/0111/0112/0115/0140/0141/0145/0148/0161/0164 + the T-0142/T-0143 epic children) — **zero
  open external deps.** Grouped into 6 batches (3A refund-seam spine → 3B payroll → {3C loyalty/membership,
  3D Functions, 3E identity/GDPR/catalog} parallel → 3F rate-limit last), honoring the PolicyBuilder,
  admin-shell, dispute-backend, and Users-migration serialization clusters. Authorized 5 L-splits
  (T-0170→4, T-0173→2, T-0171→5, T-0175→2, T-0186→2); T-0191 kept as one id with internal a/b/c/d
  sub-sequencing. Filed **Q-W3-1** (blocking, gates only T-0191 CC-06). Surfaced 5 carry-forward owner items
  (T-0159 rotate-mapbox-token outstanding; confirm Wave-0 nswag-regens T-0102/0104/0111/0112; IMP-1 OAuth
  ClientId; CZ Stripe-fee figure; DE/AT/ES fiscal go-live gates). Proposed; awaiting owner sign-off before
  any promotion to `ready`.
