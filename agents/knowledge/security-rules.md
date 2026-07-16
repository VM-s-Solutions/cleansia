# Security Rules (S1–S10) — Non-Negotiable

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
[`Staleness`](../../src/cleansia_android/core/src/main/java/cz/cleansia/core/freshness/Staleness.kt)
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
on the **named, reason-annotated allowlist** in [`consistency.md` §E9](./consistency.md) /
[`patterns-mobile.md`](./patterns-mobile.md). A stateless pass-through (no cache field) is trivially
out and needs no allowlist entry, but should carry a one-line `// Stateless — nothing cached, so no
SessionScopedCache` comment (as `DeviceManagementRepository` does) so a reviewer isn't left guessing.
**A per-user holder missing from both the set and the allowlist is an S11 violation** — caught today by
the Reviewer (reading the diff + the `check-consistency.mjs` E9 warn-only advisory); the mechanical hard
gate is a **roster-equality assertion test** (`SessionScopedModuleTest` / `SessionScopedCacheRegistryTest`)
that is **specified but not yet built** — see `enforcement.md` and §E9 (the existing `AuthRepositoryTest`/
`PushLogoutClearsTests` only exercise `clearAll()` behaviorally with an injected set; they do not assert
the production multibinding equals the expected roster, so they would not catch a forgotten new repo).

---

## Audit checklist for an existing endpoint

1. `[Permission]` or `[AllowAnonymous]` present (S2)
2. `userId` enriched from JWT, body not trusted (S1)
3. Ownership checked for resource-by-id paths (S3)
4. Response DTO has no leaked fields (S4)
5. No `IgnoreQueryFilters()` without a justifying comment (S8)
6. `CancellationToken` propagated end-to-end
7. Rate-limited if auth or external-side-effect (S5)
8. Idempotent if it has a doublable side effect (S7)
9. `IsActive` filter applied where soft-delete matters (S10)
10. No PII in logs above Debug (S6)
11. (mobile) A new per-user `@Singleton`/injected cache is in the session-wipe set or the §E9
    allowlist (S11)
