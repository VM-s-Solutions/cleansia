# Component contracts

One page per component that carries a rule the rest of the system depends on. These are **contracts**,
not descriptions: each says what the component guarantees, what it refuses, and what breaks if a caller
works around it.

They were internal build notes until the 2026-08 cleanup. They are domain truth — what the platform
does — rather than instructions for writing code, so they live here rather than beside the pattern
catalogues.

| Component | |
|---|---|
| **[AuditGate (ADR-0012 D3 + ADR-0062 D1, accepted 2026-09-13, host-aware since 2026-09-14, admin-host arm since 2026-09-15)](./audit-gate)** | **Responsibility (one sentence):** Say which audit table, if any, a request belongs to — the admin table for any `Command` an Administrator runs, the customer table for a marked `Command` a Customer runs, the marker's own table for an allowed anonymous caller on the host that serves that audience (a customer marker on a customer host, the admin sign-in on the admin host), none for everything else — so both audit behaviors answer identically |
| **[Role](./booking-price-summary)** | **✅ BUILT on both mobile clients** — iOS |
| **[CustomerActionAudit (ADR-0062, accepted 2026-09-13, amended 2026-09-14)](./customer-action-audit)** | **Responsibility (one sentence):** Record that a customer did one money-relevant or account-relevant thing — with the figures and versions they were shown, the request context, and the outcome — as a row that outlives the account, the order and the erasure |
| **[Role](./dead-letter-record)** | Introduced by **ADR-0002 D3** (the poison floor: persist + alert + ack, never re-process) |
| **[Role](./employee-payout-details)** | Introduced by **ADR-0034** (`docs/decisions/adr-0034 |
| **[Role](./express-waiver-resolver)** | **✅ BUILT. The "NOT YET BUILT" banner below is stale and is corrected here rather than deleted, so |
| **[Role](./fcm-message-factory)** | Introduced by **ADR-0025** (iOS push display via per-platform APNs alert with loc-keys) |
| **[Role](./idempotency-guard)** | Introduced by **ADR-0002 D2 |
| **[IncidentFile (ADR-0062 D6 as amended, owner ruling 2026-09-14)](./incident-file)** | **Responsibility (one sentence):** Assemble, from the database alone, the one PDF that prints a customer's identity beside their orders, disputes, consents and the whole trail on those orders, hash its data section so a printed copy matches the audited act that produced it, and print nobody else's data |
| **[LegalDocument (ADR-0063, accepted 2026-09-14)](./legal-document)** | **Responsibility (one sentence):** Be one version of one legal text — for one audience, one market or the whole platform — identified by the date it started applying, immutable once that date has passed, readable in any of its languages, so a consent row can point at exactly the text the customer read |
| **[MarketDirectory (ADR-0058, accepted 2026-09-13)](./market-directory)** | **Responsibility (one sentence):** List the markets a customer may browse in — each serviced country joined to its configured, active currency — and name the default one, from an anonymous read that never throws |
| **[Role](./membership-benefit-usage)** | **✅ ACCEPTED AND SHIPPED |
| **[MembershipPlanPrice (ADR-0059, accepted 2026-09-13)](./membership-plan-price)** | **Responsibility (one sentence):** Name what one billing period of a plan costs in one currency and which Stripe Price charges it — the figure shown and the figure billed are the same row |
| **[OperatorTenantResolver (ADR-0061, accepted 2026-09-13)](./operator-tenant-resolver)** | **Responsibility (one sentence):** Answer two questions for one country id — *is it a market?* and *which operating company serves it?* — or, when none is given, for the default market the directory chooses |
| **[OperatorTenantScopeBehavior (ADR-0061, accepted 2026-09-13)](./operator-tenant-scope-behavior)** | **Responsibility (one sentence):** Before validation, give an anonymous market-scoped request the ambient tenant of its market's operator; step aside when a claim exists; refuse `country.not_serviced` / `tenant.not_found` |
| **[Role](./order-availability)** | **THE STANDARD.** Introduced by **ADR-0037** |
| **[Role](./payout-details-validator)** | Introduced by **ADR-0034** (**`accepted`** 2026-08-02) |
| **[PayoutReferenceAllocator (ADR-0046, accepted 2026-08-09; per company since 2026-09-15)](./payout-reference-allocator)** | **Responsibility (one sentence):** Hand back the next payout-invoice reference for the ambient operating company and the current year — the ten-digit variable symbol or the `INV-YYYY-NNNNNN` invoice number — claimed by one atomic, self-committing statement, and refuse as a named business error when that company's year is exhausted |
| **[Role](./post-commit-effects)** | **LAW.** Introduced by **ADR-0038** |
| **[Role](./preferred-cleaner-hold-resolver)** | **ACCEPTED — this is the standard |
| **[Role](./preferred-offer-disclosure)** | **Both halves are SHIPPED and the decision behind them is now ratified |
| **[Role](./rate-limit-policy)** | Introduced by ADR-0003 (ADR-RATELIMIT) |
| **[Role](./refund-policy)** | Introduced by **ADR-0009** (`docs/decisions/adr-0009 |
| **[RevokedDeviceDirectory (ADR-0026, accepted](./revoked-device-directory)** | **Responsibility (one sentence):** Answer, from memory and in O(1), whether a given |
| **[RevokedUserDirectory (ADR-0027, accepted 2026-07-15, amendments U1–U3; extends ADR-0026 X1)](./revoked-user-directory)** | **Responsibility (one sentence):** Answer, from memory and in O(1), whether a given `userId`'s |
| **[Tenant (ADR-0061, accepted 2026-09-13, amended 2026-09-15)](./tenant)** | **Responsibility (one sentence):** Name an operating company under the holding so that every stamped row and the country map can point at it, and be the thing they are constrained to point at — seed-only, one row, no admin surface, 48 foreign keys |
| **[TenantConfiguration + TenantSettingCatalog (ADR-0061 O-4 as ruled 2026-09-15)](./tenant-configuration)** | **Responsibility (one sentence):** Let one operating company override one catalogued platform setting — the nine retention windows today — and give every reader, request or job, that company's value or the catalogue default, never another company's and never a value nothing reads |
| **[Role](./tenant-provider)** | The seam every tenancy bug in this repo has passed through, and the reason it has no earlier card is |
