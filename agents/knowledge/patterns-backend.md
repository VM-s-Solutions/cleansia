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
  **Pre-prod there is exactly ONE migration, `Initial`, and it is REGENERATED — never hand-folded.**
  Editing `<id>_Initial.cs`, `<id>_Initial.Designer.cs` and `CleansiaDbContextModelSnapshot.cs` by
  hand to slot in a new table or column is **withdrawn** (owner ruling, 2026-08-09). It was not
  producing wrong schema — a column-by-column comparison of the last hand-folded `Initial` against
  the regenerated one matched exactly on 69 tables, 1113 columns and 232 indexes, and the migration
  agreed with the snapshot. What it produced was a diff **nobody can review**: EF emits
  `CreateTable` blocks in its own order, so a moved block reads as a deletion plus an addition and a
  genuine omission is indistinguishable from a reordering. Regenerating makes the diff mean
  something. Two consequences a ticket must carry: the migration **id and filename change every
  time**, and `__EFMigrationsHistory` keys on the id, so **DEV must be dropped and re-seeded** or an
  existing database reports "up to date" and silently misses every new column.
  **Enforced by:** `Cleansia.IntegrationTests`, which applies the real migration to real Postgres —
  **T1-CI**.

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

## A DISCLOSURE BLOCK is withheld by the server when its sentence stops being true (ADR-0049)

> **Enforced by:** `src/Cleansia.Tests/Features/Orders/PreferredOfferDisclosureTests.cs` (backend CI,
> `.github/workflows/backend-ci.yml:69-71`) — **`T1-CI`**. The baseline is **zero as of T-0595**, which
> landed the enforcer and the one violation's fix in the same change: `ResolvePreferredOfferAsync`
> (`src/Cleansia.Core.AppServices/Features/Orders/GetOrderDetails.cs:150-182`) now returns `null` rather
> than shipping the block on a concluded or fully-staffed booking.
> Decision: **ADR-0049**, which is `accepted` **with amendments C1–C6**
> (`docs/decisions/adr-0049.md:3`).
> **Retires when:** that status line stops reading `accepted`.

**A disclosure block is a group of fields the server populates in order to make a STATEMENT about the
state of the world** — a sentence, not a datum. `PreferredOfferDetails`
(`src/Cleansia.Core.AppServices/Features/Orders/DTOs/PreferredOfferDetails.cs:5-14`) is the reference
instance: its `State` member is meaningless except as a sentence selector, and its own doc comment is
written entirely in terms of what the customer is *told*.

**The rule: the server does not ship a disclosure block whose sentence has stopped being true.** The
client renders the block off the block's **own arrival** and composes nothing — it must never have to
conjoin a second server field (a status, a count) to work out whether the server's own sentence still
holds. Three clients composing that themselves is three chances to get it wrong, and the platform
already paid for it: until T-0595 the customer web card rendered *"This booking is now open to our
whole team"* on every past order the customer ever named a cleaner for. The facade was never the
defect and is still correct —
`src/Cleansia.App/libs/cleansia-customer-features/orders/src/lib/order-detail/order-preferred-offer.facade.ts:61-63`
reads no status at all, and **must not start**; the server stopped sending the block instead.

**Scope — the load-bearing half, and it is why this could not be written as *"the server does not send
stale data"*.** That framing sweeps in the whole order-detail payload on a completed order and would
license withholding the cleaner's name, the price and the photo rail from order history. It does
**not** govern an **action** gate, a **request** gate, a **lifecycle-utility** gate (ADR-0047 §D1/§D5,
unchanged), or a plain datum. *If a field would still be worth showing with no sentence around it,
this rule does not reach it.*

**Three obligations travel with it.**

1. **Withhold the BLOCK, do not coerce the state.** The nested-optional DTO is already the "nothing to
   say" channel (`GetOrderDetails.cs:127-135` hands `null` for every non-customer caller), so
   withholding needs no wire change and no `nswag-regen`. Collapsing the state to its "none" member
   instead makes that member's documented meaning a lie
   (`src/Cleansia.Core.Domain/Orders/PreferredOffer.cs:14-18`), and a new member costs a regen on three
   generated clients *and* hands the render question straight back to them.
2. **The derivation stays a pure domain function, and the status test is written INLINE, not promoted.**
   `PreferredOffer.StateOf` keeps its four inputs (`PreferredOffer.cs:36-53`); disclosability is a
   second pure function beside it. **Do not extract a shared `OrderStatus` grouping for it** — the
   "is this order live" sets in the tree answer **different questions** that happen to share an answer:
   `OrderRepository.cs:264-271` (does a live commitment occupy this cleaner's slot),
   `GdprDeletionService.cs:112-114` (does a live order refuse this subject's erasure), and
   `AdminOverrideOrderStatus.cs:86-97` — which is not a live-order set at all, but a *target*-status
   refusal keeping `Completed` and `Cancelled` apart to preserve **two customer-facing error keys**.
   **The first two now carry the identical membership and are pinned equal by
   `ErasureBlockingOrderStatusTests`, and that is still not a reason to share one artifact:** *two
   questions with one answer today are not one question*, and a shared constant makes a future
   divergence **silent**, because the second caller inherits it. Two named sets pinned equal make
   agreement a decision re-made on every change.
   > ⚠️ **Do NOT reuse the two-form argument here — it was struck (ADR-0049 amendment C1(ii)).** This
   > entry used to say the array cannot be shared *"because EF inlines it into SQL — a C# predicate
   > cannot replace it"*. That is imported from `OrderAvailability`, which needs two forms because it is
   > a compound **expression** over four columns. A flat status set is **data**: the identical array
   > translates through `.Contains` inside an EF `Where` (`OrderRepository.cs:344`) *and* runs in memory
   > unchanged (`GdprDeletionService.cs:119`). **Nothing structural forbids sharing it** — the reason not
   > to is the one above. Reach for §*"a duplicated predicate"* (`patterns-backend.md:1074`) only when
   > the thing duplicated really is a predicate.

   **Extract on a condition, never on a count (C1(iv)):** when a site needs the membership and cannot
   state its own reason for it inline, or when a divergence is proposed and no pin would catch it.
   `IsDisclosable`'s limb (a) is **already the third** expression of this membership and is pinned to
   neither of the others — deliberately, because its question is about a *sentence*, not about liveness.
   *Residual, named not closed:* a new `OrderStatus` member reddens `ErasureBlockingOrderStatusTests`
   but **not** limb (a) nor `PreferredOfferDisclosureTests`' `[InlineData]` table, so it would arrive
   silently disclosable.
3. **Prove the withholding cannot remove an AFFORDANCE.** Where the block also carries a "you may still
   act" flag, withholding it must be shown — not assumed — to be impossible while that flag is true.
   For the preferred offer it is provable: `PreferredOfferExit.IsOpen` conjoins
   `OrderAvailability.IsOfferable` (`PreferredOfferExit.cs:40-49`, `OrderAvailability.cs:40-48`) and an
   empty-seat term, so `¬disclosable ⇒ ¬IsOpen`. **That implication is the enforcer's core assertion**,
   not a comment.

**Withhold FALSE sentences, not merely stale ones.** The tempting term for "somebody took this job" is
`AssignedEmployees.Count > 0` — the term `PreferredOfferExit.cs:46` itself uses. It is wrong for a
*sentence about the booking*: `RequiredEmployees = ceil(EstimatedTime / 120)`
(`src/Cleansia.Core.Domain/Orders/Order.cs:697-707`), so a booking over two hours carries more than one
seat and *"open to our whole team"* stays **true** while a seat is free. The term is
`Order.AvailableSpots <= 0` (`Order.cs:136`).

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

### A statutory string is DATA WITH PROVENANCE, never a label — and its fallback must not be translatable

A caption (`Datum splatnosti`, `Mezisoučet`) is a label: it belongs in the per-language label set, and
translating it is always right. A **statutory or legal notice is not** — it is a claim about a specific
jurisdiction's law, and *who checked it* is part of its meaning. Two rules follow, both learned on
T-0522's `LegalDisclaimerTemplate` (one string per country, the Czech row seeded in English):

- **Store the assurance beside the text, and make it the gate.** `CountryInvoiceConfig` carries the
  notice, its `LegalDisclaimerLanguageCode`, and a `LegalNoticeReviewStatus`
  (`NotReviewed` / `BusinessSupplied` / `CounselReviewed`). Two countries can hold the *same sentence*
  and mean different things by it — one written for that jurisdiction, one a copy of the generic
  fallback nobody looked at — so the assurance cannot be inferred from the text and is a column.
  **Below `BusinessSupplied` the text does not print at all**, which makes the flag load-bearing rather
  than documentation and turns the nine agent-authored "in accordance with <country> law" rows inert on
  the day the migration lands, instead of leaving them asserting law under a reviewed-looking heading.
- **The fallback is a `const`, not an overridable label.** English is the notice for a jurisdiction
  *nobody has reviewed*, not a translation of a reviewed one. If a label set could translate it, the
  Polish layout's Polish fallback would read exactly like a notice written for Poland — the one
  confusion the design exists to prevent. So `InvoiceLabels.UnreviewedJurisdictionNotice` is a
  `public const string` on the base type: **the type system, not a convention, stops the translation.**
  A reviewed local-language notice is a config row; it is never a labels override.

The corollary that decides the "country or reader's language?" question: **the notice follows the
jurisdiction and is printed in the language it was reviewed in, while the *heading* follows the
document.** A reader who cannot read the notice can still see what the box is; a reader handed a
machine translation of it cannot see that its authority evaporated. (T-0506 knowingly accepts that the
same document is emailed in a language the cleaner may not read — for this one block that is the
correct trade, and the fix is a *second reviewed notice*, not a translation of the first.)

**Watch for the duplicate this replaces.** The same sentence had also been added as a per-label-set
`LatePaymentInterestNotice`, so seeding the jurisdiction's real notice would have printed it twice.
When a legal string exists in both a label set and a config row, they are one thing wearing two hats —
fold them, do not de-duplicate at render.

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
- **What a reader RECEIVES is pinned on the read token, never on the upload** *(T-0464)*. Nothing on the
  write path sets `BlobHttpHeaders`: `IBlobContainerClient.UploadAsync` routes its `Metadata` into
  `SetMetadataAsync`, i.e. `x-ms-meta-*`, which the storage service never serves from. Five constants
  named `MetadataName.ContentType`/`.CacheControl`/… advertised otherwise and are **deleted** — three
  pipelines computed a correct content type and handed it to a sink that discards it, so every stored
  blob is `application/octet-stream` and browsers have been sniffing it for years. Set it instead on
  `GenerateSasUri(blobName, expiry, ServedContentType)` → `rsct`/`rscc`, which **fixes blobs already
  written** (the override is a property of the token, not the blob) and needs no backfill.
  Two rules on that seam, and the second is the one with teeth:
  - **`Cache-Control` is `private, max-age=3600`, set on the mint and taking no parameter.** These blobs
    are reachable only by signature, so a shared cache holding one outlives the token that authorised it.
    `Metadata.CacheMetadata` hardcoded `"public, max-age=31536000"` and the **avatar used it** — inert
    only because of the decoy, so the obvious "wire the constants up" fix activates
    `Cache-Control: public` on a private image. A call site cannot forget what it never passes.
  - **The served type is a closed value type (`ServedContentType`), never a string.** Every recorded
    content type is ultimately something a client said — `SaveOrderPhotos` reads it straight off the
    caller's `data:` URI with no allowlist — so promoting a stored string onto a served header is
    **stored XSS on a storage host shared by every tenant**. `image/svg+xml` is excluded alongside
    `text/html` (SVG is XML that runs `<script>` with the serving origin); unknown input resolves to
    `Opaque` rather than throwing, so a malformed record loses a capability instead of a photo. Note the
    inversion worth remembering: **the bug was preventing the vulnerability, and the natural fix
    introduces it.**
- **Mint a new blob name on every upload** *(cache correctness, not S1–S12)* — `UpdateCurrentUser`,
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
Role card: `docs/domain/roles/idempotency-guard.md`.

## A durable store of a VERBATIM wire body declares a clock, and "durable" never means "forever"

> **LAW.** ADR-0002 §"Partial supersede — 2026-08-10 (architect, T-0584)". Living doc:
> `agents/architecture/decisions/outbox.md` §"Dead-letter retention".
> **Enforced by:** `DeadLetterRetentionTests` + `DeadLetterRetentionPostgresTests`
> (`Cleansia.Tests` / `Cleansia.IntegrationTests`, named steps of `backend-ci.yml`) —
> **`(gate pending: dead-letter retention sweep + the Failed-outbox-body clock — two tickets owed, PM
> to file; T-0584 is the first)`** → **`T1-CI`** when both land. **The baseline is non-zero and
> measured:** `DeadLetter.RawBody` (closed by T-0584) and `OutboxMessage.Body` on `Failed` rows
> (`PruneOutbox.cs:72-74` prunes only `Dispatched` — still open, ADR-0002 §A8).

A table that stores a message body **as it went on the wire** is storing whatever that message
carried. On this platform that is `SendEmailMessage(EmailType, Email, UserName, Code, LanguageCode,
UserId, TenantId)` — recipient address, real name, and the **raw** reset token. So a column holding a
verbatim body carries, in writing on the entity:

1. **A retention clock with an anchor column**, or a one-line statement of which other clock disposes
   of the row (e.g. a cascade, or a sweep named by type).
2. **What the body is FOR**, in the sense of "who reads it" — and if the honest answer is *nobody*,
   say that. ADR-0002 D3 called the dead-letter row *"the recovery source"* and nothing ever read it;
   the word carried a permanence nobody had decided.

**"Durable" is a delivery guarantee, not a storage duration.** The two words sit one sentence apart in
every messaging design and mean unrelated things. If a doc-comment reads *"stored unbounded so nothing
is truncated"*, it is answering **truncation** and being read as **retention** — write both.

**Redact the body; keep the envelope.** The evidentiary half of a failed message (queue, deterministic
`MessageKey`, tenant, error, timestamp, byte count, body fingerprint) and its PII half have different
half-lives, and splitting them costs one predicate. Overwrite with `AnonymizationMarker.Value`
(`"[DELETED]"`) — the marker the order/dispute/user anonymizers already use — rather than nulling
(a nullability migration) or truncating.

> **Truncation is redaction that leaks.** The wire body is camelCase-serialized in **declaration
> order** (`OutboxPendingDispatch.cs:34-39`), so a head-preserving truncation of a `send-email` body
> keeps `emailType`, `email`, `userName` and drops the already-expired token. It optimizes for the
> wrong half.

**Do not branch retention on the queue/message type.** Two reasons, and the second is the one that
surprises people: a `switch` on a type discriminator is a **denylist maintained by memory** — the next
queue inherits whichever branch falls through — and the intuition about *which* type deserves the long
window is usually backwards. Here the queues an ADR called MANDATORY carry bodies of pure ids
(`GenerateReceiptMessage(OrderId, LanguageCode)`), while the one carrying the address, the name and
the credential had no special status at all. The opposite polarity is already on this path:
`PoisonAlert` is *"fail-closed by construction, not by denylist"* (`PoisonAlert.cs:26-30`).

**Before redacting, check what the redaction destroys.** If the row's only lookup handle is a
*substring of the body* — ADR-0002 D3 documented
`SELECT … WHERE "SourceQueue" = … AND "RawBody" LIKE '%<MessageKey>%'` (`PoisonHandlerBase.cs:58`) —
then redaction turns the row into an anonymous blob. **Promote the identity to its own indexed
column first.** The values are usually already computed and thrown away at the write site
(`PoisonHandlerBase.cs:31` builds the descriptor, `:80` passes none of it).

**Deviating form:** an entity with a verbatim-body column whose doc-comment says "unbounded" and whose
type appears in **no** retention, prune or GDPR path.

**Enforced by `SubjectDataErasureRosterTests` — `T1-CI`, and it replaces the grep test this entry used
to specify.** The grep test was *"for each such column, the type name must appear in
`Features/DataRetention/**` or in `GdprDeletionService`"*, and it was a procedure nobody ran: we found
`DeadLetter`, then `OutboxMessage`, then `LiveActivityToken` **by hand, one per ticket**, each after the
last. The roster walks `DbContext.Model.GetEntityTypes()` and requires a written verdict per
subject-bearing entity, checked against `GdprDeletionService`'s own source so a verdict and a deletion
cannot drift apart. On the run that first went green it found **four more** — a logged-out `Device`
tombstone, `EmployeeDocument` rows left behind after their blobs were deleted, `OrderPhoto` free text,
and live `RefreshToken` rows.

Three things about its shape are the transferable part:

- **It asks a fifth question the four obvious ones miss.** Subject-bearing is usually *"has a `UserId`
  FK, or a name/email/address-shaped column"* — and all four of those questions **fail on the two tables
  we actually missed**, because `DeadLetter` and `OutboxMessage` hold their subject inside a serialized
  wire body and carry no subject column at all. So the roster also asks *does this entity **declare** an
  unbounded string column*. Measured: that question adds exactly those two.
- **`DeclaringType == entityType.ClrType` is load-bearing, not defensive.** An inherited audit string is
  unbounded on nearly every table, so without the declared-here narrowing the question classifies the
  whole schema and the roster becomes a list of everything — which is the same as a list of nothing.
- **Anti-vacuity at three levels**, because a reflective walk that silently matches nothing is the
  failure mode of every guard of this kind: floors on the entity types the model exposed, on the rows
  walked, and on the verdict sites actually checked against source.

⚠️ **A verdict of "erased" is asserted against the service's source, so the roster catches a REMOVED
deletion as well as a new entity.** The addition arm is the one that matters — a new entity with a
`UserId` and no verdict — and it is the arm a hand-maintained list cannot have.

**One correction worth keeping**, because two commit messages in this area imply otherwise: the erasure
loads its subject through a **tenant-scoped** read, so it only runs when the caller's tenant already
matches the subject's. `IgnoreQueryFilters` in the outbox and live-activity walks is defence in depth,
not the thing that makes them work. `DeadLetter` is the one where it is genuinely load-bearing.

> ⚠️ **NOTHING IN ANY SUITE WATCHES THE EMITTED DDL, so a model/migration gap is invisible to CI.**
> Found 2026-08-11 while arming `IX_Users_TenantId_Email`. `NullsNotDistinctIndexModelTests` reads
> `ctx.Model` — it is an EF **model** assertion, so it goes red on a missing builder call and green the
> moment that call lands, **whether or not a migration carrying the DDL exists**. Measured in that
> order: roster row added alone → red; `.AreNullsDistinct(false)` added → green. And the integration
> fixture ignores `PendingModelChangesWarning` (`BaseIntegrationTest.cs:86-87`) for a stated and
> legitimate reason — a shadow property that maps to a Postgres system column and needs no DDL — so the
> one warning that would surface the gap is off.
>
> Net: between a model change and the owner's regenerated migration, **the code asserts a guarantee the
> database does not yet provide, and every suite is green.** The reviewer's read of the migration is the
> only gate on that half. Pre-prod this is survivable because the single `Initial` migration is
> regenerated wholesale rather than stacked — but it means *"the model test is green"* is **not**
> evidence the constraint exists, and a claim that a DB-level guarantee is live owes the migration, not
> the builder call.

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
`docs/domain/roles/post-commit-effects.md`.

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
- **A shared predicate stays PURE; the resolver for one of its inputs may sit BESIDE it, never inside
  it.** When closing a read-side/write-side gap adds a term the predicate cannot compute — *"is this
  caller an active member"* — the shape is: the predicate keeps taking the **resolved value** as an
  explicit parameter, and an `async` resolver for that value becomes a **sibling member of the same
  type**. `PreferredOfferExit` is the worked case: `IsOpen(order, callerHasActiveMembership, nowUtc)`
  is a pure expression (`PreferredOfferExit.cs:40-49`) and `CallerHasActiveMembershipAsync(session,
  membershipRepo, ct)` is a separate static beside it (`:55-65`). What would be wrong is
  `IsOpen(order, session, repo, ct)` — a predicate that does IO cannot be driven by the equivalence /
  agreement test that is the *only* reason the read side and the write side are known to agree, and
  it takes the read side's query cost with it into every in-memory evaluation. **Make the parameter
  explicit and required**, so a call site that has not answered the question cannot compile; the type's
  own doc comment says exactly this and is the right place for it.
  **Two bounds worth knowing before reaching for the shape.** (i) It is available in
  `Core.AppServices` and **not** in `Core.Domain`: `IUserSessionProvider` is an AppServices type, which
  is why `OrderAvailability` (`OrderAvailability.cs:40`) and `OrderVisibility`
  (`OrderVisibility.cs:36`, `:50`) are pure with no siblings and must stay that way. (ii) **Co-locating
  the resolver is not the same as collapsing the platform to one implementation, and it is NOT a
  finding that it did not** — `CreateOrder.Validator.CallerHasActiveMembershipAsync`
  (`CreateOrder.cs:197-206`) and `UpdateRecurringBooking.Validator.CallerHasActiveMembershipAsync`
  (`UpdateRecurringBooking.cs:113-119`) still carry their own four-line adapters, each with a doc
  comment recording a **different policy question**. What must be single-sourced — the *definition* of
  an active membership — already is: all three route through `UserMembershipRepository`'s private
  `ActiveForUserQuery` (`:20-28`). Merging the three adapters would be the premature unification
  `conventions.md` §*"Duplication"* warns about, on methods that must be free to diverge.
  **Enforced by:** `quality-gates.md` **Gate 4 (Architecture)** — **T3-HUMAN**. *(Not mechanizable: no
  line-local rule can tell a predicate's input parameter from a dependency, and the agreement tests
  mock their repositories, so they would stay green over an impure predicate. Baseline is zero — the
  two Domain predicates above are pure and `PreferredOfferExit` is the only instance of the shape.)*
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

> **LAW — ADR-0039 is `accepted`.** Its status line reads *"`accepted` — **2026-08-03, by the lead of
> the defense panel**"* after two challenge lanes (disclosure `CH-D1…D9`, query-cost `CH-Q1…Q7`) and
> eleven amendments
> (`docs/decisions/adr-0039.md:3-9`).
> **Retires when:** that status line stops reading `accepted`.
> *(This block said "PROPOSED — not yet law … `proposed` and unchallenged as of 2026-08-03" until
> 2026-08-09 — the ADR was accepted the same day the banner was written, and a sibling card
> (`roles/preferred-cleaner-hold-resolver.md:12-18`) had been quoting it as binding throughout. A
> stale "not yet law" banner over a real rule is worse than no banner: an N-candidate loop could pass
> review by pointing at it.)*
>
> **Enforced by:** ADR-0039 §*"How a reviewer verifies compliance"* **step 1** — *"`rg -n
> "HasOverlappingOrder" src/ --type cs` — no call inside a loop **on a request path**"*, which carves
> out `NewJobsDigestService`'s nested loop as expected and **not** a finding — **T3-HUMAN**. *(Not
> `T3-review`: that is not a tier token. The tokens are in `conventions.md` §"The price of a law".)*
> ⚠️ **Run the grep; do not copy the ADR's line numbers.** Checked 2026-08-09:
> `NewJobsDigestService.cs` has loops at `:130` and `:196` and **no `HasOverlappingOrderAsync` call at
> all**, so the carve-out is currently vacuous and the ADR's `:86 × :135 → :137` no longer points at
> it. The ADR is `accepted` and immutable — its citation stays as the record of what the panel ruled
> on; this note is the correction (`consistency.md` §"Catalog claims about the tree", form 3). The one mechanical candidate — a repository call inside a `foreach` over a
> candidate list — would be a `check-consistency.mjs` heuristic, i.e. **`T2-ADVISORY`** on landing,
> because that tool is in no `.github/` workflow; it promotes to `T1-CI` only with the backend checker
> step (`enforcement.md` Rollout 3).

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

## A QUOTED price and a CHARGED price come from one FUNCTION, not one rule (T-0526)

The disclosure-surface sibling of ADR-0039's *"the read path and the write path must be the SAME
CALL"*. A surface that answers **before** the customer commits — a cancellation-fee preview, a quote,
an estimate — is only worth shipping if it cannot disagree with the commit: a customer told one number
and charged another is worse than no disclosure at all. Three things make that structural rather than a
promise:

- **Extract the whole computation, not the formula.** `CancellationAssessor.Assess(order, policy, nowUtc)`
  owns the entity↔policy binding — which order fields feed the schedule, what *"a cleaner is on the job"*
  means, how the money rounds — while `BookingPolicy` keeps the schedule and stays entity-ignorant.
  Sharing only the schedule leaves the **predicate** duplicated, which is where the disagreement actually
  lives: `hasBeenAccepted` (an assignment row, not a status) was the T-0525 defect, and a preview
  re-deriving it re-ships that defect on a second surface.
- **The client-facing LABEL must come from the same evaluation as the number.** A client renders a tier
  instead of rebuilding the ladder, so the classifier returns the tier and the rate function becomes a
  one-line wrapper over it (`CalculateCancellationFeeRate` → `ClassifyCancellation` →
  `CancellationFeeRateFor`). Deriving the discriminator *beside* the rate is a second copy of the ladder
  wearing a different type. Keep the old function and its boundary tests — green, unchanged, they are the
  pin that the ladder did not change meaning while it moved (the "reduce the singular method to a
  wrapper" shape, ADR-0039).
- **Split a total into a RESIDUAL, never two roundings.** `FeeAmount = TotalPrice − RefundAmount`.
  Rounding both legs of the same half-cent independently (`100.01 × 0.50` → `50.01` **twice**) makes the
  two numbers on the customer's screen sum to a cent more than the total they paid.

**The test that earns it is an agreement test, and it must be mutation-proved.** Drive **both**
production handlers over **one** fixture in the customer's own order — quote, then commit — and assert
equality to the cent **and** equality to a hand-derived number; an equality-only assertion passes on a
build where both sides return zero and agree with themselves. Then make the preview compute its own rate
and watch it go red (`CancellationFeePreviewAgreementTests`: 9 of 10 cases fail under an independent
ladder). Cover the tier boundaries, the per-member window, and the no-actor case, because those are the
three inputs no client can evaluate and therefore the three it will get wrong.

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

## A rule that REJECTS cheaply runs before any rule that MATERIALIZES the payload

**Enforced by:** `Cleansia.Tests/Common/Validators/ImageFileValidatorTests` +
`DocumentFileValidatorTests`, over the surface `UploadIntakeRosterTests` enumerates — `T1-CI`.
**Two** `AbstractValidator<BlobFileDto>` siblings now, `ImageFileValidator` and `DocumentFileValidator`:
the third, `FileValidator`, is deleted (T-0556 follow-up), because what it checked was the *declared* content type
and its only caller now sniffs. Every base64 intake on every host runs the shared `BlobFileSize`
predicate; the last private copy of the limit (`SaveOrderPhotos`) is gone.

Ordering inside a `Cascade(CascadeMode.Stop)` chain is a **cost** decision as well as a message
decision. `ImageFileValidator`'s magic-byte rule used to allocate the decoded payload twice
(`new byte[len * 3 / 4]`, then a copy); placing the size bound after it returns the right answer having
already paid the entire cost the bound exists to avoid. Measured on that validator: rejecting one
~10 MB avatar allocated **20,979,352 bytes**. Both image and document chains now read the head only
(§"the bytes are the evidence"), so the rule that materializes the payload is the **decodability** one
at the foot of each chain — the cost argument moved a rule down, it did not go away.

So: **size first, then anything that decodes, parses, hashes, or round-trips the bytes.** The
corollaries are what make it stick:

- **Prove the order with a test, not by reading the chain.** Two shapes, and you want both — a payload
  that fails **both** rules must report only the *first* one (swap the rules and it goes red), and an
  allocation assertion over `GC.GetAllocatedBytesForCurrentThread()` around the `Validate` call (it is
  synchronous and the counter is thread-local, so this is stable under xUnit's parallelism; warm up
  once first). A test using a payload that fails only the size rule passes under either order and pins
  nothing.
- **Derive size from the ENCODED length; never decode to measure it.** `(base64Data.Length * 3) / 4`
  on `ExtractBase64Data()`'s output rounds up by ≤2 bytes — it never under-reports, which is the safe
  direction for a limit.
- **Measure the EXTRACTED data, because the clients disagree about the wire form.** Web sends a full
  `data:` URI; both mobile clients send bare base64. `Base64Content.Length` is therefore a different
  quantity per client. A fixture that pins this must sit within ~22 bytes of the limit, or the prefix
  is too small to change the verdict and the test proves nothing.
- **One limit, one place.** Two validators over the same DTO that each own a private `10 * 1024 * 1024`
  will drift. `Common/Validators/BlobFileSize.cs` holds the constant and the predicate for both.
- **A client-side cap is a UX affordance, never a control** — and if the UI *prints* the number, the
  server must enforce **that** number. A tighter server cap recreates the same defect with the sign
  flipped; a looser one makes the printed promise a lie.
- **A per-item cap over an unbounded COLLECTION is not a bound** (T-0556). The body limit divided by a
  *small* item is thousands of blob uploads and rows, so an intake taking a list caps the list too —
  and gates the `RuleForEach` on that cap (`.When(x => x.Items.Count <= Max)`), or the per-item rules
  decode every item of a list that is already refused, which is the cost the cap exists to refuse.
- **Enumerate the intakes; do not remember them** (T-0556 follow-up). Two consecutive tickets each hardened the
  path they were pointed at and each left a sibling writing the same container under the old rules —
  the miss was never the rule, it was that nothing stated how many intakes exist.
  `UploadIntakeRosterTests` walks every host action whose request graph carries an uploaded file and
  asserts the route list, annotated with the rule each one uses, so a new upload endpoint reddens
  CI. **A roster is the only artifact here that catches the NEXT instance rather than the last one.**
- **A roster is only as wide as its predicate, and a narrow one reads as coverage.** The walk was keyed
  on `BlobFileDto` and reported **10** routes out of **14**: `UploadOrderPhoto` takes a raw `byte[]` and
  `UploadDisputeEvidence` an `IFormFile` (two hosts each), so both were invisible to it while both still
  derived their stored content type from a client string — the very defect the roster exists to make
  countable. The predicate now asks *does a file reach storage from here* (reaches `BlobFileDto`, carries
  a `byte[]` member, or takes an `IFormFile`); the wire shape a client happens to use is not part of that
  question. **Assert the COUNT first**, before any per-row comparison, or a walk that finds nothing agrees
  with the roster for the wrong reason.

## The declared content type is a HINT; the bytes are the evidence (T-0556 + its follow-up)

**Enforced by:** `SaveMyDocumentsHandlerTests`, `UpdateEmployeeStoredContentTypeTests`,
`EmployeeDocumentDownloadContentTypeTests`, `UploadOrderPhotoContentTypeTests`,
`UploadDisputeEvidenceContentTypeTests`, `GetOrderPhotosServedTypeTests` + the two file validators'
tests — `T1-CI`, over both employee-document intakes, both download handlers, and the two intakes the
`BlobFileDto` roster could not see (`UploadOrderPhoto`, `UploadDisputeEvidence`).

> ⚠️ **One intake of the fourteen is OUTSIDE that scope, and until now it was recorded only in code**
> *(2026-08-06, architect)*. **`SaveOrderPhotos`** — the batch route on `Web.Partner` +
> `Web.Mobile.Partner`, and the one both mobile clients call — reads no byte of its payload.
> `SaveOrderPhotos.DetermineContentType` (`:174-187`, the literal at `:186`) takes the caller's `data:` URI prefix, else the
> caller's file-name extension, else **the string literal `"image/jpeg"`**; the blob name's extension is
> `Path.GetExtension(file.FileName)` (`:133`), the caller's string, where its sibling
> `UploadOrderPhoto.cs:104` mints it from the sniff. The exception was reasoned in
> `UploadIntakeRosterTests.cs:34-38` and `ServedContentType.cs:7-14` — **both code, neither read by
> anyone consulting this page**, which is why it is named here. Its justification, that
> `ServedContentType` clamps the answer on the read path, holds for the case it was written for
> (`text/html` and `image/svg+xml` are unreachable — pinned by `SaveOrderPhotosContentTypeTests.cs:32-47`)
> and is **one set too wide** for everything else: the clamp bounds to the six-value *serve* set
> (`ServedContentType.cs:34-42`), not to this intake's three-value *accept* set
> (`SniffedContentType.cs:91`), so `image/gif` and `application/pdf` over arbitrary bytes are storable
> and servable here and on no other order-photo path. **And a clamp cannot answer the thing that
> actually breaks:** any per-format control built on this row — a metadata scrub, a thumbnailer, a
> PDF embed — would dispatch on a client string, so declaring `data:image/png` over JPEG bytes runs the
> PNG parser on a JPEG: a **no-op the uploader selects, under a green test**, which no read-path clamp
> reaches. *(The content-policy panel closed that route for the scrub specifically — it dispatches from
> the bytes it is holding, never from a persisted `ContentType`; see `user-uploaded-artifacts.md` §8.2.)*
> **A second divergence in the same area, recorded here because it is the same fact from the read side:**
> `GetOrderPhotos.cs:96` resolves the **platform-wide** `ServedContentType` table rather than this
> intake's accepted set, which is what the last bullet of this section asks for.
> **Cross-stack note (descriptive — not a rule):** ADR-0043's metadata scrub now runs on this
> handler between the decode and the upload (`SaveOrderPhotos.cs:137`, `UploadOrderPhoto.cs:107`,
> `UploadDisputeEvidence.cs:108`), so a recorded type describes the **submitted** bytes while the
> blob holds the **scrubbed** ones. The two agree only because `ImageMetadata` dispatches to a walker
> whose own signature matched and returns the input unchanged otherwise; **that agreement is
> unpinned** — T-0561 carries the AC.
> **Scope, descriptively:** the `Enforced by:` clause above does not cover this intake, and the general
> form of the sentence below is written scoped to exclude it. **Drafted, panel owed:**
> `agents/archive/2026-08/adr-deliberation/drafts/NNNN-stored-content-type-is-byte-derived-on-every-intake.md` — rev 2, one
> independent challenge round run, lead owed; its D2 carries the general sentence, its tier and its
> enforcer, and its D4 the read-side divergence. No new intake has been written in this shape since;
> whether the shape is available to a new one is that ruling's to make, not this callout's.

`BlobFileDto.ContentType` is a string the client chose, and the file extension is a weaker one (it
survives a rename). Neither is evidence about the payload, so **neither may decide what a stored
object is, nor what it is served as** — the recorded type comes back verbatim as the `Content-Type` of
every `DownloadMyDocument`/`DownloadEmployeeDocument` response. An **allowlist of declared types is a
client-affordance filter, not a control**: it bounds what a caller may claim, and arbitrary bytes under
a permitted claim pass it unchanged. Citing one as content validation is the mistake this rule exists
to stop — it is what let the fourth employee-document intake keep storing a caller's string for a whole
ticket after the first three were fixed.

- **Sniff, then let the sniff decide the stored type.** One function answers both *may we accept this?*
  and *what is it?* (`Common/Validators/SniffedContentType.FromContent(payload, UploadIntake.X)`,
  `null` = neither), because they are the same fact and splitting them is how a path accepts on one
  basis and stores on another.
- **One signature table, one accepted set per intake.** The intakes differ in *what they may accept*,
  never in *what a given byte sequence is*, so the second thing is a table and the first is a set beside
  it. A parallel table for images had drifted from this one in both directions — it matched a bare
  `RIFF` container as `image/webp` (a WAV and an AVI open identically, so an audio file was stored as an
  image and that type is what a SAS then pins onto a header), and it accepted BMP and TIFF, which
  `ServedContentType` can only hand back as `application/octet-stream`. A signature with a fragment away
  from offset 0 — WebP's `WEBP` tag at byte 8 — is why the matcher takes *(offset, bytes)* fragments
  rather than a prefix, and why the sniffed head is 12 bytes rather than 8.
- **Sniff the HEAD, not the payload.** Base64 decodes in independent 4-character groups, so 16
  characters yield the first 12 bytes — exactly what the longest signature needs — which is what lets
  the content rule sit before the full decode rather than after it. Since the head is all the sniff
  reads, every chain closes with a decodability rule: a payload can start with a real signature and
  still be garbage further in, which reaches the handler's `Convert.FromBase64String` as a 500.
- **Say what a signature does NOT prove.** It bounds the container: `PK\x03\x04` is OOXML-or-any-zip,
  `D0CF11E0` is Office-compound-or-any. It refuses markup, scripts and arbitrary binary and it makes
  the stored type server-truth; it is not a malware scan, and nothing on this path is.
- **Keep the accepted set equal to what the clients offer**, not to what is convenient: the web
  picker's accept list and the five-locale `file.type_not_allowed` string ("Accepted: PDF, JPEG, PNG,
  DOC, DOCX") are the promise, so a format missing from the table refuses an upload the UI invited.
  **The rule binds in the other direction too, and that half went unread for longer:** a format the
  server accepts that no client offers and `ServedContentType` cannot serve is an upload that succeeds
  and an image that never renders — which is what BMP and TIFF were on the avatar path. Removing them
  refuses nothing already stored: the read path already demoted every such row to
  `application/octet-stream`, so the narrowing is write-path-only and needs no backfill.
- **Do not lean on `Content-Disposition`.** `File(bytes, type, name)` sets `attachment`, which is why a
  poisoned type is not stored XSS today — but the two-argument overload sets **no** disposition at all
  (verified by execution), so that mitigation is one call-site edit away from gone. The control is the
  byte-derived type; the disposition is luck. Both are now asserted on the *returned result*
  (`EmployeeDocumentDownloadDispositionTests`) rather than read off the call site, because losing an
  argument is the kind of change review does not see. No host sets `X-Content-Type-Options: nosniff`.
- **A write-path rule retypes nothing that is already stored** (T-0556 follow-up). Hardening an intake fixes the
  rows written after it; the rows already there keep whatever their uploader claimed, and no amount of
  validator is going to reach them. Close the residue where it is a **closed set** — the handlers that
  serve the blob — by deriving the served type from the bytes there too
  (`SniffedContentType.ForDownload`, falling back to `ServedContentType.Opaque` so an unrecognised
  legacy row is demoted rather than made undownloadable). Same argument that chose a SAS
  response-header override over re-uploading every order photo, and the same discipline: **the read
  path reads the intake's own signature table**, or the two answers drift and a document is one type on
  the way in and another on the way out.
- **A read DTO that names the type must name the SERVED one.** `GetOrderPhotos` clamped the signed URL's
  header and emitted `photo.ContentType` raw beside it, so a legacy row told the client `image/tiff`
  about a blob that arrives as `application/octet-stream` — one fact with two sources, and the client
  believes the wrong one. Resolve `ServedContentType` once per row and use it for both.
- **Where the row records no type at all, the blob NAME is the only carrier — so mint it.**
  `DisputeEvidence` has no content-type column, and its read path resolves one from an extension. That
  extension came from the caller's file name; it is minted from the sniffed type now, and the read
  resolves the stored PATH rather than the display `FileName`. The extension therefore lives in the
  signature table beside the type it belongs to, not at a call site.

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
