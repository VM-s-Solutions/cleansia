# Backend Consistency Audit

> Full audit of `Cleansia.Core.AppServices/Features/` folder against the coding standards in CLAUDE.md.

## Audit Date: 2026-04-10

---

## The Standard (Reference)

Use `GetPagedServices.cs` as the reference implementation. It follows all conventions:

```csharp
public class GetPagedServices
{
    public class Request : DataRangeRequest, IRequest<PagedData<ServiceListItem>>
    {
        public ServiceFilter? Filter { get; init; }
    }

    internal class Handler(IServiceRepository repo)
        : IRequestHandler<Request, PagedData<ServiceListItem>>
    {
        public async Task<PagedData<ServiceListItem>> Handle(Request request, CancellationToken ct)
        {
            var specification = ServiceSpecification.Create(searchTerm: request.Filter?.SearchTerm);
            var filter = specification.SatisfiedBy();
            var totalItems = await repo.GetCountAsync(filter, ct);
            var items = await repo
                .GetPagedSort<ServiceSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Select(x => x.MapToDto())
                .ToListAsync(ct);
            return items.MapToDto(totalItems, request);
        }
    }
}
```

### Key Rules

1. **Request extends `DataRangeRequest`** (Offset/Limit/Sort) — NEVER custom Page/PageSize
2. **Returns `PagedData<T>` directly**, NOT `BusinessResult<PagedData<T>>`
3. **Uses `Filter` record** with named properties for filter params
4. **Uses `Specification` class** for WHERE clauses (composable)
5. **Uses `GetPagedSort<TSort>`** from repository (NOT custom switch/OrderBy)
6. **Uses `Sort` class** inheriting `BaseSort<T>`
7. **Handler is `internal class`**, not `public class`
8. **Controller returns `Ok(result)`**, NOT `HandleResult<T>(result)` for paged data

### Other Standards

- Handlers must NOT contain validation (use `Validator` class with FluentValidation)
- Handlers must NOT contain try/catch (middleware handles exceptions)
- Handlers must NOT call `CommitAsync()` (UnitOfWork pipeline does it)
- DTOs must be `record` types with positional syntax, NEVER `class`
- Commands never return collections (queries do)
- Validators use `Cascade(CascadeMode.Stop)` for sequential checks
- No AutoMapper — use extension methods in `Mappers/` folder

---

## Findings

### Critical Issues — Paged Queries Not Following Standard

| File | Issues | Status |
|------|--------|--------|
| **`GetPagedPackages.cs`** | Uses `Page`/`PageSize` instead of `DataRangeRequest`, returns `BusinessResult<PagedData<T>>`, custom sort switch, no Specification/Filter | **FIXED 2026-04-10** |

This broke the employee detail page because callers from the new `employee-detail.facade.ts` passed `undefined` for page/pageSize params, which the validator rejected with:
```
api.validation.must_be_positive; 'Page Size' must be greater than or equal to '1'
```

### Medium Issues — Try/Catch in Handlers (6 files)

Handlers should let exceptions propagate to middleware. Remove try/catch blocks and let the global exception handler convert errors to ProblemDetails responses.

- `CreateOrder.cs`
- `HandlePaymentNotification.cs`
- `DeleteUserAccount.cs`
- `AdminDeleteUserAccount.cs`
- `DataRetentionBackgroundService.cs`
- `CancelInvoice.cs`

**Exception**: Background services (DataRetentionBackgroundService, StaleOrderCleanupService) genuinely need try/catch for fire-and-forget scenarios where the job should keep running even after one item fails. Those are acceptable.

### Medium Issues — `CommitAsync()` Called in Handlers (3 files)

The UnitOfWork MediatR pipeline behavior calls `CommitAsync()` automatically after the handler returns. Manual calls break this guarantee and may commit partial state.

- `PartnerLogin.cs`
- `StaleOrderCleanupService.cs` (acceptable — background service)
- `DataRetentionBackgroundService.cs` (acceptable — background service)

**Action**: Remove `CommitAsync()` from `PartnerLogin.cs`. Background services can keep it because they don't go through the MediatR pipeline.

### Low Issues — Handler `public class` Instead of `internal class` (2 files)

The `internal` modifier is preferred because handlers are only instantiated via DI, not consumed externally.

- `GetPagedDisputes.cs`
- `GetPagedPayConfigs.cs`

**Action**: Change `public class Handler` to `internal class Handler`. Cosmetic fix, no behavior change.

### Low Issues — DTO Defined as `class` Instead of `record` (2 files)

DTOs must be immutable records with positional syntax.

- `MyDocumentDto` in `GetMyDocuments.cs`
- `GetMyDocuments.Response` in `GetMyDocuments.cs`

**Action**: Convert to `public record MyDocumentDto(...)` and `public record Response(...)`.

### Low Issues — Validation Logic in Handler (1 file)

- `GetMyDocuments.cs` contains null checks in the handler (lines 47-60) that should be in the validator.

**Action**: Move null checks to the `Validator` class. If they're authorization checks (user session), they belong in a validator that injects `IUserSessionProvider`.

### Non-Issues

**Validators with Cascade.Stop**: 179 validators correctly use `Cascade(CascadeMode.Stop)`. Compliant.

**AutoMapper**: Not found anywhere. Compliant.

**Collection-returning queries**: 8 queries return `IQuery<List<T>>` (e.g., `GetAllFeatureFlags`, `GetUserConsents`). This is acceptable for **queries** that return a small, bounded set where pagination doesn't make sense (enums, reference data). It's only a problem when used for large datasets. Current usage is fine.

---

## Fix Priority

### Immediate (already done)

1. ✅ **Rewrite `GetPagedPackages.cs`** to follow the standard (fixes the employee detail page crash)

### This Week

2. ⏳ **Remove `CommitAsync()` from `PartnerLogin.cs`**
3. ⏳ **Convert `MyDocumentDto` and `GetMyDocuments.Response` to records**
4. ⏳ **Change `GetPagedDisputes.cs` and `GetPagedPayConfigs.cs` handler to `internal`**

### Later (Lower Priority)

5. ⏳ **Remove try/catch from 4 non-background handlers** (CreateOrder, HandlePaymentNotification, DeleteUserAccount, AdminDeleteUserAccount, CancelInvoice) — requires carefully reviewing each to ensure the middleware can handle the exceptions properly

---

## Post-Fix Checklist (Each File)

When fixing a non-compliant paged query, ensure ALL of these are updated:

1. **Filter class** created at `Features/Xxx/Filters/XxxFilter.cs`
2. **Sort class** created at `Core.Domain/Sorting/XxxSort.cs`
3. **Specification class** created at `Core.Domain/Specifications/XxxSpecification.cs`
4. **Handler rewritten** to follow the standard
5. **Controller updated** to use `Ok(result)` instead of `HandleResult<T>(result)`
6. **MANUAL_STEP: Regenerate NSwag client** for the affected API (admin/partner/customer/mobile)
7. **Frontend callers updated** to new signature (old: `getPaged(page, pageSize, ...)` → new: `getPaged(filters, sort, offset, limit)`)
8. **Build verification** on both backend + frontend

---

## Notes on Completed Fix (GetPagedPackages)

**Files created**:
- `Cleansia.Core.AppServices/Features/Packages/Filters/PackageFilter.cs` — new
- `Cleansia.Core.Domain/Sorting/PackageSort.cs` — new
- `Cleansia.Core.Domain/Specifications/PackageSpecification.cs` — new

**Files modified**:
- `Cleansia.Core.AppServices/Features/Packages/GetPagedPackages.cs` — rewritten to standard
- `Cleansia.Web.Admin/Controllers/AdminPackageController.cs` — `HandleResult` → `Ok`

**Pending frontend work** (after NSwag regen):
- `libs/cleansia-admin-features/package-management/src/lib/package-management/package-management.facade.ts` — update `loadPackages()` to use new signature
- `libs/cleansia-admin-features/package-management/src/lib/package-form/package-form.facade.ts` — update any `getPaged` call
- `libs/cleansia-admin-features/employee-management/src/lib/employee-detail/employee-detail.facade.ts` — update `loadPayConfigOptions()` to use new signature
- `libs/cleansia-admin-features/pay-config-management/src/lib/pay-config-form/pay-config-form.facade.ts` — update `loadPackages()` to use new signature
