# Answered Questions — decision log

Resolved questions, with the owner's answer and where the decision was locked in. This is the
permanent record so a settled decision is never re-litigated.

---

### Q-W5-1 — Plus-membership free-cancellation-window direction
- Raised by: pm (T-0242 / carried T-0211 TC-7 finding) · Answered: 2026-06-14
- Question: `BookingPolicy.CalculateCancellationFeeRate` treated `freeCancellationHoursOverride` so that a
  **larger** override made the free window **stricter**, contradicting the doc/intent that "Plus = more
  generous". Confirm the intended product direction and the fix path: (a) pass a smaller override on the
  Plus path, or (b) invert the override semantics in `BookingPolicy`.
- **Answer (owner): PATH (B) — Plus members get a MORE generous (longer) free-cancellation window; invert
  the override semantics in `BookingPolicy` so a larger free-window value WIDENS (does not narrow) the
  window.** Implemented by **T-0242** (Wave 6).
- **Locked in:** T-0242 (now `done`, Wave-6 close `b8f89202`). Implementation note: under the existing
  caller/resolver wiring the override is supplied as an **absolute free-window threshold** (resolver
  returns 24 for standard, `MembershipPlan.FreeCancellationWindowHours` for Plus). The owner's "Plus =
  wider" intent is therefore satisfied by the **absolute-threshold contract (AC2 path a)** — a Plus plan
  seeded below 24h is already more generous than the standard 24h — with NO out-of-lane caller/resolver/
  seed change. A literal `BookingPolicy`-only inversion (the path-(b) wording) leaked the standard tier to
  all-free (reviewer caught it; security re-gate confirmed the revert). Net effect matches the owner's
  product intent; `BookingPolicy.CalculateCancellationFeeRate` stays `freeWindow =
  freeCancellationHoursOverride ?? FreeCancellationHours` with a clarified param doc. T-0211's
  `CancellationFeeRateBoundaryTests` re-pinned to the corrected (absolute) intent; adversarial money
  review + security re-gate both PASS; orchestrator clean run green (Cleansia.Tests 1513/1513). If the
  product later wants a different Plus free window, only the seeded `FreeCancellationWindowHours` changes
  (owner-only). **RESOLVED.**

---

### Q-W1-1 — Confirm the Wave-0 close before Wave 1 opens
- Raised by: pm (Wave-1 planning) · Answered: 2026-06-05
- **Answer (owner): Wave 0 is CLOSED — T-0230 is reconciled to `done`.** Its #7/#8/#11/#12 shipped in
  PR #72 and the owner's migration `20260605165935_Initial` is in master. Only the non-blocking deferred
  items #16/#19/#20/#24 remain, moved to **Wave-2 polish**. **T-0230 does NOT gate Wave 1.**
- **Locked in:** T-0230 status → `done` (owner-reconciled); INDEX.md Wave-1 banner + sprint-3 §0 updated
  to drop the T-0230/EF-migration gate; Wave 1 is cleared to open.

### Q-W1-2 — Authorize the two Wave-1 L-splits
- Raised by: pm (Wave-1 planning) · Answered: 2026-06-05
- **Answer (owner): AUTHORIZED.** Split T-0142 into its 3 proposed children and T-0143 into its 4
  proposed children (sprint-3 §1). Confirmed the **architect owns the ADR-0002 D1.3 decision** (does the
  Functions host get the post-commit behavior / drainer / both / neither) inside the T-0155 outbox-table
  ADR.
- **Locked in:** T-0142 → children **T-0152/T-0153/T-0154** (a→{b∥c}); T-0143 → children
  **T-0155/T-0156/T-0157/T-0158** (a→b→c→d, serial; T-0157 depends_on T-0118, T-0158 depends_on T-0148).
  Parents marked `[SPLIT]` with `split_into:`. ADR children (T-0152, T-0155) promoted to `ready`.

### Q-W1-3 — BLIND-2 (Mapbox token in URL query) — Wave 1 or Wave 2?
- Raised by: pm (Wave-1 planning) · Answered: 2026-06-05
- **Answer (owner): FILE IT INTO WAVE 1.** The Mapbox access token exposed in a request URL is a
  credential/log exposure; fix it in Wave 1.
- **Locked in:** filed as **T-0159** (`security_touching: true`, `layers: [frontend, config]`,
  `sprint: 1`), independent within Batch 1B (no ADR dependency). Leak site:
  `mapbox-autocomplete.service.ts:116` (`access_token` query param). Token rotation flagged as owner
  `manual_step: rotate-mapbox-token`. Security gate mandatory.

### Q-W1-4 — T-0140 ADR-REFUND timing
- Raised by: pm (Wave-1 planning) · Answered: 2026-06-05
- **Answer (owner): author now in Batch 1A** (the default). No Wave-1 code consumer, but cheapest to
  clear while the architect is engaged so Wave-2 AUD-01 / dispute-management aren't gated later.
- **Locked in:** T-0140 promoted to `ready` in Batch 1A alongside the other three ADRs.

---

### Q-0005 — Is a STAFF dispute reply Employee-or-Admin, or Admin-only?
- Raised by: architect (ADR-0001, lead correction V1) · Answered: 2026-06-01
- **Answer (owner): ADMIN-ONLY.** Staff `CanRespondToDispute` (`IsStaffMessage=true`) → `AdminOnly`
  (was interim EmployeeOrAdmin). The customer self-reply (`CanAddDisputeMessage`, CustomerOnly) is
  unchanged. The staff-reply endpoint also moves off the Partner host onto the Admin host.
- **Locked in:** ADR-0001 (D2 row, Note C, consequences, T-AUTHZ-2 scope, verification #5, ratification
  log) + `architecture/decisions/authz.md`. A cleaner can no longer post staff messages on disputes.

### Q-0001 — Do we need a SuperAdmin tier distinct from Administrator?
- Answered: 2026-06-01 · **Answer (owner): go with default** — `AdminOnly` is the gate; no SuperAdmin
  tier today. Locked in: ADR-0001 admin-user family = AdminOnly.

### Q-0002 — Move to per-host asymmetric JWT signing keys?
- Answered: 2026-06-01 · **Answer (owner): go with default** — keep shared HS256, audience validation is
  the isolation boundary, documented blast radius. Revisit via a future ADR if needed.

### Q-0003 — One trust-zone model for both host pairs, or keep the asymmetry?
- Answered: 2026-06-01 · **Answer (owner): go with default** — freeze current semantics (Customer pair =
  one trust zone; Partner pair = two zones).

### Q-0004 — IMP-1 Google OAuth tokens must carry the `role` claim
- Answered: 2026-06-01 · **Answer (owner): go with default** — all issuance paths set the role claim
  (true today); IMP-1 work MUST preserve it (constraint carried to the IMP-1 ticket).

### Q-RATELIMIT-02 — Confirm production proxy chain / hop count / XFF handling
- Answered: 2026-06-01 · **Answer (owner): CONFIRMED.** Topology is as documented — App Service
  (Standard S1), one trusted hop (App Service front end), no Front Door / App Gateway. Set
  `ForwardedHeaders:ForwardLimit = 1` + `KnownNetworks` = the narrow App Service ingress CIDR. D3
  startup guard stays. Rate-limit feature cleared to enable in prod with this config. Locked into
  ADR-0003 deploy gate.

### Q-RATELIMIT-03 — May Wave 0 ship the confirmation-code brute-force surface unmitigated (per-IP only)?
- Answered: 2026-06-01 · **Answer (owner): YES, ship it.** Wave 0 ships per-IP-only; `BSP-4b`
  (account-lockout / per-confirmation-code throttle) is a **fast-follow**, NOT an in-wave blocker.
  Distributed code-guessing residual risk accepted for launch, tracked as BSP-4b.

### Q-RATELIMIT-01 — Distributed (cross-instance) rate limiter trigger
- Answered: 2026-06-01 · **Answer (owner): go with default** — API instances pinned to 1; in-process
  limiter acceptable for Wave 0. Scaling >1 instance requires a distributed-limiter superseding ADR first.

---

### Q-AUDIT-01 — Admin-action-audit retention window + actor-PII aging (ADR-0012)
- Raised by: architect (AUD-AUDITLOG / ADR-0012 D6) · Owner: owner | legal · Resolve-by: pre-prod
  (but **non-blocking for implementation**) · Answered: 2026-06-22
- Question: the regulatory **minimum retention window** for `AdminActionAudit` rows (refund,
  order-status override, pay-config change, GDPR delete/export, loyalty grant/revoke, dispute resolve),
  and whether the **actor's** PII (email) must age out independently of the accountability record.
- **Answer (owner, 2026-06-22) — DEFAULT NOW, RATIFY-BEFORE-PROD.** The owner chose "sensible default
  now, ratify the exact window + redaction list before production." The default is **baked into the
  Wave-9 tickets and locked in here**:
  1. **Retention = keep audit rows INDEFINITELY for now** — **no auto-prune of the audit table**.
     Accountability data is cheap and legally safer to keep; a retention window is a **separate pre-prod
     decision**, not a launch blocker. The `Cleansia.Functions` cleanup seam MUST NOT delete
     `AdminActionAudit` rows (enforced by ADR-0012 D6 + T-0287 AC2's explicit exclusion).
  2. **PII = the before/after snapshots store entity ids + the CHANGED fields ONLY, never raw subject
     PII** (no name/email/address/card data). Enforced by ADR-0012 D4.1 + **T-0284 AC2/AC3** (typed,
     producer-redacted snapshots, reviewed at the type level).
  3. **GDPR-delete is a legal-basis exception to erasure** — the GDPR-delete audit keeps **actor + scope
     + subject id**, NOT the subject's personal data, so the audit row **legitimately survives** the
     subject's erasure (the audit log is the accountability record of "an admin deleted user X"). Enforced
     by ADR-0012 D6 + **T-0284 AC3** (survives-erasure test, scope+ids only).
- **Status:** this is a **default the owner ratifies pre-prod** — the owner ratifies the **exact retention
  window** + the **exact redaction list** before production. Until then the append-only / no-auto-delete /
  PII-minimized default ships; an eventual answer only **narrows** retention (a config/cleanup-policy
  value) and does **not** change the seam. **RESOLVED (default adopted; pre-prod ratification still owed —
  carried on the pre-PROD readiness checklist, NOT as an open question).**
- **Locked into:** ADR-0012 D6 / D4.1 + the Wave-9 tickets T-0282 (no-delete config) / T-0284 (PII-min +
  survives-erasure) / T-0287 AC2 (cleanup excludes the audit table). Living doc
  `architecture/decisions/audit-log.md` "Open questions" updated to point here.
