# Backend Architecture

The Cleansia backend is built on .NET 10 using a Clean Architecture layout with CQRS, split across 12+ projects and 4 separate API hosts.

## Project Structure

```
src/
├── Cleansia.Core.Domain/          # Entities, enums, repository interfaces
├── Cleansia.Core.AppServices/     # CQRS handlers, validators, services
├── Cleansia.Config/               # Shared startup, middleware, auth config
├── Cleansia.Infra.Database/       # EF Core DbContext, migrations
├── Cleansia.Infra.Common/         # Shared infra utilities
├── Cleansia.Infra.Services/       # Service implementations (email, PDF, etc.)
├── Cleansia.Infra.Azure.Storage.Blobs/  # Azure Blob Storage wrappers
├── Cleansia.Infra.Azure.Storage.Queues/ # Azure Queue Storage wrappers
├── Cleansia.Infra.Clients/        # External API clients (Stripe, SendGrid)
├── Cleansia.ServiceDefaults/      # .NET Aspire service defaults
├── Cleansia.Web/                  # Partner API (port 5000)
├── Cleansia.Web.Admin/            # Admin API (port 5001)
├── Cleansia.Web.Mobile/           # Mobile API (port 5002)
├── Cleansia.Web.Customer/         # Customer API (port 5003)
├── Cleansia.Functions/            # Azure Functions (Docker)
└── Cleansia.AppHost/              # .NET Aspire orchestrator
```

### Layer Dependency Graph

```
Web / Web.Admin / Web.Mobile / Web.Customer / Functions
        │
        ▼
    Cleansia.Config
        │
        ├──► Cleansia.Core.AppServices
        │         │
        │         ▼
        │    Cleansia.Core.Domain
        │
        ├──► Cleansia.Infra.Database
        ├──► Cleansia.Infra.Services
        ├──► Cleansia.Infra.Azure.Storage.*
        └──► Cleansia.Infra.Clients
```

::: tip Design Principle
Core projects (`Domain`, `AppServices`) have zero infrastructure dependencies. All infrastructure is injected via interfaces defined in `Core.Domain`.
:::

## CQRS with MediatR

All business logic flows through MediatR command and query handlers. The project defines custom marker interfaces for clarity and pipeline targeting.

### Commands and Queries

```csharp
// Marker interfaces
public interface ICommand : IRequest<BusinessResult> { }
public interface ICommand<TResult> : IRequest<BusinessResult<TResult>> { }
public interface IQuery<TResult> : IRequest<BusinessResult<TResult>> { }

// Handler interfaces
public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, BusinessResult>
    where TCommand : ICommand { }

public interface ICommandHandler<TCommand, TResult> : IRequestHandler<TCommand, BusinessResult<TResult>>
    where TCommand : ICommand<TResult> { }

public interface IQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, BusinessResult<TResult>>
    where TQuery : IQuery<TResult> { }
```

### BusinessResult

Every handler returns a `BusinessResult` (or `BusinessResult<T>`) instead of throwing exceptions for expected failures:

```csharp
public class BusinessResult
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    public static BusinessResult Success() => new(true, null);
    public static BusinessResult Failure(Error error) => new(false, error);
}

public class BusinessResult<T> : BusinessResult
{
    public T? Value { get; }

    public static BusinessResult<T> Success(T value) => new(true, null, value);
}
```

### Example: Creating an Order

```csharp
// Command definition
public static class CreateOrder
{
    public record Command(
        Guid CustomerId,
        Guid ServiceId,
        DateOnly Date,
        TimeOnly Time,
        Guid AddressId
    ) : ICommand<Guid>;

    // Validator (auto-discovered by FluentValidation)
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CustomerId).NotEmpty();
            RuleFor(x => x.ServiceId).NotEmpty();
            RuleFor(x => x.Date).GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow));
        }
    }

    // Handler
    public class Handler(
        IOrderRepository orderRepository,
        IServiceRepository serviceRepository
    ) : ICommandHandler<Command, Guid>
    {
        public async Task<BusinessResult<Guid>> Handle(
            Command request, CancellationToken ct)
        {
            var service = await serviceRepository.GetByIdAsync(request.ServiceId, ct);
            if (service is null)
                return BusinessResult<Guid>.Failure(Errors.NotFound("Service"));

            var order = Order.Create(
                request.CustomerId, service, request.Date, request.Time, request.AddressId);

            orderRepository.Add(order);
            return BusinessResult<Guid>.Success(order.Id);
        }
    }
}
```

## Pipeline Behaviors

MediatR pipeline behaviors run automatically on every request in order:

| Order | Behavior | Applies To | Purpose |
|-------|----------|-----------|---------|
| 1 | `ValidationPipelineBehavior` | All requests | Runs FluentValidation validators |
| 2 | `UnitOfWorkPipelineBehavior` | Commands only | Auto-commits via `IUnitOfWork.SaveChangesAsync()` |

### ValidationPipelineBehavior

Collects all `IValidator<TRequest>` instances and runs them before the handler executes. If validation fails, it returns a `BusinessResult.Failure` with validation errors — the handler never runs.

```csharp
public class ValidationPipelineBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : BusinessResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            return (TResponse)BusinessResult.ValidationFailure(failures);

        return await next();
    }
}
```

### UnitOfWorkPipelineBehavior

Only wraps `ICommand` requests. After the handler succeeds, it calls `SaveChangesAsync()` on the `IUnitOfWork` (implemented by `CleansiaDbContext`). This means handlers never call `SaveChanges` themselves.

```csharp
public class UnitOfWorkPipelineBehavior<TRequest, TResponse>(
    IUnitOfWork unitOfWork
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand
    where TResponse : BusinessResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var response = await next();

        if (response.IsSuccess)
            await unitOfWork.SaveChangesAsync(ct);

        return response;
    }
}
```

::: warning Important
Handlers for commands should **never** call `SaveChangesAsync()` directly. The pipeline behavior handles this automatically, ensuring consistent transaction boundaries.
:::

## Controller Base Class

All API controllers inherit from `CleansiaApiController`, which provides the `HandleResult` pattern for converting `BusinessResult` into appropriate HTTP responses.

```csharp
[ApiController]
public abstract class CleansiaApiController : ControllerBase
{
    private ISender _sender = null!;
    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IActionResult HandleResult(BusinessResult result)
    {
        if (result.IsSuccess)
            return Ok();

        return result.Error!.Type switch
        {
            ErrorType.Validation => BadRequest(result.Error),
            ErrorType.NotFound => NotFound(result.Error),
            ErrorType.Conflict => Conflict(result.Error),
            ErrorType.Forbidden => Forbid(),
            ErrorType.Unauthorized => Unauthorized(result.Error),
            _ => StatusCode(500, result.Error)
        };
    }

    protected IActionResult HandleResult<T>(BusinessResult<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return HandleResult((BusinessResult)result);
    }
}
```

### Example Controller

```csharp
[Route("api/orders")]
public class OrdersController : CleansiaApiController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrder.Command command)
        => HandleResult(await Sender.Send(command));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
        => HandleResult(await Sender.Send(new GetOrderById.Query(id)));
}
```

## Authentication and Authorization

### JWT Configuration

All APIs use JWT bearer authentication with tokens issued by the platform. Each API project configures its own auth policies based on its audience.

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetimeExpiration = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
        };
    });
```

### Rate Limiting

APIs use the built-in .NET rate limiting middleware with a fixed window policy:

```csharp
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("default", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 100;
        opt.QueueLimit = 10;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

### CORS

Each API configures CORS for its specific frontend origin:

| API | Allowed Origin |
|-----|---------------|
| Partner API | `https://partner.cleansia.cz` |
| Admin API | `https://admin.cleansia.cz` |
| Mobile API | All origins (mobile apps) |
| Customer API | `https://cleansia.cz` |

## Request Logging Middleware

A custom middleware logs every HTTP request with timing, user identity, and tenant context:

```csharp
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [User: {User}, Tenant: {Tenant}]",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.User.Identity?.Name ?? "anonymous",
                context.User.FindFirst("tenant_id")?.Value ?? "none");
        }
    }
}
```

## Shared Configuration (`Cleansia.Config`)

The `Cleansia.Config` project provides extension methods that all 4 API projects call during startup. This ensures consistent configuration across all hosts:

```csharp
// In each API's Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();          // .NET Aspire defaults
builder.AddCleansiaInfrastructure();   // DbContext, repositories, blob/queue clients
builder.AddCleansiaAuth();             // JWT, authorization policies
builder.AddCleansiaCors();             // CORS per API
builder.AddCleansiaMediator();         // MediatR + pipeline behaviors + validators

var app = builder.Build();

app.UseCleansiaMiddleware();           // Request logging, error handling
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

::: tip NSwag API Client Generation
The Angular frontends use NSwag to auto-generate TypeScript API clients from the backend OpenAPI specs. When you add or modify an endpoint, regenerate the client with `npm run generate:api` from the frontend workspace.
:::
