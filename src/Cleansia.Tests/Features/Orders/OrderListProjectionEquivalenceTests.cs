using System.Reflection;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The order LIST queries moved from materializing the full entity graph
/// (Include x7 + <c>MapToDto(Order)</c>) onto the server-side
/// <see cref="OrderMappers.SelectOrderListRows"/> projection. These tests pin that, over the same
/// seeded rows, the projection + row mapper produce <see cref="OrderListItem"/>s
/// JSON-identical to the old entity path (which is reconstructed here with the handlers'
/// previous Include set), including the edge rows: a pre-backfill NULL
/// <c>Orders.CurrentStatus</c> falling back to the history rule (with a same-tick Sequence
/// tie), an order with no services/packages/assignees, and a cancelled order.
/// </summary>
public sealed class OrderListProjectionEquivalenceTests : IAsyncLifetime, IDisposable
{
    private const string CustomerUserId = "user-proj-customer";

    private readonly SqliteConnection _connection;

    public OrderListProjectionEquivalenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(tenantId: null));

    public async Task InitializeAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var currency = Currency.Create("CZK", "Kč", "Czech koruna", 1.0m);
        currency.Id = "cur-proj-czk";
        currency.SetAsDefault(true);

        var category = ServiceCategory.Create("deep-clean", "Deep cleaning", "Thorough cleaning", displayOrder: 2);
        category.Id = "cat-proj-1";
        category.SetTranslation("cs", "Hloubkový úklid", "Důkladný úklid");
        ctx.Add(category);

        var serviceOne = Service.Create(category.Id, "Windows", "Window cleaning", 300m, 50m, estimatedTime: 60);
        serviceOne.Id = "svc-proj-1";
        serviceOne.SetTranslation("cs", "Okna", "Mytí oken");
        serviceOne.SetTranslation("uk", "Вікна", "Миття вікон");
        var serviceTwo = Service.Create(category.Id, "Fridge", "Fridge cleaning", 200m, 0m, estimatedTime: 30);
        serviceTwo.Id = "svc-proj-2";
        ctx.Add(serviceOne);
        ctx.Add(serviceTwo);

        var package = Package.Create("Move-out", "Full move-out bundle", 2500m);
        package.Id = "pkg-proj-1";
        package.SetTranslation("cs", "Stěhování", "Kompletní balíček");
        ctx.Add(package);
        // A real bundle row exists in the DB, but the list queries never load it — the old
        // entity path emitted an EMPTY PackageListItem.IncludedServices and the projection
        // must preserve that.
        ctx.Add(PackageService.Create(package, serviceOne));

        var employee = NewEmployee("emp-proj-1", "user-proj-emp-1", "cleaner-proj@cleansia.test", "Anna", "Aslan");
        ctx.Add(employee);

        var stamp = DateTimeOffset.UtcNow.AddDays(-2);

        var full = NewOrder(
            "proj-full",
            address: Address.Create("Main St 1", "Praha", "14000", "cz", latitude: 50.05, longitude: 14.41),
            extras: new Dictionary<string, bool> { ["windows"] = true, ["fridge"] = false },
            promoDiscountAmount: 150m);
        full.SetCurrency(currency);
        full.AddSelectedServices(new[] { OrderService.Create(full, serviceOne), OrderService.Create(full, serviceTwo) });
        full.AddSelectedPackages(new[] { OrderPackage.Create(full, package) });
        full.SetMaxEmployees(2);
        full.AddAssignedEmployee(OrderEmployee.Create(full, employee));
        AppendTrack(full, OrderStatus.New, stamp);
        AppendTrack(full, OrderStatus.Confirmed, stamp.AddHours(1));
        AppendTrack(full, OrderStatus.InProgress, stamp.AddHours(2));
        ctx.Add(full);

        // Same-tick Sequence tie so the NULL-column fallback subquery must apply the full
        // CreatedOn-desc-then-Sequence-desc rule (→ Completed); the column is NULLed after commit.
        var legacy = NewOrder(
            "proj-legacy-null",
            address: Address.Create("Old St 2", "Brno", "60200", "cz", latitude: 49.19, longitude: 16.60),
            extras: new Dictionary<string, bool>(),
            tierDiscountAmount: 90m);
        legacy.SetCurrency(currency);
        var tie = stamp.AddHours(3);
        AppendTrack(legacy, OrderStatus.New, stamp);
        AppendTrack(legacy, OrderStatus.InProgress, tie);
        AppendTrack(legacy, OrderStatus.Completed, tie);
        ctx.Add(legacy);

        var bare = NewOrder(
            "proj-bare",
            address: Address.Create("Bare St 3", "Ostrava", "70200", "cz"),
            extras: new Dictionary<string, bool>(),
            tierDiscountAmount: 40m,
            membershipDiscountAmount: 60m);
        bare.SetCurrency(currency);
        AppendTrack(bare, OrderStatus.New, stamp.AddHours(4));
        ctx.Add(bare);

        var cancelled = NewOrder(
            "proj-cancelled",
            address: Address.Create("Gone St 4", "Praha", "11000", "cz"),
            extras: new Dictionary<string, bool> { ["balcony"] = true });
        cancelled.SetCurrency(currency);
        AppendTrack(cancelled, OrderStatus.New, stamp.AddHours(5));
        AppendTrack(cancelled, OrderStatus.Confirmed, stamp.AddHours(6));
        AppendTrack(cancelled, OrderStatus.Cancelled, stamp.AddHours(7));
        ctx.Add(cancelled);

        await ctx.CommitAsync(CancellationToken.None);

        await using var nullCtx = NewContext();
        await nullCtx.Database.ExecuteSqlRawAsync(
            "UPDATE \"Orders\" SET \"CurrentStatus\" = NULL WHERE \"Id\" = {0}", "proj-legacy-null");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Projection_Produces_Dtos_Identical_To_The_Entity_Mapper_Path()
    {
        var expectedById = (await OldEntityPathAsync()).ToDictionary(d => d.Id);

        await using var ctx = NewContext();
        var actual = (await ctx.Set<Order>()
            .SelectOrderListRows()
            .AsSplitQuery()
            .ToListAsync(CancellationToken.None))
            .Select(row => row.MapToDto())
            .ToList();

        Assert.Equal(expectedById.Count, actual.Count);
        foreach (var dto in actual)
        {
            var expected = expectedById[dto.Id];
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(dto));
        }
    }

    [Fact]
    public async Task GetCustomerOrders_Handler_Returns_The_Same_Dtos_As_The_Entity_Path()
    {
        var expectedById = (await OldEntityPathAsync()).ToDictionary(d => d.Id);

        await using var ctx = NewContext();
        var handlerType = typeof(GetCustomerOrders).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = (IRequestHandler<GetCustomerOrders.Request, PagedData<OrderListItem>>)Activator.CreateInstance(
            handlerType,
            new OrderRepository(ctx),
            new TestUserSessionProvider(CustomerUserId, "customer-proj@cleansia.test"))!;

        var page = await handler.Handle(new GetCustomerOrders.Request(), CancellationToken.None);

        Assert.Equal(expectedById.Count, page.Total);
        foreach (var dto in page.Data!)
        {
            var expected = expectedById[dto.Id];
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(dto));
        }
    }

    [Fact]
    public async Task Null_CurrentStatus_Row_Falls_Back_To_The_History_Rule_With_Sequence_Tiebreak()
    {
        await using var ctx = NewContext();
        var row = (await ctx.Set<Order>()
            .Where(o => o.Id == "proj-legacy-null")
            .SelectOrderListRows()
            .ToListAsync(CancellationToken.None))
            .Single();

        Assert.Equal(OrderStatus.Completed, row.OrderStatus);
        Assert.Equal(nameof(OrderStatus.Completed), row.MapToDto().OrderStatus.Name);
    }

    /// <summary>
    /// The pre-refactor read path, verbatim: the handlers' old Include set + the entity mapper.
    /// </summary>
    private async Task<List<OrderListItem>> OldEntityPathAsync()
    {
        await using var ctx = NewContext();
        var orders = await ctx.Set<Order>()
            .Include(o => o.OrderStatusHistory)
            .Include(o => o.Currency)
            .Include(o => o.SelectedPackages)
                .ThenInclude(sp => sp.Package)
            .Include(o => o.SelectedServices)
                .ThenInclude(sp => sp.Service)
                    .ThenInclude(s => s!.Category)
            .Include(o => o.CustomerAddress)
            .Include(o => o.AssignedEmployees)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        return orders.Select(o => o.MapToDto()).ToList();
    }

    private static Order NewOrder(
        string orderId,
        Address address,
        Dictionary<string, bool> extras,
        decimal? tierDiscountAmount = null,
        decimal? promoDiscountAmount = null,
        decimal? membershipDiscountAmount = null)
    {
        var order = Order.Create(
            customerName: "Projection Customer",
            customerEmail: "customer-proj@cleansia.test",
            customerPhone: "+420777000111",
            customerAddress: address,
            rooms: 3,
            bathrooms: 2,
            extras: extras,
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1800m,
            currencyId: "cur-proj-czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerUserId,
            tierDiscountAmount: tierDiscountAmount,
            promoDiscountAmount: promoDiscountAmount,
            membershipDiscountAmount: membershipDiscountAmount);
        order.Id = orderId;
        order.Created("system", DateTimeOffset.UtcNow.AddDays(-3));
        return order;
    }

    private static Employee NewEmployee(string employeeId, string userId, string email, string first, string last)
    {
        var user = User.CreateWithPassword(email, "Test-password-1!", first, last, UserProfile.Employee);
        user.Id = userId;
        user.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        var employee = Employee.CreateWithUser(user);
        employee.Id = employeeId;
        employee.Created("system", DateTimeOffset.UtcNow.AddDays(-10));
        return employee;
    }

    private static void AppendTrack(Order order, OrderStatus status, DateTimeOffset createdOn)
    {
        var track = OrderStatusTrack.Create(status, order);
        track.Created("system", createdOn);
        order.AddOrderStatus(track);
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
