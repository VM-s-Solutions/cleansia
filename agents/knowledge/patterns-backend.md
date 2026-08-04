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
the behavior never computes a diff or references a domain type (T-0284). `RecordChange` is also the
mechanism when the correct resource id is NOT on the command — the employee-affecting admin actions
(`employee.approve/reject/update/availability.update`, T-0436) key their row on the loaded
`employee.UserId` (the drill-in subject), never the `Employee.Id` the command carries; when the changed
values are themselves the subject's PII (profile edit), the snapshot is ids-only, before == after
(mirrors `gdpr.user.delete`). Never set an `AdminActionAudit` to `Modified`/`Deleted` (append-only,
init-only).

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

### When a server table DECLARES that client copy exists, the guard belongs on the server

`FcmMessageFactory.ApnsDisplayMap` is not a lookup — it is an assertion that
`push.<event_key>.title|body` already ships in both iOS app catalogs. Registering a key whose strings
are missing puts the raw string `push.order.preferred_offer.title` on a cleaner's lock screen, and a
client-side test **cannot** stop that: a backend author adding a map row never runs the iOS test
targets. So the guard is `ApnsDisplayMapIosCatalogSyncTests` in `Cleansia.Tests` — it walks up to the
`.sln` (`StartupSeedScriptSyncTests`' cross-tree idiom), parses both
`cleansia_ios/Cleansia{Partner,Customer}/Resources/Localizable.xcstrings`, and asserts per key ×5
locales: present, non-empty, `state == "translated"`, the **body's highest positional slot equals the
mapped loc-arg count**, and the **title carries no specifier at all** (the factory sends `LocArgs` but
never `title-loc-args`, so a specifier there renders verbatim). Every failure names the resolved path,
so an iOS folder rename reads as a rename rather than a mystery.

Two generalizable rules, and the second is the one that keeps such a guard honest:

- **Assert against the artifact, never against a hand-typed mirror of it.** The Swift-side
  `PushLocKeyCatalogTests` assert the right property off a 13-entry array against a 15-row server map —
  two registered events were covered by neither app, under a doc-comment reading "all 13 displayable
  events". A count in prose and a literal list both go stale silently; iterate the live map.
- **Do not widen a cross-tree guard past where the server owns the name.** This deliberately stops at
  iOS. Android's templates are `notification_preferred_offer_title`, not the event key, so asserting
  them from here would make the server own a client naming transform — the map would stop being a
  declaration and become a convention for somebody else's tree. Android keeps its own template test.

### A fixture that supplies an input production never produces makes the test green and the feature dead

The sibling of the rule above, and the more common one: the guard is real and the assertion is right,
but the *arrangement* hands it a value no caller can hand it. Nothing fails, so nothing is looked at.
T-0522 hit it twice on one document:

- The payout-invoice layout test **filled the supplier's bank fields by hand**, so it proved the layout
  renders them while the mapper set only `Iban` — every real invoice printed `—` for account number,
  SWIFT and bank.
- The layout-selection test asked the factory for **`"CZ"`**. Production passes `Country.IsoCode`, which
  is **alpha-3 `"CZE"`**, so the Czech layout was never selected and every Czech invoice rendered in
  English — under a green test named `Factory_Selects_The_Czech_Layout_For_Cz`.

**The check:** for each arranged value, name the production code that produces it. If the answer is "the
test does", the test is pinning the *layout*, not the *feature* — so either build the fixture through
the real mapper/loader, or add one test that does and let the hand-built ones stay unit-scoped. When a
mapper feeds a renderer, at least one test must run the **whole** mapper→render path, and **render and
look** at the artifact: a field-model assertion and a rendered document are different claims, and only
the second catches an English legal box on a Czech invoice or `objednávka N/A` where an `.Include` was
missed.

**Corollary — a new required parameter beats a defaulted one on exactly this seam.** The fix threaded
`EmployeePayoutDetails?` into `CreatePdfData` as a **required** parameter, so the two call sites had to
be updated and a future third is a compile error. A defaulted parameter would have re-armed the original
defect: a call site that silently omits it and three blank fields on a document nobody re-reads.

## Reading a blob back to a client — what T-0446 did, and an OPEN question for the architect

**Descriptive, not prescriptive.** There are three live shapes for turning a stored blob name into a
readable reference, they disagree, and picking one as *the* way is an **architect panel call that has
not happened**. This section records what exists so the next implementer chooses knowingly.

The shared facts, true in all three: a stored blob **name** is not a readable reference, so the read
DTO carries a short-lived read SAS beside it in a field named **`BlobUrl`** (`OrderPhotoDto.BlobUrl`,
`DisputeEvidenceDto.BlobUrl`, `BlobFileDto.BlobUrl`), the lifetime is **1 hour**
(`GenerateSasUri(blobName, TimeSpan.FromHours(1))`), and the grant is `sr=b` + `sp=r` — read on ONE
blob, not widenable to a container listing. That last property is a **tenant-isolation** control in
`user-files` (a flat container with no per-tenant or per-user prefix), so pin it with a real-client
test (`ProfilePhotoSasGrantScopeTests`) rather than a mock's return value, and do not soften it.

Where the three diverge is **failure handling**:

| Site | Shape | Behaviour when SAS generation throws |
|---|---|---|
| `GetOrderPhotos.GenerateSasUrl` (`:104-126`) | private helper in the handler, **no** `try/catch` | the whole query 500s |
| `DisputeMappers.MapToDto` (`:65-70`) | in the mapper, bare `catch {}` | degrades to null, **silently** (no log) |
| `UploadDisputeEvidence.Handler` (`:110-123`) · `GetCurrentUser.Handler` (T-0446) | in the handler, `try/catch` → `LogWarning` → null | degrades to null, logged |

T-0446 adopted the third because the ticket named it and because runtime-readiness asks for graceful
degradation plus no silent swallow — the image is decoration, the read is the core action. It did
**not** retrofit the other two. **Open for the architect:** ratify one shape and decide whether to
retrofit; until then, mirroring the third is the safe default, not a rule.

Two things that are **not** open — one a security invariant, one a durability/cache-correctness one:

- **Never log the URI** *(security — S6)*. Log the blob name; a signed URL in a log is a credential in a log. The
  hosts' `RequestLoggingMiddleware` slices request/response bodies into Information-level logs, so
  every host's `SensitiveFieldRegex` carries `blobUrl` (five copies) and every host **redacts before
  it truncates** — truncate-first cannot match a value whose closing quote falls past the cut, so it
  logs the visible prefix raw. Both are pinned by `RequestLogSignedUrlRedactionTests`; a new
  signed-URL field under a different name must join that list in the same change.
  **The corollary is the trap, and it is not an upload-only concern** — that framing is exactly why
  the first three fixes missed the fourth. Redacting collapses ANY long value (a base64 payload, a
  SAS, a JWT) to a 15-character sentinel, so whatever sat behind it in the record moves INTO the
  window: `SaveMyDocuments.Description`, `SaveOrderPhotos.Notes`, `UploadOrderPhoto.Notes`,
  `GetOrderPhotos`' per-photo `Notes` on the RESPONSE, and `JwtTokenResponse.Email` behind the
  admin refresh token. Free text cannot be reached by a field-name denylist, so those routes go in
  **`IsSensitivePath`** (wholesale suppression); a *named secret* behind another one — the Stripe
  `ephemeralKey` behind `clientSecret` — belongs in the regex instead.
  **Do not rely on remembering this.** `RedactionUnmaskedFreeTextGuardTests` walks every wire DTO
  reachable from a controller action, reads the token list out of the live regex, and fails naming
  the type, member and route. Adding a redaction token automatically widens it.

  **Where this stands, in three parts — do not read the first two as covering the third:**
  1. *Free text behind a token* — **closed by enumeration** (four instances, all suppressed).
  2. *A field unmasked when the token in front of it collapses* — **closed by a test**, the guard above.
  3. *A secret whose field name was never in the token list at all* — **NOT closed. Nothing detects
     it.** `SetupIntentClientSecret` is the proof: the alternation is quote-anchored, so the
     `clientSecret` token never matched it and a live Stripe setup-intent secret logged raw.
     `ephemeralKey` was the same class and was found only by luck, because it happened to sit behind
     an already-redacted field. Closing it needs a name/shape heuristic (`*Secret*`, `*Token*`,
     `*Key*`, `*Password*`, values shaped `sk_`/`ek_`/`seti_`/`pi_`) over the same wire walk — a
     follow-up ticket, not done here. **Adding a credential-shaped field to a DTO today is caught by
     nothing; add the token by hand.**
- **Mint a new blob name on every upload** *(cache correctness, not S1–S10)* — `UpdateCurrentUser`,
  `SaveOrderPhotos`, `UploadOrderPhoto` and `UploadDisputeEvidence` all do. That keeps the name
  content-addressed, which is what lets a client cache the image on the name; reuse makes a replaced
  image unrenderable behind any name-keyed cache.
  **The supersede rules apply only where a blob is REPLACED in place** — today that is
  `UpdateCurrentUser` alone; the other three only ever add. Where you do replace: upload the new blob
  **before** deleting the superseded one (a failed upload must not destroy what the user still has),
  and do delete it, or every replace orphans a blob. **Open:** deleting inline still races the
  commit — the UoW pipeline commits *after* the handler, so a failed commit rolls the row back to a
  name whose blob is already gone. The durable shape is to not delete inline at all (a retryable
  sweep keyed off the superseded name); that is the SEC-4 follow-up, and it also closes the GDPR gap
  where erasure only deletes the *current* name.

The field is nullable + defaulted so it is additive on the wire (S9).

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

## "Post-persist" means POST-COMMIT, or the FK will say so (ADR-0038)

> **LAW.** ADR-0038 `accepted` 2026-08-03 (panel lead's `## Verdict`, amendments AM-1 … AM-11). Living
> doc: `agents/architecture/decisions/promo-redemption-ordering.md`.
>
> **Enforced by:** the ordering rule — `quality-gates.md` **Gate 4 (Architecture)** + the deviating-form
> list in `consistency.md` §"Post-commit ordering + fail-soft admissibility" — **T3-HUMAN**. *(Not
> mechanizable: "references a row this request has not committed, under an FK" needs the FK graph, not a
> regex. The **baseline is also non-zero and knowingly so** —
> `PromoCodeRepository.TryIncrementGlobalRedemptionsAsync` self-commits inside a handler as a documented,
> sanctioned exception, so the sentence below is scoped to FK-referencing writes and self-committing
> writes **without** the sanctioned-exception doc-comment.)*
> The seam's own five laws carry their own tiers — see `roles/post-commit-effects.md`.

The handler's `orderRepository.Add(order)` does **not** write a row. `UnitOfWorkPipelineBehavior:27-30`
commits **after the handler returns**. So inside a handler, "the order is persisted" is false, and the
word "post-persist" in a comment means *tracked*, not *durable*.

> **The rule: no write that references a not-yet-committed row under a foreign key, and no write that
> self-commits, may run inside the handler.** Either it rides the pipeline's `SaveChangesAsync` (a
> change-tracked write — EF orders principal before dependent inside the one batch) or it runs
> **strictly after** the commit.

This cost a total outage: `CreateOrder.cs:315` → `OrderPromoApplier` → `PromoCodeService.ApplyAsync` →
a raw self-committing `INSERT` against `FK_PromoCodeRedemptions_Orders_OrderId` — `23503`, no order
created, on **every** promo booking. ADR-0035 AM-4 predicted the same shape in the membership path.

**Where post-commit work goes — two seams, one line between them:**

| Need | Seam |
|---|---|
| An **external** side effect (queue, email, push, fiscal, HTTP) | **`IPendingDispatch`** → outbox row atomic with the commit, drained by `OutboxDrainerFunction` (ADR-0002 D1, ADR-0008). Durable, at-least-once. **Latency: up to ~40s** — see below. |
| A **local, idempotent, same-database** write that must not join the order's transaction | **`IPostCommitEffects`** (ADR-0038) → in-process, same request scope, ambient tenant + actor. At-most-once. **Adds milliseconds to the request and completes before the response leaves the pipeline.** |

**Outbox latency — the number, because the wrong one was published here and it misprices every seam
choice (ADR-0038 AM-1).** It is **not** "~10s". Three legs, not one:

| Leg | Evidence | Cost |
|---|---|---|
| Drainer tick | `OutboxDrainerFunction.cs:14` — `[TimerTrigger("*/10 * * * * *")]` | ≤10s |
| Queue **listener back-off** — the drainer only *enqueues* (`OutboxDrainerService.cs:62`) | `src/Cleansia.Functions/host.json:19-23` — `maxPollingInterval: 00:00:30`, `newBatchThreshold: 0`, `batchSize: 1`; an **idle** queue (the normal state) has backed off to the ceiling | ≤30s |
| Consumer execution | the handler itself | — |

**Worst case ≈ 40s; typical ~15–25s.** Quote that when deciding whether an email/push/fiscal/receipt
effect is "fast enough", and quote it when arguing *against* the outbox: the promo reservation is on the
in-process seam because ~40s makes a per-user cap **serially** farmable (browser + stopwatch) where the
in-process route requires **overlapping requests**. Conversely, do **not** claim the post-commit effect
seam narrows a *check-then-act* window to milliseconds — it narrows the FK/orphan window, while the
guard-to-durable-write window is one request duration either way.

*Durable-external → outbox; local-idempotent-post-commit → effect.* `IPendingDispatch` **cannot** be
overloaded for the second row: under the durable backing `OutboxPendingDispatch.Drain()` returns `[]`
by construction, so an in-process effect recorded there is silently discarded. An effect must be a
**serializable intent record, never a closure** (that is what keeps an outbox leg additive), must own
its own commit (**a tracked `Add` inside an effect is a silent no-op**), must not fail the request, and
must carry a named detection query in its doc-comment. Full contract + the five laws:
`agents/knowledge/roles/post-commit-effects.md`.

**Corollary 1 — the ledger row reads the PERSISTED entity, never the preview.** `OrderFactory`
may discard a previewed promo when membership+tier is larger (`ResolveLoy003Discount`), so
`Order.PromoCodeId` / `Order.PromoDiscountAmount` — not the preview — say what actually applied, and a
redemption/usage row gated on the preview burns a one-shot benefit the customer never received. Record
the **frozen** persisted amount; never recompute it later (§B8, ADR-0009 D2). Never put the *inputs* of
that computation on the same record as its *output* — carry `PromoDiscountAmount`, not the subtotal, or
somebody will recompute (ADR-0038 AM-2).

**Corollary 2 — a change-tracked write is invisible to every DB-read guard over it, for the rest of the
unit of work (ADR-0038 AM-4/AM-5).** This is the mirror of the seam's law 3 (*a tracked `Add` in a
post-commit effect is a silent no-op*) and it bites the moment somebody "fixes" an ordering problem by
converting a self-committing write into a tracked one:

> `Add(entity)` puts a row in the **change tracker**; `GetDbSet().Where(…)` reads the **database**. An
> EF LINQ query never returns an `Added` entity. So every idempotency check, uniqueness pre-read and
> "does one already exist?" guard over that write is **disarmed until the pipeline commits** — and the
> duplicate surfaces at `SaveChangesAsync` as a `DbUpdateException` that **rolls back the whole unit of
> work**, or, if the unique index is nulls-distinct on a NULL tenant, does not surface at all.

**When you convert a self-committing write to a tracked one, re-read every guard over it.** Either make
the guards change-tracker-aware (`Context.Set<T>().Local` first, DB second) or *prove and pin* that the
write happens at most once per unit of work — a call-graph accident holding a safety property is a
defect waiting for its second call site. **Deviating form:** a repository method that stages an entity
whose caller's uniqueness/idempotency guard is a plain `DbSet` query.

**Corollary 3 — a reconciliation predicate must be keyed on a column the anonymizer PRESERVES
(ADR-0038 AM-9).** `Order.AnonymizeCustomerData()` (`Order.cs:635-648`, called live by
`DataRetentionBackgroundService` and `GdprDeletionService`) nulls **identifiers** — `UserId`,
`PromoCodeId`, `MembershipPlanIdAtPurchase`, `PreferredEmployeeId`, `RecurringTemplateId` — and
preserves **amounts**, because amounts are financial record and identifiers are personal linkage. A
detection query gated on a source **FK** therefore goes silently blind over the retention horizon: a
false negative, not noise — the report stays clean and stops seeing anything. **Gate on the applied
amount instead of the id, never in addition to it** (an `AND id IS NOT NULL` re-introduces the
blindness). So: *an order with a discount **amount** stamped and no matching ledger row.*

## Fail-soft is admissible only over an operation that normally SUCCEEDS (ADR-0038 §D8)

> **LAW.** ADR-0038 `accepted` 2026-08-03.
> **Enforced by:** `quality-gates.md` **Gate 4 (Architecture)** + ADR-0038 reviewer check #1 (the
> `catch`-grep on the reservation path) + the deviating-form list in `consistency.md` §"Post-commit
> ordering + fail-soft admissibility" — **T3-HUMAN**. *(Condition (2) — "does this operation succeed in
> the normal case?" — is a judgment about runtime behaviour and is not mechanizable; a regex can find a
> `catch`, it cannot find a `catch` over something that always fails.)*

A `catch` that logs and continues is admissible only when **all three** hold:

1. **It is post-commit.** The committed state cannot be rolled back by rethrowing, so a throw buys
   nothing but a 500 on an operation that already succeeded (ADR-0002 D1's adjudicated posture).
2. **The wrapped operation succeeds in the normal case.** Evidence bar: an integration test against
   **real PostgreSQL** proving the happy path lands its row. (SQLite does not enforce the FK the same
   way — a mocked repository proves nothing here.)
3. **The failure is detectable without the log** — a named reconciliation predicate over persisted state.

**(2) is the one reviewers miss.** Fail-soft over a *deterministic* failure is not resilience, it is a
silent outage: it converts a loud 500 into permanent, undetected data loss. That is why the promo FK
bug must **not** be "fixed" with a try/catch, and why the integration test that proved the bug is also
the acceptance evidence for the fix.

**A compensating catch is the opposite of a swallowing catch and stays allowed:** releasing a reserved
global slot before returning the failure restores an invariant. A catch that hides one does not.

Two things learned writing the live one (`PromoCodeService.ApplyAsync`), because "compensating catch"
is the name of the rule but not the best shape for it:

- **Reach for `try`/`finally`, not `try`/`catch`.** The requirement is "release on **any**
  non-success" — the `null` return *and* a throw — which is exactly what a `finally` guarded by
  `if (result is null)` says. Nothing is caught, so the failure propagates untouched and the
  reviewer's grep for `catch` on that path stays **empty**, which is the check ADR-0038 asks them to
  run. A `catch` + rethrow is the same behaviour with a swallow one edit away.
- **Compensate with a non-cancellable token.** The thing being released already **auto-committed** and
  outlives an aborted request; passing the caller's token means a client disconnect skips the release
  and leaks the very slot the compensation exists to return.
- **Catch the compensation; never the operation (ADR-0038 AM-10).** `await` inside a `finally` while an
  exception is in flight **replaces** it if the compensation throws — and the compensation usually runs
  on the same `DbContext`/connection that just failed, so it is most likely to throw in exactly the
  transient-error case it exists for. Wrap the *release call itself* in a `try`/`catch` that logs at
  **Error** (naming the entity, the actor and the reconciliation the operator must run) and **never
  rethrows**, so the original failure reaches the caller untouched. That catch is outside the three
  conditions above, because it does not let anyone believe the operation succeeded — its test is simply
  *does the caller's outcome change?* If yes, it is a swallow. **Error, not Warning:** the invariant is
  still broken until someone reconciles it.

## Per-user metered entitlements — the reserved-slot ledger (ADR-0035)

> **LAW.** ADR-0035 `accepted` 2026-08-02 (16 binding amendments), amended by owner ruling 2026-08-03
> (AM-17/18/19). Reference implementation: `MembershipBenefitUsage` +
> `MembershipBenefitUsageRepository.TryReserveSlotAsync` + `IExpressWaiverResolver` /
> `IExpressWaiverConsumer` / `IBenefitPeriodKeyFactory`.
> **Enforcer / tier (ADR-0032):** **T3-review** — the ordinal derivation and the cardinality bound are
> read from a diff, and both are *mutation-proved* by their own real-PostgreSQL tests
> (`MembershipBenefitReservationTests`) rather than by a linter.

A cap on how many times **one user** may receive **one benefit** in **one period** is a row, never a
counter and never a derived count.

- **Shape:** an `Auditable` + `ITenantEntity` ledger row carrying `UserId`, a **`Kind` discriminator**
  (int-stored, never reordered), a **stored `PeriodKey`** string, and a 0-based `SlotOrdinal`.
- **Concurrency — three layers, all three named:** a non-authoritative app-level read (the resolver's
  count — it decides the *quoted* price, never the claim), **one atomic claim statement**, and a
  **filtered partial UNIQUE index** as the backstop. Sole arbiter of a concurrent claim ⇒
  `NULLS NOT DISTINCT` (see `consistency.md`).
- **The ordinal is the SMALLEST FREE one, not a count and not `MAX+1`**, whenever slots can be released:
  `generate_series(0, @max-1)` + `NOT EXISTS` + `ORDER BY g LIMIT 1` + `ON CONFLICT DO NOTHING` +
  `RETURNING <col> AS "Value"`. A count derives an *occupied* ordinal after a non-maximal release —
  the claim then loses to its own index forever while the read path still says "1 left"; `MAX+1` never
  re-uses a hole at all. Send a nullable `TenantId` as an **explicit `NpgsqlDbType.Text`** parameter
  (`42P08` fires only in single-tenant mode and survives a tenanted test run).
- **⚠️ Add an INDEPENDENT cardinality bound in the SAME statement — the smallest-free-ordinal
  derivation does not imply the cap.** "A full quota yields no candidate ordinal" holds only while the
  quota is **invariant across the period**. The moment the quota can be edited or swapped mid-period
  (an admin plan edit, a mid-month downgrade), `generate_series` finds a hole below the *new* smaller
  max and over-grants, while the read path truthfully reports 0 remaining: read and claim disagree
  silently, in the over-granting direction. `AND (SELECT COUNT(*) … WHERE <quota key> AND IsActive) <
  @max`. It is redundant under a constant quota and costs one indexed count; that is the price of not
  having to prove the quota never moves.
- **0 rows ⇒ `null` ⇒ no slot — a RESULT, never an exception** at the caller's commit.
- **Ordering:** ADR-0023's repeatable-effect test applies — an entitlement grant is money-shaped, so
  **Mode A**: reserve **before** the benefit changes the price. Note the `PromoCodeRedemption` ancestor
  is **reserve-after-persist and fail-soft**; inverting it is a decision that must be **argued** (a hard
  cap is needed when the entitlement requires nothing but a subscription, and is farmable by every
  subscriber with concurrent requests alone), never inherited.
- **Mode A has a price and you must pay it explicitly:** the validator's approved total and the
  reservation's outcome can disagree inside one request. **Never persist a price higher than the one the
  caller consented to** — fail with a **dedicated** error and re-quote. Reusing the generic
  "price changed" code is a silent downgrade: every client renders it as a re-quote, so the one sentence
  that explains the state never runs.
- **Out-of-band means out-of-band.** A raw-SQL reservation auto-commits; anything that stamps a
  *later-created* row's id onto it must ride the UoW commit, or it fires against a principal row that
  does not exist yet (the handler returns **before** `CommitAsync`). A self-committing reservation is a
  **declared** exception and carries the doc-comment saying so.
- **Period:** a **stored key**, computed once at reservation, by **exactly one factory**, from a source
  **every call site can reach** (a preview path usually has no order and no address) — the
  platform-default `CountryConfiguration.TimeZoneId`, UTC fallback, never throwing. **Never** the client
  `X-Time-Zone` header, which is unauthenticated and lets a member pick their own month boundary. Never
  recompute the key for an existing row; the key crosses no DTO boundary.
- **Preview vs consume are different calls.** The "does this user get it" question is answered by a
  **pure resolver** (the `CancellationPolicyResolver` shape) that every pricing path may call freely;
  consuming happens **once**, at persist. A resolver that consumes burns the entitlement on every quote.
  **The consumed answer is passed INTO the builder, not resolved by it** — that is what makes "exactly
  one consuming call site" true by construction instead of by grep.
- **One clock reading, threaded.** Where the entitlement's eligibility is time-windowed, two
  `DateTime.UtcNow` reads inside one request can put the resolver and the policy on opposite sides of
  the boundary — a live slot attached to an item that was never charged, which no release rule covers
  and no orphan sweep sees.
- **The counting key is the quota key and nothing else.** Whatever "which subscription/enrolment was
  this against" column the row carries is a **payload** for humans: it may appear in the `INSERT` column
  list and nowhere else. In a `WHERE`/`GROUP BY`/`HAVING`/join it silently makes the quota reset on
  churn and not carry across a plan switch — and it reads as *more* correct than the right key, which is
  what makes it the likely violation.
- **Reversal:** release (soft-delete → the filtered index frees the ordinal) only when the user did not
  consume the thing the entitlement bought — and key the release on a signal that means what you say it
  means (**census every writer** of the status you are reading). Whatever the rule is, **name the
  exploit you accept**, state the bound **that actually holds** (usually the quota itself, not a fee),
  and **disclose the forfeiture to the user before they trigger it** — especially in the cases where it
  is otherwise invisible.
- **An out-of-band claim has an orphan class, and the sweep that reclaims it is NOT the sweep you
  already have.** A job that reads the *order* table structurally cannot see a claim whose order never
  committed. Repository method + partial index + a second command on an existing schedule.
- **A ONCE-EVER entitlement (quota 1, no period) is the same rule with the enrolment row removed from
  the key — and that removal is the whole feature.** Re-subscribing creates a NEW `UserMembership`, so
  anything read off *the current row* resets on exactly the event the rule exists to catch. Read the
  user's row **history** instead: any row, **any status, including soft-deleted** (a deactivated row
  still consumed the thing — the deliberate S10 exception), carrying the marker. That needs **no new
  column**: the fact is already recorded on the row that granted it, and a mirror column on `User`
  would be a second source of truth with its own writer to forget. Reference:
  `IUserMembershipRepository.HasEverStartedTrialAsync` + `IMembershipTrialResolver` (one trial per
  customer, owner ruling 2026-08-03).
  Two traps that come with it: **(a) the marker must be the PROVIDER's answer, not the number we asked
  for** — mirror Stripe's `trial_end` off the created subscription, never `plan.TrialPeriodDays`, or a
  provider that silently declined leaves a marker for a benefit nobody received; **(b) a marker that a
  webhook can NULL is not a marker** — coalesce (`X = incoming ?? X`) **in the entity**, not in the one
  handler that happens to call it today, so a dunning event carrying a different object type cannot
  erase it (ADR-0035 AM-18).
  And **enforcing silently converts a loop defect into a false-price defect**: once the server refuses
  the second trial, every surface still advertising one is lying. Ship the per-user eligibility flag on
  the authenticated read in the same change (`GetMyMembership.TrialEligible`), defaulted to today's
  behaviour so an un-regenerated client is unaffected.

## Bounded exclusivity on a pull board — the stored-deadline hold (ADR-0036)

When a rule must give one actor **temporary exclusive access** to a work item on a first-come board
(Cleansia: a customer's preferred cleaner gets first refusal on their booking):

- **Store an absolute deadline, never a duration.** `<X>UntilUtc`, nullable, set **once** at creation,
  **never recomputed**. `null` means "no exclusivity, ever", so existing rows and rows outside the rule
  are unaffected **by construction**, with no backfill. A duration read at query time retroactively
  re-times every live row when the constant is tuned.
- **Expiry must have no actor.** `now >= deadline` in a `WHERE` clause. A job/sweep/status-transition
  expiry has a failure mode — *the item is stuck exclusive* — that a clock comparison does not.
- **Key the predicate on the DEADLINE, and make an inconsistent pair fail OPEN at BOTH ends.** A
  predicate keyed on the beneficiary id retroactively switches behaviour on for every historical row.
  And a deadline **without** a beneficiary must be (a) unwritable — the aggregate owns the pair
  (`GrantX(beneficiary, untilUtc)` / `ClearX()`, no independent setter) — **and** (b) harmless if a row
  carries it anyway: the predicate includes `beneficiaryId == null ⇒ open`. **One end is not enough.**
  *(ADR-0036 CH-V1: the draft had neither, and the anonymizer nulled one column of the pair.)*
- **Exclusivity is consumed the moment the item has ANY holder** — not when the beneficiary takes it,
  and never merely by the clock. Check whether your items have **more capacity than one**: a Cleansia
  order's cap is `RequiredEmployees + BookingPolicy.SpareSeatsPerOrder` (the spare is **0** by owner
  ruling, but any job over one work unit still carries several seats), so a hold keyed only on the clock
  locks a seat *after* the perk has already been delivered, to a beneficiary who could not take it either.
- **Bound the exclusivity as a FRACTION of the item's own fill window, with a ceiling**, and state the
  resulting invariant **per unit of capacity, not per item**: *≥90% of every **seat's** fill window is
  always open to everyone.* A fixed duration is arbitrarily aggressive on urgent items and timid on
  distant ones.
- **Reuse the constant that already defines "urgent" as the floor — or DERIVE from it.** A multiple of
  the one constant (`2 * BookingPolicy.StandardLeadTimeHours`) is **not** a second constant: the
  relationship stays derivational and cannot drift. A second literal is the drift.
- **Grant the NOTIFICATION on a wider predicate than the exclusivity.** *No signal ⇒ no exclusivity*
  (latency nobody can act on is pure loss) — but **not** its converse. Where exclusivity is unsafe
  (too little lead time), the notification alone still keeps the promise, costs one outbox row, and is
  what lets a **single static string** be true in both outcomes. Check **every** way the signal can
  fail: the category mute, the device-level kill switch (`Device.NotificationsEnabled`), and **no device
  row at all**.
- **Write the rule ONCE, then classify the surfaces by KIND** — queryable visibility · in-memory
  authorization · write gate · notification freshness — and enumerate them in the ADR. A rule applied to
  n−1 of n surfaces is a leak; a rule applied *uniformly* to surfaces that answer different questions is
  a different bug.
- **Sharing one lambda does NOT make two evaluators agree**, so a queryable form and an in-memory form
  must be pinned by an **equivalence test against the real provider** — never by a shared expression
  tree, and never by review. Corollary: **never `.Compile()` on a request path**; a `static readonly`
  delegate compiled once, or a plain method, is the shape.
  **The reason usually given for this rule is wrong for EF, and the rule survives anyway — know which is
  which.** *Raw* SQL and C# do disagree on null equality (`col = @p` with a null parameter is UNKNOWN;
  `null == null` is `true`), but **EF Core's null semantics rewrite `x.Col == @p` to `Col IS NULL` when
  the captured value is null**, so an EF-translated predicate matches C# on exactly that case. Measured,
  not assumed: mutating `OrderVisibility`'s null-beneficiary disjunct out of the **queryable form only**
  left the `caller == null` row of TC-PREF-EQUIV-0 **green** and turned both non-null callers red. **What
  the equivalence test actually catches is a term edited on ONE side** — which is the failure that
  happens, because the two forms are two pieces of text. Keep the in-memory null guard regardless: it
  costs nothing and it is the brace to the null-beneficiary term's belt.
- **Enumerating grep hits is not coverage — check CALL SITES.** A specification factory with all-optional
  parameters accepts a new one **without breaking a single caller**, which means every caller that
  forgets it leaks silently and builds green.
- **The refusal at the write gate must never NAME the exclusivity.** Reuse the most generic refusal the
  caller could already have received (`OrderNotFound`), and **specify where in the validator chain it
  goes** — appended after a more specific rule, the caller gets the more specific error, which is the
  leak. *(The stronger form — "the write's error must always agree with the caller's read" — is NOT the
  rule: this codebase already violates it.)*
- **A watermark-based notification sweep must treat the expiry as the item's arrival instant** for
  non-beneficiaries, **as a bounded window** — `> watermark AND <= sweepStart` — **written
  disjunctively**, never as `max(a, b) > k`. Two reasons, both load-bearing: an availability instant in
  the **future** marks the item "new" *before* it is available, inflating the notification's count and
  walking the watermark past the expiry; and `max(...)` compiles to a per-row `CASE` over correlated
  aggregates with a cast on a column, where `a > k OR b > k` stays a semi-join. **Pass the sweep's own
  start instant, never `UtcNow` inside the loop.**
- **And know the limit you are patching.** A single per-recipient watermark scalar assumes eligibility is
  monotone in time and derivable from a **global** timestamp on the item. Any **per-recipient,
  non-monotone** rule breaks that assumption; a fix for one such rule is a point fix, not a class fix,
  and the ADR must say so rather than claim convergence.
- **Patch it with a second freshness source keyed on the RECIPIENT-side event, and let that source
  OVER-approximate (T-0528).** When a per-recipient rule flips *back* to eligible, nothing on the item
  changes, so the watermark structurally cannot see it — the item is burned the moment the recipient is
  notified about anything else. The disjunct therefore keys on the event that flipped the rule (Cleansia:
  one of the cleaner's **own** commitments being cancelled/completed hands the slot back), carries the same
  `> watermark AND <= sweepStart` bound as every other disjunct so exactly one sweep consumes it, and
  re-offers the **widest** window that event could have freed rather than the exact set it was blocking.
  The asymmetry is the whole argument: a redundant candidate costs one more evaluation of the real
  predicate — which still decides — while a missing candidate is the item lost forever, which is the defect
  being closed. **Prefer the formulation that duplicates one enum set** (pinnable by a test asserting it is
  the complement of the owning set, even across an assembly boundary via reflection) **over one that
  duplicates an arithmetic predicate** (unpinnable without a real-provider equivalence test).
- **Never grant exclusivity to an actor you already know cannot act (ADR-0039).** A bound like *"the
  exclusivity is at most 10% of the fill window"* prices what you **cannot** know; it is **not** a
  licence to spend what you **can**. If a *statically-checkable-at-grant-time* gate would refuse this
  actor the item anyway, the exclusivity is 100% of that unit's fill window spent on a **zero**-
  probability outcome. **And the same test decides the notification**: a signal about work the recipient
  is gated out of taking is noise on the one channel the mechanism depends on being worth reading — so
  *"no signal ⇒ no exclusivity"* has a sibling, ***"cannot act ⇒ neither"***.
  **The distinction that decides which gates qualify is in the gates' own signatures, not in taste:**
  a gate parameterised by **the item's** instant (Cleansia: `HasOverlappingOrderAsync(employeeId,
  cleaningDateTime, …)`) answers the question you are asking and **must** be consulted; a gate
  parameterised by **`now`** (`GetEmployeeOrderCountThisWeekAsync`, whose window is
  `DateTime.UtcNow.Date`-derived) answers a *different* question for any future item and **must not**
  be. Read the signature before you decide a check is "too dynamic".
- **Never present a choice you cannot honour (ADR-0039).** If a UI lets a customer pick a party the
  mechanism then silently drops, the defect is the *offer*, not the drop. Mark the unofferable option:
  **shown · disabled · one neutral line that names no reason and promises no alternative**. Hiding it
  discloses the same fact (a shorter list is a diff) while manufacturing a mystery; greying it silently
  is a behaviour change with no text. And **the label must stay true if the predicate later widens** —
  *"not available for this date and time"* survives; *"already booked"* becomes a lie the first time
  another cause is folded in.
- **Marking some options unavailable silently upgrades the promise for the rest.** Greying two of five
  implies the other three *are yours*. Whatever standing sentence sets the expectation ("first chance,
  not a guarantee") **must be left unchanged and must keep rendering** — the marking is subtractive
  only.

## Ask N candidates ONE question, not one candidate N questions (ADR-0039)

> **PROPOSED — not yet law.** ADR-0039 is `proposed` and unchallenged as of 2026-08-03.
> **Enforcer / tier (ADR-0032):** **T3-review** — the shapes below are read from a diff, not detected
> by a linter. The one mechanical candidate (a repository call inside a `foreach` over a candidate
> list) is a `check-consistency.mjs` heuristic if it earns its place.

When a screen asks the same yes/no question about a **list** of entities — *"which of these people are
free at this hour?"*, *"which of these are over their limit?"* — the singular repository method that
already exists is the wrong tool, and calling it in a loop is the defect, not the shortcut:

- **The set-based method is the seam, and it takes the set.** `Task<IReadOnlySet<string>>
  GetBusyEmployeeIdsInWindowAsync(ids, startUtc, endUtc, ct)` — **return the positive subset (the
  "busy" ones), not the free ones**, so "absent from the result" is the fail-**open** default and an
  empty/failed answer degrades to today's behaviour rather than to a lie.
- **Reduce the singular method to a wrapper over the set method.** Two predicates answering one
  question in one repository is the defect class, not a shape to leave behind — and the wrapper means
  every later fix (a range bound, a tenancy variant) lands on the **write gate** for free. The existing
  status-matrix test on the singular method is the pin that proves the predicate did not change meaning
  while it moved.
- **The read path and the write path must be the SAME CALL, not "the same rule".** If a picker can say
  *available* and the command can then say *busy* for a reason of its own, the feature has already
  failed and no shared documentation prevents it.
- **An overlap/interval predicate needs a LOWER bound or it scans all of history.** `start < @end AND
  start + duration > @start` has exactly one sargable term — the **upper** one — because the second is
  a per-row computation. Floor the scan (`start >= @windowStart − MaxSpan`) so an existing
  `(status, date)` index serves it as a range scan. **Choose the constant by its failure asymmetry, not
  by tuning:** too generous costs a wider scan of a nearly-empty band; too tight makes an overlap
  invisible **on the write gate** — a double-booking. **When in doubt, widen it**, and make it
  verifiable in one line (`SELECT MAX("Duration") …`) rather than believed. The durable alternative — a
  persisted end-instant column + index — is the right long-term answer; record it with its flip
  condition instead of pretending the floor is free.
- **Derive the interval's length server-side, from ONE definition shared with whatever persists it.** A
  nominal window is wrong in both directions (too short re-opens the failure you are closing; too long
  silently denies a valid option), and a **client-supplied** length that decides a server answer is an
  S1 violation. Extract the computation, give it two callers and one test asserting they agree.
- **Extend the caller's existing feed rather than adding a general "is X available?" endpoint.** The
  general endpoint is a **schedule oracle** for any id, over any range. The extension keeps two limits
  *structural*: you may only ask about entities already in **your** result set, and only about the one
  instant you are acting on. **A range parameter is not a feature request — it is a different
  decision.**
- **A per-row answer that can be "not evaluated" is a TRI-STATE on the wire, and the third state is
  load-bearing.** `bool?` with `null` = *not evaluated* — reachable on day one from a client that has
  not been rebuilt. **`null` must render as "no marking".** A client mapping it to a non-optional
  boolean either flags everything or defeats the feature; pin it with a fixture that sends `null`.

## A predicate that spans STACKS needs a parity test; a state set needs a writer census (ADR-0037)

> **LAW — ADR-0037 `accepted` 2026-08-03** after a defense panel (19 findings, 8 blocking, all
> resolved). Cite it.
> **Enforcer / tier (ADR-0032):** (a) *structural* — the duplication is deleted, so there is nothing
> to drift; (b) **T1-CI** — a cross-stack parity check parsing the canonical C# and asserting **every**
> client literal **and button gate**, baseline zero; (c) **T2-advisory** — a `check-consistency.mjs`
> line rule for availability status literals outside the owning class.
> Layer (b) is the load-bearing one: it is the only enforcer that spans the four languages.
> ⚠️ **And it must actually run — see the trigger rule below.** The ADR's draft specified layer (b) as
> a Jest spec under `nx affected`, which is selected on **none** of the diffs it guards.

Extends the ADR-0036 section above (write the rule once · classify surfaces by kind · call sites not
grep hits · two evaluation forms + an equivalence test). What that section does not cover, and what
cost Cleansia **ten** disagreeing definitions of "which orders may a cleaner take":

- **⭐ If you write down an invariant, write down the thing that goes RED when it stops holding — in
  the same change.** This is the rule the ADR-0037 panel produced, and three of its four blocking
  mechanism findings were instances of one failure: **an asserted property with no artifact that fails
  when the property breaks.** The draft asserted (i) *"rule ordering guarantees a held order never
  returns the status key"* — FluentValidation ran a second chain and the guarantee never existed;
  (ii) *"for cash nothing is in flight"* — a live hourly sweep retracted those orders at T−1h;
  (iii) *"a parity test is the layer that would have caught this drift"* — no workflow ran it on the
  diffs it guarded. **Each sentence was true-sounding, load-bearing, and unfalsifiable in CI.** That
  is the same disease as a "mirrors X" comment, one abstraction level up: *prose asserting a property
  the machine never checks.* The test is not documentation of the invariant — **it is the invariant**;
  the prose is a pointer to it.
- **A predicate that names a scheduled job's victims must be the NEGATION of that job's own `WHERE`
  clause, term for term — never a paraphrase.** Cleansia's offerability rule said "cash can't be
  retracted"; the recurring sweep keyed on `PaymentStatus == Pending` **with no payment-type term**, so
  it retracted cash too. And the rule trusted `Confirmed` to imply paid; the card sweep keyed on
  `PaymentStatus` **with no status term**, so it killed `Confirmed`-but-unpaid orders. **Open the
  sweep, copy its predicate, negate it.** If a new scheduled retractor is added later, the *first*
  place to change is the availability rule — record that obligation on the role card, because nothing
  in the compiler connects a background job to a read filter.
- **A rule that trusts state A to imply fact B stored in column C is a "mirrors X" comment written in
  code.** "`Confirmed` means paid" was true of *one* of four writers. Before relying on such an
  implication, **census the writers of A** the same way you census the writers of an enum member — and
  if any writer can produce A without B, the rule needs the B term explicitly. Symmetry across the
  branches of a predicate is not tidiness; an asymmetric predicate is one that was only tested against
  the happy writer.
- **An enforcer's TRIGGER is part of the enforcer. A check with no trigger is a comment with a
  `.spec.ts` extension — and it is worse than a comment, because the decision record now claims
  coverage that does not exist.** Before citing any CI check as an enforcer, verify four things:
  *does the workflow fire on the paths the check reads* (Cleansia's frontend CI is scoped to
  `src/Cleansia.App/**` on push; backend CI **excludes** both mobile trees); *is the step selected*
  (`nx affected` selects nothing for a C#/Kotlin/Swift-only diff); *can it be served from cache*
  (Nx inputs cannot reference paths above the workspace root, so cross-stack sources are **not**
  declared inputs and a stale green is reachable); and *what is the acceptance test* (break the thing
  on a branch touching only that file and watch the PR go red). **A cross-stack check therefore should
  not be a Jest spec inside the Nx workspace at all** — make it a plain Node script with its own
  trigger, so it is uncacheable and unskippable *by construction* rather than by configuration.
  Precedent that works: the unconditional non-Nx `typecheck:test` step. **If you cannot make it run,
  label the layer ADVISORY in the ADR.** Under-claiming is fine; over-claiming is not.
- **Census the BUTTONS, not just the queries.** A parity check over query literals and not over the
  gates on the action button tests the wrong half: the query decides what is *listed*, the button
  decides what is *clickable*, and the whole point of a shared rule is that those cannot diverge.
  Cleansia's web detail page gated Take on `{Pending, Confirmed}` ≡ `{Confirmed}` and so **hid the
  button for exactly the state the new rule exists to admit**, while the proposed three-file spec would
  have gone green over it.
- **One array answering two questions is the same defect as one comment claiming two lists agree.**
  `AdminOverrideOrderStatus.Lifecycle` served both *"what rank is this status"* and *"what may an admin
  target"*. Deleting a member to answer the second silently broke the first — `Array.IndexOf` returns
  `-1`, the forward-only guard `targetRank <= currentRank` passes for **every** target, and the
  transition becomes **backwards**-legal for exactly the legacy rows the change was about. **Split the
  arrays**, and never let "the implementer confirms the index semantics before landing" stand in for a
  seeded test.

- **A "mirrors X" comment is an assertion the compiler never checks, and it is worse than no comment
  — a reviewer reads it and stops looking.** Cleansia shipped *three* of them, all false, two of which
  claimed to mirror sets that disagreed on their **first term**. When you find one, the fix is to
  **delete the duplication so the comment has nothing to assert** — not to correct the sentence.
  Amending it to "mostly mirrors" is a fail.
- **When the same predicate exists in C#, TypeScript, Kotlin and Swift, no compiler and no
  single-stack linter spans it — so the enforcer must parse across stacks.** This is cheap and there
  is a working precedent: `error-contract-parity.spec.ts:43-52` reads **C# source** from a Jest spec,
  locating the solution root by walking up to `Cleansia.Api.sln` (`:9-20`). Copy that shape. Note the
  trap in the precedent itself: it is scoped to **one** app (`:27-30`), so it silently covers none of
  the others — a parity test must state which surfaces it covers, and the ADR must list the ones it
  does not.
- **Push the rule to the SERVER floor and let the clients be a display refinement.** Three clients
  sending three different status lists is a symptom; the disease is that the server had no floor of
  its own (`GetPagedOrders.cs:87` forwards whatever the client asks for). Once the server-side scope
  predicate carries the rule, a wrong client list is a cosmetic bug instead of an authorization hole
  (S1 server-truth) — and clients that already agree need no change.
- **Before writing a state set, take a writer census of every member.** Grep each enum member for a
  production writer. Cleansia carried `OrderStatus.Pending` in three availability sets for years with
  **no writer anywhere**, so `{Pending, Confirmed}` silently meant `{Confirmed}` — and the surface
  that used it reported a structurally impossible **zero**. A member with no writer makes some set
  that contains it a lie.
- **A member with no writer is usually a fact already stored on another axis — do not "add the
  missing writer".** `OrderStatus.Pending` names "card payment initiated", which the system already
  tracks as `PaymentType.Card + PaymentStatus.Pending` — the pair the live cleanup sweep actually
  keys on. Adding the writer would create **two sources of truth for one fact** and force every
  reader to know which wins. Deprecate the duplicate, remove it from the sets **and from any generic
  admin/override writer that could resurrect it**, and keep readers tolerating legacy rows.
  Correct the documentation: if `CLAUDE.md` and the code disagree about a lifecycle, **the code is
  the evidence and the doc is the bug** — but only after a writer census proves which is which.
- **Ask which AXES the predicate really spans before naming the set — and expect to be wrong about how
  many.** "Which orders are offerable" looks like a question about `OrderStatus` and is not: the ADR
  found it was `OrderStatus` **× the payment model**, and the *panel* found that was still short — it
  is `OrderStatus` × payment **model** × payment **progress** × *is this a recurring occurrence*,
  because those are the columns the two live retraction sweeps read. **A set of literals cannot express
  a multi-axis rule**, which is why ten surfaces disagreed. *Corollary: when a role's "does NOT know"
  list blocks a correct answer, the responsibility was drawn wrong — that is the RDD rule working, not
  an exception to it. Strike the line, record why, and keep the lines that still hold.*
- **An unexplained literal in a domain formula is a decision nobody made — name it even when the value
  is zero.** `MaxEmployees = RequiredEmployees + 1` cost a second full wage per order against an
  unchanged price and had no recorded rationale anywhere. The fix is not `= RequiredEmployees`; it is
  `+ BookingPolicy.SpareSeatsPerOrder` with the number at **0**, so the constant records that the option
  was considered and priced, and changing it later is one edit instead of an archaeology exercise.
  **When the policy constant lives in a layer the entity cannot reference** (`BookingPolicy` is
  `Core.AppServices`; `Order` is `Core.Domain`), **pass it in — do not move the policy down.** That is
  the shape `Order.Cancel(feeRate, refundAmount, …)` already uses, and it keeps the entity
  policy-ignorant. Make the parameter **required**, never defaulted: a default is a second copy of the
  number that drifts silently.
- **Collapsing a formula collapses its predicates too — go looking for the pair.** Once the cap equalled
  the requirement, `HasAvailableSpots` (`assigned < MaxEmployees`) and `IsFullyAssigned`
  (`assigned >= RequiredEmployees`) became one rule with two independent expressions. **Delete the unread
  one rather than making it delegate**: delegation would silently redefine it for the rows where the two
  still differ (here, an explicitly raised cap), which is a behaviour change disguised as a cleanup.
- **Dead code that asserts a safety net is the same defect at class scope.** `StaleOrderCleanupService`
  had an unsatisfiable `WHERE` (it required the writerless status above) **and no caller** — yet it was
  cited in good faith as the reason a risk was covered. When you retire a mechanism, delete the class;
  a resident class is read as a live guarantee. It is deleted; the sweep that actually runs is
  `CleanupStalePendingOrders`.

## Moving a gate onto a new column: the term you delete is the outage (ADR-0034 D7)

When a denormalized flag replaces an old column inside a gate — a profile-completeness check, an
eligibility predicate, anything that decides whether a user may act — **the flag is `false` for every
existing row on the morning of the release unless a backfill ran.** Swapping
`!string.IsNullOrEmpty(LegacyColumn)` for `NewFlag` therefore does not preserve behaviour "by
construction"; it preserves behaviour only if something wrote the flag for the rows that already exist.

- **Ship the writer first, then the gate, and keep the old term as a disjunct** —
  `NewFlag || <the old condition>` — with a comment naming the outage it prevents and the condition
  under which the term retires. The disjunct is not defensive clutter; it is the whole migration.
- **Both terms must be scalars on the row the gate already loads.** A term that reads a navigation is
  load-order-dependent in a repository with no lazy loading (this one), so a hand-written `.Include`
  list silently becomes the gate.
- **Pin it with a host/route test that seeds the pre-release row shape** (flag false, legacy value
  present, no child row) **and drives a real gated endpoint.** A unit test on a hand-built aggregate
  passes over exactly this bug, because the bug is what the loader materialized.
  Reference: `Employee.HasPayoutDestination()` + `Cleansia.HostTests/Tests/PayoutGateDeployDayTests`.

**Sibling rule for the same wave — erasure and other must-never-miss operations do not move onto the
navigation either.** Keep them id-keyed on the repository (`RemoveForEmployeeAsync(employeeId)`) so they
are correct regardless of what the caller loaded, and **load-and-remove rather than `ExecuteDelete`** when
they run inside a caller's unit of work — `ExecuteDelete` commits on its own connection and would delete
even when the surrounding erasure rolls back. Prove it with an integration test that goes through the
real service's query shape, not a hand-populated navigation.

## Validate and canonicalize in ONE call, and return the stored form

A validator that answers only `bool` forces the handler to re-derive the value it is about to store, and
two derivations are two definitions. Return the canonical record from the validation call
(`PayoutValidationResult(IsValid, ErrorKey, Canonical)`, mirroring `TaxIdValidationResult` and widening
it) — the FluentValidation rule uses the key, the handler uses the canonical form, and there is exactly
one place the derived value comes from.

Two corollaries this shape makes cheap:
- **Compare the DERIVED value, never the typed parts,** for equality, uniqueness and fraud checks. Parts
  that canonicalize to the same identifier are the same thing; comparing parts silently under-reports.
- **A dynamic error key needs `RuleFor(c => c).CustomAsync(...)` + `context.AddFailure(new
  ValidationFailure(prop, key) { ErrorCode = prop })`**, not `.WithMessage(<constant>)`. `CreateProblemDetails`
  drops failures whose `ErrorCode` is null, so an un-coded failure reaches the client as a bare `detail`
  and every client falls back to a generic message. `UpdateIdentificationInfo` collapses its service's key
  to one constant — fine for one outcome, wrong when the caller must tell "bad checksum" from "that is a
  card number".

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
