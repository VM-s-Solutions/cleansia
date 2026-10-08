using Cleansia.TestUtilities.MockDataFactories.Orders;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.Orders.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RequestSort = Cleansia.Core.AppServices.Shared.DTOs.Sorting.SortDefinition;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The order LIST queries moved from materializing the full entity graph
/// (Include x7 + <c>MapToDto(Order)</c>) onto the server-side
/// <see cref="OrderMappers.SelectOrderListRows"/> projection. These tests pin that, over the same
/// seeded rows, the projection + row mapper produce <see cref="OrderListItem"/>s
/// JSON-identical to the old entity path (which is reconstructed here with the handlers'
/// previous Include set), including the edge rows: an order with no
/// services/packages/assignees, and a cancelled order.
/// </summary>
public sealed class OrderListProjectionEquivalenceTests : IAsyncLifetime, IDisposable
{
    private const string CustomerUserId = "user-proj-customer";
    private const decimal EarlierHeavyRate = 0.60m;

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

    private CleansiaDbContext NewContext(Action<string>? log = null)
    {
        var options = new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection);
        if (log is not null)
        {
            options.LogTo(log, [RelationalEventId.CommandExecuted]);
        }

        return new(
            options.Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(TestTenants.Default));
    }

    public async Task InitializeAsync()
    {
        await using var ctx = NewContext();
        await ctx.Database.EnsureCreatedAsync();

        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = "cur-proj-czk";
        currency.SetAsDefault(true);

        var category = ServiceCategory.Create("deep-clean", "Deep cleaning", "Thorough cleaning", displayOrder: 2);
        category.Id = "cat-proj-1";
        category.SetTranslation("cs", "Hloubkový úklid", "Důkladný úklid");
        ctx.Add(category);

        var serviceOne = Service.Create(category.Id, "Windows", "Window cleaning", estimatedTime: 60);
        serviceOne.Id = "svc-proj-1";
        serviceOne.SetTranslation("cs", "Okna", "Mytí oken");
        serviceOne.SetTranslation("uk", "Вікна", "Миття вікон");
        var serviceTwo = Service.Create(category.Id, "Fridge", "Fridge cleaning", estimatedTime: 30);
        serviceTwo.Id = "svc-proj-2";
        ctx.Add(serviceOne);
        ctx.Add(serviceTwo);

        var package = Package.Create("Move-out", "Full move-out bundle");
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
            // One row, not two: "fridge = false" was an extra the customer did NOT take, and the row
            // model cannot express that — absence is how it is said now.
            extras: [("windows", 150m)],
            promoDiscountAmount: 150m);
        full.SetCurrency(currency);
        full.AddSelectedServices(new[]
        {
            OrderLineMockFactory.ServiceLine(full, serviceOne),
            OrderLineMockFactory.ServiceLine(full, serviceTwo)
        });
        full.AddSelectedPackages(new[] { OrderLineMockFactory.PackageLine(full, package) });
        full.SetMaxEmployees(2);
        full.SetDirtinessSurcharge(DirtinessLevel.Heavy, 540.30m, EarlierHeavyRate);
        full.AddAssignedEmployee(OrderEmployee.Create(full, employee));
        AppendTrack(full, OrderStatus.New, stamp);
        AppendTrack(full, OrderStatus.Confirmed, stamp.AddHours(1));
        AppendTrack(full, OrderStatus.InProgress, stamp.AddHours(2));
        ctx.Add(full);

        // Same-tick Sequence tie so the NULL-column fallback subquery must apply the full
        // CreatedOn-desc-then-Sequence-desc rule (→ Completed); the column is NULLed after commit.
        var legacy = NewOrder(
            "proj-legacy-null",
            extras: [],
            address: Address.Create("Old St 2", "Brno", "60200", "cz", latitude: 49.19, longitude: 16.60),
            tierDiscountAmount: 90m);
        legacy.SetCurrency(currency);
        var tie = stamp.AddHours(3);
        AppendTrack(legacy, OrderStatus.New, stamp);
        AppendTrack(legacy, OrderStatus.InProgress, tie);
        AppendTrack(legacy, OrderStatus.Completed, tie);
        ctx.Add(legacy);

        var bare = NewOrder(
            "proj-bare",
            extras: [],
            address: Address.Create("Bare St 3", "Ostrava", "70200", "cz"),
            tierDiscountAmount: 40m,
            membershipDiscountAmount: 60m);
        bare.SetCurrency(currency);
        AppendTrack(bare, OrderStatus.New, stamp.AddHours(4));
        ctx.Add(bare);

        var cancelled = NewOrder(
            "proj-cancelled",
            address: Address.Create("Gone St 4", "Praha", "11000", "cz"),
            extras: [("balcony", 90m)]);
        cancelled.SetCurrency(currency);
        AppendTrack(cancelled, OrderStatus.New, stamp.AddHours(5));
        AppendTrack(cancelled, OrderStatus.Confirmed, stamp.AddHours(6));
        AppendTrack(cancelled, OrderStatus.Cancelled, stamp.AddHours(7));
        ctx.Add(cancelled);

        await ctx.CommitAsync(CancellationToken.None);
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Order_Pages_Default_To_Newest_Then_Unique_Id_Before_Paging(bool admin, bool emptySort)
    {
        await StampPagingFixture();

        var first = await ReadOrderPage(admin, offset: 0, sort: emptySort ? [] : null);
        var second = await ReadOrderPage(admin, offset: 2, sort: emptySort ? [] : null);

        Assert.Equal(new[] { "proj-legacy-null", "proj-full" }, first.Data.Select(row => row.Id));
        Assert.Equal(new[] { "proj-cancelled", "proj-bare" }, second.Data.Select(row => row.Id));
        Assert.Equal(4, first.Total);
        Assert.Equal(2, second.PageNumber);
        Assert.Empty(first.Data.Select(row => row.Id).Intersect(second.Data.Select(row => row.Id)));
    }

    [Fact]
    public async Task Split_Order_List_Projection_Selects_The_Ordered_Page_Only_Once()
    {
        await StampPagingFixture();
        var commands = new List<string>();

        var page = await ReadOrderPage(true, 0, null, log: commands.Add);

        Assert.Equal(new[] { "proj-legacy-null", "proj-full" }, page.Data.Select(row => row.Id));
        Assert.Equal(4, page.Total);
        Assert.Contains(commands, sql => sql.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, sql => sql.Contains("\"OrderServices\"", StringComparison.Ordinal));
        Assert.Contains(commands, sql => sql.Contains("\"OrderEmployees\"", StringComparison.Ordinal));
        var globalPageSelectors = commands.Where(sql =>
            sql.Contains("FROM \"Orders\"", StringComparison.Ordinal)
            && sql.Contains("LIMIT", StringComparison.Ordinal)
            && sql.Contains("ORDER BY", StringComparison.Ordinal)).ToList();
        Assert.Single(globalPageSelectors);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(79980)]
    public async Task Empty_Order_Page_Keeps_The_Count_Without_Loading_Split_Collections(int offset)
    {
        await StampPagingFixture();
        var commands = new List<string>();

        var page = await ReadOrderPage(true, offset, null, log: commands.Add);

        Assert.Empty(page.Data);
        Assert.Equal(4, page.Total);
        Assert.Equal(offset / 2 + 1, page.PageNumber);
        Assert.Equal(2, commands.Count(sql => sql.Contains("SELECT", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Order_Projection_Revalidates_Filter_And_Tenant_After_Selecting_Page_Ids(bool moveTenant)
    {
        await StampPagingFixture();
        Mock<OrderRepository>? repository = null;
        var filter = new OrderFilter(
            Id: null, IsActive: true, CustomerName: null, CustomerEmail: null, CustomerPhone: null,
            DisplayOrderNumber: null, EmployeeId: null, CleaningDateFrom: null, CleaningDateTo: null,
            PaymentStatuses: null, PaymentTypes: null, MinTotalPrice: null, MaxTotalPrice: null,
            OrderStatuses: null, HasAvailableSpots: null, IsUnassigned: null, ExcludeEmployeeId: null);

        var page = await ReadOrderPage(true, 0, null, repositoryFactory: context =>
        {
            repository = new Mock<OrderRepository>(context) { CallBase = true };
            repository.Setup(r => r.GetFiltered(It.IsAny<Expression<Func<Order, bool>>>()))
                .Callback(() =>
                {
                    if (moveTenant)
                    {
                        context.Database.ExecuteSqlRaw(
                            "UPDATE \"Orders\" SET \"TenantId\" = {0} WHERE \"Id\" = {1}",
                            "other-tenant", "proj-full");
                    }
                    else
                    {
                        context.Database.ExecuteSqlRaw(
                            "UPDATE \"Orders\" SET \"IsActive\" = 0 WHERE \"Id\" = {0}", "proj-full");
                    }
                }).CallBase();
            return repository.Object;
        }, filter: filter);

        Assert.Equal(new[] { "proj-legacy-null" }, page.Data.Select(row => row.Id));
        Assert.Equal(4, page.Total);
        repository!.Verify(r => r.GetFiltered(It.IsAny<Expression<Func<Order, bool>>>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Supplied_Nonunique_Sort_Keeps_Its_Direction_And_Gains_An_Id_Tie_Breaker(bool admin)
    {
        await StampPagingFixture();

        var first = await ReadOrderPage(admin, 0, [new RequestSort("createdOn", SortDirection.Ascending)]);
        var second = await ReadOrderPage(admin, 2, [new RequestSort("createdOn", SortDirection.Ascending)]);

        Assert.Equal(new[] { "proj-cancelled", "proj-bare" }, first.Data.Select(row => row.Id));
        Assert.Equal(new[] { "proj-legacy-null", "proj-full" }, second.Data.Select(row => row.Id));
    }

    [Theory]
    [InlineData(false, SortDirection.Ascending)]
    [InlineData(false, SortDirection.Descending)]
    [InlineData(true, SortDirection.Ascending)]
    [InlineData(true, SortDirection.Descending)]
    public async Task An_Explicit_Id_Sort_Retains_Its_Requested_Direction(bool admin, SortDirection direction)
    {
        await AssertExplicitIdPaging(admin, direction, "id");
    }

    [Theory]
    [InlineData(false, "id", SortDirection.Ascending)]
    [InlineData(false, "id", SortDirection.Descending)]
    [InlineData(false, "Id", SortDirection.Ascending)]
    [InlineData(false, "Id", SortDirection.Descending)]
    [InlineData(false, "ID", SortDirection.Ascending)]
    [InlineData(false, "ID", SortDirection.Descending)]
    [InlineData(true, "id", SortDirection.Ascending)]
    [InlineData(true, "id", SortDirection.Descending)]
    [InlineData(true, "Id", SortDirection.Ascending)]
    [InlineData(true, "Id", SortDirection.Descending)]
    [InlineData(true, "ID", SortDirection.Ascending)]
    [InlineData(true, "ID", SortDirection.Descending)]
    public async Task Explicit_Id_Casing_Does_Not_Change_Order_Paging_In_Turkish_Culture(
        bool admin, string field, SortDirection direction)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            await AssertExplicitIdPaging(admin, direction, field);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Default_And_Supplied_Order_Pages_Keep_Unique_Ties_In_Turkish_Culture(
        bool admin, bool suppliedSort)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            await StampPagingFixture();
            RequestSort[]? sort = suppliedSort ? [new RequestSort("createdOn", SortDirection.Ascending)] : null;
            var first = await ReadOrderPage(admin, 0, sort);
            var second = await ReadOrderPage(admin, 2, sort);

            var expected = suppliedSort
                ? new[] { "proj-cancelled", "proj-bare", "proj-legacy-null", "proj-full" }
                : new[] { "proj-legacy-null", "proj-full", "proj-cancelled", "proj-bare" };
            Assert.Equal(expected.Take(2), first.Data.Select(row => row.Id));
            Assert.Equal(expected.Skip(2), second.Data.Select(row => row.Id));
            Assert.Empty(first.Data.Select(row => row.Id).Intersect(second.Data.Select(row => row.Id)));
            Assert.Equal(4, first.Total);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private async Task AssertExplicitIdPaging(bool admin, SortDirection direction, string field)
    {
        await StampPagingFixture(reverseTimestampGroups: true);

        var first = await ReadOrderPage(admin, 0, [new RequestSort(field, direction)]);
        var second = await ReadOrderPage(admin, 2, [new RequestSort(field, direction)]);

        var expected = new[] { "proj-bare", "proj-cancelled", "proj-full", "proj-legacy-null" };
        if (direction == SortDirection.Descending)
        {
            Array.Reverse(expected);
        }
        Assert.Equal(expected.Take(2), first.Data.Select(row => row.Id));
        Assert.Equal(expected.Skip(2), second.Data.Select(row => row.Id));
    }

    private async Task StampPagingFixture(bool reverseTimestampGroups = false)
    {
        await using var ctx = NewContext();
        var orders = await ctx.Set<Order>().Include(order => order.CustomerAddress).ToListAsync();
        var older = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        foreach (var order in orders)
        {
            var isNewer = order.Id is "proj-full" or "proj-legacy-null";
            order.Created("system", isNewer != reverseTimestampGroups ? older.AddDays(1) : older);
            // Keep this paging test on a synchronous read path without starting geocoding tasks.
            var address = order.CustomerAddress!;
            address.Update(address.Street, address.City, address.ZipCode, address.CountryId,
                latitude: 50.05, longitude: 14.41);
        }
        await ctx.CommitAsync(CancellationToken.None);
    }

    private async Task<PagedData<OrderListItem>> ReadOrderPage(
        bool admin, int offset, RequestSort[]? sort, Action<string>? log = null,
        Func<CleansiaDbContext, IOrderRepository>? repositoryFactory = null, OrderFilter? filter = null)
    {
        await using var ctx = NewContext(log);
        if (!admin)
        {
            var handlerType = typeof(GetCustomerOrders).GetNestedType("Handler", BindingFlags.NonPublic)!;
            var handler = (IRequestHandler<GetCustomerOrders.Request, PagedData<OrderListItem>>)Activator.CreateInstance(
                handlerType, new OrderRepository(ctx),
                new TestUserSessionProvider(CustomerUserId, "customer-proj@cleansia.test"))!;
            return await handler.Handle(new GetCustomerOrders.Request { Offset = offset, Limit = 2, Sort = sort }, CancellationToken.None);
        }

        var adminHandlerType = typeof(GetPagedOrders).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var adminHandler = (IRequestHandler<GetPagedOrders.Request, PagedData<OrderListItem>>)Activator.CreateInstance(
            adminHandlerType, repositoryFactory?.Invoke(ctx) ?? new OrderRepository(ctx), Mock.Of<IOrderAccessService>(),
            new TestUserSessionProvider("admin", "admin@cleansia.test", [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]),
            Mock.Of<IEmployeePayConfigRepository>(), Mock.Of<IOrderEmployeePayRepository>(),
            Mock.Of<ICurrencyResolutionService>(), Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IAppConfigurationProvider>(),
            Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(adminHandlerType)))!;
        return await adminHandler.Handle(new GetPagedOrders.Request { Offset = offset, Limit = 2, Sort = sort, Filter = filter }, CancellationToken.None);
    }

    /// <summary>
    /// The board's pay estimate reads the rate from the row, which no DTO carries, so the comparisons
    /// above cannot see it. The full order was booked at a rate that is not today's, so a projection that
    /// looked the level up in <c>BookingPolicy</c> fails here.
    /// </summary>
    [Fact]
    public async Task The_Row_Carries_The_Rate_The_Order_Was_Booked_At()
    {
        Assert.NotEqual(BookingPolicy.HeavyDirtinessSurchargeRate, EarlierHeavyRate);

        await using var ctx = NewContext();
        var rates = (await ctx.Set<Order>()
            .SelectOrderListRows()
            .AsSplitQuery()
            .ToListAsync(CancellationToken.None))
            .ToDictionary(row => row.Id, row => row.DirtinessRate);

        Assert.Equal(EarlierHeavyRate, rates["proj-full"]);
        Assert.Equal(0m, rates["proj-bare"]);
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
            // Extras became a navigation when they stopped being a JSON column on the order. Without
            // this Include the entity path returns an EMPTY extras map against a populated projection —
            // and since both sides feed the same assertion, the comparison would go quietly vacuous
            // rather than red.
            .Include(o => o.SelectedExtras)
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
        (string Slug, decimal Price)[] extras,
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
            cleaningDateTime: DateTime.UtcNow.AddDays(3),
            paymentType: PaymentType.Card,
            totalPrice: 1800m,
            currencyId: "cur-proj-czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerUserId,
            tierDiscountAmount: tierDiscountAmount,
            promoDiscountAmount: promoDiscountAmount,
            membershipDiscountAmount: membershipDiscountAmount,
            cancellationTerms: BookingPolicy.CancellationTermsAtBooking);
        order.Id = orderId;
        order.AddSelectedExtras(extras.Select(e =>
            OrderExtra.Create(order, Extra.Create(e.Slug, e.Slug, null), e.Price)));
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
