# Security Rules (S1–S12) — Non-Negotiable

> These rules exist because this codebase has already had at least one production-class security
> regression. Treat them as **laws, not guidelines.** When rules conflict, the priority is:
> **security > correctness > cleanliness > consistency.** Never trade a security rule for shorter
> code.

The Security Reviewer audits every `security_touching` ticket against this list and names the
**specific** risk when something fails. Backend developers self-check against it before handing off.

---

## S1 — UserId is server-truth, not client input

Never trust `userId`, `tenantId`, or `email` from the request body or query string. Derive the
caller from the JWT in the controller, then enrich the command:

```csharp
var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
var enriched = command with { UserId = userId };
var result = await Mediator.Send(enriched, ct);
```

Service-layer code injects `IUserSessionProvider` and calls `GetUserId()`. If a `Command` record
carries a `UserId` field it must: default to `""` (NSwag clients generate strict required fields;
clients send empty, backend overwrites), be commented as server-enriched, and be set by the
controller from the JWT **before** `Mediator.Send`. Anonymous endpoints should need no `UserId` at
all.

**An identity a client cannot know is not an authorization check.** A self-write that authorizes by
comparing the session user to a *client-supplied* id passes only for callers that already know the
answer, so it protects nothing and silently locks out any client that cannot guess it. `MyProfileDto`
carries no `Id` and the web session is an HttpOnly cookie, so the web can never supply one — mobile
only satisfied such checks because it decodes its own token. Resolve the subject from
`IUserSessionProvider` in **every** arm of the feature (ownership rule, uniqueness rules that must
exempt the caller's own row, and the handler), and leave the wire field inert.

**A wire field kept for compatibility must be `string?`, not `string`.** MVC's implicit-required for
non-nullable reference types rejects an *absent* member with
`400 {"errors":{"Id":["The Id field is required."]}}` **before** the command reaches MediatR — so no
validator or handler change can make the endpoint callable without it. This layer is invisible to
unit tests (they construct the command directly); only a host/route test catches it. The `= ""`
default above is not an alternative when the field is not last in the positional parameter list.

## S2 — Authorization on every endpoint

Every controller method has exactly one of:
- `[Permission(Policy.CanXxx)]` — the project's policy attribute (default expectation), or
- `[AllowAnonymous]` — only for genuinely public routes (landing, signup, password-reset request,
  public order-lookup-by-confirmation-code), or
- `[Authorize]` with no policy — only for "any authenticated user" routes (e.g. `GetMyProfile`).

A new endpoint with **none** of these is a hole: the default policy requires authentication, but a
missing policy attribute lets *any* authenticated user (any role, any tenant) hit it.

**Accountability (ADR-0012).** Every admin mutation (a `Command` run by an `Administrator`) leaves an
append-only `AdminActionAudit` row, captured generically by `AuditLogBehavior` — you write no audit
code. An admin mutation with **no** row, a behavior that **computes** before/after (it must only drain
the handler's `IAuditContext` snapshot), a snapshot carrying **raw subject PII**, or a non-atomic /
best-effort *success*-audit are ADR-0012 violations (the success row must ride the action's commit;
only *failures* are written out-of-band and must never re-throw into the caller's error).

**Token lifetime (ADR-0024).** The access-token TTL on a host that issues device-bound sessions is a
security bound, not a tuning knob — changing `AccessTokenExpMinutes` on a mobile host requires a
superseding ADR (it *is* the device-revocation latency; pinned by TC-REVOKE-TTL-4's raw-file test).

**Immediate device revocation (ADR-0026).** Device-revocation latency on the two mobile hosts is
bounded by the `RevokedDeviceDirectory` refresh interval (**≤ 30 s**, `DeviceRevocation:RefreshSeconds`),
with the 30-min TTL as the fail-open backstop. `DeviceRevocation:Enabled` and `RefreshSeconds` are
security bounds — changing either requires a superseding ADR (raw-file test-pinned, TC-REVOKE-NOW-7).
Enforcement keys on the **signed `device_id` claim** (login: `requestMetadata.DeviceId`; refresh: the
*persisted* `issued.Record.DeviceId`) — **never a client-sent `X-Device-Id` header** (the adversary is
the client). Device-deactivation write paths must stamp `DeactivatedOn` (the directory's `RevokedAt`);
any *future bulk* device-deactivation job must be checked against ADR-0026 first (it inflates the
snapshot and triggers a fleet-wide silent-refresh ripple).

**Immediate password-reset session cutoff (ADR-0027).** Password RESET ends the reset user's mobile
sessions within the same **≤ 30 s** bound via a sibling `RevokedUserDirectory` keyed on `sub` and fed
from the persisted `password_reset` refresh-token rows (no migration); password CHANGE is deliberately
*not* accelerated (authenticated hygiene spares the caller's own session). The shared
`DeviceRevocation:Enabled`/`RefreshSeconds` bounds govern **both** mobile revocation checks.

## S3 — Resource-by-id endpoints must check ownership

Anything that takes a resource id and operates on it must verify the caller owns the resource —
**in the handler or domain service**, not the controller (so it holds regardless of which API host
exposes it):

```csharp
var order = await orderRepo.GetByIdAsync(cmd.OrderId, ct);
if (order is null || order.UserId != cmd.UserId)
    return BusinessResult.NotFound(BusinessErrorMessage.Order.NotFound); // NotFound, not Forbidden — don't leak existence
```

Project convention: return **NotFound** for cross-user access attempts so we don't confirm a
resource exists to someone not allowed to see it. For `[AllowAnonymous]` endpoints there is **no
tenant claim**, so the global filter is bypassed — anonymous routes must not return tenant-scoped
data unless gated by a different shared secret (e.g. a confirmation code in the URL).

## S4 — DTO leak prevention

**Never return an entity from a handler — always map to a DTO.** Even if every field is safe today,
the entity gains a sensitive field tomorrow. Audit every Response/DTO for fields that must not
reach the client:
- `UserId` (the client knows their own id); other users' ids
- `TenantId` (never expose)
- email / phone / full name of non-self users (exception: cleaner first-name on an assigned order
  is documented intent)
- Stripe customer/subscription ids, token hashes, password hashes
- Soft-deleted rows leaking through unfiltered queries

**One DTO, two audiences, one projection.** When the same DTO is served to a caller who owns the row
*and* to one who merely may look at it, the second reading is a **redaction of the first**, held in one
place and asked from the same seam that granted access — never re-derived per handler and never keyed
on "is the caller assigned". Reference: `OrderPiiRedaction.cs` for the two shapes,
`GetOrderDetails.cs:57`/`:136-138` for the predicate and its application. Enforced by the
`OrderRedactionSurfaceTests` row of the S12 table (**`T1-CI`**), which is where the diagnostic that
finds this class lives.

## S5 — Rate limiting on auth + side-effecting endpoints

Auth endpoints (login, register, forgot-password, refresh, confirm-email, resend-confirmation) use
the shared `"auth"` window (10 req/min/partition) via `[EnableRateLimiting("auth")]`. Mutations
that cost money or send email (create-order, send-invoice, request-refund) get a narrower per-user
limit. Decide the limit whenever you add a side-effecting mutation.

**Windows MUST be partitioned AND cardinality-bounded (ADR-0003 / ADR-RATELIMIT).** A named limiter
with **no** partition key is one global bucket shared by all callers — that is an S5 *violation*, not
compliance (it lets one client DoS-lock every other caller and does not throttle brute-force per
attacker). The shared `"auth"` / `"interactive"` policies in `CleansiaStartupBase` are partitioned
**per real client IP** for anonymous requests and **per JWT `sub`** for authenticated ones, with
`UseForwardedHeaders` (narrow trusted proxy only; over-broad/unset `KnownNetworks` → the app refuses to
boot in non-dev) at the top of the pipeline and `UseRateLimiter` **after** `UseAuthentication` (CSRF
`UseHostAuthMiddleware` unchanged after the limiter). Anonymous per-IP partitions sit **behind a global
cardinality cap** so a botnet of distinct real IPs cannot trade the rate-DoS for a memory-DoS. Reuse
this shape for any new per-user side-effect window — do not hand-roll an un-partitioned
`AddFixedWindowLimiter`, and do not ship an unbounded per-IP partition.

**Partitioning is not coverage.** A correctly partitioned policy applied to *some* endpoints does not
satisfy S5 for the money/side-effect endpoints that carry **no** `[EnableRateLimiting]` at all —
those remain S5 gaps (tracked as `BSP-4d`; verified-uncovered today include
`Web.Customer/MembershipController.CreateCheckoutSession` and the Partner payroll controllers).

## S6 — Logging hygiene (no PII above Debug)

No email, phone, name, address, payment/Stripe detail, JWT, refresh token, or confirmation code in
logs at Information level or higher. Log `userId`, not `user.Email`. `LogDebug` is acceptable for
PII during local investigation only.

> **There is a SECOND sink, it has no middleware, and it ships off-box to a different vendor.** The
> isolated Functions worker runs no ASP.NET pipeline, so `RequestLoggingMiddleware` is structurally
> unreachable from it — and since `ac2243d2` its `ILogger` feeds Sentry at `MinimumEventLevel.Error`.
> A single `LogError("… {Body}", body)` there produces **three** copies: the formatted message, an
> **indexed searchable tag** holding the raw value org-wide, and a **scope breadcrumb that re-attaches
> to later, unrelated events** from that worker. `SendDefaultPii = false` does not touch any of them —
> it governs request and user context, not your own log arguments.
>
> **The live instance, found and fixed 2026-08-10 (`e84aed25`).** `PoisonHandlerBase` logged the whole
> message body, and the `send-email` body carries `Code` — the raw confirmation/reset token. A message
> reaches its poison queue in about **two minutes** (`visibilityTimeout` 30 s × `maxDequeueCount` 5)
> and those codes live **fifteen**, so the token was published with roughly **thirteen minutes still
> valid**, to an audience — Sentry read access — that is strictly broader than production-Postgres
> access.
>
> **Two things this teaches that the middleware rule cannot.** First, *the denylist would not have
> caught it*: `SensitiveFieldRegex` enumerates `password|token|apiKey|…` and **`code` is absent**, which
> is this rule's own named residual — a secret under a name with no credential word in it. Second, *the
> same queue already knew better*: `SendEmailHandler` carries **"Never log the payload — it carries the
> recipient email and a live confirmation/reset code."** The live consumer refused; the poison consumer
> printed. **A rule held on the happy path and dropped on the failure path is the shape to look for.**
>
> The seam is `PoisonAlert` (`Functions.Core/Handlers/`), which is an **allowlist by field name —
> fail-closed, opposite polarity to the middleware's denylist** — so a message type that gains a field
> tomorrow is withheld by default rather than published by default. **Enforced by:**
> `PoisonAlertRedactionTests` — **T1-CI**. Note its assertions read the **structured state**, not only
> the formatted string: a formatted-only assertion misses the indexed tag entirely, which is how
> `SendEmailHandlerTests.cs:265-266` is weaker than the mechanism it guards.

**The dominant sink is not a `logger.Log*` call — it is `RequestLoggingMiddleware.SafeBody`**, which
slices request and response bodies into Information on all five hosts. It is generic over every route,
so **an S6 leak is almost never one endpoint**: when T-0457 was filed against `GET /api/User/GetCurrent`
("the largest S6 exposure in the codebase"), the mechanical sweep found **152 PII-shaped members across
80+ routes**, of which that route was five. Adding a path to `IsSensitivePath` fixes the URL you looked
at and leaves the class open — which is exactly how `/auth/` missed `/api/AdminAuth/…` and then
`/gdpr` missed `/api/v1/AdminGdpr/export/{userId}` (a whole subject-access dump, payout block included).

Three tools, and picking the wrong one is the usual mistake:

| The value | Tool |
|---|---|
| A named field whose name says what it holds (`*email`, `*phone*`, `*firstName`, `birthDate`) | `ContactIdentityFieldRegex` — matched by **shape**, not enumerated, so the next `contactEmail` is covered without anyone remembering |
| A named credential (`clientSecret`, `ephemeralKey`, `blobUrl`) | `SensitiveFieldRegex` — literal names; **values are unbounded**, so collapsing one frees window and can unmask what follows |
| Free text no name can reach (`Notes`, `Description`, `ReviewNotes`, `HolderName`) | `IsSensitivePath` — wholesale route suppression |

Keep the two regexes **separate**. They redact identically but they do not free window identically, and
merging them makes `RedactionUnmaskedFreeTextGuardTests` report every string member of every DTO as
unmasked — a guard that has stopped saying anything.

**What makes a denylist admissible at all is the guard, not the list.** `RequestLogPiiSurfaceGuardTests`,
`RequestLogPayoutPathSuppressionTests` and `RequestLogCredentialShapeGuardTests` walk every wire DTO
reachable from a controller action on the five hosts (shared walk: `WireSurface`), read the token list
**out of the live compiled regex** so the three cannot drift, and redden CI naming the DTO, the member and
the routes. A new PII-, payout- or credential-shaped member that is neither redacted, nor on a suppressed
route, nor excepted **in writing** fails the build. A redaction list without that is the same defect class
as a comment asserting an invariant.
**Enforced by:** the three guards above (`Cleansia.Tests`, a named step of `backend-ci.yml:69-71`) — **T1-CI**.

**The credential half is now closed too, and closing it found one.** A secret whose field name was never
in the token list used to be caught by nothing; `RequestLogCredentialShapeGuardTests` asks the question by
*shape* — a member whose PascalCase words include `secret`/`token`/`key`/`password` must be redacted,
suppressed or excepted with a reason. Its first run found `RegisterDevice.Command.DeviceToken` writing the
raw FCM/APNs push token to Information on every device registration, because the alternation is
quote-anchored and `token` therefore never matched `deviceToken`. The same value was already redacted when
called `Token` and suppressed when called `TrustedDeviceToken` — which is the arbitrariness of a name list,
measured rather than argued. **What the shape still cannot see** is a secret under a name with no
credential word in it (`Payload`, `Handle`); no name heuristic reaches that, and a value-shaped leg was
tried and dropped — nothing in the wire surface carries a statically discoverable example value to read.

## S7 — Idempotency on side-effecting commands

Any command that creates a Stripe charge/subscription, sends an email, grants loyalty points,
awards a referral, or writes a financial record (invoice, receipt, payout) **must be idempotent** —
check whether the side effect already happened (ledger entry / transaction id exists) before doing
it again. Reference patterns: `LoyaltyService.GrantForCompletedOrderAsync` (checks the loyalty
ledger), `ReferralService.ProcessQualifyingOrderAsync` (checks `Referral.Status`). This protects
against webhook re-delivery (Stripe retries on 5xx/socket reset), pipeline retries, double-clicks,
and admin re-triggers.

**S7a — A check-then-act read is NOT atomic; under concurrency the DB must be the source of truth.**
A `if (await CountAsync(...) < cap)` / `if (await GetActiveAsync(...) == null)` guard followed by an
insert is a TOCTOU race: two concurrent requests both pass the read, both write, and the cap/uniqueness
is breached. The read is a fast-path optimization, not the guarantee. Enforce the invariant with one of:
- an **atomic conditional UPDATE** that returns rows-affected — `ExecuteUpdateAsync(... WHERE counter <
  max)`; **0 rows = limit reached** (no exception). Reference: `PromoCodeRepository
  .TryIncrementGlobalRedemptionsAsync` (T-0110 / LG-SEC-01).
- a **unique index that you convert into a clean result, never an unhandled throw.** When a
  unique-violation can race, catch the `DbUpdateException` (Postgres `SqlState == "23505"`) at the
  boundary that owns the write and resolve to the existing row / return the deterministic business error
  — do **not** let it surface as a 500. Reference: `CreateMembershipSubscription.Handler` catches the
  `StripeSubscriptionId` unique violation and resolves via `GetByStripeSubscriptionIdAsync` →
  `MembershipAlreadyActive` (T-0111 / LG-SEC-02, round 2).

**S7b — Mind WHERE the violation surfaces vs. WHERE you catch it.** With the `UnitOfWorkPipelineBehavior`,
`CommitAsync` runs AFTER the handler returns — so a `DbUpdateException` from a tracked insert surfaces at
the *pipeline*, not in the handler, and a `try/catch` around the handler body won't catch it. If you need
to map the violation, **flush the insert in the handler** (its own `CommitAsync`/`SaveChangesAsync` in a
`catch (DbUpdateException) when (IsUniqueViolation)`) so it's caught where you can resolve it; the
pipeline's final commit is then a safe no-op (the row is `Unchanged`). And never put a throwing
unique-insert inside a *larger* transaction whose rollback would be worse than the bug — e.g. the promo
redemption inside the paid-order `CreateOrder` txn (T-0110) used the non-throwing conditional-UPDATE path
precisely so a race could not roll back the paid order.

**Idempotency keys must be client-stable, not `Guid.NewGuid()` per call.** A fresh GUID per request
defeats the provider's idempotency (Stripe replays only on the *same* key). Derive the key from a stable
client-supplied token (one per logical attempt, new for a genuine retry-of-intent like re-subscribe) with
a deterministic server-side fallback. Reference: `CreateMembershipSubscription.DeriveStripeAttemptId`
(T-0111).

## S8 — Tenant isolation correctness

Every entity holding user-scoped data implements `ITenantEntity`; the global EF query filter then
auto-scopes reads. When adding an entity, ask "could two tenants both have rows here?" — if yes,
`ITenantEntity`; if no (true platform config), document why it isn't. Unique indexes on
tenant-scoped tables are `(TenantId, X)`, not `(X)` — `Code` is unique *per tenant*. The global
filter applies to `Set<T>()` reads but **not** to raw SQL (`FromSqlRaw`/`ExecuteSqlRaw`),
`IQueryable` exposed from the wrong layer, or joins where only one side carries the filter — audit
those paths.

### The one question that decides every bypass (ADR-0051)

**The filter is the default. A bypass is owed exactly one demonstration and must pay exactly one price.**
The demonstration: *can the ambient tenant at the moment this row was **written** differ from the ambient
tenant at the moment it is **read**?* The price: a **re-pinning predicate bound to the caller**. The
three named forms below are elaborations of this one question, not independent rules — reach for the
question first and the form second.

|  | **Read under a tenant claim** | **Read with no claim (anonymous / job)** |
|---|---|---|
| **Written under a tenant claim** | **symmetric → FILTERED.** `src/Cleansia.Config/Filters/RequireCompleteProfileAttribute.cs:25`, `src/Cleansia.Core.AppServices/Authentication/OrderAccessService.cs:112`, the employee self-service `Update*` handlers, `src/Cleansia.Infra.Database/Repositories/LiveActivityTokenRepository.cs:10-45` | **ASYMMETRIC → bypass + re-pin.** `src/Cleansia.Infra.Database/Repositories/EmployeeRepository.cs:19-26` on the token-mint paths; `src/Cleansia.Infra.Database/Repositories/LiveActivityTokenRepository.cs:47-61`; `src/Cleansia.Infra.Database/Repositories/DeviceRepository.cs:46-57` and `src/Cleansia.Infra.Database/Repositories/DeviceRepository.cs:59-68` |
| **Written with no claim (anonymous)** | **ASYMMETRIC → bypass + re-pin.** `src/Cleansia.Infra.Database/Repositories/RefreshTokenRepository.cs:10-23` and the revoke family at `src/Cleansia.Infra.Database/Repositories/RefreshTokenRepository.cs:120-150` | **symmetric → FILTERED.** `src/Cleansia.Infra.Database/Repositories/UserRepository.cs:105-118`; the register / resend admission pre-checks |

**Two things this is deliberately NOT.** *"The endpoint is anonymous"* is not the test — the bottom-right
cell is anonymous and stays filtered, and widening the register/resend pre-checks across tenants
re-creates the cross-tenant existence oracle the composite index was chosen to remove
(`UserEntityConfiguration.cs:99-105`). *"The key is an unguessable secret"* is not the test either —
`GetByConfirmationCodeAsync` and `GetByTokenHashAsync` key on the same kind of SHA-256 hash and land on
**opposite** sides, because their cells differ. A secret makes a bypass *safe*; it never makes one
*necessary*.

**The re-pin may not be an appeal to a uniqueness property the schema does not enforce.** Permitted pins:
an unguessable server-issued secret, the caller's own id from their JWT, or a row id read out of an
already-pinned row. *Not* permitted: "the email is unique across the platform" — email uniqueness is
`(TenantId, Email)` by design and, while `TenantId` is dormant, is enforced by nothing at all until the
owner-run migration emits `NULLS NOT DISTINCT` (`consistency.md` §*"Tenant-scoped unique indexes"*, the
`Users` arming note; the C# builder call has landed and the DDL has not). Where the true pin is a
**caller obligation** the method cannot verify (`GetActiveByUserIdAsync`'s "the `UserId` comes from the
caller's own JWT"), say so in those words rather than dressing it as an invariant.

**Enforced by:** `src/Cleansia.Tests/Features/Auth/UserRepositoryTokenLookupTenantTests.cs` (bypass
sites confined to an enumerated roster; confirm-family pinned filtered) +
`src/Cleansia.Tests/Features/Auth/EmployeeRepositoryTenantTokenLookupTests.cs` (the write-authenticated /
read-anonymous cell, seeded with a **non-null** tenant so it can fail), both run by
`.github/workflows/backend-ci.yml:69-74` with no `continue-on-error` — **`T1-CI`**, **baseline 0**, over
the **closed scope of those two repositories only**. For the repository-wide scope the tier is
**`(gate pending: T-0606)`**: the gate is the same roster-confinement shape applied across
`src/Cleansia.Infra.Database/Repositories/**`, and its **baseline is every unrostered bypass site,
because the roster does not exist yet** — writing it is the ticket. No count is stated here; derive it
with `grep -rn "IgnoreQueryFilters(\|GetQueryableIgnoringTenant()" src/Cleansia.Infra.Database/Repositories/`.
**ADR-0051 is `proposed`**
(`docs/decisions/adr-0051.md:3`).
**Retires when:** that status line stops reading `proposed`.

**Anonymous-write / authenticated-read asymmetry (the silent-zero-rows trap).** A row written on an
**anonymous** path (no tenant claim → stamped `TenantId = null`) but later read/updated on an
**authenticated** request (JWT carries `tenant_id`) is **hidden by the global filter** — the
write silently matches zero rows and the side effect (confirm an order, revoke a token) never happens.
Same class as the *tenant-ignoring-read-on-webhook-paths* memory note. The fix on the read side:
`IgnoreQueryFilters()` **plus an explicit caller-scoped predicate** that re-pins the surface — never
just clearing the filter. Pin by an unguessable secret (`TokenHash`) or the caller's own `UserId` from
the JWT, so the read finds the caller's own null-stamped rows without widening across tenants
(preserves S1/S3). References: the order webhook existence check `ExistsIgnoringTenantAsync` (T-0245);
the refresh-token revoke/rotate reads `RefreshTokenRepository.GetByTokenHashAsync` /
`GetActiveByUserIdAsync` / `RevokeChainAsync` (T-0236).

**The mirror case: a tenant-ignoring sweep whose WRITE-BACK is tenant-scoped.** A background job
(timer/Function/webhook) runs with **no** tenant claim, so it deliberately selects across tenants with
`GetQueryableIgnoringTenant()` — and then loads the row it is about to mutate through a tenant-**scoped**
`GetByIdAsync`, which narrows to `TenantId == null` and resolves **nothing** for a tenanted row. The
guard reads `if (x is null) return;`, so the job's *effect* still happens and only its *bookkeeping*
silently doesn't. **A sweep must be tenant-ignoring on BOTH sides of the loop, not just the selection**
— audit the write-back of every `GetQueryableIgnoringTenant()` sweep, and the pattern is invisible in
single-tenant mode, so **the pinning test must seed a non-null `TenantId`** (a fixture wired
`tenantId: null` proves nothing here). Reference: `NewJobsDigestService.StampWatermarkAsync` →
`GetByIdIgnoringTenantAsync`, which left the watermark frozen and re-notified tenanted cleaners on every
sweep, forever (T-0529). *(`EmployeeRepository.GetByUserEmailIgnoringTenantAsync` (T-0361) used to be
listed here and is **not** an instance of this case — it has no loop, no write-back and no sweep. It is
the write-authenticated / read-anonymous cell of the matrix above. Re-filed by ADR-0051; the mis-filing
is kept visible because matching a case against a war story instead of the question is exactly the
failure the matrix exists to stop.)* A sweep keeps the entity **change-tracked** — `IgnoreQueryFilters()` on the tracked set, never
`ExecuteUpdateAsync`, which would commit outside the caller's unit of work and break the job's atomicity.
Where the mutation creates **child** rows, prefer the `SetTenantOverride`/clear-per-iteration shape
(`CleanupStalePendingOrders`, `MaterializeRecurringBookings`) so the children inherit the right tenant —
**and commit inside the same iteration, or the override is decorative.** `CommitAsync` stamps
`TenantId` on every `Added` `ITenantEntity` from the tenant that is ambient **at commit time**
(`CleansiaDbContext.CommitAsync`), not at `Add` time, so a sweep that sets an override per iteration and
then rides the pipeline's single deferred commit stamps **every** child row of **every** iteration with
the **last** one's tenant. Both sweeps shipped that way and both were repaired the same way: the
`unitOfWork.CommitAsync(ct)` at the foot of the loop body is what gives the override meaning. The pinning
test must seed **non-null and DIFFERENT** `TenantId`s on at least two iterations — one tenant, or a null
one, passes over the bug — and must assert on the CHILD rows, since the parent already carries its own
tenant from the row it was read from.

**The third form: ONE repository method serving TWO callers with OPPOSITE tenancy requirements.** The
two cases above are about a *caller* getting its tenancy wrong. This one is about a *method* deciding
tenancy on its callers' behalf — and then being reused. A method written for a request path
(`GetDbSet()`, correct: the caller has a `tenant_id` claim) is later called from a timer/Function that
has **no** claim; under a tenant the filter resolves `TenantId == null` against non-null rows, **every
branch is false, the query returns nothing, and the method reports the safe-sounding answer** — *no
conflict, no duplicate, nothing found. The sweep does not fail; it silently agrees with you.*
**A repository method reachable from BOTH a request path and a background job must not pick its own
tenancy — name the two variants and let the call site say which world it is in**, per the shipped
`EmployeeRepository.GetByIdAsync` / `GetByIdIgnoringTenantAsync` pair (`:44-57`).

**The worked example — now FIXED, and kept here as the reference pair rather than as an open hole.**
`OrderRepository.HasOverlappingOrderAsync` used a single `GetDbSet()` body while
`NewJobsDigestService.cs:137` called it from inside a `GetQueryableIgnoringTenant()` sweep — so under
a tenant **every cleaner reported as free and the digest would advertise double-booked jobs** — while
the *same* method is `TakeOrder`'s write gate, where the scoped read is correct. It is now one private
predicate parameterised by the queryable, with two public wrappers
(`HasOverlappingOrderAsync` / `HasOverlappingOrderIgnoringTenantAsync`), so the call site names its
world. Pinned by `HasOverlappingOrderTenancyAndScanFloorTests`, whose tenanted case fails if the
ignoring wrapper is reverted to `GetDbSet()`.

> **Do not leave a security law asserting a live hole that has been closed.** A reader who checks the
> citation and finds it fixed learns to distrust the rest of the catalog, which is the same defect
> class as a comment claiming two lists agree. Re-label to the shipped reference pair and name the pin.

**Reviewer test:** for every repository method, list its callers and ask whether
they all live in the same tenancy world. If not, one name is wrong. **The pinning test must seed a
non-null `TenantId`** — same as the mirror case, for the same reason. **And note the direction of the
lie:** the anonymous-write trap makes a *write* do nothing; this one makes a *guard* say yes.

## S9 — Migration & DTO-contract safety

- Add **nullable** columns freely. **Non-nullable** columns need a default or a backfill.
- **Never** rename a column in one migration — add new, deploy, dual-write, backfill, switch reads,
  drop old.
- **Dropping** a column: only after confirming no code *and no NSwag-generated client* references it
  (stale generated DTOs throw on deserialization).
- DTO changes are breaking unless: added fields are defaulted/nullable, removed fields were
  deprecated a release first, renamed fields expose both shapes for a release.
- Schema/DTO changes are flagged as `manual_steps` (`ef-migration`, `nswag-regen`) — owner-only.

## S10 — Soft-delete / `IsActive` semantics

`BaseEntity.IsActive` is the soft-delete flag and there is **no** global query filter for it
(intentional — admins must see all rows). Therefore every query that should hide deactivated rows
must filter `Where(e => e.IsActive)` itself. Common miss: "list my saved addresses", "catalog
packages", "pay configs" must exclude deactivated. Note the collision on recurring templates, where
`IsActive` is the user's *pause/resume* flag, not soft-delete — don't conflate them; if a true
soft-delete is ever needed there, add a separate column.

## S11 — Every per-user cache on mobile is wiped on session end (shared-device leak)

**On a shared device, the previous user's cached data must NOT survive to the next account.** ANY
mobile `@Singleton` (Android) / long-lived injected class (iOS) that holds **per-user state** — a
cached `StateFlow`/`@Published`, a persistent DataStore/`UserDefaults` row, a
[`Staleness`](https://github.com/VM-s-Solutions/cleansia/blob/master/src/cleansia_android/core/src/main/java/cz/cleansia/core/freshness/Staleness.kt)
watermark, or a per-key `Map` of any of these — **is a member of the session-wipe set and must be
flushed on session end.** Leaving one out leaks the prior user's orders / profile / invoices /
notifications to the next account on that handset — a security defect, not a UX nit (this rule was
authored after the class recurred 5+ times: `PushTokenRepository`, `NotificationFeedCache`,
`UserProfileStore`, customer `UserRepository`, and the T-0416 stragglers Dashboard/Orders/Invoices/
Profile/OrderChecklist/NotificationPreferences).

**The mechanism (single source of truth, never a hand-maintained clear-list):**
- **Android** — implement `cz.cleansia.core.auth.SessionScopedCache` and join the Hilt multibinding
  (`@Binds @IntoSet … : SessionScopedCache` in the app's `SessionScopedModule` / feature module). The
  auth layer iterates `Set<SessionScopedCache>` on every wipe path.
- **iOS** — conform to `SessionScopedCache` and `register(self)` with the injected
  `SessionScopedCacheRegistry` (held weakly); `clearAll()` iterates it.

**Three wipe-triggers, one set — they must not drift.** The set is iterated on **all three**:
1. **Sign-out** (voluntary `logout()`),
2. **Authenticator forced-401** (the refresh-terminal path — a revoked/reset session),
3. **Account deletion** (customer `UserRepository.deleteAccount()` — which, being itself a member,
   injects `Provider<Set<@JvmSuppressWildcards SessionScopedCache>>` / iterates the registry to
   break the self-referential Dagger cycle).

**The allowlist (the only sanctioned exception).** A `@Singleton` that holds cached state but whose
state is **device-level or public, not per-user**, is legitimately out of the set — but only if it is
on the **named, reason-annotated allowlist** in [`consistency.md` §E9](https://github.com/VM-s-Solutions/cleansia/blob/master/agents/knowledge/consistency.md) /
[`patterns-mobile.md`](https://github.com/VM-s-Solutions/cleansia/blob/master/agents/knowledge/patterns-mobile.md). A stateless pass-through (no cache field) is trivially
out and needs no allowlist entry, but should carry a one-line `// Stateless — nothing cached, so no
SessionScopedCache` comment (as `DeviceManagementRepository` does) so a reviewer isn't left guessing.
**A per-user holder missing from both the set and the allowlist is an S11 violation** — caught today by
the Reviewer (reading the diff + the `check-consistency.mjs` E9 warn-only advisory); the mechanical hard
gate is a **roster-equality assertion test** (`SessionScopedModuleTest` / `SessionScopedCacheRegistryTest`)
that is **specified but not yet built** — see `enforcement.md` and §E9 (the existing `AuthRepositoryTest`/
`PushLogoutClearsTests` only exercise `clearAll()` behaviorally with an injected set; they do not assert
the production multibinding equals the expected roster, so they would not catch a forgotten new repo).
**Retires when:** `SessionScopedModuleTest.kt` and `SessionScopedCacheRegistryTest.swift` exist.

## S12 — What is inside a stored artifact is disclosed to everyone who can fetch it

A file a user uploads is a **container**, not a value: pixels *plus* the capture coordinates, device
identity, author names and revision history that travel with them. A magic-byte check is an
accept/reject test — it bounds what the container **claims to be** and removes nothing from inside it.
An allowlist of *declared* types is weaker still: it is a client-affordance filter, and arbitrary bytes
under a permitted claim pass it unchanged.

**The hinge is the audience, not the delivery mechanism (ADR-0043 D7).** *"Served back by a URL"* is the
wrong question and excludes its own worst case: employee documents are **never** served by URL — three
routes, all `File(bytes, type, name)` → `Content-Disposition: attachment` — and they are the surface
carrying the **most** metadata. The question to ask is: **does a fetcher who is not the uploader receive
these bytes?**

For every upload surface, answer three questions **in writing, on the intake roster**
(`UploadIntakeRosterTests.cs:39-55` — 14 rows today):

1. **Who fetches these bytes — and what does the response hand them?** Two questions, not one, and a
   surface can pass the first while failing the second. If the only fetcher is the uploader the artifact
   discloses nothing new — record *"audience: self"*. **That answer expires the moment a second audience
   is added**, and the ticket that adds one owes the scrub.

   **The gate.** Read the gate the **fetch** actually uses, not the one you expect.
   `OrderAccessService.cs:68-91` returns `true` for **any** caller with role `Employee` and a resolvable
   employee id while `order.HasAvailableSpots && OrderVisibility.NotHeldFrom(…)` — so anything behind
   that gate has an audience which is **not enumerable at upload time**. Order photos are no longer
   behind it: `GetOrderPhotos.cs:63` gates on the strict `CanAccessOrderAsync` (`:58-61` records why —
   a signed URL is a forwardable bearer capability over an interior view of a private dwelling, and
   nothing inside the home is part of deciding whether to take the job).
   `GetOrderPhotosAssignmentGateTests:55-68` asserts the loose gate is not even *consulted*, so a
   fallback cannot quietly re-open it.

   **The projection.** A gate can be right and the response still wrong, and that is the half this law
   used to miss. The browse gate on `GetOrderDetails.cs:48` was doing its job — a cleaner must read a
   job before taking it — and the leak was that nothing shaped what "read" returned: the door code, the
   address with its coordinates, the confirmation code, the crew's surnames and phone numbers. The fix
   is a **projection**, not a narrower gate. The strict gate is asked *again* at `:57` purely as a
   redaction predicate and applied at `:136-138`, through one shared rule (`OrderPiiRedaction.cs` —
   seventeen `OrderItem` members at `:34-54`, the list twin at `:22-32`). Make that predicate the
   **entitlement**, never "is the caller assigned": an employee who books a cleaning for their own home
   arrives at that handler as the order's **customer**, and an assignment test would redact their own
   data from them. It fails **closed** — a later widening of the browse gate redacts by default.

   **The diagnostic, and run it before you go looking for a missing gate: when two routes serve the same
   entity to the same audience, compare their projections — the narrower one is usually the rule and the
   wider one is usually the omission.** The list handler had withheld the customer all along and said so
   in its own comment (`GetPagedOrders.cs:180-183`); the detail route simply never got one, so one extra
   GET undid the list's withholding. (The projection's *check* — read the DTO's field list — is S4's;
   Q1 is where you are standing when you discover it, because Q1 is the question that makes you open the
   fetch route at all.)
2. **What is it served as?** Server-derived, from a **closed set**, decided on the **read** path so it
   also governs rows written before the rule (`ServedContentType.ServableTypes`, `:34-42`, private
   constructor at `:56`; `text/html` and `image/svg+xml` are absent **by name**, and the mint pins it
   into the signature — `BlobContainerClient.cs:89-110` sets `rsct`/`rscc`). Never the client's declared
   type; never the file extension. **On any surface served by URL the accepted set must stay inside the
   servable set** — accepting a format that can only ever be served opaquely is an upload that succeeds
   and never renders. *(The document intakes are the deliberate exception and not a violation: they never
   mint a SAS, so `application/msword` and the OOXML type are accepted by
   `SniffedContentType.AcceptedByIntake` (`:88-104`) and served by `SniffedContentType.ForDownload`
   (`:127-128`), which never consults `ServedContentType`'s table.)*
3. **What travels inside it?** For an artifact whose audience is **not** its uploader, metadata
   containers are removed at **intake** — the read path cannot do it, because a signed URL hands the
   client the stored bytes directly and `rsct` retypes a header without changing a byte. **The removal
   dispatches on the bytes in hand, never on a stored or declared type**, or the uploader selects the
   no-op: declare `data:image/png`, send JPEG, and the PNG walker finds no `IHDR`, bails, and the
   coordinates survive **under a green "scrub applied" test**. `ImageMetadata.Scrub(byte[])`
   (`ImageMetadata.cs:35`) therefore takes no content type at all, and a container it cannot identify is
   passed through untouched and **reported as not scrubbed** — never as scrubbed. **Degrade explicitly:**
   EXIF `Orientation` is carried across **iff** the source reads unambiguously into 2–8; on anything else
   emit **no EXIF** and accept the rotation. *Never guess, never repair* — a rotated photo is a cosmetic
   defect on a rare and largely adversarial branch; a corrupted photo, or a surviving GPS tag, is not.
   **A surface that does not scrub records why, by name, on the roster** — the exclusion is written per
   surface, because one reason rarely covers two (ADR-0043 D8): employee documents are excluded on
   *mechanism* (a PDF/OOXML object-graph rewrite is the thing the no-decoder rule refuses) **and**
   *audience* (an admin who already holds the cleaner's legal name, tax id and payout details) **and**
   *delivery* (`attachment`, never by URL) — all three, which is what also covers the image formats that
   intake accepts; dispute-evidence PDFs are excluded on the **mechanism limb alone**, because there the
   uploader is a customer, the adverse party a cleaner and the fetcher staff adjudicating money, and the
   file is served **inline** (no `rscd`). That exclusion is evadable in one sentence — wrap the photo in a
   PDF — which is named rather than hidden, and narrowing the accept set is an owner question (Q-ART-01b),
   not an architecture one.

**And one prohibition: no request path decompresses user-supplied image data.** A decoder turns a
bounded upload into an allocation the uploader chooses — a few-hundred-KB single-colour 30 000 × 30 000
PNG into gigabytes of bitmap — on a memory-blind autoscale over a plan carrying seven sites. Nothing in
this system needs pixels; removal is a **container rewrite** (walk the segments/chunks, drop the metadata
containers, re-emit the rest byte-identically), not a re-encode. **This is a reachability property, not a
package-inventory one:** a complete JPEG/PNG/WebP decoding stack is *already* on the image as a
transitive native asset of QuestPDF, so the thing forbidden is the **call site**. Adding one is an
**ADR**, not a package reference, and it owes a header-derived dimension bound checked **before** any
decode.

**The incident.** `ImageFileValidator` was a 3–4 byte magic-prefix check standing in front of three
shipped pipelines — the avatar, order photos, dispute evidence; `SaveOrderPhotos` read its stored type
off the client's own `data:` URI prefix; every employee-document intake stored the string its uploader
claimed. EXIF GPS and device serials rode into `order-photos`, a container whose fetch set *then*
included cleaners with no relationship to the job (the gate has since been tightened — see Q1) —
defeating by *content* the two controls that deliberately withhold cleaner identity by *field*
(`GetOrderPhotos.cs:111-116` withholds `CapturedByEmployeeId` and the surname from a customer caller;
ADR-0036 keeps `PreferredEmployeeId` off every partner DTO). **None
of it was a violation of S1–S11** — S4 governs DTO fields, S6 governs logs, S8/S10 govern query scoping,
and none of them reaches inside a byte array. The reviewers were not wrong against the rules; the rules
were silent. *(All three intakes are hardened at HEAD — `ImageFileValidator` now runs
`SniffedContentType.FromContent(…, UploadIntake.Avatar)` behind a size bound, and the scrub is live at
`SaveOrderPhotos.cs:137`, `UploadOrderPhoto.cs:107`, `UploadDisputeEvidence.cs:108`. The incident is the
rule's origin, not an open hole — do not cite it as one.)*

**Scope.** S12 binds **new uploads**. It obliges no audit or rewrite of already-stored artifacts: the
type half was fixable on the read path, the content half is not (D3), so a backfill is a real data
migration and is its own ticket (ADR-0043 D9). And S12 is **not** an extension of S4: same principle —
*do not hand a client something you did not intend* — but a rule's identity is its **check**, and S4's
check is "read the DTO's field list," and no reading of a field list reaches inside a byte array. A
reviewer walking the S-series for an upload ticket will not open "DTO leak prevention."

### Enforcement (ADR-0032) — per clause, because the clauses are not tiered alike

**Enforced by:** the twelve-row table below — **mixed tier; read the row, not the rule.** The strongest
clause is `T1-CI` and the weakest has no mechanism at all; a single token would misdescribe at least
five rows whichever one you picked.

**Do not read S12 as `T1-CI` wholesale.** Of the twelve rows below, **seven** are enforced today,
**four** are specified and ticketed, and **one has no mechanism at all** and says so. A mechanism that cannot
fail a build is `T2-ADVISORY` however it is labelled — note that
`check-consistency.mjs` runs in **zero** `.github/` workflows and the frontend lint step is
`continue-on-error: true`, so neither can carry any clause here.

| Clause | Enforcer — and the assertion it **actually** makes | Tier |
|---|---|---|
| Q2 — served type is server-derived from a closed set | `ServedContentTypeTests` (`text/html`, `image/svg+xml`, `""`, `null`, `"nonsense"` → `application/octet-stream`, `:42-55`; **no public constructor and no `op_Implicit`/`op_Explicit`**, `:80-87`) · `SasResponseHeaderOverrideTests` (`rsct` on the minted token, `:38-43`; `rscc` never `public`, `:53-63`; the override rides **inside** the signature, `:85-91`) · `EmployeeDocumentDownloadContentTypeTests` (four legacy rows where the recorded string and the bytes disagree, over **both** download handlers, `:37-69`) · `EmployeeDocumentDownloadDispositionTests` (asserts `FileContentResult.FileDownloadName` is non-empty on all three routes, i.e. the 3-arg `File(…)` overload) | **`T1-CI`** — `Cleansia.Tests`, a named step of `backend-ci.yml:69-71` |
| Q2 — accepted set stays inside the servable set | **True by construction and unpinned.** No test reads `SniffedContentType.Signatures` at all (grep: it appears in one `src` file, its own). A seventh row that `ServedContentType` cannot serve reintroduces the defect silently. **What would close it:** assert every `Signatures` MIME reachable from a SAS-served intake resolves to a non-`Opaque` `ServedContentType`, count-asserted first. *(T-0562 pins the adjacent `ExtensionFor` ↔ `ForFileName` round-trip, which is a different assertion.)* | **`(gate pending: T-0458)`** |
| Q1/Q3 — the roster **enumerates** every intake | `UploadIntakeRosterTests` — count first (`Assert.Equal(ExpectedIntakes.Length, intakes.Count)`, `:64`) so an empty walk cannot agree with an empty roster, then the route-name list (`:66-68`), plus a `[Theory]` naming the four `byte[]`/`IFormFile` routes (`:76-84`) so narrowing the predicate cannot silently pass | **`T1-CI`** |
| Q1/Q3 — every intake **declares** its audience and its scrub | **Nothing.** The roster's `— <rule>` annotation is asserted by **no** test: `:66-68` splits each row on `" — "` and compares index `[0]` only, so the text after the dash is read by nobody. Adding `audience`/`scrub` columns without changing that assertion buys a string nobody reads. **What would close it:** assert the annotation vocabulary as a closed set, plus a per-intake refusal theory that names the failure's **identity** (that route's error code) and carries a **positive control** per case — `Assert.False(result.IsValid)` alone is green on any un-stubbed constructor dependency | **`(gate pending: T-0458)`** |
| Q1 — the response **projects**, not just gates | `OrderRedactionSurfaceTests` — every property of `OrderItem` and `OrderListItem` is classified blanked / reshaped / kept, and the coverage assertion runs **both** ways plus a count (`:209-222`), so a field added to either DTO fails the build naming itself; each blanked member is arranged non-empty first (`:146`, `:165`) so a fixture that never set it cannot pass · `OrderDetailBrowsingCleanerRedactionTests` (a unit twin and a real-Postgres twin) reads the same fixture back **in full** as the assigned cleaner, so "blank it for everyone" fails too | **`T1-CI`** |
| Q3 — the scrub actually removes metadata | `UploadDisputeEvidenceMetadataScrubTests` · `UploadOrderPhotoMetadataScrubTests` · `SaveOrderPhotosMetadataScrubTests` — each reads **the bytes handed to `IBlobContainerClient.UploadAsync`** (a `Callback` copying the stream), never "a helper was called", and asserts the GPS and device sentinels are absent while the image body survives. Each dies to its **own** call site being removed and no other's | **`T1-CI`** |
| Q3 — the scrub dispatches on bytes, and reports honestly | `ImageMetadata.Scrub` takes `byte[]` and nothing else (`ImageMetadata.cs:35`) · `ImageMetadataDispatchTests` (six payloads no walker claims → `Assert.False(result.Scrubbed)` **and** `Assert.Same(payload, result.Bytes)` — identity, not equality) · the three feature suites each send a format under a deliberately **wrong** declared type / `data:` prefix / file name | **`T1-CI`** |
| Q3 — orientation degrades, never guesses | `JpegMetadataScrubTests` — 2–8 re-emitted as a **server-synthesized** 36-byte `APP1` written out in the test rather than read from the code (`:23-31`), with `ByteSequence.Count(scrubbed, FF E1) == 1`; 13 unreadable sources (both TIFF byte orders, orientation 0/1/9/65535/absent, bad magic, out-of-range IFD pointers, wrong tag type/count, two disagreeing entries) each emit **no** `APP1`; six malformed containers are **refused, not repaired** (`Assert.Same` on the input). Carries its own anti-vacuity fact — `:43` asserts the fixture *does* contain GPS before asserting the output does not | **`T1-CI`** |
| Q1 — the avatar exemption is honoured | `UpdateCurrentUserAvatarScrubExemptionTests` — asserts the stored bytes **equal** what was sent and still contain the GPS sentinel, so it reddens the day someone wires the scrub in "for consistency" | **`T1-CI`** |
| Q1 — the avatar exemption's **expiry** | **Nothing, and the test above says so in its own docstring.** The exemption expires when an avatar URL first appears on a cross-user DTO — one line in `UserMappers` / `EmployeeMappers`, which no shipped test can see. In production only three call sites touch `user-files`: `GetCurrentUser.cs:59` (the self SAS mint), `UpdateCurrentUser.cs:160` (write), `GdprDeletionService.cs:134` (delete). **What would close it:** a frozen wire-surface assertion in the shape of `PayoutDtoSurfaceTests` — the avatar URL may appear on the self DTO and on no other. **Ticket owed** | **`(guidance — no gate)`** |
| Prohibition — no **direct package reference** to a decoder | **Nothing.** `SixLabors` / `SkiaSharp` / `System.Drawing` / `Magick` return **zero** matches across all of `src/**` — including test sources, so no denylist test exists either. **What would close it:** a `.csproj` denylist walk with a project-count non-vacuity floor | **`(gate pending: T-0458)`** |
| Prohibition — no **call site** reaching a decoder | **Nothing.** `ImageDescriptor` / `Image.FromBinaryData` return zero matches across `src/**`; the property holds and is asserted by no test. A package-name denylist **cannot** express it — one `.Image(orderPhotoBytes)` inside a QuestPDF invoice or dispute pack creates the primitive while the denylist stays green. **What would close it:** a source scan of `src/**/*.cs` with a non-vacuity floor. **If T-0458 cannot build it, this clause is declared `T2-ADVISORY` here with a named reviewer check — it is not left labelled as a gate** (ADR-0043 §B.6) | **`(gate pending: T-0458)`** |

**Reviewer test.** For a ticket that adds or changes an upload route: open the roster row, and ask
whether its `audience` answer is derived from the gate the *fetch* uses or from the gate the *write*
uses — they are two call sites and either can move alone. On `SaveOrderPhotos` the write gate is
assignment (`:115-118`); the fetch gate is `GetOrderPhotos.cs:63`, which used to be looser and is now
strict, so an answer copied from the write side would have been right by accident for a year. Every
wrong audience answer this codebase has produced came from reading the write gate. Then ask what the
**response** projects, not only what the gate admits, and what the scrub is handed: a `contentType`
parameter anywhere on that path is the defect, not a convenience. Full context and the trade-off space:
`agents/architecture/decisions/user-uploaded-artifacts.md`; the record is **ADR-0043**.

---

## Audit checklist for an existing endpoint

1. `[Permission]` or `[AllowAnonymous]` present (S2)
2. `userId` enriched from JWT, body not trusted (S1)
3. Ownership checked for resource-by-id paths (S3)
4. Response DTO has no leaked fields — and where one DTO serves two audiences, the weaker one gets a
   projection, not just a gate (S4)
5. No `IgnoreQueryFilters()` without a justifying comment (S8)
6. `CancellationToken` propagated end-to-end
7. Rate-limited if auth or external-side-effect (S5)
8. Idempotent if it has a doublable side effect (S7)
9. `IsActive` filter applied where soft-delete matters (S10)
10. No PII in logs above Debug (S6)
11. (mobile) A new per-user `@Singleton`/injected cache is in the session-wipe set or the §E9
    allowlist (S11)
12. (upload) The route is on the intake roster, and its row answers **who fetches** (read the *fetch*
    gate, not the write gate), **what it is served as** (closed set, read path) and **what is scrubbed**
    — with a named reason if it is not. The scrub takes bytes, never a content type. No decoder is
    reachable from the request path (S12)
