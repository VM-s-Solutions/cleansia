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
