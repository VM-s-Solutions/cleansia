# Answered Questions — decision log

Resolved questions, with the owner's answer and where the decision was locked in. This is the
permanent record so a settled decision is never re-litigated.

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
