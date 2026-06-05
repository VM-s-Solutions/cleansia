# Authorization — living decision notes

> Companion to the **immutable** `agents/backlog/adr/0001-authorization-model.md` (ADR-AUTHZ).
> The ADR is the frozen contract; this file is the *evolving* design notes, trade-off space, and
> current shape. Update this when the decision evolves; supersede the ADR for a real mapping change.

## Current shape (as of ADR-0001, accepted 2026-06-01)

Authorization is a **two-stage indirection**, deliberately preserved as a seam:

```
[Permission(Policy.CanXxx)]  ──(ToPhysicalPolicy, at attribute construction)──▶  PhysicalPolicy.*
        logical vocabulary                                                        physical gate
   (~150 constants, Policy.cs)                              (Anonymous/Authenticated/CustomerOnly/
                                                            EmployeeOrAdmin/AdminOnly/OwnerOrElevated/Deny)
                                                                      │
                                                          registered identically on every host by
                                                          the shared AddCleansiaAuthorization (D4)
```

Why the indirection is the asset: it lets all five hosts (Web.Admin/Customer/Partner,
Mobile.Customer/Partner) share **one** permission vocabulary while differing only in JWT audience, and
lets us re-target a permission (e.g. add SuperAdmin) in one map instead of N controllers. ADR-0001 fixes
the indirection's *default* and *completeness*; it does not remove it.

### The seven physical policies
| Physical policy | Meaning |
|---|---|
| `Anonymous` | `[AllowAnonymous]` (not a registered policy) |
| `Authenticated` | any logged-in user |
| `CustomerOnly` | authenticated AND not Employee AND not Admin (by-absence; see risk below) |
| `EmployeeOrAdmin` | Employee \| Administrator |
| `AdminOnly` | Administrator |
| `OwnerOrElevated` | Admin OR (caller `sub` == requested subject id) — **redefined in D3** to drop the blanket employee grant |
| `Deny` | always 403 — the fail-closed sentinel (D1) |

### The five invariants that hold the model together
1. **Fail-CLOSED default.** `ToPhysicalPolicy` defaults to `Deny`, never `Authenticated`. A
   `PolicyBuilder.AssertComplete()` startup assertion (run via a shared `IStartupFilter`) bricks boot —
   in dev and CI — if any `Policy.*` is unmapped and not on the `AnonymousAllowList`. (Fixes BSP-1/BSP-6.)
2. **Complete + correct map.** Every `Policy.*` → exactly one `PhysicalPolicy.*` (frozen D2 table) or the
   allow-list. Completeness alone is not enough — `CanRespondToDispute` proved the map must also be
   *audited for correctness* against actual handler/controller behavior.
3. **One permission = one gate on every host (map principle).** If two hosts need a different gate for the
   "same" operation, they are **two permissions**. Divergent-intent collisions found so far:
   `CanViewPagedInvoices` (Admin list vs Partner self-service → kept one perm, EmployeeOrAdmin [OWN-DATA])
   and `CanRespondToDispute` (customer self-reply vs staff reply → **split** into `CanAddDisputeMessage` +
   `CanRespondToDispute`).
4. **[OWN-DATA] = coarse policy + a REAL handler check.** When the physical policy can't express
   "own data," the handler is the inner gate — and the check must **exist in code** (verified per row),
   with a mandatory ownership integration test. Discovered counter-examples: `GetUser.Handler` had NO
   check (added in T-AUTHZ-3); `GetPeriodPays.Handler:52-61` is the one that already did.
5. **JWT trust boundary is explicit.** Shared HS256 secret (any host can *verify* any token); isolation is
   **audience validation only**. Refresh must re-check BOTH `RequiredAudience` AND `RequiredProfile`.

## Trust zones (D5)
- **Customer pair = ONE trust zone:** Web.Customer + Mobile.Customer share `cleansia.customer`; tokens
  (access + refresh) are cross-accepted. Intentional — same role, same data, no client discriminator.
- **Partner pair = TWO trust zones:** Web.Partner (`cleansia.partner`) ≠ Mobile.Partner (`cleansia.mobile`);
  tokens are mutually rejected. Historical asymmetry, recorded as the current contract.
- The asymmetry between the two pairs is the open question Q-0003.

## Trade-off space (what was weighed, what's deferred)
| Axis | Chosen | Rejected / Deferred | Trigger to revisit |
|---|---|---|---|
| Unmapped-permission default | `Deny` sentinel + startup assertion | keep `Authenticated`; throw at attribute ctor | — (settled) |
| Logical→physical indirection | keep the seam | flat `[Roles]` on controllers | only if multi-host vocabulary sharing is abandoned |
| Per-host policy registration | one shared `AddCleansiaAuthorization` | per-host hand-copied bodies (the BSP-7 source) | — (settled) |
| JWT signing key | shared HS256, audience-isolated | per-host asymmetric (RS256/JWKS) | **Q-0002 → keep HS256 (owner)**; revisit on leaked-secret/compliance need |
| Customer web/mobile separation | one shared audience | `cleansia.mobile.customer` split | **Q-0003 → keep asymmetry (owner)**; revisit if mobile sessions must be revoked independently |
| `CanViewPagedInvoices` | one perm, EmployeeOrAdmin [OWN-DATA] | split into list(Admin)+myInvoices(Employee) | Admin host needing list-all via same path → superseding ADR |
| `CanRespondToDispute` (staff) | **split**; staff path **AdminOnly** | EmployeeOrAdmin staff path | **Q-0005 → AdminOnly (owner)**; staff endpoint moves Partner→Admin host |
| SuperAdmin tier | `AdminOnly` for admin-user mgmt | invent `SuperAdmin` role now | **Q-0001 → AdminOnly, no tier (owner)**; revisit if admin self-elevation becomes a concern |

## Known residual risks (live, tracked)
- **`CustomerOnly`-by-absence.** Safe only because every issuance path sets `ClaimTypes.Role`. A role-less
  principal (future IMP-1 OAuth, a webhook, a service principal) would pass `CustomerOnly` as an elevated
  user. → Q-0004; the durable fix is a positive `RequireRole(Customer)` once a Customer role claim exists.
- **`IsStaffMessage` was client-supplied.** `AddDisputeMessage.Handler` trusted the request-body flag; a
  customer could post a "staff" message. Hardened in T-AUTHZ-2 to derive it from the caller's profile.
  General lesson: a privilege-bearing flag must never come from the request body.
- **Shared secret blast radius.** One leaked `JwtSettings:Secret` is a platform-wide forgery key (bounded
  only by audience). Treated as a top-tier secret; Q-0002 tracks the asymmetric-key upgrade.

## Implementation ledger (ADR-0001 tickets)
- **T-AUTHZ-0** — `Cleansia.HostTests` `WebApplicationFactory` project (prerequisite for end-to-end tests).
- **T-AUTHZ-1+2 (atomic PR)** — fail-closed default + `AssertComplete` + the complete map fill (payroll
  family + dispute SPLIT `CanAddDisputeMessage`/`CanRespondToDispute` + `IsStaffMessage` hardening).
  Must be atomic: the assertion before the map fill bricks every host.
- **T-AUTHZ-3** — shared `AddCleansiaAuthorization` + delete legacy `AddJwt` `AdminOnly` +
  presence+semantics startup assertions + `OwnerOrElevated` redefinition + `GetUser`/invoice handler
  ownership checks.
- **T-AUTHZ-4** — per-host refresh `RequiredProfile`.
- **T-AUTHZ-5** — `check-consistency.mjs` scan for unmapped `Policy.*`.

## How this evolves
- **Purely additive permission row** (new `Policy.*` → existing physical policy): update the checked-in
  snapshot in the same PR, no ADR needed. The snapshot test message states this so devs amend, not delete.
- **Any semantic mapping change** (change/remove an existing row's physical policy, or a new physical
  policy value): requires a **superseding ADR** — and a `## Defense` panel, per `process/deliberation.md`.
- **A new divergent-intent collision** discovered during implementation: split the permission (map
  principle) and record it in the trade-off table above.

## Resolved questions (owner, 2026-06-01) — see `agents/backlog/questions/answered.md`
- **Q-0005 → staff dispute reply is ADMIN-ONLY** (staff `CanRespondToDispute` = AdminOnly; endpoint
  moves Partner→Admin; customer self-reply via `CanAddDisputeMessage` unchanged). Locked into ADR-0001.
- **Q-0001/Q-0002/Q-0003/Q-0004 → confirmed on documented defaults** (AdminOnly admin-user mgmt /
  shared HS256 / current trust-zone asymmetry / IMP-1 must preserve the role claim).

No open questions remain on the authorization model.
