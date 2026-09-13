# AuditGate (ADR-0012 D3 + ADR-0062 D1, accepted 2026-09-13)

**Responsibility (one sentence):** Say which audit table, if any, a request belongs to — the admin
table for any `Command` an Administrator runs, the customer table for a `Command` marked
`Audience = Customer` that a Customer (or, where the marker allows it, an anonymous caller) runs,
and none for everything else — so that both audit behaviors, the one that sees successes and the one
that sees refusals, answer the same question identically.

> `Cleansia.Core.AppServices/Auditing/AuditGate.cs`, `static AuditAudience? Resolve(request,
> descriptor, session)`. It replaced `AdminMutationGate` in [ADR-0062](/decisions/adr-0062) T-AUD-1; the
> admin arm is the [ADR-0012](/decisions/adr-0012) D3 predicate byte-for-byte, and the customer arm is
> the extension. There is no `Employee` arm: `EmployeeActionAudit` is handler-written, and a cleaner
> running a customer-marked command lands nowhere.

```csharp
if (!descriptor.Audited || !name.EndsWith("Command")) return null;
var role = session.GetTypedUserClaim(ClaimTypes.Role)?.Value;
if (role == Administrator)                                                  return AuditAudience.Admin;
if (descriptor.Audience == AuditAudience.Customer
    && (role == Customer || (role is null && descriptor.AllowsAnonymousActor))) return AuditAudience.Customer;
return null;
```

## Collaborators

- **`AuditActionDescriptor`** — resolved from the `[AuditAction]` marker on the outer feature class:
  `Audited` (opt-out for the admin arm — every admin `Command` is audited unless marked
  `Audited = false`), `Audience` (opt-in for the customer arm — only `AuditAudience.Customer` is
  recorded), `AllowsAnonymousActor` (true on exactly `Register` and `CreateOrder`), `ResourceType`,
  `ResourceIdProperty`, `Sensitive`.
- **`IUserSessionProvider`** — the role claim, the only host-independent discriminator a MediatR
  behavior can see (ADR-0012 D3: the route policy is invisible from inside the pipeline).
- **`AuditLogBehavior`** (inner, just outside the handler) and **`AuditFailureCaptureBehavior`**
  (outermost) — the two callers. Both call `Resolve` first and branch once on the answer; if they ever
  disagreed, a refusal would be recorded on a surface the success path does not cover, or vice versa.
- **`AuditEntryFactory`** — builds the admin row or the customer row for the audience the gate
  answered; the gate never builds anything.

## Does NOT know

- **The host or the route policy.** `[Permission]`, `[AllowAnonymous]` and `[EnableRateLimiting]` live
  on the controller; the gate reads the command type name and the role claim only. That is why an
  `[EnableRateLimiting]` on every route that dispatches a marked command is a separate guard
  (`RateLimitCoverageGuardTests`), not a gate concern.
- **The handler's outcome.** Success or refusal is the behaviors' business; the gate decides *whether*
  and *where*, not *what*.
- **The tenant.** The writer and the sink stamp it from the ambient provider at write time.
- **Why a request has no role.** An anonymous HTTP request and a Functions-host dispatch look
  identical to it (`role is null`); the marker's `AllowsAnonymousActor` is what keeps a system job that
  one day dispatches `CancelOrder.Command` from being recorded as a guest act.

## Invariants a reviewer checks

- **Exactly two arms, in this order.** The admin arm wins: an Administrator running a customer-marked
  command lands in the admin table only (`CustomerAuditPipelinePostgresTests`).
- **The admin predicate is textually ADR-0012 D3's** — `Audited && EndsWith("Command") && role ==
  Administrator`. Changing it is a superseding ADR, not a refactor.
- **A customer command without `Audience = Customer` produces no row; an Employee produces no row; an
  anonymous caller of a marker without `AllowsAnonymousActor` produces no row** (`AuditGateTests`).
- **`AllowsAnonymousActor` is on exactly `Register` and `CreateOrder`**, and `GoogleAuth`/`AppleAuth`
  carry no marker at all — a sign-in-or-register command would write a registration row on every social
  login (`CustomerAuditActionRosterTests`).
- **Both behaviors are registered in the ADR-0012 order** — `AuditFailureCapture` outermost,
  `AuditLog` just outside the handler (`AuditLogPipelineOrderTests`) — so the gate's answer is asked
  before validation and again after the unit of work, on the same request.
