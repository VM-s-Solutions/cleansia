# Cleansia Coding Standards

This document defines the coding standards and best practices for both frontend (Angular/TypeScript) and backend (C#/.NET) development in the Cleansia project.

---

## Table of Contents

1. [Backend Code Rules (C# / .NET)](#backend-code-rules-c--net)
2. [Frontend Code Rules (Angular / TypeScript)](#frontend-code-rules-angular--typescript)
3. [General Rules (Both Frontend and Backend)](#general-rules-both-frontend-and-backend)

---

## Backend Code Rules (C# / .NET)

### 1. CQRS Command/Query Structure

- Use MediatR pattern with `Command`/`Query`, `Response`, `Validator`, and `Handler` classes
- Keep handlers focused on the **happy path only**
- All validation logic must be in FluentValidation validators, **NOT in handlers**

#### Query vs Command - Key Differences

| Aspect | Query | Command |
|--------|-------|---------|
| Purpose | Read data (no side effects) | Modify data (creates, updates, deletes) |
| Interface | `IQuery<TResponse>` | `ICommand<TResponse>` |
| Handler Interface | `IQueryHandler<TQuery, TResponse>` | `ICommandHandler<TCommand, TResponse>` |
| Validator | **Optional** - only when input needs validation (e.g., ID existence checks) | **Required** - validate all inputs |
| CommitAsync | Never called (read-only) | Automatic via UnitOfWork pipeline |
| HTTP Method | **GET** (use `[FromQuery]` for params) | **POST/PUT/DELETE** (use `[FromBody]`) |

#### When Queries Need Validators

Queries need validators **only when they have required parameters that need existence checks**:

```csharp
// ✅ Query WITH validator - has ID parameter that needs existence validation
public class GetOrderDetails
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(IOrderRepository orderRepository)
        {
            RuleFor(x => x.OrderId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage(BusinessErrorMessage.Required)
                .MustAsync(orderRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }
    }

    public record Query(string OrderId) : IQuery<OrderItem>;

    public class Handler(IOrderRepository orderRepository) : IQueryHandler<Query, OrderItem>
    {
        public async Task<BusinessResult<OrderItem>> Handle(Query query, CancellationToken ct)
        {
            var order = await orderRepository.GetByIdAsync(query.OrderId, ct);
            return BusinessResult.Success(order!.MapToDetail());
        }
    }
}

// ✅ Query WITHOUT validator - simple date range filter, no existence checks needed
public class GetRevenueReport
{
    public record Query(ReportFilter Filter) : IQuery<RevenueReportDto>;

    internal class Handler(IOrderRepository orderRepository)
        : IQueryHandler<Query, RevenueReportDto>
    {
        public async Task<BusinessResult<RevenueReportDto>> Handle(Query request, CancellationToken ct)
        {
            var orders = await orderRepository
                .GetOrdersByDateRange(request.Filter.StartDate, request.Filter.EndDate)
                .ToListAsync(ct);
            // ... process and return
        }
    }
}

// ✅ Query WITHOUT validator - no parameters or uses session context
public class GetDashboardStats
{
    public record Query(string EmployeeId) : IQuery<DashboardStatsDto>;

    internal class Handler(...) : IQueryHandler<Query, DashboardStatsDto>
    {
        public async Task<BusinessResult<DashboardStatsDto>> Handle(Query query, CancellationToken ct)
        {
            // EmployeeId comes from authenticated session, no validation needed
            // ... implementation
        }
    }
}
```

#### Controller HTTP Methods

**CRITICAL:** Use correct HTTP methods based on operation type:

```csharp
// ✅ CORRECT - GET for queries with route parameter
[HttpGet("{orderId}")]
public async Task<IActionResult> GetOrderDetails(string orderId, CancellationToken ct)
{
    var result = await Mediator.Send(new GetOrderDetails.Query(orderId), ct);
    return HandleResult<OrderItem>(result);
}

// ✅ CORRECT - GET with query parameters for filter-based queries
[HttpGet("revenue")]
public async Task<IActionResult> GetRevenueReport(
    [FromQuery] DateTime startDate,
    [FromQuery] DateTime endDate,
    CancellationToken ct)
{
    var filter = new ReportFilter(startDate, endDate);
    var result = await Mediator.Send(new GetRevenueReport.Query(filter), ct);
    return HandleResult<RevenueReportDto>(result);
}

// ✅ CORRECT - POST for commands
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrder.Command command, CancellationToken ct)
{
    var result = await Mediator.Send(command, ct);
    return HandleResult<CreateOrder.Response>(result);
}

// ❌ WRONG - POST for queries (queries should use GET)
[HttpPost("revenue")]
public async Task<IActionResult> GetRevenueReport([FromBody] ReportFilter filter, CancellationToken ct)
{
    // DON'T use POST for read operations!
}
```

#### Paged Query Pattern

**For paginated list endpoints**, use the standard paged query pattern with MediatR `IRequest<PagedData<T>>`:

**Key Components:**
1. **Request class** - Extends `DataRangeRequest`, uses `IRequest<PagedData<T>>`
2. **Filter class** - Contains filter properties (in `Features/{Entity}/Filters/` folder)
3. **Specification class** - Builds LINQ expressions (in `Domain/Specifications/` folder)
4. **Sort class** - Defines sortable fields (in `Domain/Sorting/` folder)
5. **Handler** - Internal class with `IRequestHandler<Request, PagedData<T>>`

**Example - GetPagedServices.cs:**
```csharp
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Features.Services.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Specifications;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Services;

public class GetPagedServices
{
    public class Request : DataRangeRequest, IRequest<PagedData<ServiceListItem>>
    {
        public ServiceFilter? Filter { get; init; }
    }

    internal class Handler(IServiceRepository serviceRepository)
        : IRequestHandler<Request, PagedData<ServiceListItem>>
    {
        public async Task<PagedData<ServiceListItem>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = ServiceSpecification.Create(
                searchTerm: request.Filter?.SearchTerm
            );

            var filter = specification.SatisfiedBy();

            var totalItems = await serviceRepository.GetCountAsync(filter, cancellationToken);
            var items = await serviceRepository
                .GetPagedSort<ServiceSort>(request.Offset, request.Limit, filter, request.Sort.MapToDomain())
                .AsNoTracking()
                .Select(service => service.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }
    }
}
```

**Example - ServiceFilter.cs:**
```csharp
namespace Cleansia.Core.AppServices.Features.Services.Filters;

public class ServiceFilter
{
    public string? SearchTerm { get; init; }
}
```

**Example - ServiceSpecification.cs:**
```csharp
using System.Linq.Expressions;
using Cleansia.Core.Domain.Services;
using Cleansia.Infra.Common.Specifications;

namespace Cleansia.Core.Domain.Specifications;

public class ServiceSpecification : BaseSpecification<string?>, ISpecification<Service>
{
    public string? SearchTerm { get; set; }

    public Expression<Func<Service, bool>> SatisfiedBy()
    {
        Specification<Service> specification = new TrueSpecification<Service>();

        if (!string.IsNullOrEmpty(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            specification &= new DirectSpecification<Service>(x =>
                x.Name.ToLower().Contains(searchLower) ||
                x.Description.ToLower().Contains(searchLower)
            );
        }

        return specification.SatisfiedBy();
    }

    public static ServiceSpecification Create(string? searchTerm = null) =>
        new() { SearchTerm = searchTerm };
}
```

**Example - ServiceSort.cs:**
```csharp
using System.Linq.Expressions;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Sorting.Common;

namespace Cleansia.Core.Domain.Sorting;

public class ServiceSort(string propertyName, bool isAscending)
    : BaseSort<Service>(propertyName, isAscending)
{
    public override Expression<Func<Service, object>> DefaultSort => x => x.Name;

    protected override Expression<Func<Service, object>> GetSortingExpression(string propertyName)
    {
        if (string.Equals(propertyName, nameof(Service.Name), StringComparison.CurrentCultureIgnoreCase))
            return x => x.Name;
        if (string.Equals(propertyName, nameof(Service.BasePrice), StringComparison.CurrentCultureIgnoreCase))
            return x => x.BasePrice;
        if (string.Equals(propertyName, "CreatedOn", StringComparison.CurrentCultureIgnoreCase))
            return x => x.CreatedOn;
        return DefaultSort;
    }
}
```

**Controller for Paged Queries (uses POST due to complex filter object in body):**
```csharp
[HttpPost("get-paged")]
[Permission(Policy.CanViewServices)]
[ProducesResponseType(typeof(PagedData<ServiceListItem>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetPagedServices(
    [FromBody] GetPagedServices.Request request,
    CancellationToken cancellationToken)
{
    var result = await Mediator.Send(request, cancellationToken);
    return Ok(result);
}
```

**NOTE:** Paged queries use `IRequest<T>` (plain MediatR) instead of `IQuery<T>` (CQRS abstraction) because:
- They don't need validators (filter params are optional)
- They return `PagedData<T>` directly, not wrapped in `BusinessResult<T>`
- They use POST with `[FromBody]` due to complex filter objects (not suitable for query string)

### 2. Validation Rules

**ALL validation must be in `Validator` classes** using FluentValidation:

- Validators should inherit from `UserEmailValidator<TCommand>` when user context is needed
- Use `Cascade(CascadeMode.Stop)` to prevent multiple validation errors
- **NEVER use `BusinessResult.Failure()` in handlers** - move all checks to validators
- Use `MustAsync()` for async validations (e.g., existence checks)
- Custom validators should be private methods in the Validator class

**Example:**
```csharp
public class Validator : UserEmailValidator<Command>
{
    private readonly IEmployeeDocumentRepository _documentRepository;

    public Validator(
        IUserRepository userRepository,
        IUserSessionProvider userSessionProvider,
        IEmployeeDocumentRepository documentRepository)
        : base(userRepository, userSessionProvider)
    {
        _documentRepository = documentRepository;

        RuleFor(x => x.DocumentId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(BusinessErrorMessage.Required)
            .MustAsync(documentRepository.ExistsAsync)
            .WithMessage(BusinessErrorMessage.DocumentNotFound)
            .MustAsync(BeOwnedByCurrentEmployeeAsync)
            .WithMessage(BusinessErrorMessage.EmployeeDocumentNotOwned);
    }

    private async Task<bool> BeOwnedByCurrentEmployeeAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        var userEmail = _userSessionProvider.GetUserEmail();
        var employee = await _employeeRepository.GetByUserEmailAsync(userEmail!, cancellationToken);
        if (employee is null) return false;

        var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
        return document?.EmployeeId == employee.Id;
    }
}
```

### 3. Handler Rules

Handlers should **ONLY contain happy path logic**:

- ✅ **DO:** Focus on business logic execution
- ❌ **DON'T:** Use `BusinessResult.Failure()` calls
- ❌ **DON'T:** Perform manual validation or null checks
- ❌ **DON'T:** Call `CommitAsync()` in Command handlers - UnitOfWork pipeline handles this
- ✅ **DO:** Use `BusinessResult.Success()` to return responses
- ✅ **DO:** Use non-null assertion (`!`) for values validated by validators

**Example:**
```csharp
public class Handler(
    IEmployeeRepository employeeRepository,
    IEmployeeDocumentRepository documentRepository,
    IUserRepository userRepository,
    IUserSessionProvider userSessionProvider) : ICommandHandler<Command, Response>
{
    public async Task<BusinessResult<Response>> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        // All validation is done by the Validator - we can safely use non-null assertions
        var adminEmail = userSessionProvider.GetUserEmail();
        var adminUser = await userRepository.GetByEmailAsync(adminEmail!, cancellationToken);
        var document = await documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);

        // Business logic only
        document!.Approve(adminUser!.Id, request.Notes);

        return BusinessResult.Success(new Response
        {
            DocumentId = document.Id
        });
    }
}
```

### 4. DTO Rules

**ALL DTOs must be `record` types**, NOT classes (except PagedData DTOs):

- Use positional record syntax with named parameters
- DTOs should be immutable
- Use descriptive property names

**Example:**
```csharp
// ✅ CORRECT - Record type with positional syntax
public record EmployeeDocumentItem(
    string Id,
    string FileName,
    string FilePath,
    string ContentType,
    long FileSizeBytes,
    DocumentType DocumentType,
    string? Description,
    int Version,
    string? PreviousVersionId,
    string EmployeeId,
    DocumentStatus Status,
    string? ReviewNotes,
    string? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    bool IsActive,
    DateTimeOffset CreatedOn,
    string CreatedBy,
    DateTimeOffset? UpdatedOn
);

// ❌ WRONG - Class type
public class EmployeeDocumentItem
{
    public string Id { get; init; } = default!;
    public string FileName { get; init; } = default!;
    // ...
}
```

### 5. Mapper Rules

ALL mapping logic must be in dedicated `Mappers` classes:

- Use extension methods for entity-to-DTO mapping
- Method name convention: `.MapToDto()`
- ❌ **NEVER use static methods on DTOs** (e.g., `EmployeeDocumentItem.FromEntity()`)
- Keep all mappers in the `Mappers` folder

**Example:**
```csharp
// File: Cleansia.Core.AppServices/Mappers/EmployeeDocumentMappers.cs
namespace Cleansia.Core.AppServices.Mappers;

public static class EmployeeDocumentMappers
{
    public static EmployeeDocumentItem MapToDto(this EmployeeDocument document)
    {
        return new EmployeeDocumentItem(
            Id: document.Id,
            FileName: document.FileName,
            FilePath: document.FilePath,
            ContentType: document.ContentType,
            FileSizeBytes: document.FileSizeBytes,
            DocumentType: document.DocumentType,
            Description: document.Description,
            Version: document.Version,
            PreviousVersionId: document.PreviousVersionId,
            EmployeeId: document.EmployeeId,
            Status: document.Status,
            ReviewNotes: document.ReviewNotes,
            ReviewedByUserId: document.ReviewedByUserId,
            ReviewedAt: document.ReviewedAt,
            IsActive: document.IsActive,
            CreatedOn: document.CreatedOn,
            CreatedBy: document.CreatedBy,
            UpdatedOn: document.UpdatedOn
        );
    }
}

// Usage in handlers:
var documentDto = document.MapToDto();
var documentDtos = documents.Select(d => d.MapToDto()).ToList();
```

### 6. Constants and Error Messages

ALL hardcoded strings must be in the `BusinessErrorMessage` class:

- Use descriptive constant names in dot notation
- Format: `{category}.{specific_error}`
- **ALL `BusinessErrorMessage` keys MUST have translations in frontend i18n files**

**Example:**
```csharp
// File: Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs
public static class BusinessErrorMessage
{
    // Employee Documents
    public const string DocumentNotFound = "employee_document.not_found";
    public const string Unauthorized = "employee_document.unauthorized";
    public const string EmployeeDocumentNotOwned = "employee_document.not_owned";

    // Employee
    public const string EmployeeNotFound = "employee.not_found";
    public const string EmployeeProfileIncomplete = "employee.profile_incomplete";
    public const string EmployeeAlreadyApproved = "employee.already_approved";
}
```

### 7. Do NOT Call CommitAsync

**CRITICAL RULE:** Never call `CommitAsync()` in Command handlers.

- ✅ UnitOfWork pipeline behavior automatically calls `CommitAsync()` for all Commands
- ❌ Manual `CommitAsync()` calls are redundant and should be removed
- ✅ Only Query handlers should NOT trigger commits (they don't modify data)

**Example:**
```csharp
// ❌ WRONG
public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken ct)
{
    var document = await documentRepository.GetByIdAsync(request.DocumentId, ct);
    document!.Approve(adminUser!.Id, request.Notes);

    await documentRepository.CommitAsync(ct); // ❌ DON'T DO THIS

    return BusinessResult.Success(new Response { DocumentId = document.Id });
}

// ✅ CORRECT
public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken ct)
{
    var document = await documentRepository.GetByIdAsync(request.DocumentId, ct);
    document!.Approve(adminUser!.Id, request.Notes);

    // ✅ CommitAsync is automatically called by UnitOfWork pipeline
    return BusinessResult.Success(new Response { DocumentId = document.Id });
}
```

---

## Frontend Code Rules (Angular / TypeScript)

### 1. Enum Usage Rules

**NEVER use magic numbers** - always use enum values:

- For dropdown options, use `EnumName.EnumValue` syntax
- When comparing DTO string values to enums, use `EnumName[EnumName.Value]` pattern
  - This is because backend returns enum **names** as strings, not numeric values
- Always import enums from `@cleansia/services`
- Expose enums to templates by declaring them as `protected readonly` properties

**Example - Dropdown Options:**
```typescript
// Facade
import { PayPeriodStatus } from '@cleansia/services';

@Injectable()
export class PayPeriodManagementFacade {
  private readonly translate = inject(TranslateService);

  // ✅ CORRECT - Using enum values with translations
  readonly statusOptions = [
    {
      label: this.translate.instant('payPeriods.status.open'),
      value: PayPeriodStatus.Open
    },
    {
      label: this.translate.instant('payPeriods.status.closed'),
      value: PayPeriodStatus.Closed
    },
    {
      label: this.translate.instant('payPeriods.status.paid'),
      value: PayPeriodStatus.Paid
    },
  ];

  // ❌ WRONG - Magic numbers and hardcoded labels
  readonly statusOptions = [
    { label: 'Open', value: 0 },
    { label: 'Closed', value: 1 },
    { label: 'Paid', value: 2 },
  ];
}
```

**Example - String Comparison in Templates:**
```typescript
// Component
import { PayPeriodStatus } from '@cleansia/services';

export class PayPeriodDetailComponent {
  protected readonly facade = inject(PayPeriodDetailFacade);

  // Expose enum to template
  protected readonly PayPeriodStatus = PayPeriodStatus;
}
```

```html
<!-- Template -->
<!-- ✅ CORRECT - Using enum for string comparison -->
@if (facade.payPeriod()?.status === PayPeriodStatus[PayPeriodStatus.Open]) {
  <cleansia-button
    [label]="'payPeriods.detail.closePeriod' | translate"
    icon="pi pi-lock"
    (onClick)="onClosePayPeriod()"
  />
}

<!-- ❌ WRONG - Hardcoded string comparison -->
@if (facade.payPeriod()?.status === 'Open') {
  <button>Close Period</button>
}
```

**Why use `PayPeriodStatus[PayPeriodStatus.Open]` instead of `PayPeriodStatus.Open`?**

The backend serializes enums as their **string names** (e.g., "Open", "Closed"), not numeric values. The pattern `PayPeriodStatus[PayPeriodStatus.Open]` converts the enum to its string representation:
- `PayPeriodStatus.Open` = `0` (number)
- `PayPeriodStatus[PayPeriodStatus.Open]` = `"Open"` (string)

**Example - Code Object Enum Comparisons:**

When working with `Code` objects from the API (which have `value`, `name`, and `type` properties), **always compare using `code.value` against enum values**:

```typescript
// The Code class structure from API:
// class Code {
//   type: string;    // e.g., "OrderStatus"
//   name: string;    // e.g., "Pending" (display name)
//   value: number;   // e.g., 1 (enum numeric value)
// }

import { Code, OrderStatus, PaymentStatus } from '@cleansia/services';

// ✅ CORRECT - Compare code.value against enum
getOrderStatusClass(status: Code | undefined): string {
  if (!status) return 'order-status-badge status-pending';
  switch (status.value) {
    case OrderStatus.Pending:
      return 'order-status-badge status-pending';
    case OrderStatus.Confirmed:
      return 'order-status-badge status-confirmed';
    case OrderStatus.InProgress:
      return 'order-status-badge status-inprogress';
    case OrderStatus.Completed:
      return 'order-status-badge status-completed';
    case OrderStatus.Cancelled:
      return 'order-status-badge status-cancelled';
    default:
      return 'order-status-badge status-pending';
  }
}

// ❌ WRONG - String comparison on name (fragile, case-sensitive)
getOrderStatusClass(statusName: string | undefined): string {
  const normalized = statusName?.toLowerCase().replace(/\s+/g, '-');
  switch (normalized) {
    case 'pending':
      return 'order-status-badge status-pending';
    case 'inprogress':
    case 'in-progress':  // Need multiple cases for variations!
      return 'order-status-badge status-inprogress';
    // ... fragile and error-prone
  }
}
```

**Why use `code.value` instead of `code.name`?**

- `code.value` is the **numeric enum value** (stable, type-safe)
- `code.name` is the **display string** (may vary by locale, casing issues)
- Using `code.value` with TypeScript enums provides compile-time safety
- No need to handle string variations like "InProgress" vs "In Progress"

**Template Usage with Code Objects:**

```html
<!-- Display the name for users -->
<span [class]="facade.getOrderStatusClass(order.orderStatus)">
  {{ order.orderStatus?.name }}
</span>
```

### 2. Translation Rules

**ALL user-facing text must use translation keys** - NO hardcoded strings:

- Use `translate.instant()` for dynamic values (e.g., dropdown labels)
- Use translation pipe `| translate` in templates
- Structure translation keys hierarchically: `pages.{page_name}.{section}.{key}`
- **ALWAYS provide translations in BOTH `en.json` AND `cs.json`**
- Translation keys should be in snake_case for consistency

**Example - Facade with Dynamic Translations:**
```typescript
@Injectable()
export class EmployeeManagementFacade {
  private readonly translate = inject(TranslateService);

  // ✅ CORRECT - Using translations
  readonly contractStatusOptions = [
    {
      label: this.translate.instant('pages.employee_management.contract_status.pending'),
      value: ContractStatus.Pending
    },
    {
      label: this.translate.instant('pages.employee_management.contract_status.active'),
      value: ContractStatus.Active
    },
  ];

  // ❌ WRONG - Hardcoded labels
  readonly contractStatusOptions = [
    { label: 'Pending', value: ContractStatus.Pending },
    { label: 'Active', value: ContractStatus.Active },
  ];
}
```

**Example - Template Translations:**
```html
<!-- ✅ CORRECT - Using translation pipe -->
<span class="label">
  {{ 'pages.employee_detail.document_description' | translate }}:
</span>

<cleansia-button
  [label]="'payPeriods.detail.closePeriod' | translate"
  icon="pi pi-lock"
/>

<!-- ❌ WRONG - Hardcoded text -->
<span class="label">Description:</span>
<button>Close Period</button>
```

**Translation File Structure:**
```json
{
  "pages": {
    "employee_management": {
      "title": "Employee Management",
      "contract_status": {
        "pending": "Pending",
        "active": "Active",
        "approved": "Approved",
        "rejected": "Rejected",
        "terminated": "Terminated"
      }
    },
    "employee_detail": {
      "document_description": "Description",
      "personal_info": "Personal Information"
    }
  },
  "errors": {
    "employee_document": {
      "not_found": "Document not found",
      "not_owned": "You can only access your own documents"
    }
  }
}
```

### 3. Component File Structure Rules

**CRITICAL:** Component files must be separated - **NO inline templates or styles**:

- ✅ Template in separate `.component.html` file
- ✅ Styles in separate `.component.scss` file (in shared assets)
- ✅ TypeScript logic in `.component.ts` file
- ❌ **NEVER** use inline `template:` in `@Component` decorator
- ❌ **NEVER** use inline `styles:` in `@Component` decorator

**File Location Conventions:**
```
Component TypeScript:
  libs/{feature}/src/lib/{component-name}/{component-name}.component.ts

Component Template:
  libs/{feature}/src/lib/{component-name}/{component-name}.component.html

Component Styles (shared):
  libs/shared/assets/src/styles/pages/cleansia-admin/{component-name}.component.scss
  libs/shared/assets/src/styles/pages/cleansia-partner/{component-name}.component.scss
  libs/shared/assets/src/styles/components/{component-name}.component.scss (for reusable)
```

**Example - Correct Component Structure:**
```typescript
// File: admin-order-photos.component.ts
@Component({
  selector: 'admin-order-photos',
  standalone: true,
  templateUrl: './admin-order-photos.component.html',  // ✅ External template
  styleUrls: [],  // Styles imported via global SCSS
  imports: [
    CommonModule,
    TranslatePipe,
    CleansiaSectionComponent,
    CleansiaLoaderComponent,  // ✅ Using shared components
  ],
})
export class AdminOrderPhotosComponent {
  // ...
}

// ❌ WRONG - Inline template and styles
@Component({
  selector: 'admin-order-photos',
  standalone: true,
  template: `
    <div class="order-photos">...</div>  // ❌ DON'T use inline templates
  `,
  styles: [`
    .order-photos { ... }  // ❌ DON'T use inline styles
  `],
})
```

### 4. Use Shared Component Wrappers

**ALWAYS use existing shared component wrappers** from `@cleansia/components`:

| Instead of | Use |
|------------|-----|
| `<button>` | `<cleansia-button>` |
| `<input>` | `<cleansia-text-input>` |
| `<select>` / `<p-dropdown>` | `<cleansia-select>` |
| `<textarea>` | `<cleansia-textarea>` |
| Loading spinner | `<cleansia-loader>` |
| Section container | `<cleansia-section>` |
| Page title | `<cleansia-title>` |
| Date picker | `<cleansia-calendar>` |
| Time picker | `<cleansia-time-picker>` |
| File upload | `<cleansia-file>` |
| Checkbox | `<cleansia-checkbox>` |
| Data table | `<cleansia-table>` |
| Multi-select | `<cleansia-multiselect>` |

**Example:**
```html
<!-- ✅ CORRECT - Using shared components -->
<cleansia-section [title]="'pages.order_detail.photos' | translate">
  @if (loading()) {
    <cleansia-loader />
  } @else {
    <cleansia-button
      [label]="'global.actions.save' | translate"
      icon="pi pi-save"
      (onClick)="onSave()"
    />
  }
</cleansia-section>

<!-- ❌ WRONG - Using raw HTML elements -->
<div class="section">
  <h3>Order Photos</h3>
  @if (loading()) {
    <div class="loader"><i class="pi pi-spin pi-spinner"></i></div>
  } @else {
    <button type="button" (click)="onSave()">
      <i class="pi pi-save"></i> Save
    </button>
  }
</div>
```

### 5. Facade Pattern for Data Operations

Use **Facade pattern** for all data operations and business logic:

- Components should only handle UI logic and delegate to facades
- Store shared state in facade signals
- Keep components thin - move complex logic to facades
- Facades are injected via `inject()` function
- Use `protected readonly` for facade references to expose them to templates

**Example Structure:**
```typescript
// Facade
@Injectable()
export class EmployeeManagementFacade {
  private readonly adminClient = inject(AdminClient);
  private readonly snackbarService = inject(SnackbarService);
  private readonly translate = inject(TranslateService);

  // Signals for reactive state
  readonly employees = signal<AdminEmployeeListItem[]>([]);
  readonly loading = signal<boolean>(false);
  readonly totalRecords = signal<number>(0);

  loadEmployees(): void {
    this.loading.set(true);
    // ... implementation
  }

  approveEmployee(employeeId: string): void {
    // ... implementation
  }
}

// Component
@Component({
  selector: 'cleansia-admin-employee-management',
  standalone: true,
  providers: [EmployeeManagementFacade],
  // ...
})
export class EmployeeManagementComponent {
  // Use 'protected' to expose facade to template
  protected readonly facade = inject(EmployeeManagementFacade);

  ngOnInit(): void {
    this.facade.loadEmployees();
  }
}
```

### 6. Dropdown and Select Rules

Define options arrays in **facades**, NOT components:

- ✅ Options should be defined in the facade as `readonly` properties
- ✅ Always use translations for labels
- ✅ Use enum values, NOT magic numbers
- ✅ Reference facade options in templates: `[options]="facade.statusOptions"`
- ❌ Never duplicate option arrays in components

**Example:**
```typescript
// ✅ CORRECT - Options in Facade
@Injectable()
export class PayPeriodManagementFacade {
  readonly statusOptions = [
    { label: this.translate.instant('payPeriods.status.open'), value: PayPeriodStatus.Open },
    { label: this.translate.instant('payPeriods.status.closed'), value: PayPeriodStatus.Closed },
    { label: this.translate.instant('payPeriods.status.paid'), value: PayPeriodStatus.Paid },
  ];
}

// Component - just references facade
@Component({ /* ... */ })
export class PayPeriodManagementComponent {
  protected readonly facade = inject(PayPeriodManagementFacade);
}

// Template - references facade options
<cleansia-select
  formControlName="status"
  [options]="facade.statusOptions"
  [placeholder]="'payPeriods.list.filterByStatus' | translate"
/>

// ❌ WRONG - Duplicate options in component
@Component({ /* ... */ })
export class PayPeriodManagementComponent {
  statusOptions = [ /* duplicated array */ ];  // ❌ Don't do this
}
```

### 5. Form and Filter Rules

- Keep filter state in facade signals
- Use reactive forms with FormBuilder
- Type form values properly (e.g., `status: [null as number | null]`)
- Subscribe to form changes and delegate to facade methods

**Example:**
```typescript
@Component({ /* ... */ })
export class PayPeriodManagementComponent implements AfterViewInit {
  private readonly fb = inject(FormBuilder);
  protected readonly facade = inject(PayPeriodManagementFacade);

  filterForm = this.fb.group({
    status: [null as number | null],
    year: [null as number | null],
  });

  ngAfterViewInit(): void {
    this.filterForm.valueChanges
      .pipe(debounceTime(500), distinctUntilChanged())
      .subscribe(() => this.applyFilters());
  }

  applyFilters(): void {
    const formValues = this.filterForm.value;
    this.facade.applyFilter({
      status: formValues.status ?? undefined,
      year: formValues.year ?? undefined,
    });
  }
}
```

### 6. Error Handling Rules

ALL backend error keys must have frontend translations:

- Backend error keys from `BusinessErrorMessage` must exist in `errors.*` section
- Use `SnackbarService` to display errors with translated messages
- Structure: `errors.{category}.{specific_error}`
- Match the exact error key structure from backend

**Example:**
```typescript
// Backend error key
public const string DocumentNotFound = "employee_document.not_found";

// Frontend translation (en.json)
{
  "errors": {
    "employee_document": {
      "not_found": "Document not found",
      "not_owned": "You can only access your own documents",
      "unauthorized": "You are not authorized to access this document"
    }
  }
}

// Frontend translation (cs.json)
{
  "errors": {
    "employee_document": {
      "not_found": "Dokument nenalezen",
      "not_owned": "Můžete přistupovat pouze ke svým vlastním dokumentům",
      "unauthorized": "Nemáte oprávnění k přístupu k tomuto dokumentu"
    }
  }
}

// Usage in facade
approveDocument(documentId: string): void {
  this.adminClient.documentClient
    .approve(documentId)
    .pipe(
      catchError((error) => {
        this.snackbarService.showError(
          this.translate.instant('pages.employee_detail.messages.document_approve_error')
        );
        return of(null);
      })
    )
    .subscribe(/* ... */);
}
```

### 7. Table Definition Rules

Keep table definitions in separate `.models.ts` files:

- Use factory functions to create table definitions
- Import enums for visibility conditions
- Use translations for all column headers and labels
- Pass dependencies (translate service, callbacks) as parameters

**Example:**
```typescript
// File: pay-period-management.models.ts
import { TableDefinition } from '@cleansia/components';
import { PayPeriodDto, PayPeriodStatus } from '@cleansia/services';
import { TranslateService } from '@ngx-translate/core';

export interface PayPeriodFilterParams {
  status?: number;
  year?: number;
}

export function getPayPeriodTableDefinition(
  defs: {
    onViewDetails: (row: PayPeriodDto) => void;
    onClose: (row: PayPeriodDto) => void;
  },
  translate: TranslateService
): TableDefinition<PayPeriodDto> {
  return {
    columns: [
      {
        id: 'periodLabel',
        headerName: translate.instant('payPeriods.list.columns.periodLabel'),
        value: 'periodLabel',
        sortable: true,
      },
      {
        id: 'status',
        headerName: translate.instant('payPeriods.list.columns.status'),
        value: (row?: PayPeriodDto) =>
          row?.status
            ? translate.instant(`payPeriods.status.${row.status.toLowerCase()}`)
            : '',
        sortable: true,
      },
      {
        id: 'actions',
        headerName: translate.instant('payPeriods.list.columns.actions'),
        columnActions: [
          {
            icon: 'pi pi-eye',
            onClick: (row: PayPeriodDto) => defs.onViewDetails(row),
            tooltip: {
              title: translate.instant('payPeriods.list.viewDetails'),
            },
          },
          {
            icon: 'pi pi-lock',
            onClick: (row: PayPeriodDto) => defs.onClose(row),
            // ✅ Using enum for visibility condition
            visible: (row: PayPeriodDto) =>
              row.status === PayPeriodStatus[PayPeriodStatus.Open],
          },
        ],
      },
    ],
  };
}
```

---

## General Rules (Both Frontend and Backend)

### 1. Consistency

- **Follow existing patterns** in the codebase
- Don't create new patterns unless absolutely necessary
- Check similar features for reference implementation
- When in doubt, look at how other features solve the same problem

### 2. No Comments Unless Necessary

- Code should be **self-documenting** through clear naming
- Only add comments for complex business logic that isn't obvious
- Avoid redundant comments that just repeat what the code does

```csharp
// ❌ BAD - Redundant comment
// Get the employee by ID
var employee = await employeeRepository.GetByIdAsync(employeeId, ct);

// ✅ GOOD - No comment needed, code is clear
var employee = await employeeRepository.GetByIdAsync(employeeId, ct);

// ✅ GOOD - Comment explains WHY, not WHAT
// We need to validate documents before approval to ensure compliance with GDPR
var isValid = await ValidateDocumentsForGdprCompliance(employee.Documents);
```

### 3. Error Messages

- **Backend** defines error keys in `BusinessErrorMessage`
- **Frontend** provides translations in `en.json` and `cs.json`
- Every backend error key MUST have a frontend translation
- Use dot notation: `category.specific_error`

**Workflow:**
1. Backend developer adds error key: `BusinessErrorMessage.EmployeeNotFound = "employee.not_found"`
2. Frontend developer adds translations:
   - `en.json`: `"errors": { "employee": { "not_found": "Employee not found" } }`
   - `cs.json`: `"errors": { "employee": { "not_found": "Zaměstnanec nenalezen" } }`

### 4. Type Safety

- Use proper TypeScript types, **avoid `any`**
- Use enum types consistently
- Properly type form controls and signals
- Use strict null checks and non-null assertions appropriately

```typescript
// ✅ GOOD - Proper typing
readonly employees = signal<AdminEmployeeListItem[]>([]);
readonly loading = signal<boolean>(false);

filterForm = this.fb.group({
  status: [null as number | null],
  year: [null as number | null],
});

// ❌ BAD - Using 'any'
readonly employees: any;
readonly loading: any;
filterForm: any;
```

### 5. Testing

Before committing code:

- Run linting: `npx nx lint {project-name}`
- Ensure TypeScript compilation succeeds
- Test translations work in both languages (EN and CS)
- Verify enum values work correctly in dropdowns and comparisons

---

## Quick Reference Checklist

### Backend Checklist

- [ ] **Query vs Command:** Using correct type (`IQuery` for reads, `ICommand` for writes)
- [ ] **HTTP Methods:** GET for queries, POST/PUT/DELETE for commands
- [ ] **Validators:** Commands always have validators; Queries only when ID existence check needed
- [ ] All validation is in `Validator` classes, not handlers
- [ ] Handlers only contain happy path logic
- [ ] No `BusinessResult.Failure()` in handlers
- [ ] No `CommitAsync()` calls in Command handlers
- [ ] All DTOs are `record` types (except PagedData)
- [ ] Mapping logic is in dedicated `Mappers` classes using extension methods
- [ ] All hardcoded strings are in `BusinessErrorMessage`
- [ ] All error keys have frontend translations

### Frontend Checklist

- [ ] No magic numbers - using enum values
- [ ] String comparisons use `EnumName[EnumName.Value]` pattern
- [ ] Code object comparisons use `code.value` against enum values (NOT `code.name` string matching)
- [ ] All user-facing text uses translations (no hardcoded strings)
- [ ] Translations exist in both `en.json` AND `cs.json`
- [ ] Dropdown options defined in facades, not components
- [ ] Options use `translate.instant()` for labels
- [ ] Components delegate to facades for business logic
- [ ] Table definitions in separate `.models.ts` files
- [ ] All backend error keys have translations in `errors.*` section

---

## Examples Repository

For real-world examples of these patterns, refer to:

### Backend Examples
- ✅ **Query with Validator:** `GetOrderDetails.cs`, `GetPayPeriodById.cs` (ID existence validation)
- ✅ **Query without Validator:** `GetRevenueReport.cs`, `GetDashboardStats.cs` (filter/session params)
- ✅ **Good Command:** `ApproveEmployee.cs`, `RejectEmployee.cs`
- ✅ **Good Validator:** `ApproveDocument.cs`, `RejectDocument.cs`
- ✅ **Good Mapper:** `EmployeeDocumentMappers.cs`, `OrderMappers.cs`
- ✅ **Good DTO:** `EmployeeDocumentItem.cs`, `RevenueReportDto.cs`

### Frontend Examples
- ✅ **Good Facade:** `EmployeeManagementFacade`, `PayPeriodManagementFacade`
- ✅ **Good Component:** `EmployeeManagementComponent`, `PayPeriodManagementComponent`
- ✅ **Good Table Definition:** `pay-period-management.models.ts`
- ✅ **Good Translations:** `apps/cleansia-admin.app/src/assets/i18n/en.json`

---

*Last Updated: 2026-01-03*
