# Sprint 4 — Wave 2 plan (the refund epic + fiscal go-live gates + non-blocking fast-follows)

- **Date:** 2026-06-07
- **Goal:** Sequence Wave 2 for owner approval. **PLAN ONLY — nothing promoted to `ready`, no code, no
  migrations, no NSwag.** Wave 2 builds the **refund money path** ADR-0006/ADR-0009 designed (the spine),
  lands the two **fiscal go-live gates** that block DE/AT/ES, and clears a few small non-blocking
  fast-follows. The broad admin-feature block (T-0170…T-0195) is **explicitly Wave 3** — see §6.
- **Status:** proposed. Awaiting owner sign-off on scope, the L-split children, and the must-decide item
  (Q-REFUND-03 default) before any ticket is promoted to `ready`.

---

## 0. Pre-flight reconciliation (done as part of this planning pass)

Verified against git + ticket frontmatter — the INDEX was stale; corrected now:

- **Wave 0** = PR #72 (`9a774435`). All T-0100…T-0128 + T-0230 PR-review fixes `done`. Migration
  `20260605165935_Initial` applied. ✅
- **Wave 1 Batch 1A (the 4 ADRs)** = `done`: T-0140→ADR-0006(+0009), T-0141→ADR-0005, T-0152→ADR-0007,
  T-0155→ADR-0008. All ADRs `accepted`. ✅
- **Wave 1 Batch 1B** = merged in `a4f14094` ("Wave-1 Batch 1B — integration resilience, outbox durability,
  soft-delete, loyalty/membership hardening"). **Local master == origin/master == a4f14094.** The ticket
  frontmatter still said `ready`/`draft` for T-0144…T-0159 and the INDEX header still said "WAVE 1 OPEN" —
  **the PM reconciled all 14 (T-0144, T-0145, T-0146, T-0147, T-0148, T-0149, T-0150, T-0151, T-0153,
  T-0154, T-0156, T-0157, T-0158, T-0159) to `done`** with a status-log line on each. T-0166 hotfix already
  `done`. ✅
- **No new ADR is needed for the refund build.** ADR-0006 (seam) + ADR-0009 (policy) are `accepted` and
  freeze every refund decision (allocator, window, fee bearer, loyalty clawback, bundled pricing, corrective
  fiscal doc). The Wave-2 refund tickets are pure BUILD against an accepted contract — **no architect
  deliberation gates Wave 2.**

---

## 1. Scope — the Wave-2 tickets (ordered)

The refund foundation is the spine. Each `L` is **split before `ready`** (children created this pass).

| ID | Title (short) | Size | Layers | Owner(s) | sec | manual_step |
|----|---------------|------|--------|----------|-----|-------------|
| **T-0160** | AUD-01a: `Refund` entity + EF + `PaymentStatus.PartiallyRefunded=6` + `RefundReason` enum | M | db, backend | db (+reviewer) | no | ef-migration |
| **T-0161** | AUD-01b: `IRefundService` seam + ceiling + deterministic `RefundKey` + `IStripeClient` key param | M | backend, clients | backend (+reviewer, **+security**) | **yes** | nswag-regen* |
| **T-0163** | AUD-01d: `ILoyaltyService.RevokeForPartialRefundAsync` (proportional, per-refund-keyed) + enum + index | M | backend, db | backend (+reviewer) | no | ef-migration |
| **T-0231** | AUD-02p1 (split of T-0165): `PackageService.PriceWeight` + even-weight backfill + bundled-gross | M | db, backend | db→backend (+reviewer) | no | ef-migration |
| **T-0164** | AUD-01e: migrate `CancelOrder` + `ResolveDispute` onto the seam (remove inline un-keyed refund) | M | backend | backend (+reviewer, **+security**) | **yes** | — |
| **T-0167** | AUD-01c1 (split of T-0162): admin partial-refund cmd + allocator + `RefundPolicy` + `PartiallyRefunded` | M | backend | backend (+reviewer, **+security**) | **yes** | nswag-regen |
| **T-0168** | AUD-01c2 (split of T-0162): admin partial-refund UX (lines + reason, facade, i18n ×5) | M | frontend | frontend (+reviewer) | no | nswag-regen (consumes) |
| **T-0232** | AUD-02p2 (split of T-0165): admin package-form per-included-service weight UX | S | frontend | frontend (+reviewer) | no | nswag-regen (consumes) |
| **T-0220** | FISCAL-SEQ: gapless-monotonic-atomic fiscal sequence allocator (`FiscalCounter`) | M | backend, db | backend+db (+reviewer, **+security**) | **yes** | ef-migration |
| **T-0221** | FISCAL-AUTH-IDEMP: per-provider `RegisterReceiptAsync` idempotency on `ReceiptNumber` | M | backend, clients | backend (+reviewer, **+security**) | **yes** | — |
| **T-0219** | Anon-catalog → platform config (Service/Category/Package/Extra/ServiceCity) | M | backend, db | db+backend (+reviewer, **+security**) | **yes** | ef-migration |
| **T-0222** | SplitPay currency-minor-unit split + last-share remainder reconciliation | S | backend | backend (+reviewer) | no | — |

\* T-0161 `nswag-regen` only if a refund **response DTO** surfaces on a client — flag at implementation time;
the admin refund command DTO regen is firmly on **T-0167**.

**`[SPLIT]` tracking epics (do NOT run as one ticket):**
- **T-0162** (AUD-01c, `L`) → **T-0167** (backend) + **T-0168** (frontend).
- **T-0165** (AUD-02p, `L`) → **T-0231** (db+backend) + **T-0232** (frontend). The old `T-0162 depends_on
  T-0165` edge is now carried by **T-0167 depends_on T-0231**.

---

## 2. Sequence + dependency rationale (the DAG governs, not id order)

The refund DAG (verified against frontmatter this pass):

```
T-0160 (entity+enums) ──┬─► T-0161 (seam) ──┬─► T-0164 (migrate cancel/dispute)
                        │                   └─► T-0167 (admin cmd) ─► T-0168 (admin UX)
                        ├─► T-0163 (loyalty revoke)        ▲
                        └────────────────────────────────  │
T-0231 (PriceWeight) ───────────────────────────────────────┘  (blocks T-0167) ─► T-0232 (weight UX)
```

### Batch 2A — the refund foundation (parallel where files are disjoint)
The schema + the two independent derivation pieces. **T-0160 is the single root.**

| Ticket | depends_on | Parallel with | Why |
|---|---|---|---|
| **T-0160** | — | — | Root. Entity + enums + unique `RefundKey` index. Owner **ef-migration** gates everything below. |
| **T-0163** | T-0160 | T-0231, (after T-0160) T-0161 | Loyalty clawback method — disjoint from the seam (edits `LoyaltyService.cs`, NOT the Stripe path). |
| **T-0231** | — | T-0160, T-0163 | `PackageService.PriceWeight` — fully independent of the refund entity; can start day 1 alongside T-0160. **Blocks T-0167.** |

> **Lower-id-depends-on-higher callout:** none new in the refund epic itself. But **T-0167 (lower id)
> `depends_on: T-0231 (higher id)`** — the bundled-gross basis must exist before the allocator can refund a
> bundled line (ADR-0009 fact 8). **AUD-02p → AUD-01c is the load-bearing cross-edge: T-0231 must be `done`
> before T-0167 goes `ready`.**

### Batch 2B — the seam + its two migrations (serial on `StripeClient.cs`/money path)
| Ticket | depends_on | Notes |
|---|---|---|
| **T-0161** | T-0160 (+owner ef-migration applied) | The one Stripe-refund seam. **security_touching.** Touches `StripeClient.cs` — no live conflict now (T-0144 BLIND-5 pooled client is `done`). |
| **T-0164** | T-0160, T-0161 | Migrates `CancelOrder` + `ResolveDispute` onto the seam; removes the inline un-keyed refund. **security_touching.** Serialize against any other live `CancelOrder.cs`/`ResolveDispute.cs` editor (none in this wave). |

### Batch 2C — the admin partial-refund command + UX (serial on the contract)
| Ticket | depends_on | Notes |
|---|---|---|
| **T-0167** | T-0160, T-0161, **T-0231** | Allocator + `RefundPolicy` + admin-only command + `PartiallyRefunded`. **security_touching.** Edits `Policy.cs`/`PolicyBuilder.cs` — serialize the `AdminOnly` map row against other `Policy.cs` editors (none in this wave). **nswag-regen** (admin client gains the command). |
| **T-0168** | T-0167 (+owner nswag-regen) | Admin refund UX. **Held until the owner regenerates the admin client.** |
| **T-0232** | T-0231 (+owner nswag-regen) | Admin package-weight UX. **Held until regen.** Resolves Q-REFUND-03 per-bundle. |

### Batch 2D — fiscal go-live gates + non-blocking fast-follows (independent of the refund epic)
These do **not** touch the refund money path and can run **fully in parallel** with Batches 2A–2C.

| Ticket | depends_on | Notes |
|---|---|---|
| **T-0220** FISCAL-SEQ | T-0119✓ (ADR-0004) | DE/AT/ES go-live gate. Gapless sequence allocator. **security_touching**, ef-migration. |
| **T-0221** FISCAL-AUTH-IDEMP | T-0119✓ (ADR-0004) | DE/AT/ES go-live gate. Per-provider register idempotency. **security_touching.** Serialize against T-0220 if both edit the fiscal register path. |
| **T-0219** anon-catalog | T-0100✓, T-0113✓ | Multi-tenant correctness for 5 catalog entities. **security_touching**, ef-migration. Serialize against any `CleansiaDbContext` global-filter editor (none live). |
| **T-0222** SplitPay rounding | — | Cold money-math correctness; **gate before multi-employee payouts go live** (not a Wave-2 dependency, but cheap and right-sized to fold in). |

### Parallelism summary
- **Strictly serial:** T-0160 → {T-0161, T-0163}; T-0161 → {T-0164, T-0167}; **T-0231 → T-0167**;
  T-0167 → T-0168; T-0231 → T-0232.
- **Safely parallel (disjoint files, deps met):** **T-0160 ∥ T-0231** (start day 1); **T-0163 ∥ T-0161**
  (loyalty vs Stripe path); the **entire Batch 2D** runs alongside the refund epic; the two NSwag-held UX
  children (T-0168, T-0232) are file-disjoint and run together once their regens clear.
- **Owner-migration gates between batches:** the T-0160 ef-migration gates 2B/2C; the T-0231 ef-migration
  gates T-0167's bundled path; the T-0163 ef-migration can fold with T-0160's if sequenced together.

---

## 3. External / owner-action blockers (which tickets each gates)

| Blocker (owner-only) | Gates | Notes |
|---|---|---|
| **ef-migration — T-0160** (`Refund` table + `PaymentStatus`/`RefundReason` columns + unique `RefundKey` idx) | T-0161, T-0164, T-0167 (the whole seam build) | Claude will not run `dotnet ef`. Held until applied. |
| **ef-migration — T-0231** (`PackageService.PriceWeight` column + even-weight backfill) | T-0167 (bundled-line path), T-0232 | Folds with T-0160's migration if sequenced together. |
| **ef-migration — T-0163** (`LoyaltyEarnSource.OrderPartiallyRefunded` persistence + filtered unique idx on the refund key) | T-0163 close | Folds with T-0160's migration if sequenced together. |
| **ef-migration — T-0220** (`FiscalCounter` table/allocator) | T-0220 close | DE/AT/ES go-live gate. |
| **ef-migration — T-0219** (drop `TenantId` cols + reindex the 5 catalog entities) | T-0219 close | Folds into the owner's Initial regen or an incremental. |
| **nswag-regen — T-0167** (admin client gains the refund command DTO) | **T-0168** (admin refund UX held) | The DTO is added by T-0167; T-0168 cannot consume it until regen. |
| **nswag-regen — T-0231** (admin package DTO gains `PriceWeight`) | **T-0232** (weight UX held) | |
| **nswag-regen — T-0161** (only if a refund **response DTO** surfaces) | flag at impl time | Likely none; the admin command regen is on T-0167. |
| **rotate-mapbox-token** (T-0159, Wave 1) — **STILL OUTSTANDING** | not a Wave-2 ticket; **a live security exposure until rotated** | The code fix merged (token off the URL); the exposed token must still be rotated in the Mapbox account. **Surfaced here so it is not lost.** |
| **Q-REFUND-01 corrective fiscal doc** (resolved/CONFIRMED) | DE/AT/ES go-live only (rides T-0220/T-0221) — **NOT** the CZ/SK/PL refund build | The CZ/SK/PL refund epic (Batches 2A–2C) is NOT gated on this; only the BlockingOnline-country go-live is. |
| **Google OAuth IMP-1** (needs a Google Cloud project/ClientId) | live Google sign-in only | Not a Wave-2 ticket dependency (T-0105 server-side is `done`). Listed for completeness. |

> **No Wave-2 refund ticket is gated on the fiscal-corrective requirement for CZ/SK/PL.** Q-REFUND-01 binds
> the corrective-document piece to the existing DE/AT/ES go-live gate (T-0220/T-0221), confirming the
> refund BUILD ships for CZ/SK/PL without it.

---

## 4. Open questions for the owner (must decide before / during execution)

> Q-REFUND-01 and Q-REFUND-02 are **resolved** (ADR-0009). One non-blocking item remains.

- **Q-REFUND-03 [non-blocking] — per-bundle business weighting of legacy packages.** ADR-0009 D5 backfills
  **even weights** for existing `PackageService` rows. The even-split default ships in **T-0231**; the owner
  sets per-bundle weights in the admin UI (**T-0232**) afterwards. **No decision is needed to start Wave 2.**
  The owner should, before any DE/AT/ES or high-value bundle refund goes live, either (a) confirm even-split
  is acceptable for all current bundles, or (b) set the real weights via T-0232. **Default taken: ship
  even-split; owner corrects post-T-0232.** Recorded in `questions/open.md`.

- **(For owner awareness, not a blocker) — Wave-3 scope confirmation.** The broad admin-feature block
  (T-0170…T-0195, 26 tickets) is sequenced as **Wave 3**, not Wave 2 (§6). T-0170 (admin order ops) and
  T-0173 (admin dispute mgmt) had their `depends_on` **corrected this pass** to include the refund seam
  (T-0161) + the seam migration (T-0164) they consume — they were previously wired only to the ADR. Confirm
  this Wave-2 = "refund foundation + go-live gates" framing, vs. pulling some admin features forward.

---

## 5. Definition of "Wave 2 done"
Every Wave-2 ticket `done` (reviewer reconciled; security gate green for T-0161/T-0164/T-0167/T-0219/T-0220/
T-0221; qa where behavior is verifiable); both `L` epics (T-0162, T-0165) remain `[SPLIT]` tracking epics
with all four children `done`; all owner ef-migrations + the two NSwag regens confirmed applied; the
`rotate-mapbox-token` Wave-1 manual step confirmed done; INDEX.md rows + this doc reflect reality. Then —
and only then — Wave 3 opens.

---

## 6. Explicitly NOT in Wave 2 (deferred to Wave 3, on purpose)

Right-sizing: Wave 2 is the **money-correctness + go-live-gate** wave. The following are real and ticketed
but deliberately Wave 3 — pulling them in would make the wave an un-reviewable mega-batch.

- **The admin-feature block T-0170…T-0195** (26 `sprint:2`-labelled tickets: admin order ops, payroll
  lifecycle, disputes, membership/referral/GDPR/device, catalog activate/deactivate, rate-limit
  fast-follows). Several are themselves `L` and need their own splits. **T-0170 (AUD-01 admin order ops)**
  and **T-0173 (admin dispute mgmt)** are the natural **first Wave-3 batch** — they directly consume the
  Wave-2 refund seam and now depend on it correctly. (Note: these carry `sprint: 2` in frontmatter from the
  original audit plan; treat the **wave** label in this doc as governing — the PM will re-tag them `sprint: 3`
  when Wave 3 opens, to avoid churning 26 files in a plan-only pass.)
- **T-0196…T-0206** — the consistency/decomposition/quality sweep (`sprint: 3`).
- **T-0210…T-0218** — the test + a11y wave (`sprint: 4` in the original plan).
- **T-0230 deferred items #16/#19/#20/#24** — cold-path receipt/GDPR micro-improvements. **DROPPED from
  Wave 2** (judged): they ride their feature when those cold paths are next touched; a standalone polish
  ticket now is backlog noise for marginal cold-path gain (per T-0230's own deferral rationale). Re-file
  only if/when those paths are reopened.
- **FUP-1…FUP-7 / T-0001…T-0016** — folded into the wave plan / Wave 3 per the audit execution plan.

---

## Status log
- 2026-06-07 — Wave-2 plan drafted (PM). Pre-flight reconciled Wave-1 Batch 1B (14 tickets ready/draft →
  `done` against merge a4f14094). Split the two refund `L` epics: T-0162 → T-0167/T-0168; T-0165 →
  T-0231/T-0232 (4 child tickets created). Corrected T-0170/T-0173 `depends_on` to include the refund seam
  (T-0161) + seam migration (T-0164). Wave 2 scoped to the refund foundation + fiscal go-live gates +
  T-0219/T-0222; admin-feature block deferred to Wave 3. Proposed; awaiting owner sign-off before any
  promotion to `ready`. Q-REFUND-03 remains the one (non-blocking) open item.
