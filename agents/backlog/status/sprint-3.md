# Sprint 3 — Wave 1 plan (foundational ADRs + integration/outbox contracts)

- **Date:** 2026-06-05
- **Goal:** Sequence Wave 1 for owner approval. **PLAN ONLY — nothing promoted to `ready`, no code,
  no migrations.** Wave 1 lands the foundational ADRs (REFUND, INTEGRATION, soft-delete, outbox-table)
  and the contract/plumbing tickets the Wave-2 feature work depends on.
- **Status of this doc:** proposed. Awaiting owner sign-off on scope, the two must-decide-first items,
  and the L-split authorizations before the PM promotes any ticket to `ready`.

---

## 0. Preconditions / gate into Wave 1

Wave 1 must not open until these hold (execution is strictly wave-by-wave per INDEX.md):

- **T-0230 (Wave-0 PR-review fixes) must close.** It is `in-progress` (`sprint: 0`) with open items:
  **#7** (PromoCodeService slot leak), **#8** (GenerateInvoice envelope dual-read — latent), **#11**
  (`Orders (PaymentStatus, CreatedOn)` recon index — **owner ef-migration**), **#12** (DeadLetter
  ITenantEntity intent). It also has a **pending owner EF-migration** for the #6 `(TenantId, Email)`
  unique index + #11 recon index — the EF model no longer matches `20260605103318_Initial`. **Wave 0
  is not green until this migration is regenerated and applied and the remaining items land.**
- **ADR prerequisites already satisfied:** ADR-0001 (authz), 0002 (outbox contract), 0003 (ratelimit),
  0004 (fiscal) are all `accepted`. Wave 1 *writes* the four remaining ADRs (REFUND, INTEGRATION,
  soft-delete, outbox-table) as ticket deliverables.

> **Correction to a stale INDEX note:** INDEX.md:63 says BLIND-1/T-0146 is "blocked on a non-existent
> T-0141 ADR-INTEGRATION." **T-0141 now exists** as a real ticket file (`tickets/T-0141-adr-integration.md`,
> `status: draft`). The dependency is real and satisfiable — T-0146 is gated on T-0141 + T-0118 (both
> trackable, T-0118 is `done`), not on a phantom. INDEX row will be corrected when Wave 1 opens.
> BLIND-2 (Mapbox token in URL query) genuinely has **no ticket yet** — see §4 open question Q-W1-3.

---

## 1. Scope — the Wave-1 tickets

All 12 are filed and `draft`. Three are `L` and **must be split before `ready`** (the PM never runs an
`L`). Sizes/layers/flags below are from each ticket's frontmatter.

| ID | Title (short) | Size | Layers | Owner(s) | sec | manual_step |
|----|---------------|------|--------|----------|-----|-------------|
| **T-0141** | ADR-INTEGRATION (IHttpClientFactory + error-class + async-email) | M | architect, backend | architect (+reviewer) | no | — |
| **T-0140** | ADR-REFUND (refund/dispute money path + chargeback) | M | architect, backend | architect (+reviewer) | no | — |
| **T-0142** | ADR + soft-delete sweep (Deactivate vs Remove) | **L → split** | architect, backend, db | architect→db→backend | no | ef-migration |
| **T-0143** | Full transactional outbox (table + drainer + Bucket-B) | **L → split** | backend, functions, db | architect→db→backend→functions | no | ef-migration |
| **T-0144** | Stripe + SendGrid via IHttpClientFactory | M | backend | backend (+reviewer) | no | — |
| **T-0145** | Error classification across integration layer | M | backend | backend (+reviewer) | no | — |
| **T-0146** | Registration/reset email off critical path (async) | M | backend, functions | backend→functions (+reviewer, **+security**) | **yes** | — |
| **T-0147** | Membership commands: provider try/catch + S7 reconcile | M | backend | backend (+reviewer, **+security**) | **yes** | — |
| **T-0148** | Tier-threshold config read + persist grant/revoke Reason | M | backend | backend (+reviewer) | no | — |
| **T-0149** | Refresh-token rotation re-checks profile (per host) | S | backend | backend (+reviewer, **+security**) | **yes** | — |
| **T-0150** | Centralize CZE/Mapbox-bounds/2000-char constants | S | backend, frontend, mobile | backend+frontend+android (+reviewers) | no | — |
| **T-0151** | Migrate remaining queue consumers onto Functions.Core | M | functions | functions (+reviewer) | no | — |

**L-split proposals** (each child carries its slice of the parent's ACs; the parent stays `draft` as an
epic until split). These splits need owner acknowledgement before the children go `ready`:

- **T-0142 → 3 children:** (a) the soft-delete ADR (architect); (b) SavedAddress soft-delete +
  `IsActive` read filters + null-FK decision + the filtered-unique-index migration (backend+db);
  (c) Device verdict application (backend). (b) and (c) are file-disjoint → parallel after (a).
- **T-0143 → 4 children:** (a) outbox-table ADR (architect, answers ADR-0002 D1.3 in-Functions-drainer
  question + table schema); (b) outbox table + EF config + migration flag (db); (c) durable
  `IPendingDispatch` backing + drainer + host decision (backend/functions); (d) Bucket-B migration onto
  the per-iteration outbox row (backend). Strictly serial — same dispatch/pipeline surface.

**Explicitly NOT in Wave 1** (left out on purpose):
- T-0219 / T-0220 / T-0221 (`sprint: 2`) — anon-catalog + the two fiscal go-live gates. Wave 2.
- T-0222 (`sprint: 2`, pay-split rounding) — S follow-up from TC-PAY; cold money-math correctness,
  no Wave-1 dependent. Wave 2.
- T-0230 deferred items #16/#19/#20/#24 — cold-path receipt/GDPR follow-ups; ride a later wave with
  their feature, not Wave 1. (The *open MUST/recommended* items #7/#8/#11/#12 are the **Wave-0 close**,
  not Wave 1 — see §0.)
- All AUD-* / FUP-* / T-0001…T-0016 — Wave 2/3 per the wave plan.

---

## 2. Sequence + dependency rationale (the DAG governs, not id order)

Wave 1 splits into **three ordered batches**. The whole wave is gated behind the §0 Wave-0 close.

### Batch 1A — ADRs first (architect, parallel). The gate for everything else.
Run all four ADR authorings concurrently (each ADR-only ticket touches no shared source, so no
collision). Each gets a reviewer in parallel.

| Ticket | depends_on | Why first |
|---|---|---|
| **T-0141** ADR-INTEGRATION | — | Gates T-0144, T-0145, T-0146, T-0147 (and Wave-2 BLIND-7). |
| **T-0140** ADR-REFUND | — (cites 0001/0002) | Gates Wave-2 AUD-01, dispute-mgmt bundle, D-06. No Wave-1 code consumer — could even slip to the Wave-1/Wave-2 boundary, but cheapest to clear now while the architect is engaged. |
| **T-0142(a)** soft-delete ADR | — | Gates T-0142(b/c) and Wave-2 CC-02/03/04/06. |
| **T-0143(a)** outbox-table ADR | — (cites 0002; T-0118 done) | Gates T-0143(b/c/d). Must answer ADR-0002 D1.3 (does the Functions host get the drainer). |

> **Gate:** No dependent code ticket goes `ready` until its governing ADR is `accepted` (reviewer
> reconciled). This is the one hard serialization point of the wave.

### Batch 1B — contract/plumbing code (fan out by dependency once the ADRs land).
These can run **largely in parallel** — they touch disjoint surfaces — with two serialization chains.

**The integration chain (serial, one surface family):**
- **T-0144** (Stripe+SendGrid → IHttpClientFactory) — `depends_on: T-0141`. Touches `StripeClient.cs`,
  `EmailService.cs`, host DI.
- **T-0145** (error classification) — `depends_on: T-0141, T-0144`. Adds the classifier onto the
  routed transport; overlaps EmailService/StripeClient → **serialize after T-0144**.
- **T-0146** (async registration/reset email) — `depends_on: T-0141, T-0118✓`. Edits the four auth
  handlers (`Register/RegisterEmployee/ResendConfirmationEmail/RequestPasswordChange`) + adds an email
  consumer. **File-disjoint from T-0144/0145** (different handlers) → may run parallel to them, but
  hold until T-0141 is `accepted`. **security_touching** (reset-token path) → Security gate mandatory.

**The outbox chain (serial — `UnitOfWorkPipelineBehavior` + queue cluster):**
- **T-0143(a→b→c→d)** after its ADR. This is the same dispatch/pipeline cluster as Wave-0 F2/F4/F3
  (TICKET-MAP shared-file row 3). **Must not run concurrently with any other cluster member.** Its
  Bucket-B child (d) edits `LoyaltyService.cs` → serialize against T-0148 (also `LoyaltyService.cs`).
- **T-0151** (migrate remaining consumers onto Functions.Core) — `depends_on: T-0121✓`. Edits the same
  `Cleansia.Functions/Functions/*.cs` files as the outbox work → **serialize against T-0143's Functions
  edits**; sequence it after the Wave-0 Functions tickets have settled (they're `done`), and not
  concurrently with T-0143(c). Pure move-and-reference; precondition for the Wave-4 TC-8 sweep.

**Independent / parallel (no integration- or outbox-surface collision):**
- **T-0147** (membership try/catch + S7) — `depends_on: T-0141`. Edits the four membership commands.
  **Serialize against T-0111 (LG-SEC-02, Wave-0 `done`) only if both ever touch
  `CreateMembershipSubscription.cs` at once** — T-0111 is done, so no live conflict. security_touching.
- **T-0148** (tier thresholds + grant/revoke Reason) — `depends_on: T-0112✓`. `LoyaltyService.cs`
  cluster → **serialize against T-0143(d)'s Bucket-B `LoyaltyService.cs` edit.**
- **T-0149** (refresh-token profile re-check) — `depends_on: T-0100✓`. Four distinct AuthControllers,
  no cluster collision → fully parallel. security_touching → Security gate.
- **T-0150** (centralize constants) — no deps. Cross-stack (backend+frontend+mobile). **Serialize
  against any DA-8 work on `AddSavedAddress.cs`/`UpdateSavedAddress.cs`** (DA-8 is Wave 3, not live) →
  effectively free to run anytime. Good parallel filler / first-mover.

### Batch 1C — closeout
PM verifies every Wave-1 ticket is `done` (reviewer + security where applicable + qa), updates INDEX +
this doc, flags all owner manual_steps as cleared, then opens Wave 2.

### Parallelism summary
- **Strictly serial:** the four ADRs gate their dependents; the integration chain
  (T-0144 → T-0145); the outbox chain (T-0143 a→b→c→d, with T-0151 not concurrent with it on Functions
  files); `LoyaltyService.cs` (T-0143d ↔ T-0148).
- **Safely parallel (different files, deps satisfied):** within Batch 1A all four ADRs; within Batch 1B
  the three chains (integration / outbox / loyalty-auth-constants) run alongside each other; T-0149 and
  T-0150 are fully independent and can start as soon as the wave opens (T-0149 needs only T-0100✓).

### Lower-id-depends-on-higher-id callouts (DAG over id order)
- **T-0143** (lower) `depends_on: T-0148` (higher) **and** T-0118. The outbox Bucket-B migration rides
  the post-T-0148 `LoyaltyService` shape → T-0148 must precede T-0143's Bucket-B child. *(Within the
  wave this also means T-0148 is effectively a prerequisite of the outbox closeout, not just a sibling.)*
- **T-0145** (lower) `depends_on: T-0144` (lower) — fine, but note T-0145 also needs T-0141.
- All ADR-dependents (T-0144/145/146/147) depend on T-0141 which is a **lower** id than its own
  dependents — natural here, but the PM sequences by the edge, not the number.

---

## 3. External / owner-action blockers

| Blocker (owner-only) | Blocks | Notes |
|---|---|---|
| **Wave-0 close: regenerate + apply the T-0230 EF migration** ((TenantId,Email) unique idx + recon idx) | the entire Wave-1 gate (§0) | `dotnet ef` is owner-only; Claude will not run it. |
| **EF migration — T-0142(b)** (soft-delete: filtered/partial unique index for single-default-address across deactivated rows) | T-0142(b) backend close | Flagged; held until owner confirms applied. |
| **EF migration — T-0143(b)** (outbox table + ProcessedMessage/DeadLetter as needed) | T-0143(c) drainer/backing | Held until applied. |
| **Provision real `Csrf:Secret`** (from T-0123/PROD-CONFIG) | prod deploy of any wave, not a Wave-1 *merge* | Surfaced so it isn't lost. |
| **ForwardedHeaders prod config** (Q-RATELIMIT-02, from T-0115) | enabling rate-limit in prod | Merge-safe; feature-disabled until confirmed. |
| **Google Cloud project / live ClientId (IMP-1)** | live Google OAuth | Not a Wave-1 ticket dependency; T-0105 is `done` server-side. Listed for completeness. |
| **NSwag regen** | none in Wave 1 (every Wave-1 ticket is `manual_steps: []` for NSwag) | The DTO-additive note in T-0148 only triggers regen if an admin-ledger UI is scheduled in the same wave — it is not. |

> No Wave-1 ticket needs NSwag regen. The two go-live fiscal gates (T-0220/T-0221) gate **DE/AT/ES**
> launch and are **Wave 2** — not a Wave-1 blocker.

---

## 4. Open questions for the owner (must decide before execution)

> Filed to `agents/backlog/questions/open.md` as Q-W1-1…Q-W1-3. Q-W1-1 and Q-W1-2 are **blocking** the
> start of Wave 1.

- **Q-W1-1 [blocking] — Confirm the Wave-0 close.** T-0230 is still `in-progress` with #7/#8/#11/#12
  open and an unrun owner EF-migration. Wave execution is strictly wave-by-wave. Do we (a) finish
  T-0230 + apply its migration as the gate into Wave 1 (recommended), or (b) treat the merged PR #72 as
  "Wave 0 done" and absorb #7/#8/#11/#12 into Wave 1? This decides whether Wave 1 starts now or after a
  short Wave-0 finish.

- **Q-W1-2 [blocking] — Authorize the two L-splits.** T-0142 and T-0143 are `L` and cannot go `ready`
  un-split. Approve the proposed child breakdowns in §1 (3 children for T-0142, 4 for T-0143)? The
  outbox-table ADR child (T-0143a) must also **answer ADR-0002 D1.3** (does the Functions host get the
  post-commit behavior / drainer / both / neither) — confirm the architect owns that decision in the ADR.

- **Q-W1-3 [non-blocking] — BLIND-2 (Mapbox token in URL query → leaks into traces/logs).** It's a
  real Wave-1-labeled security finding with **no ticket**. Default taken: the PM files it as a small
  `security_touching` backend ticket (move the token to a header/secret, scrub query from logs) and
  slots it into Batch 1B alongside the integration work, since it lives near the Mapbox client T-0144/
  0145 touch. Owner: confirm it belongs in Wave 1, or defer to Wave 2 with the other integration
  hardening. (If confirmed Wave 1, the PM creates T-0152 and runs `security` on it.)

- **Q-W1-4 [non-blocking] — T-0140 ADR-REFUND timing.** It has no Wave-1 *code* consumer (its consumers
  are all Wave 2). Default: author it in Batch 1A while the architect is engaged, so Wave-2 AUD-01 /
  dispute-mgmt aren't gated later. Owner may defer it to the Wave-1→Wave-2 seam instead. No downside to
  doing it now.

---

## 5. Definition of "Wave 1 done"
Every Wave-1 ticket `done` (reviewer reconciled; security gate green for T-0146/0147/0149 and any
BLIND-2 ticket; qa where there's behavior to verify); the four ADRs `accepted`; both L-epics split and
their children `done`; all owner manual_steps (the two ef-migrations) confirmed applied; INDEX.md rows
and this doc reflect reality. Then — and only then — Wave 2 opens.

---

## 6. Batch-1A reconciliation + Batch-1B promotion (2026-06-06)

### Batch 1A — all four ADR tickets reconciled to `done`
The architect authored the four Wave-1 ADRs (+ the superseding refund-policy ADR); the PM ran the
reviewer reconciliation against each ADR's decision-completeness, internal consistency, frontmatter
wiring, and non-contradiction with the ADRs it extends, with spot-verification of load-bearing code
anchors. **All four reconciled to `done`; no reviewer flagged a real gap.**

| Ticket | ADR produced | Reviewer verdict | State |
|---|---|---|---|
| **T-0141** ADR-INTEGRATION | **ADR-0005** integration-resilience | APPROVE (D1-D4 complete; extends ADR-0002 D3.3; 0005-not-0004 filename explained) | **done** |
| **T-0140** ADR-REFUND | **ADR-0006** money-path seam **+ ADR-0009** refund policy (supersedes 0006 on policy only) | APPROVE (seam frozen; 0009 resolves Q-REFUND-01/02; code facts verified) | **done** |
| **T-0152** soft-delete ADR | **ADR-0007** soft-delete policy | APPROVE (ratifies B6; S10 per-read filter; GDPR boundary; per-entity verdicts) | **done** |
| **T-0155** outbox-table ADR | **ADR-0008** outbox table + drainer | APPROVE (answers ADR-0002 D5+D1.3; backs not contradicts; S8 exception reasoned) | **done** |

### Refund decisions folded into the backlog (Wave-2 BUILD)
ADR-0009 resolved **Q-REFUND-01** (confirmed: corrective fiscal doc per BlockingOnline country, bound to
DE/AT/ES go-live) and **Q-REFUND-02** (all four: 14-day soft window; platform absorbs Stripe fee on
service-fault; proportional keyed loyalty clawback; FUND per-included-service package pricing), and raised
**Q-REFUND-03** (non-blocking — per-bundle legacy weighting; even-split default ships). The refund BUILD is
**Wave-2**; new ticket files created:

- **T-0160 / AUD-01a** — Refund entity + EF + `PaymentStatus.PartiallyRefunded=6` + `RefundReason` enum.
- **T-0161 / AUD-01b** — `IRefundService` impl + `IStripeClient` idempotency-key param.
- **T-0162 / AUD-01c** — admin partial-refund cmd + share-of-`TotalPrice` allocator + `RefundPolicy` + UX
  (depends_on T-0160, T-0161, **T-0165**; `L`→split).
- **T-0163 / AUD-01d** — `ILoyaltyService.RevokeForPartialRefundAsync` (proportional, per-refund-keyed).
- **T-0164 / AUD-01e** — migrate `CancelOrder`/`ResolveDispute` onto the seam.
- **T-0165 / AUD-02p** — `PackageService.PriceWeight` + even-weight backfill + bundled-gross + admin UX
  (**blocks T-0162**; `L`→split; Q-REFUND-03 gates only its legacy business weighting).

### Batch 1B — promoted to `ready` (ADR gate cleared)
With all four governing ADRs `done`/`accepted`, the gate stated in §2 ("no dependent code ticket goes
`ready` until its governing ADR is accepted") is satisfied. **11 promoted to `ready`; 3 serial-chain tails
stay blocked.**

**Ready (11):**
| ID | Title (short) | Owner | Why ready |
|---|---|---|---|
| T-0150 | Centralize constants | backend (+frontend, android) | no deps — first-mover |
| T-0149 | Refresh-token profile re-check | backend (+security) | T-0100 ✓ |
| T-0159 | BLIND-2 Mapbox token in URL | frontend, config (+security) | independent; owner rotate flagged |
| T-0144 | Stripe+SendGrid via IHttpClientFactory | backend | ADR-0005/T-0141 ✓ — head of integration chain |
| T-0146 | Async registration/reset email | backend, functions (+security) | ADR-0005/T-0141 ✓ + T-0118 ✓ |
| T-0147 | Membership try/catch + S7 | backend (+security) | ADR-0005/T-0141 ✓ |
| T-0148 | Tier thresholds + grant/revoke Reason | backend | T-0112 ✓ |
| T-0153 | SavedAddress soft-delete | backend, db | ADR-0007/T-0152 ✓ — ef-migration flagged |
| T-0154 | Device soft-delete verdict | backend | ADR-0007/T-0152 ✓ |
| T-0156 | Outbox table + EF + migration | db | ADR-0008/T-0155 ✓ — head of outbox chain; ef-migration flagged |
| T-0151 | Migrate consumers onto Functions.Core | functions | T-0121 ✓ |

**Blocked (3) — serial-chain tails, gate satisfied but predecessor not `done`:**
- **T-0145** (error classification) — `depends_on: T-0144` (now `ready`, not `done`); promote when T-0144 done.
- **T-0157** (outbox backing + drainer + host) — `depends_on: T-0156` (now `ready`, not `done`) **and** the
  owner T-0156 ef-migration must be applied; promote when both clear.
- **T-0158** (Bucket-B onto outbox) — `depends_on: T-0157` + `T-0148`; promote when both `done`.

**Serialization respected:** T-0144→T-0145 (`StripeClient.cs`/`EmailService.cs`); T-0156→T-0157→T-0158
(outbox, strictly serial); `LoyaltyService.cs` cluster T-0148→T-0158→T-0163 (T-0148 first); Functions files
T-0151↔T-0157 (T-0151 first). Reviewer per developer; security gate on T-0149/T-0146/T-0147/T-0159.

### Owner manual-steps to flag (held until confirmed)
- **ef-migration** — T-0153 (SavedAddress filtered unique index / columns), T-0156 (`OutboxMessages` table +
  unique `(QueueName, MessageKey)` index). **T-0157 is held until the T-0156 migration is applied.**
- **rotate-mapbox-token** — T-0159 (owner-only; the code fix is merge-safe ahead of rotation).
- The Wave-2 refund tickets carry `ef-migration` (T-0160/T-0163/T-0165) + `nswag-regen` (T-0161/T-0162/
  T-0165) — flagged on the tickets; not due until Wave 2 opens.

## Status log
- 2026-06-05 — Wave-1 plan drafted (PM). Proposed; awaiting owner sign-off on Q-W1-1/Q-W1-2 before any
  ticket is promoted to `ready`.
- 2026-06-06 — Batch 1A reconciled to `done` (4 ADRs accepted, reviewer-reconciled). Refund decisions
  (ADR-0009) folded into 6 new Wave-2 ticket files (T-0160…T-0165). Batch 1B promoted: 11 → `ready`, 3
  serial-chain tails stay blocked. INDEX.md updated. Q-REFUND-03 noted (non-blocking, gates AUD-02p legacy
  weighting). Next: execute Batch 1B (fan out by chain; reviewer per developer; hold the two ef-migrations).
