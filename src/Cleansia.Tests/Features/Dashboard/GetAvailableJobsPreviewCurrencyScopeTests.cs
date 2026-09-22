using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Features.Dashboard;
using Cleansia.Core.AppServices.Features.Dashboard.DTOs;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Dashboard;

/// <summary>
/// The hero card is one cleaner's board, in one currency: the one they are paid in. It used to choose
/// its top-N by a cross-currency price sort and only then scope the headline, so a EUR cleaner facing
/// five 3 000 CZK jobs and one 150 EUR job saw "6 jobs" and no "earn up to" at all — the five taken rows
/// were all in a currency the headline could not add. Now the board itself admits only the cleaner's
/// currency (<c>OrderVisibility.PayableTo</c>) and the top-N is chosen inside it.
/// </summary>
public class GetAvailableJobsPreviewCurrencyScopeTests
{
    private const string EmployeeId = "emp-preview-eur";
    private const string ServiceId = "service-preview-scope";
    private const string CzkId = "currency-czk-scope";
    private const string EurId = "currency-eur-scope";
    private const decimal EurPayPerJob = 13m;

    [Fact]
    public async Task The_Board_Is_Chosen_Inside_The_Cleaners_Currency()
    {
        var service = Service.Create("category-scope", "Deep clean", "Payable", 120);
        service.Id = ServiceId;

        var orders = Enumerable.Range(0, 5)
            .Select(index => NewOfferableOrder($"order-czk-{index}", CzkId, totalPrice: 3000m, service))
            .Append(NewOfferableOrder("order-eur-0", EurId, totalPrice: 150m, service))
            .ToList();

        var handler = CreateHandler(orders, paidIn: EurId);

        var result = await (Task<BusinessResult<AvailableJobsPreviewResponse>>)HandleMethod.Invoke(
            handler, [new GetAvailableJobsPreview.Query(Limit: 5), CancellationToken.None])!;

        Assert.True(result.IsSuccess);
        var response = result.Value!;
        var job = Assert.Single(response.Jobs);
        Assert.Equal("order-eur-0", job.Id);
        Assert.Equal(1, response.TotalAvailableCount);
        Assert.Equal(EurPayPerJob, response.TotalPotentialEarnings);
    }

    // The handler is internal, as every query handler in this folder is; the dashboard sibling test
    // reaches its (public) handler the same Activator way.
    private static readonly Type HandlerType =
        typeof(GetAvailableJobsPreview).GetNestedType("Handler", BindingFlags.NonPublic)!;

    private static readonly MethodInfo HandleMethod = HandlerType.GetMethod("Handle")!;

    private static object CreateHandler(IReadOnlyList<Order> orders, string paidIn)
    {
        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(r => r.GetQueryable()).Returns(orders.AsQueryable().BuildMock());
        orderRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Order, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Order, bool>> predicate, CancellationToken _) => orders.Count(predicate.Compile()));

        var payConfigs = new Mock<IEmployeePayConfigRepository>();
        payConfigs
            .Setup(r => r.GetServiceConfigsForOrderAsync(
                It.IsAny<IEnumerable<string>>(), EmployeeId, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                EmployeePayConfig.CreateForService(ServiceId, 337m, CzkId),
                EmployeePayConfig.CreateForService(ServiceId, EurPayPerJob, EurId),
            ]);

        var accessService = new Mock<IOrderAccessService>();
        accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);

        var currency = Currency.Create(paidIn == EurId ? "EUR" : "CZK", paidIn, paidIn);
        currency.Id = paidIn;
        var resolver = new Mock<ICurrencyResolutionService>();
        resolver
            .Setup(s => s.ResolveCurrencyForEmployeeAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currency);

        return Activator.CreateInstance(
            HandlerType,
            orderRepository.Object,
            payConfigs.Object,
            accessService.Object,
            resolver.Object)!;
    }

    private static Order NewOfferableOrder(string id, string currencyId, decimal totalPrice, Service service)
    {
        var order = Order.Create(
            customerName: "Preview Customer",
            customerEmail: "preview@cleansia.test",
            customerPhone: "+420777111666",
            customerAddress: Address.Create("Vinohradska 12", "Praha", "12000", "cz"),
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(2),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currencyId,
            paymentStatus: PaymentStatus.Paid);
        order.Id = id;
        order.SetMaxEmployees(1);
        order.AddOrderStatus(OrderStatusTrack.Create(OrderStatus.New, order));
        order.AddSelectedServices([OrderLineMockFactory.ServiceLine(order, service)]);
        return order;
    }
}
