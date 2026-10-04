using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Features.TenantSettings;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Owner ruling 2026-10-03, on the cleaner's order list: the crew's row shows the reward its seat's contract
/// for work was priced at, 900, even after the cleaner's rate has moved to 700; a cleaner browsing the job is
/// offered today's 700. The list runs the real row projection, so the frozen figures must survive it, and so
/// must the extras' prices the browsing cleaner's quote takes the company's share of.
/// </summary>
public sealed class GetPagedOrdersFrozenSeatPayTests
{
    private const string OrderId = "order-list-frozen-1";
    private const string CurrencyId = "currency-list-frozen";
    private const string CrewEmployeeId = "emp-list-frozen-crew";
    private const string BrowserEmployeeId = "emp-list-frozen-browser";

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _orderAccessService = new();
    private readonly Mock<IUserSessionProvider> _userSessionProvider = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigRepository = new();
    private readonly Mock<IOrderEmployeePayRepository> _orderEmployeePayRepository = new();
    private readonly Mock<ICurrencyResolutionService> _currencyResolutionService = new();
    private readonly Mock<IAppConfigurationProvider> _configurationProvider = new();

    private IRequestHandler<GetPagedOrders.Request, PagedData<OrderListItem>> CreateHandler()
    {
        var handlerType = typeof(GetPagedOrders).GetNestedType("Handler", BindingFlags.NonPublic)!;
        return (IRequestHandler<GetPagedOrders.Request, PagedData<OrderListItem>>)Activator.CreateInstance(
            handlerType,
            _orderRepository.Object,
            _orderAccessService.Object,
            _userSessionProvider.Object,
            _payConfigRepository.Object,
            _orderEmployeePayRepository.Object,
            _currencyResolutionService.Object,
            Mock.Of<IServiceScopeFactory>(),
            _configurationProvider.Object,
            Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(handlerType)))!;
    }

    [Theory]
    [InlineData(CrewEmployeeId, 900)]
    [InlineData(BrowserEmployeeId, 700)]
    public async Task A_Held_Seat_Lists_Its_Contract_Reward_And_A_Browsing_Cleaner_The_Live_Rate(
        string caller, int expectedPay)
    {
        var page = await ListAs(caller, extrasPrice: null);

        var row = Assert.Single(page.Data!);
        Assert.Equal((decimal)expectedPay, row.EstimatedCleanerPay);
    }

    /// <summary>
    /// 300 of extras at the company's 40 % share: the browsing cleaner is offered 700 + 120 = 820, what the
    /// contract for work would state; the crew keeps its frozen 900.
    /// </summary>
    [Theory]
    [InlineData(CrewEmployeeId, 900)]
    [InlineData(BrowserEmployeeId, 820)]
    public async Task A_Browsing_Cleaner_Is_Offered_The_Companys_Share_Of_The_Extras_Booked(
        string caller, int expectedPay)
    {
        _configurationProvider
            .Setup(c => c.GetTenantSettingAsync(TenantSettingCatalog.ExtrasSharePercentKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync("40");

        var page = await ListAs(caller, extrasPrice: 300m);

        var row = Assert.Single(page.Data!);
        Assert.Equal((decimal)expectedPay, row.EstimatedCleanerPay);
    }

    private async Task<PagedData<OrderListItem>> ListAs(string caller, decimal? extrasPrice)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech koruna");
        currency.Id = CurrencyId;
        var order = OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial { Id = OrderId, CurrentStatus = OrderStatus.Confirmed },
            currency: currency);
        order.SetMaxEmployees(2);

        var service = Service.Create("cat-1", "Standard clean", "Regular");
        typeof(Service).GetProperty(nameof(Service.Category))!
            .SetValue(service, ServiceCategory.Create("standard", "Standard", "d"));
        order.AddSelectedServices([OrderService.Create(order, service, 1000m, 0m, 1000m)]);

        var user = User.CreateWithPassword("crew-list@example.com", "x", "Petra", "Svobodova");
        user.Id = $"{CrewEmployeeId}-user";
        var employee = Employee.CreateWithUser(user);
        employee.Id = CrewEmployeeId;
        var seat = OrderEmployee.Create(order, employee);
        order.AddAssignedEmployee(seat);
        seat.FreezeJobPay((900m, 0m, 0m, 0m));
        if (extrasPrice is { } price)
        {
            order.AddSelectedExtras([OrderExtra.Create(order, Extra.Create("windows", "Windows", null), price)]);
        }

        _userSessionProvider
            .Setup(s => s.GetTypedUserClaim(ClaimTypes.Role))
            .Returns(new Claim(ClaimTypes.Role, UserProfile.Employee.ToString()));
        _orderAccessService.Setup(a => a.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _currencyResolutionService
            .Setup(s => s.ResolveCurrencyForEmployeeAsync(caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);
        _orderRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _orderRepository
            .Setup(r => r.GetPagedSort<OrderSort>(
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Expression<Func<Order, bool>>>(),
                It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _payConfigRepository
            .Setup(r => r.GetServiceConfigsForOrderAsync(It.IsAny<IEnumerable<string>>(), caller, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([EmployeePayConfig.CreateForService(service.Id, 700m, CurrencyId)]);
        _payConfigRepository
            .Setup(r => r.GetPackageConfigsForOrderAsync(It.IsAny<IEnumerable<string>>(), caller, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _orderEmployeePayRepository
            .Setup(r => r.GetTotalPayByOrderIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), caller, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, decimal>());

        return await CreateHandler().Handle(new GetPagedOrders.Request(), CancellationToken.None);
    }
}
