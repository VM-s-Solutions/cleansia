# Component contracts

One page per component that carries a rule the rest of the system depends on. These are **contracts**,
not descriptions: each says what the component guarantees, what it refuses, and what breaks if a caller
works around it.

They were internal build notes until the 2026-08 cleanup. They are domain truth — what the platform
does — rather than instructions for writing code, so they live here rather than beside the pattern
catalogues.

| Component | |
|---|---|
| **[Role](./booking-price-summary)** | **✅ BUILT on both mobile clients** — iOS |
| **[Role](./dead-letter-record)** | Introduced by **ADR-0002 D3** (the poison floor: persist + alert + ack, never re-process) |
| **[Role](./employee-payout-details)** | Introduced by **ADR-0034** (`docs/decisions/adr-0034 |
| **[Role](./express-waiver-resolver)** | **✅ BUILT. The "NOT YET BUILT" banner below is stale and is corrected here rather than deleted, so |
| **[Role](./fcm-message-factory)** | Introduced by **ADR-0025** (iOS push display via per-platform APNs alert with loc-keys) |
| **[Role](./idempotency-guard)** | Introduced by **ADR-0002 D2 |
| **[Role](./membership-benefit-usage)** | **✅ ACCEPTED AND SHIPPED |
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
| **[Role](./tenant-provider)** | The seam every tenancy bug in this repo has passed through, and the reason it has no earlier card is |
