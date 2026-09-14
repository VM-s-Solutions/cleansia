# AuditGate (ADR-0012 D3 + ADR-0062 D1, accepted 2026-09-13, host-aware since 2026-09-14)

**Responsibility (one sentence):** Say which audit table, if any, a request belongs to — the admin
table for any `Command` an Administrator runs, the customer table for a `Command` marked
`Audience = Customer` that a Customer runs (or, where the marker allows it, an anonymous caller **on a
customer host**), and none for everything else — so that both audit behaviors, the one that sees
successes and the one that sees refusals, answer the same question identically.

> `Cleansia.Core.AppServices/Auditing/AuditGate.cs`, `static AuditAudience? Resolve(request,
> descriptor, session, hostAudience)`. It replaced `AdminMutationGate` in [ADR-0062](/decisions/adr-0062)
> T-AUD-1; the admin arm is the [ADR-0012](/decisions/adr-0012) D3 predicate byte-for-byte, and the
> customer arm is the extension. The **host clause** on the anonymous arm arrived with the session acts
> (ADR-0062 D3 as amended): `Login`, the password reset and the e-mail confirmation are anonymous by
> nature and routed on the partner hosts too, where the caller is a cleaner whose session history
> belongs in no table. There is no `Employee` arm: `EmployeeActionAudit` is handler-written, and a
> cleaner running a customer-marked command lands nowhere.

```csharp
if (!descriptor.Audited || !name.EndsWith("Command")) return null;
var role = session.GetTypedUserClaim(ClaimTypes.Role)?.Value;
if (role == Administrator)                                                  return AuditAudience.Admin;
if (descriptor.Audience == AuditAudience.Customer
    && (role == Customer
        || (role is null && descriptor.AllowsAnonymousActor
            && hostAudience.Audience == JwtAudiences.Customer)))              return AuditAudience.Customer;
return null;
```

## Collaborators

- **`AuditActionDescriptor`** — resolved from the `[AuditAction]` marker on the outer feature class:
  `Audited` (opt-out for the admin arm — every admin `Command` is audited unless marked
  `Audited = false`), `Audience` (opt-in for the customer arm — only `AuditAudience.Customer` is
  recorded), `AllowsAnonymousActor` (true on nine commands: `Register`, `CreateOrder`, `Login`,
  `MobileLogin`, `GoogleAuth`, `AppleAuth`, `ConfirmUserEmail`, `RequestPasswordChange`,
  `ChangePassword`), `ResourceType`, `ResourceIdProperty`, `Sensitive`.
- **`IUserSessionProvider`** — the role claim, the only host-independent discriminator a MediatR
  behavior can see (ADR-0012 D3: the route policy is invisible from inside the pipeline).
- **`IHostAudienceProvider`** — the JWT audience of the **serving host**, read only on the anonymous
  arm: an anonymous act is a customer act only on a customer host (`JwtAudiences.Customer` — the
  customer web and the customer mobile host share it). A Customer-role caller needs no such check: a
  customer token is only ever valid on a customer host.
- **`IAuditContext.DeclineSuccessRow()`** — not the gate's, but the gate's complement: the gate says
  *which table*; a handler whose marked command took a branch the marker does not describe (a social
  sign-in that provisioned instead of signing in) says *not this row*. The failure arms ignore it.
- **`AuditLogBehavior`** (inner, just outside the handler) and **`AuditFailureCaptureBehavior`**
  (outermost) — the two callers. Both call `Resolve` first and branch once on the answer; if they ever
  disagreed, a refusal would be recorded on a surface the success path does not cover, or vice versa.
- **`AuditEntryFactory`** — builds the admin row or the customer row for the audience the gate
  answered; the gate never builds anything.

## Does NOT know

- **The route policy.** `[Permission]`, `[AllowAnonymous]` and `[EnableRateLimiting]` live on the
  controller; the gate reads the command type name, the role claim and — on the anonymous arm only —
  the host's audience. That is why an `[EnableRateLimiting]` on every route that dispatches a marked
  command is a separate guard (`RateLimitCoverageGuardTests`), not a gate concern.
- **Which label an Administrator's own act should carry.** An Administrator signing out on the admin
  host lands in the admin table under the marker's frozen `customer.session.logout` — the admin arm
  wins and keeps the label (`AuditLogBehaviorTests`). Whether that should be the type name instead is a
  ratification question, not the gate's.
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
  anonymous caller of a marker without `AllowsAnonymousActor` produces no row; an anonymous caller of
  one WITH it on a partner host produces no row** (`AuditGateTests`, pinned on the production `Register`
  and `Login` commands; end to end on the customer, partner and admin hosts in `Cleansia.HostTests`).
- **`AllowsAnonymousActor` is on exactly the nine commands above, every one an
  `IOperatorScopedRequest`** — a guest-allowed marker outside the scope would drop every refusal row
  with one warning (`CustomerAuditActionRosterTests`). `GoogleAuth`/`AppleAuth` are marked as the
  **sign-in** and decline the success row on their provisioning branch — a registration's proof is the
  consent rows; a refusal on either branch is still recorded.
- **Both behaviors are registered in the ADR-0012 order** — `AuditFailureCapture` outermost,
  `AuditLog` just outside the handler (`AuditLogPipelineOrderTests`) — so the gate's answer is asked
  before validation and again after the unit of work, on the same request.
