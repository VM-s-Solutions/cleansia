# Backend Patterns (.NET 10 / CQRS / EF Core) — REAL TYPES

The concrete "how we write backend code" catalog, bound to the **actual base types in this
repository** (verified from source). Read this + [`security-rules.md`](./security-rules.md) +
[`conventions.md`](./conventions.md) before touching `.cs`. **Reuse these exact types — never invent
parallel ones.** Authoritative architecture prose:
[`../../docs/architecture/backend.md`](../../docs/architecture/backend.md).

> **Binding rule for every backend agent:** before writing a feature, open the nearest existing
> feature in the same `Features/<Domain>/` folder and mirror its idiom exactly. The samples below are
> copied from live code (`Features/Orders/`).

---

## The exact base contracts (use these names)

| Concept | Exact type | Location |
|---|---|---|
| Command marker | `ICommand`, `ICommand<TResponse>` | `Cleansia.Core.AppServices/Abstractions/ICommand.cs` |
| Query marker | `IQuery<TResponse>` | `Cleansia.Core.AppServices/Abstractions/IQuery.cs` |
| Command handler | `ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>` | `…/Abstractions/ICommandHandler.cs` |
| Query handler | `IQueryHandler<TQuery, TResponse>` | `…/Abstractions/IQueryHandler.cs` |
| Result | `BusinessResult`, `BusinessResult<TValue>` | `Cleansia.Infra.Common/Validations/BusinessResult.cs` |
| Error | `Error(string Code, string Message)` | `Cleansia.Infra.Common/Validations/Error.cs` |
| Error codes | `BusinessErrorMessage` (static class of `const string`) | `Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs` |
| Controller base | `CustomerApiController` / `PartnerApiController` / `AdminApiController` / mobile variants (all expose `Mediator` + `HandleResult`) | each `Web.*/Abstractions/` |
| Permission attr | `[Permission(Policy.CanXxx)]` | `Web.*/Attributes/PermissionAttribute.cs` |
| Policy names | `Policy.CanXxx` (`const string`) | `Cleansia.Core.AppServices/Authentication/Policy.cs` |
| Session | `IUserSessionProvider` (`GetUserId()`, `GetTypedUserClaim(...)`) | `Cleansia.Core.Domain/Repositories/IUserSessionProvider.cs` |
| Repo base | `BaseRepository<TEntity> : IRepository<TEntity, string>` | `Cleansia.Infra.Database/BaseRepository.cs` |
| Unit of work | `IUnitOfWork` (`CommitAsync`) | `Cleansia.Core.Domain/SeedWork/IUnitOfWork.cs` |
| Entity bases | `BaseEntity`, `Auditable : BaseEntity`, `IEntity`/`IEntity<T>`, `ITenantEntity` | `Cleansia.Core.Domain/Common/` |
| Paging in | `DataRangeRequest` (`Offset`, `Limit`, `Sort`) | `…/Shared/DTOs/RequestModels/DataRangeRequest.cs` |
| Paging out | `PagedData<T>` (`PageNumber`, `PageSize`, `Total`, `Data`) | `…/Shared/DTOs/ResponseModels/` |
| Sort | `SortDefinition`, `BaseSort<TEntity>`, `<Entity>Sort` | `…/Shared/DTOs/Sorting/`, `Core.Domain/Sorting/` |
| Filter/spec | `<Entity>Filter`, `<Entity>Specification.Create(...).SatisfiedBy()` | `Features/<Domain>/Filters/`, `Core.Domain/Specifications/` |
| Page map | `pagedList.MapToDto(total, request)` → `PagedData<T>` | `Mappers/PageDataMapper.cs` |

`BusinessResult` factories that actually exist: `Success()`, `Success<T>(value)`, `Failure(Error)`,
`Failure<T>(Error)`, `Create<T>(value?)`. **There is no `NotFound()`/`Forbidden()`/`ValidationFailure()`
helper and no `ErrorType` enum** — construct failures as `BusinessResult.Failure<Response>(new Error(code, BusinessErrorMessage.X))`.

---

## The one-file feature — exact shape (a COMMAND, from `Features/Orders/CancelOrder.cs`)

The feature is a **`public class`** (not `static`) with nested `record Command`/`record Response` +
`class Validator` + `class Handler`. Note the real validator (FluentValidation `.WithMessage(BusinessErrorMessage.X)`),
the real in-handler ownership check (S3), and the real failure construction:

```csharp
public class CancelOrder
{
    public record Command(string OrderId, string? Reason) : ICommand<Response>;

    public record Response(string OrderId, decimal FeeRate, decimal RefundAmount,
                           decimal TotalPrice, bool RefundInitiated);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync).WithMessage(BusinessErrorMessage.OrderNotFound);

            RuleFor(x => x.Reason)
                .MaximumLength(500).WithMessage(BusinessErrorMessage.MaxLength);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        IUserSessionProvider userSessionProvider,
        /* …other injected deps… */
        ILogger<Handler> logger) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken ct)
        {
            var userId = userSessionProvider.GetUserId()!;           // S1: identity from session, not body
            var order = await orderRepository.GetQueryable()
                .Include(o => o.OrderStatusHistory)
                .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

            if (order is null || order.UserId != userId)             // S3: ownership; NotFound for cross-user
                return BusinessResult.Failure<Response>(new Error(nameof(command.OrderId), BusinessErrorMessage.OrderNotFound));

            // …happy-path domain logic via rich entity methods (order.Cancel(...), order.AddOrderStatus(...))…
            // NO SaveChangesAsync / CommitAsync here — the UnitOfWork pipeline commits commands.

            return BusinessResult.Success(new Response(order.Id, feeRate, refundAmount, order.TotalPrice, refundInitiated));
        }
    }
}
```

**Rules confirmed from this code:**
- Feature class is `public class <UseCase>` (not `static`); `Command`/`Response` are `record`s; the
  command record type **ends in `Command`** (the UoW pipeline keys commit on that name — misname it
  and the row is silently not saved).
- Validator inherits `AbstractValidator<Command>`, uses `.Cascade(CascadeMode.Stop)`, injects repos
  for async existence checks (`MustAsync(repo.ExistsAsync)`), and maps every rule to a
  `BusinessErrorMessage.X` constant via `.WithMessage(...)`.
- Handler implements `ICommandHandler<Command, Response>`, uses a primary constructor, is happy-path
  only after the validator, derives identity from `IUserSessionProvider.GetUserId()`, checks
  ownership, and returns `BusinessResult.Success(...)` / `BusinessResult.Failure<Response>(new Error(...))`.
  **No `try/catch` for control flow, no `CommitAsync()`.** (A narrow `try/catch` around a specific
  external call like a Stripe refund — to keep a non-blocking side effect from failing the command —
  is allowed and used in the real code; that's not validation control flow.)

## The one-file feature — exact shape (a PAGED QUERY, from `Features/Orders/GetPagedOrders.cs`)

Paged queries are **different from commands**: the request inherits `DataRangeRequest` and
`IRequest<PagedData<T>>` (a plain MediatR request, *not* `IQuery<T>`), the handler is **`internal`**,
returns `PagedData<T>` **directly** (not wrapped in `BusinessResult`), and uses the
specification + `GetPagedSort<TSort>` + `MapToDto(total, request)` machinery:

```csharp
public class GetPagedOrders
{
    public class Request : DataRangeRequest, IRequest<PagedData<OrderListItem>>
    {
        public OrderFilter? Filter { get; init; }
    }

    internal class Handler(IOrderRepository orderRepository, IUserSessionProvider userSessionProvider /* … */)
        : IRequestHandler<Request, PagedData<OrderListItem>>
    {
        public async Task<PagedData<OrderListItem>> Handle(Request request, CancellationToken ct)
        {
            var specification = OrderSpecification.Create(/* fields off request.Filter */);
            var filter = specification.SatisfiedBy();

            var totalItems = await orderRepository.GetCountAsync(filter, ct);
            var orders = await orderRepository
                .GetPagedSort<OrderSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .Include(o => o.Currency)         // include ONLY what the mapper reads (perf)
                .AsSplitQuery().AsNoTracking()
                .ToListAsync(ct);

            var items = orders.Select(o => o.MapToDto()).ToList();
            return items.MapToDto(totalItems, request);   // → PagedData<OrderListItem>
        }
    }
}
```

The matching controller endpoint returns the `PagedData<T>` straight from `Mediator.Send` (no
`HandleResult`):

```csharp
[HttpGet("GetPaged")]
[Permission(Policy.CanViewPagedUserOrder)]
[ProducesResponseType(typeof(PagedData<OrderListItem>), StatusCodes.Status200OK)]
public async Task<PagedData<OrderListItem>> GetPaged([FromQuery] GetCustomerOrders.Request request, CancellationToken ct)
    => await Mediator.Send(request, ct);
```

## Controller pattern (from `Web.Customer/Controllers/OrderController.cs`)

```csharp
[Route("api/[controller]")]
[ApiController]
public class OrderController(IMediator mediator) : CustomerApiController(mediator)
{
    [HttpPost("Cancel")]
    [Permission(Policy.CanCancelOrder)]                              // S2: every endpoint authorized
    [ProducesResponseType(typeof(CancelOrder.Response), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelOrder([FromBody] CancelOrder.Command command, CancellationToken ct)
    {
        var result = await Mediator.Send(command, ct);
        return HandleResult<CancelOrder.Response>(result);           // maps BusinessResult → HTTP
    }
}
```

Real authorization vocabulary: `[Permission(Policy.CanXxx)]` for role-gated routes,
`[AllowAnonymous]` for public ones (order create/lookup, quote), `[Authorize]` for any-authenticated.
Rate-limit windows in use: `[EnableRateLimiting("auth")]` and `[EnableRateLimiting("interactive")]`.
Identity is enriched via `IUserSessionProvider.GetUserId()` in the handler — commands here often
don't carry a `UserId` field at all (the handler reads the session), which is the preferred shape.

## Repository pattern (from `Infra.Database/BaseRepository.cs`)

`<Entity>Repository : BaseRepository<TEntity>` with the interface in `Core.Domain/Repositories`.
The base gives you: `GetByIdAsync`, `ExistsAsync`, `ExistWithIdsAsync`, `GetByIds`, `GetPaged`,
`GetPagedSort<TSort>`, `GetCountAsync`, `GetFiltered`, `GetQueryable`, `GetQueryableIgnoringTenant`
(deliberate cross-tenant — S8), `Add`/`AddRange`, `Deactivate`/`DeactivateRange` (soft-delete via
`IsActive`), `CommitAsync`/`BeginTransactionAsync`/`Rollback`. IDs are **`string`**. Repositories
expose `IQueryable` to *handlers in the same feature* via `GetQueryable()` (the real code composes
`.Include(...).FirstOrDefaultAsync(...)` in the handler) — but never return raw `IQueryable` across a
trust boundary, and never let a query escape tenant scope (S8). Use `.AsNoTracking()` + `.AsSplitQuery()`
on read paths.

**Failure-path counters (lockout / attempt budgets) bypass the UoW deliberately.** A security counter
that must persist when the COMMAND FAILS (failed-login lockout, per-code attempt budget) cannot ride
the UnitOfWork pipeline — the pipeline only commits successful `BusinessResult`s. The canonical shape
is an **atomic conditional `ExecuteUpdateAsync`** on the repository (`WHERE counter < cap`, 0 rows =
limit reached; mirrors `PromoCodeRepository.TryIncrementGlobalRedemptionsAsync`, S7a), invoked from
the validator/handler that detects the failure: `UserRepository.RecordFailedLoginAsync` /
`TryCharge*CodeAttemptAsync`. The entity keeps only the read side (`IsLockedOut(now)`) and the
success-path resets.

**Admin-action audit is automatic — do NOT hand-write audit rows (ADR-0012).** Every admin mutation
(a `Command` run by an `Administrator` role claim) is captured by `AuditLogBehavior`, registered
**inner to `UnitOfWorkPipelineBehavior`** (the line after the UoW registration in
`FluentValidationExtensions`), so the success row rides the action's single `SaveChangesAsync` and is
atomic. Outcome on failure is written out-of-band by `IAuditFailureSink` in its own scope (best-effort,
never re-thrown): a handler-returned business failure is caught by the inner `AuditLogBehavior`, while
the two shapes it structurally cannot see — a **validation reject** (short-circuited outer to it) and a
**commit-throw** (raised after it returned its success-add) — are caught by the **outermost**
`AuditFailureCaptureBehavior`. The two share one scoped `IAuditContext` latch
(`TryClaimFailureRecording`) so a failure is recorded exactly once. A failed/blocked admin attempt is
therefore never trail-less. To capture an admin action you write **no audit code**:
the type name is the label by default, or freeze it with `[AuditAction("admin.user.create",
ResourceType="AdminUser")]` on the `Command` record (rename-proof; `Sensitive=true` for the
before/after subset; `Audited=false` to opt a noisy command out). The five sensitive money/state
handlers additionally push a typed, pre-redacted snapshot to scoped `IAuditContext.RecordChange(...)` —
the behavior never computes a diff or references a domain type (T-0284). Never set an
`AdminActionAudit` to `Modified`/`Deleted` (append-only, init-only).

## Entities (from `Core.Domain/Common/`)

- `IEntity` = `{ object Id; bool IsActive; }`; `IEntity<T>` narrows `Id`/`IsActive`. IDs are strings.
- `Auditable : BaseEntity` adds `TenantId`, `CreatedBy/On`, `UpdatedBy/On`, `DeactivatedBy/On`, with
  fluent `Created(...)`, `Updated(...)`, `Deactivated(...)` (the last sets `IsActive=false`).
- Rich domain: private setters, factory `Create(...)`, behavior methods (`order.Cancel(...)`,
  `order.AddOrderStatus(OrderStatusTrack.Create(...))`, `order.UpdatePaymentStatus(...)`). Entity
  classes carry **no EF attributes** — mapping lives in `Infra.Database/EntityConfigurations/`
  (DB Master's domain). Implement `ITenantEntity` for user-scoped data (S8).

## Errors & i18n binding (critical, verified)

`BusinessErrorMessage` is a static class of **flat PascalCase `const string`** whose **values are
dot-notation keys** the frontend translates, e.g.:

```csharp
public const string Required          = "common.required";
public const string MaxLength         = "common.max_length";
public const string OrderNotFound     = "order.not_found";
public const string InvalidEnumValue  = "common.invalid_enum_value";
```

So a new error = add a `const string` here whose value is a dot key, then add that key to every
frontend locale under the matching path (the frontend normalizes the code → translation key, see
`patterns-frontend.md`). Never inline a raw code string — always reference the constant.

### Catalog entity translations (CC-06, owner decision Q-W3-1 path b)

Catalog items (Service, Package) carry a per-language `Translations` dictionary, and translations
are **mandatory for every ACTIVE `Language` row** — there is no `Language.IsDefault` and no
fallback language. The enforcement lives in the Create/Update validators
(`CreateService`/`UpdateService`/`CreatePackage`/`UpdatePackage`): the provided translation codes
must **exactly equal** the active-language code set (`GetAll().Where(l => l.IsActive)` +
`SetEquals`), failing with `service.translations_required` / `service.missing_translation_for_language`.
**Add-a-language behavior:** activating a new `Language` row does not retro-block existing items —
they keep serving their stored translations — but every item is *incomplete* from that moment: its
next admin save is rejected until the new language's translation is supplied. New catalog
entities with translations reuse the shared rule extension — `RuleFor(x => x.Translations)
.MustCoverAllActiveLanguages(languageRepository)` from `Common/Validators/ValidationExtensions.cs`
— never a hand-rolled copy of the block.

## Canonical recipes (copy, then fill in)

> The fastest path must also be the correct one. Start from these skeletons; they encode the
> `consistency.md` rules (A* for queries, B* for commands). Deviating from them is a review fail.

**Paged query** (rules A1–A8):

```csharp
public class GetPagedXxx
{
    public class Request : DataRangeRequest, IRequest<PagedData<XxxListItem>>   // A1
    {
        public XxxFilter? Filter { get; init; }                                 // A7 (init, not set)
    }

    internal class Handler(IXxxRepository repo) : IRequestHandler<Request, PagedData<XxxListItem>>  // A2
    {
        public async Task<PagedData<XxxListItem>> Handle(Request request, CancellationToken ct)
        {
            // A8: scope the filter to the caller (admin sees all; else own) BEFORE building the spec
            var filter = XxxSpecification.Create(/* request.Filter fields */).SatisfiedBy();          // A3
            var total = await repo.GetCountAsync(filter, ct);                                          // A4
            var items = await repo
                .GetPagedSort<XxxSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())  // A4
                .Include(x => x.Nav).AsNoTracking().Select(x => x.MapToDto()).ToListAsync(ct);         // A6
            return items.MapToDto(total, request);                                                     // A5
        }
    }
}
```

**Create / Update / Delete command** (rules B1–B9):

```csharp
public class UpdateXxx
{
    public record Command(string XxxId, /* fields */) : ICommand<Response>;     // B1
    public record Response(string XxxId);                                       // B1

    public class Validator : AbstractValidator<Command>                         // B3 (no custom base)
    {
        public Validator()
        {
            RuleFor(x => x.XxxId).Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required);         // B3 (BusinessErrorMessage)
            // B4: validate SHAPE here; ownership + the entity fetch live in the handler.
        }
    }

    public class Handler(IXxxRepository repo, IUserSessionProvider session) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken ct)
        {
            var userId = session.GetUserId()!;                                  // B2
            var entity = await repo.GetByIdAsync(command.XxxId, ct);            // B4 fetch-and-guard
            if (entity is null || entity.OwnerId != userId)                     // B4 + S3 ownership in handler
                return BusinessResult.Failure<Response>(new Error(nameof(command.XxxId), BusinessErrorMessage.XxxNotFound)); // B5
            entity.Update(/* fields */);                                        // B7 rich domain method
            // Delete instead? -> repo.Deactivate(entity);  (B6 soft-delete by default)
            // Side effects (Stripe/email/queue)? -> narrow try/catch + idempotency (B8)
            return BusinessResult.Success(new Response(entity.Id));             // B9 map output
        }
    }
}
```

## What to mirror, not invent

- `BusinessResult` / `Error` / `BusinessErrorMessage` — exactly these. No new result type, no
  `ErrorType` enum, no `NotFound()` helper.
- `DataRangeRequest` + `PagedData<T>` + `<Entity>Specification` + `<Entity>Sort` for every paged
  list. Don't hand-roll Skip/Take/sorting.
- `CustomerApiController`/`PartnerApiController`/`AdminApiController` + `HandleResult` + `Policy.CanXxx`.
- `IUserSessionProvider.GetUserId()` for identity (S1). Ownership check in the handler (S3).
- `manual_step: ef-migration` (schema) and `manual_step: nswag-regen` (DTO/endpoint) — owner-only.

## Order status reads & list projections (the CurrentStatus discipline)

`Orders.CurrentStatus` is a persisted denormalization of the latest `OrderStatusHistory` row,
written ONLY at the `Order.AddOrderStatus` seam (CreatedOn-desc, Sequence-desc rule);
`OrderStatusHistory` stays the authoritative audit trail. Two read rules, pinned by
`OrderCurrentStatusPersistenceTests` / `ColdPathCurrentStatusQueryTests`:

- **Filters/counts read the column and exclude NULL** — `o.CurrentStatus == OrderStatus.X` or
  `o.CurrentStatus != null && set.Contains(o.CurrentStatus.Value)`. Index-served; pre-backfill
  NULL rows are closed by the idempotent backfill (seed script + the deploy runbook's re-run).
- **Projections fall back** — a NULL column must still emit the row's true status:
  `o.CurrentStatus ?? o.OrderStatusHistory.OrderByDescending(s => s.CreatedOn)
  .ThenByDescending(s => s.Sequence).Select(s => (OrderStatus?)s.Status).FirstOrDefault()`
  (GDPR export, `SelectOrderListRows`).
- **Exception: fail-closed conflict predicates also fall back** — a filter whose NULL-exclusion
  would fail OPEN (e.g. the overlap/time-conflict check: skipping a pre-backfill row would let an
  active legacy order stop blocking and double-book the cleaner) must not exclude NULL; it reads
  the column for non-null rows and falls back to the latest-history subquery (same
  CreatedOn-desc/Sequence-desc rule) for NULL rows only (`HasOverlappingOrderAsync`, pinned by
  `HasOverlappingOrderStatusTests`).

Never write the column outside `AddOrderStatus`, and never hand-roll a new latest-history status
subquery — filter on the column; project with the fallback. The **only** sanctioned exception is
the fail-closed case above: the fallback runs for NULL rows only, non-null rows stay on the indexed
column, the subquery uses the same CreatedOn-desc/Sequence-desc rule, and the call site carries a
comment naming the fail-open risk plus a status-matrix test pinning it (mirror
`HasOverlappingOrderAsync`). A latest-history subquery without all four is still a violation.

The order LIST queries (`GetPagedOrders`/`GetCustomerOrders`) do not materialize entity graphs:
they project server-side via `OrderMappers.SelectOrderListRows()` into the backend-only
`OrderListRow` records and map with `MapToDto(OrderListRow)`; the wire DTO stays `OrderListItem`.
Any change to the list shape must keep `OrderListProjectionEquivalenceTests` (JSON-equivalence
against the retained entity-mapper path) green — that test is the contract that the projection
and the entity path emit identical DTO values.

## B8 — the refund money path (ADR-0006 seam + ADR-0009 policy)

A refund is the one side effect with both money and fiscal consequences, so it has a frozen contract:
- **One seam.** Every Stripe refund goes through `IRefundService.IssueRefundAsync` (ADR-0006). No handler
  calls `RefundCheckoutSessionAsync` directly; the seam carries the deterministic `RefundKey` and clamps to
  the refundable ceiling. A refund issued outside the seam, or without the deterministic key, is a
  B8/S7/ADR-0006 violation.
- **Policy is caller-side, not in the seam** (ADR-0009). The 14-day soft window (anchored to
  `Order.CompletedAt`, null→closed, chargeback-exempt, admin-overridable with a recorded reason) and the
  Stripe-fee bearer (platform absorbs on `RefundReason.ServiceNotRendered`/`DisputeResolution`, deducts only
  on `AdminDiscretion`) live in a `RefundPolicy` policy class (sibling to `BookingPolicy`) and are checked by
  the caller. Enforcing the window inside `IRefundService` is an ADR-0009 violation.
- **Partial allocation = share of the FROZEN `Order.TotalPrice`** (ADR-0009 D2). `Order.TotalPrice` already
  embeds discount + the express surcharge (`OrderFactory.cs:91-95`); the refund allocator multiplies a
  line-share by `TotalPrice` and **never re-applies discount/surcharge**. Last refunded line absorbs the
  sub-cent residual; VAT apportioned by the same ratio (`0` when `AppliedVatRate` is null / non-VAT-payer).
  A bundled service's gross comes from the `PackageService.PriceWeight` split of `Package.Price`.
- **Partial loyalty clawback** uses the per-refund-keyed `ILoyaltyService.RevokeForPartialRefundAsync`
  (cumulative-capped, `UserId==null` skip), **not** the one-shot `RevokeForCancelledOrderAsync` mirror.

## User notifications — ONE seam, never a hand-rolled push (T-0393 FD-AC12)

Every user-facing notification (push + in-app feed row) is produced through
**`INotificationProducer.NotifyAsync(userId, eventKey, args, tenantId, subject, ct)`**
(`Core.AppServices/Services/NotificationProducer.cs`). One call records BOTH halves into the
caller's scoped unit of work — the `UserNotification` feed row (for feed-scoped events, per the
`NotificationFeedEventKeys` audience keysets beside `NotificationEventCatalog`) and the outbox push
row — so both commit atomically with the domain change and neither exists on rollback.
**Constructing `new SendPushNotificationMessage(...)` anywhere else is a violation**, mechanically
pinned by `SendPushNotificationSeamTripwireTests` (allowed sites: the seam, the sitewide-promo
fan-out, the record's own file). Rules the seam encodes: category mutes gate the PUSH (checked by
the dispatch consumer), never the feed row — except the new-jobs digest, whose producer skips muted
cleaners entirely; the digest collapses onto the user's single UNREAD `order.new_available` row
(`RefreshDigest`); `subject` is the `MessageKeys.Push` dedup segment (order/dispute/membership id).
Feed reads/marks are always scoped to the calling mobile host's audience keyset — the host
controller overwrites the `Audience` field server-side (S1-style enrichment); never trust it from
the client.

## Queue-consumer idempotency — the claim-ordering rule (ADR-0002 D2.2 · ADR-0010 · ADR-0023)

Every effect-realizing queue consumer MUST assert its terminal effect has not already happened
(ADR-0002 D2.2): a **domain target-state check** where one exists (preferred — e.g. the
already-calculated pay validator), else the durable **`IIdempotencyGuard` / `ProcessedMessage`
unique-row backstop** (ADR-0010: `BaseEntity`, UNIQUE `MessageKey`, claimed in the guard's **own**
committed unit of work, PG 23505 → "already processed"). **When the marker is written is a
per-consumer decision**, governed by one test (ADR-0023):

> **The repeatable-effect test:** if this consumer's terminal effect ran twice, would anything need
> un-doing (a refund, a reversal, a duplicate document/ledger/pay row, a double charge)?
> **Yes → Mode A is mandatory. At-worst-a-nuisance → Mode B is permitted.**

- **Mode A — claim-BEFORE-act (at-most-once after the marker). MANDATORY for non-repeatable effects**
  (receipt/invoice generation, pay calculation, fiscal registration — anything money-shaped):
  `if (await guard.AlreadyProcessedAsync(key, ct)) return;` then act. Residual: a crash between claim
  and act loses that one effect — accepted, because the duplicate would be worse. Reference:
  `SendPushNotificationHandler`.
- **Mode B — claim-AFTER-successful-act (at-least-once). Permitted where a duplicate is benign**
  (today: the send-email consumer ONLY; push is a candidate follow-up, not ratified): non-claiming
  pre-check `HasProcessedAsync(key)` (a redelivery *filter*, deliberately not atomic) → act →
  `MarkProcessedAsync(key)` post-success (23505 = benign no-op inside the guard; any other claim-write
  failure is caught by the *handler*, logged "sent but unclaimed", and ACKED — never thrown, since
  throwing after a successful send manufactures the duplicate). A failed act leaves **no row**, so the
  queue retry genuinely retries — the point of Mode B (the SendGrid config-gap incident: claim-first
  turned every retry into a green no-op and permanently ate the emails). Residual: rare duplicates in
  two windows — concurrent redeliveries both passing the pre-check, and a crash between act-success
  and claim-write.

Adopting Mode B for a new consumer requires an ADR (or an explicit ticket decision note citing
ADR-0023's test) + the two duplicate windows documented in the consumer's doc-comment. **Never mix
modes in one consumer**, and never hide the mode behind a boolean — the member name at the call site
is the greppable evidence of which mode the consumer runs (ADR-0002 verification check #3 logic).
Role card: `agents/knowledge/roles/idempotency-guard.md`.

## Tenancy is APP; region is INFRA — they are orthogonal (ADR-0017)

Two isolation axes meet in this codebase, and they live in **different layers** — keep them there.

- **Tenancy = an APP concern, and it already exists.** Tenant rows are isolated **logically** by the
  global query filter in `CleansiaDbContext.ApplyTenantQueryFilters` (applied to every
  `ITenantEntity` — `{ string? TenantId }`), driven by the `tenant_id` JWT claim resolved by
  `TenantProvider`. The filter body is exactly
  `tenantProvider == null || (currentTenantId == null && e.TenantId == null) || e.TenantId == currentTenantId`
  — design-time bypass, the single-tenant `null/null` middle clause, then the multi-tenant happy path.
  Cross-tenant work (background jobs, anonymous webhooks) is **explicit**: `tenantProvider.SetTenantOverride(...)`
  or `IgnoreQueryFilters` (see the webhook/`IgnoreQueryFilters` memory notes). **This is the proven path;
  do not move tenancy to infra (DB-per-tenant / schema-per-tenant) and do not touch the filter.**
- **Region = an INFRA/config concern, and it is net-new.** Region answers *"which physical
  deployment/DB does this request hit?"* — it never answers *"whose rows is this?"* There is **no
  region concept in the domain or data model** (the only geography is `CountryConfiguration`, the
  per-**market** seam). Region lives entirely in the Bicep/pipeline (a `region` parameter, the
  `weu` name token) and, on the data side, in **one** connection-string resolver (T-0330) — today a
  constant returning the single shared West-Europe DB.

**The two compose, they do not conflict.** A tenant's rows are isolated by the filter **regardless**
of which region's DB they sit in; a tenant has **exactly one home region** (its rows live in one
region's DB), so `e.TenantId == currentTenantId` is sufficient *within* that DB and region selects
*which DB*, not *which rows*.

**Hard rules a reviewer enforces (ADR-0017):**
- **Never add a region clause to the tenancy filter.** `ApplyTenantQueryFilters` stays `TenantId`-only.
  `e.TenantId == tenant && e.Region == region` is a **conflation finding** — region is resolved
  *before* the query (the connection-string resolver), never *inside* it.
- **Never branch on a region code in a handler** — the same rule as "never branch on a country code in
  a handler." Region (like country) is read from config / the resolver, never hard-coded. The CQRS
  handlers, fiscal modes, the pay formula, and the per-audience hosts **do not change** for region;
  they operate on whatever DB the resolver hands them.
- **The DB connection string is chosen in exactly one place** (the resolver indirection, T-0330) — the
  analogue of the `DeviceIdProvider` single-source rule. No handler/repo hard-codes a region or reaches
  a second connection string. That single seam is what makes per-region DBs *later* a resolver change,
  not an app rewrite.
- The `CountryConfiguration.HomeRegion` **column is deferred** (a schema change → owner ef-migration,
  gated on the first real second region); only the resolver indirection is laid now, keeping this wave
  migration-free.

## Deployment / IaC — Bicep, Key Vault refs + managed identity (ADR-0015)

Deployment is **orthogonal to the domain** — no handler, config key, or connection-string slot changes
for it — but every backend agent should know the shape so it never hard-codes what infra supplies:

- **Bicep is the source of truth, in `deploy/bicep/`** (`main.bicep` + per-resource `modules/*.bicep`
  + per-env `<region>.<stage>.bicepparam`, e.g. `weu.dev.bicepparam`). One reusable `appService` module
  is instantiated **six times** — the **five** API hosts (partner, admin, customer, partner-mobile,
  **customer-mobile** — the five-not-four correction; the old YAML omitted `Cleansia.Web.Mobile.Customer`)
  plus the customer SSR. Adding a host/country/region is a new module instantiation + a param value, not
  a bespoke block.
- **Config flows as App Service settings that are Key Vault references** (`@Microsoft.KeyVault(SecretUri=...)`),
  resolved at runtime by each host's **system-assigned managed identity** (Key Vault Secrets User; CI =
  Secrets Officer). The App Service `__` → `:` mapping means the app reads its **existing** config keys
  (`ConnectionStrings:ConnectionString`, `Stripe:SecretKey`, `SendGrid:ApiKey`, `Sentry:Dsn`, the two
  storage slots) with **no code change** — do not add new config plumbing for this.
- **Functions is a container from ACR** (mandatory — QuestPDF needs native `libfontconfig1`/`libfreetype6`;
  a code/zip deploy fails PDF generation at runtime). **Storage is mandatory** (blob + queue + the
  Functions runtime store).
- **CI keeps OIDC + the migrate-before-deploy EF bundle.** The pipeline only *applies* an
  already-committed migration; it never runs `migrations add` (schema authoring stays owner-gated —
  `manual_step: ef-migration`). GitHub Environments are `dev-weu` (auto on merge) / `prod-weu` (protected:
  required reviewers + manual approval).
- **No real secret is ever committed** — see [`conventions.md`](./conventions.md). Bicep/param/YAML carry
  Key Vault secret **names** only; values are owner/CI-populated into Key Vault.

The living, evolving home for the topology diagram + dev/prod SKU table + resource→secret map is
`agents/architecture/decisions/azure-deployment.md`; the tenancy↔region composition note is
`agents/architecture/decisions/multi-tenancy-and-region.md`.
