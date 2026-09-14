using Cleansia.Core.AppServices.Features.Auditing;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Services;

/// <summary>
/// What the incident file service selects and prints, over mocked repositories: scoped to an order the
/// customer arm is the subject's own rows and the guest rows on it — never a stranger's, whose id and
/// request context would otherwise leave the platform in a document about someone else; the proven-order
/// cap keeps the NEWEST acts, as the timeline's cap does; and a US/CA address prints its state. The
/// real-Postgres leg is <c>Cleansia.IntegrationTests/Features/Gdpr/IncidentFileTests</c>.
/// </summary>
public sealed class IncidentFileServiceTests
{
    private const string SubjectId = "user-subject";
    private const string StrangerId = "user-stranger";
    private const string OrderId = "order-1";
    private const string OtherOrderId = "order-2";
    private const string AdminEmail = "admin@cleansia.test";

    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Currency Czk = Currency.Create("CZK", "Kč", "Czech koruna");

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<IRefundRepository> _refunds = new();
    private readonly Mock<IDisputeRepository> _disputes = new();
    private readonly Mock<IUserConsentRepository> _consents = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<ICustomerActionAuditRepository> _customer = new();
    private readonly Mock<IAdminActionAuditRepository> _admin = new();
    private readonly Mock<IEmployeeActionAuditRepository> _employee = new();

    public IncidentFileServiceTests()
    {
        var subject = User.CreateWithPassword("subject@cleansia.test", "Seed-Password-123", "Inci", "Dent");
        subject.Id = SubjectId;
        _users.Setup(r => r.GetQueryable()).Returns(new[] { subject }.AsQueryable().BuildMock());
        _currencies.Setup(r => r.GetQueryable()).Returns(new[] { Czk }.AsQueryable().BuildMock());
        _refunds.Setup(r => r.GetQueryable()).Returns(Array.Empty<Refund>().AsQueryable().BuildMock());
        _disputes.Setup(r => r.GetQueryable()).Returns(Array.Empty<Dispute>().AsQueryable().BuildMock());
        _consents.Setup(r => r.GetQueryable()).Returns(Array.Empty<UserConsent>().AsQueryable().BuildMock());
        _admin.Setup(r => r.GetQueryable()).Returns(Array.Empty<AdminActionAudit>().AsQueryable().BuildMock());
        _employee.Setup(r => r.GetQueryable()).Returns(Array.Empty<EmployeeActionAudit>().AsQueryable().BuildMock());
        SeedOrders(NewOrder(OrderId, SubjectId), NewOrder(OtherOrderId, SubjectId));
        SeedCustomerRows();
    }

    [Fact]
    public async Task Scoped_To_An_Order_The_Customer_Arm_Is_The_Subjects_And_Guest_Rows_On_It_Never_A_Strangers()
    {
        SeedCustomerRows(
            CustomerRow("subject-booking", SubjectId, OrderId, "customer.order.create", T0, success: true),
            CustomerRow("guest-booking", userId: null, OrderId, "customer.order.create", T0.AddHours(-1), success: true),
            CustomerRow("stranger-probe", StrangerId, OrderId, "customer.order.cancel", T0.AddHours(2), success: false),
            CustomerRow("subject-elsewhere", SubjectId, OtherOrderId, "customer.order.cancel", T0.AddHours(3), success: true),
            CustomerRow("stranger-elsewhere", StrangerId, OtherOrderId, "customer.order.create", T0.AddHours(4), success: true));

        var data = await Service().BuildAsync(SubjectId, OrderId, AdminEmail, CancellationToken.None);

        var customerRows = data.Trail.Where(e => e.Source == IncidentFileService.CustomerSource).ToList();
        Assert.Equal(2, customerRows.Count);
        Assert.Contains(customerRows, e => e.ActorId == SubjectId && e.ResourceId == OrderId);
        Assert.Contains(customerRows, e => e.ActorId is null && e.ActorRole == "Guest" && e.ResourceId == OrderId);
        Assert.DoesNotContain(data.Trail, e => e.ActorId == StrangerId);
        Assert.DoesNotContain(data.Trail, e => e.ResourceId == OtherOrderId);
    }

    [Fact]
    public async Task Unscoped_The_Customer_Arm_Is_The_Subjects_Own_Rows_Only()
    {
        SeedCustomerRows(
            CustomerRow("subject-booking", SubjectId, OrderId, "customer.order.create", T0, success: true),
            CustomerRow("guest-booking", userId: null, OrderId, "customer.order.create", T0.AddHours(-1), success: true),
            CustomerRow("stranger-probe", StrangerId, OrderId, "customer.order.cancel", T0.AddHours(2), success: false));

        var data = await Service().BuildAsync(SubjectId, null, AdminEmail, CancellationToken.None);

        var row = Assert.Single(data.Trail, e => e.Source == IncidentFileService.CustomerSource);
        Assert.Equal(SubjectId, row.ActorId);
    }

    [Fact]
    public async Task The_Proven_Order_Cap_Keeps_The_Newest_Acts_And_Cuts_The_Oldest()
    {
        // Every order here no longer names the subject (erased); only the subject's own successful acts
        // reach them. One more distinct order than the cap, enumerated oldest first, so an unordered cut
        // would drop the newest act rather than the oldest.
        var count = GetActionTimeline.RecentResourceCap + 1;
        var rows = Enumerable.Range(0, count)
            .Select(i => CustomerRow($"row-{i}", SubjectId, $"order-proven-{i}", "customer.order.create", T0.AddMinutes(i), success: true))
            .ToArray();
        var oldest = NewOrder("order-proven-0", SubjectId).AnonymizeCustomerData();
        var newest = NewOrder($"order-proven-{count - 1}", SubjectId).AnonymizeCustomerData();
        SeedOrders(oldest, newest);
        SeedCustomerRows(rows);

        var service = Service();

        Assert.True(await service.IsSubjectOrderAsync(SubjectId, newest.Id, CancellationToken.None));
        Assert.False(await service.IsSubjectOrderAsync(SubjectId, oldest.Id, CancellationToken.None));
    }

    [Fact]
    public async Task A_Refused_Act_Proves_Nothing()
    {
        SeedOrders(NewOrder(OrderId, SubjectId).AnonymizeCustomerData());
        SeedCustomerRows(CustomerRow("refused", SubjectId, OrderId, "customer.order.cancel", T0, success: false));

        Assert.False(await Service().IsSubjectOrderAsync(SubjectId, OrderId, CancellationToken.None));
    }

    [Fact]
    public async Task The_Address_Prints_Its_State_When_The_Market_Has_One()
    {
        SeedOrders(NewOrder(OrderId, SubjectId, Address.Create("1 Main St", "Austin", "73301", "US", state: "TX")));

        var data = await Service().BuildAsync(SubjectId, null, AdminEmail, CancellationToken.None);

        Assert.Equal("1 Main St, 73301, Austin, TX, US", Assert.Single(data.Orders).Address);
    }

    [Fact]
    public async Task An_Address_Without_A_State_Prints_No_Gap()
    {
        SeedOrders(NewOrder(OrderId, SubjectId));

        var data = await Service().BuildAsync(SubjectId, null, AdminEmail, CancellationToken.None);

        Assert.Equal("Testovaci 12, 11000, Praha, CZ", Assert.Single(data.Orders).Address);
    }

    private IncidentFileService Service() =>
        new(_users.Object, _orders.Object, _refunds.Object, _disputes.Object, _consents.Object, _currencies.Object,
            _customer.Object, _admin.Object, _employee.Object);

    private void SeedOrders(params Order[] orders) =>
        _orders.Setup(r => r.GetQueryable()).Returns(orders.AsQueryable().BuildMock());

    private void SeedCustomerRows(params CustomerActionAudit[] rows) =>
        _customer.Setup(r => r.GetQueryable()).Returns(rows.AsQueryable().BuildMock());

    private static Order NewOrder(string id, string userId, Address? address = null) =>
        OrderMockFactory.Generate(
            new OrderMockFactory.OrderPartial
            {
                Id = id,
                UserId = userId,
                CustomerAddress = address ?? Address.Create("Testovaci 12", "Praha", "11000", "CZ"),
            },
            currency: Czk);

    private static CustomerActionAudit CustomerRow(string id, string? userId, string orderId, string action, DateTimeOffset occurredOn, bool success)
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: "cleansia.customer", ipAddress: "203.0.113.9", deviceLabel: "iPhone 15", deviceId: "device-1",
            action: action, resourceType: nameof(Order), resourceId: orderId, success: success, errorCode: success ? null : "order.not_found",
            payloadJson: null, correlationId: null);
        row.Id = id;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!.SetValue(row, occurredOn);
        return row;
    }
}
