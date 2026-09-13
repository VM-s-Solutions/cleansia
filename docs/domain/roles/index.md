# Component contracts

One page per component that carries a rule the rest of the system depends on. These are **contracts**,
not descriptions: each says what the component guarantees, what it refuses, and what breaks if a caller
works around it.

They were internal build notes until the 2026-08 cleanup. They are domain truth — what the platform
does — rather than instructions for writing code, so they live here rather than beside the pattern
catalogues.

| Component | |
|---|---|
| **[AuditGate (ADR-0012 D3 + ADR-0062 D1, accepted 2026-09-13)](./audit-gate)** | **Responsibility (one sentence):** Say which audit table, if any, a request belongs to — the admin table for any `Command` an Administrator runs, the customer table for a marked `Command` a Customer (or an allowed anonymous caller) runs, none for everything else — so both audit behaviors answer identically |
| **[Role](./booking-price-summary)** | **✅ BUILT on both mobile clients** — iOS |
| **[CustomerActionAudit (ADR-0062, accepted 2026-09-13)](./customer-action-audit)** | **Responsibility (one sentence):** Record that a customer did one money-relevant thing — with the figures and versions they were shown, the request context, and the outcome — as a row that outlives the account, the order and the erasure |
| **[Role](./dead-letter-record)** | Introduced by **ADR-0002 D3** (the poison floor: persist + alert + ack, never re-process) |
| **[Role](./employee-payout-details)** | Introduced by **ADR-0034** (`docs/decisions/adr-0034 |
| **[Role](./express-waiver-resolver)** | **✅ BUILT. The "NOT YET BUILT" banner below is stale and is corrected here rather than deleted, so |
| **[Role](./fcm-message-factory)** | Introduced by **ADR-0025** (iOS push display via per-platform APNs alert with loc-keys) |
| **[Role](./idempotency-guard)** | Introduced by **ADR-0002 D2 |
| **[MarketDirectory (ADR-0058, accepted 2026-09-13)](./market-directory)** | **Responsibility (one sentence):** List the markets a customer may browse in — each serviced country joined to its configured, active currency — and name the default one, from an anonymous read that never throws |
| **[Role](./membership-benefit-usage)** | **✅ ACCEPTED AND SHIPPED |
| **[MembershipPlanPrice (ADR-0059, accepted 2026-09-13)](./membership-plan-price)** | **Responsibility (one sentence):** Name what one billing period of a plan costs in one currency and which Stripe Price charges it — the figure shown and the figure billed are the same row |
| **[OperatorTenantResolver (ADR-0061, accepted 2026-09-13)](./operator-tenant-resolver)** | **Responsibility (one sentence):** Answer two questions for one country id — *is it a market?* and *which operating company serves it?* — or, when none is given, for the default market the directory chooses |
| **[OperatorTenantScopeBehavior (ADR-0061, accepted 2026-09-13)](./operator-tenant-scope-behavior)** | **Responsibility (one sentence):** Before validation, give an anonymous market-scoped request the ambient tenant of its market's operator; step aside when a claim exists; refuse `country.not_serviced` / `tenant.not_found` |
| **[Role](./order-availability)** | **THE STANDARD.** Introduced by **ADR-0037** |
| **[Role](./payout-details-validator)** | Introduced by **ADR-0034** (**`accepted`** 2026-08-02) |
| **[Role](./payout-reference-allocator)** | **✅ BUILT AND SHIPPED |
| **[Role](./post-commit-effects)** | **LAW.** Introduced by **ADR-0038** |
| **[Role](./preferred-cleaner-hold-resolver)** | **ACCEPTED — this is the standard |
| **[Role](./preferred-offer-disclosure)** | **Both halves are SHIPPED and the decision behind them is now ratified |
| **[Role](./rate-limit-policy)** | Introduced by ADR-0003 (ADR-RATELIMIT) |
| **[Role](./refund-policy)** | Introduced by **ADR-0009** (`docs/decisions/adr-0009 |
| **[RevokedDeviceDirectory (ADR-0026, accepted](./revoked-device-directory)** | **Responsibility (one sentence):** Answer, from memory and in O(1), whether a given |
| **[RevokedUserDirectory (ADR-0027, accepted 2026-07-15, amendments U1–U3; extends ADR-0026 X1)](./revoked-user-directory)** | **Responsibility (one sentence):** Answer, from memory and in O(1), whether a given `userId`'s |
| **[Tenant (ADR-0061, accepted 2026-09-13)](./tenant)** | **Responsibility (one sentence):** Name an operating company under the holding so that stamped rows and the country map can point at it — seed-only, one row, no admin surface |
| **[Role](./tenant-provider)** | The seam every tenancy bug in this repo has passed through, and the reason it has no earlier card is |
