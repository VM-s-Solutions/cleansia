using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Gdpr;

/// <summary>
/// A cleaner's own deletion FILES A REQUEST; it does not erase. Ending a working relationship needs
/// signed paperwork and an in-person step, and the records that survive it — invoices, pay rows, the
/// self-billing agreement — are financial, not the subject's to delete.
///
/// <para><b>Why this file exists at all.</b> Before it, the only employee-aware guard on the
/// self-delete path was an open-invoice check, and the blocking-order check filters
/// <c>Order.UserId</c> — the CUSTOMER axis — so it never saw a seat a cleaner holds on someone
/// else's job. A cleaner could erase themselves mid-engagement, taking every
/// <c>EmployeeDocument</c> with them including <c>Contract</c> and <c>TaxDocument</c>.</para>
///
/// <para><b>The admin case is the one that matters most here.</b> The first attempt at this change
/// forked inside the service on the subject alone, and because <c>AdminDeleteUserAccount</c> calls
/// the same method it made cleaners unerasable by ANYONE — worse than the defect it fixed. The
/// deferral is now an explicit parameter, and
/// <see cref="An_Admin_Deletion_Still_Erases_A_Cleaner"/> is what stops that returning.</para>
///
/// <para>Real repositories over in-memory SQLite, mirroring <c>ErasureBlockingOrderStatusTests</c> —
/// the verdict has to come from the service, not from a re-reading of its branches.</para>
/// </summary>
public sealed class EmployeeDeletionRequestTests : IDisposable
{
    private const string CleanerUserId = "user-cleaner-del-1";
    private const string CleanerEmail = "lenka.markova@cleansia.test";
    private const string EmployeeId = "employee-del-1";
    private const string CustomerUserId = "user-customer-del-1";
    private const string CustomerEmail = "petra.svobodova@cleansia.test";

    private readonly SqliteConnection _connection;
    private readonly Mock<IBlobContainerClientFactory> _blobClientFactory = new();

    public EmployeeDeletionRequestTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = OFF;";
        pragma.ExecuteNonQuery();

        _blobClientFactory
            .Setup(f => f.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(Mock.Of<IBlobContainerClient>());
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// The deferral itself. Success is load-bearing — <c>UnitOfWorkPipelineBehavior</c> commits only
    /// on a successful <c>BusinessResult</c>, so a failure here would discard the very row the branch
    /// exists to write — and the subject must come back UNTOUCHED, which is what separates "filed" from
    /// "half-deleted".
    /// </summary>
    [Fact]
    public async Task A_Cleaners_Own_Deletion_Files_A_Pending_Request_And_Erases_Nothing()
    {
        await SeedAsync();

        var result = await DeleteAsync(CleanerUserId, deferEmployeeErasure: true);

        Assert.True(result.IsSuccess);

        await using var verify = NewContext();
        var request = Assert.Single(await verify.GdprRequests.ToListAsync());
        Assert.Equal(CleanerUserId, request.UserId);
        Assert.Equal("Deletion", request.RequestType);
        Assert.Equal(GdprRequestStatus.Pending, request.Status);

        var user = await verify.Users.Include(u => u.Employee)
            .FirstAsync(u => u.Id == CleanerUserId);
        Assert.Equal(CleanerEmail, user.Email);
        Assert.True(user.IsActive);
        Assert.NotNull(user.Employee);
        Assert.True(user.Employee!.IsActive);
    }

    /// <summary>
    /// The admin path erases employees — this is how a filed request is fulfilled once the paperwork
    /// is done. Guards it against the regression described in the class summary.
    /// </summary>
    [Fact]
    public async Task An_Admin_Deletion_Still_Erases_A_Cleaner()
    {
        await SeedAsync();

        var result = await DeleteAsync(CleanerUserId, deferEmployeeErasure: false);

        Assert.True(result.IsSuccess);

        await using var verify = NewContext();
        var user = await verify.Users.IgnoreQueryFilters()
            .Include(u => u.Employee)
            .FirstAsync(u => u.Id == CleanerUserId);
        Assert.NotEqual(CleanerEmail, user.Email);
        Assert.False(user.IsActive);
        Assert.False(user.Employee!.IsActive);
    }

    /// <summary>
    /// A customer has no <c>Employee</c>, so the fork must not touch them whichever way the flag is
    /// set — otherwise the self-delete path would quietly stop erasing customers too.
    /// </summary>
    [Fact]
    public async Task A_Customers_Own_Deletion_Is_Unaffected_By_The_Employee_Fork()
    {
        await SeedAsync();

        var result = await DeleteAsync(CustomerUserId, deferEmployeeErasure: true);

        Assert.True(result.IsSuccess);

        await using var verify = NewContext();
        var user = await verify.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == CustomerUserId);
        Assert.NotEqual(CustomerEmail, user.Email);
        Assert.False(user.IsActive);
    }

    /// <summary>
    /// The gap the customer-axis check could never see: the cleaner is not the order's customer, they
    /// are staffed on it. Both directions are asserted so "refuse everything" does not pass.
    /// </summary>
    [Theory]
    [InlineData(OrderStatus.New, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.OnTheWay, true)]
    [InlineData(OrderStatus.InProgress, true)]
    [InlineData(OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public async Task A_Cleaner_Holding_A_Seat_On_A_Live_Order_Is_Refused(
        OrderStatus status, bool expectBlocked)
    {
        await SeedAsync(assignedOrderStatus: status);

        var result = await DeleteAsync(CleanerUserId, deferEmployeeErasure: true);

        Assert.Equal(expectBlocked, !result.IsSuccess);
        if (expectBlocked)
        {
            Assert.Equal(BusinessErrorMessage.GdprDeletionBlockedByAssignedOrder, result.Error!.Message);
        }
    }

    /// <summary>
    /// Unsettled pay, in the two shapes it takes — a pay row not yet attached to an invoice, and one
    /// whose period has not reached <c>Paid</c>. Deliberately one error key: to the cleaner both mean
    /// "work I have not been paid for", and the remedy is the same.
    /// </summary>
    [Theory]
    [InlineData(PayPeriodStatus.Open, false, true)]
    [InlineData(PayPeriodStatus.Closed, true, true)]
    [InlineData(PayPeriodStatus.Paid, false, true)]
    [InlineData(PayPeriodStatus.Paid, true, false)]
    public async Task A_Cleaner_With_Unsettled_Pay_Is_Refused(
        PayPeriodStatus periodStatus, bool invoiced, bool expectBlocked)
    {
        await SeedAsync(payPeriodStatus: periodStatus, payInvoiced: invoiced);

        var result = await DeleteAsync(CleanerUserId, deferEmployeeErasure: true);

        Assert.Equal(expectBlocked, !result.IsSuccess);
        if (expectBlocked)
        {
            Assert.Equal(BusinessErrorMessage.GdprDeletionBlockedByUnsettledPay, result.Error!.Message);
        }
    }

    /// <summary>
    /// The guards run on BOTH paths — an admin cannot erase a cleaner out from under a live job
    /// either, which is the same reading the pre-existing open-invoice guard already had.
    /// </summary>
    [Fact]
    public async Task The_Guards_Also_Refuse_An_Admin_Deletion()
    {
        await SeedAsync(assignedOrderStatus: OrderStatus.InProgress);

        var result = await DeleteAsync(CleanerUserId, deferEmployeeErasure: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.GdprDeletionBlockedByAssignedOrder, result.Error!.Message);
    }

    private async Task<BusinessResult> DeleteAsync(string userId, bool deferEmployeeErasure)
    {
        await using var ctx = NewContext();
        var session = new TestUserSessionProvider(userId, CleanerEmail);

        var service = new GdprDeletionService(
            new UserRepository(ctx),
            new OrderRepository(ctx),
            new EmployeeDocumentRepository(ctx),
            new DocumentDeletionRequestRepository(ctx),
            new EmployeeInvoiceRepository(ctx),
            new EmployeePayoutDetailsRepository(ctx),
            new UserMembershipRepository(ctx),
            new OrderPhotoRepository(ctx),
            new DeviceRepository(ctx, session),
            new LiveActivityTokenRepository(ctx),
            new CartRepository(ctx),
            new UserConsentRepository(ctx),
            new GdprRequestRepository(ctx),
            new DisputeRepository(ctx),
            new SavedAddressRepository(ctx, session),
            new OrderEmployeePayRepository(ctx),
            new RecurringBookingTemplateRepository(ctx),
            new UserNotificationRepository(ctx),
            new DeadLetterRepository(ctx),
            new OutboxMessageRepository(ctx),
            Mock.Of<IRefreshTokenService>(),
            Mock.Of<IStripeClient>(),
            _blobClientFactory.Object,
            NullLogger<GdprDeletionService>.Instance);

        var result = await service.DeleteUserAccountAsync(
            userId, "gdpr_erasure_test", _ => ("test-actor", null), deferEmployeeErasure, CancellationToken.None);

        await ctx.CommitAsync(CancellationToken.None);
        return result;
    }

    private async Task SeedAsync(
        OrderStatus? assignedOrderStatus = null,
        PayPeriodStatus? payPeriodStatus = null,
        bool payInvoiced = false)
    {
        await using (var schema = NewContext())
        {
            await schema.Database.EnsureCreatedAsync();
        }

        await using var ctx = NewContext();

        var cleanerUser = User.CreateWithPassword(
            CleanerEmail, "Test-password-1!", "Lenka", "Markova", UserProfile.Employee);
        cleanerUser.Id = CleanerUserId;
        ctx.Add(cleanerUser);

        var employee = Employee.CreateWithUser(cleanerUser);
        employee.Id = EmployeeId;
        ctx.Add(employee);

        var customer = User.CreateWithPassword(
            CustomerEmail, "Test-password-1!", "Petra", "Svobodova", UserProfile.Customer);
        customer.Id = CustomerUserId;
        ctx.Add(customer);

        if (assignedOrderStatus is { } status)
        {
            // The order belongs to the CUSTOMER — the cleaner only holds a seat on it. That is
            // precisely the shape the customer-axis check cannot see.
            var order = Order.Create(
                customerName: "Petra Svobodova",
                customerEmail: CustomerEmail,
                customerPhone: "+420777222333",
                customerAddress: Address.Create("Seat St 1", "Praha", "11000", "cz"),
                rooms: 2,
                bathrooms: 1,
                extras: new Dictionary<string, bool>(),
                cleaningDateTime: DateTime.UtcNow.AddHours(6),
                paymentType: PaymentType.Cash,
                totalPrice: 1500m,
                currencyId: "czk",
                paymentStatus: PaymentStatus.Pending,
                userId: CustomerUserId);
            order.Id = "order-seat-del-1";
            order.AddOrderStatus(OrderStatusTrack.Create(status, order));
            ctx.Add(order);
            ctx.Add(OrderEmployee.Create(order, employee));
        }

        if (payPeriodStatus is { } periodStatus)
        {
            var period = PayPeriod.Create(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)),
                DateOnly.FromDateTime(DateTime.UtcNow));
            period.Id = "pay-period-del-1";
            SetPayPeriodStatus(period, periodStatus);
            ctx.Add(period);

            var pay = OrderEmployeePay.Create(
                orderId: "order-pay-del-1",
                employeeId: EmployeeId,
                payPeriodId: period.Id,
                basePay: 500m,
                totalPay: 500m);
            pay.Id = "order-employee-pay-del-1";
            if (payInvoiced)
            {
                pay.AssignToInvoice("employee-invoice-del-1");
            }

            ctx.Add(pay);
        }

        await ctx.CommitAsync(CancellationToken.None);
    }

    // PayPeriod exposes Close()/MarkPaid() as a state machine rather than a setter; the test needs an
    // arbitrary starting status without walking the machine, so the backing property is set directly.
    private static void SetPayPeriodStatus(PayPeriod period, PayPeriodStatus status)
    {
        var property = typeof(PayPeriod).GetProperty(nameof(PayPeriod.Status))!;
        property.SetValue(period, status);
    }

    private CleansiaDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<CleansiaDbContext>().UseSqlite(_connection).Options,
            new TestUserSessionProvider("system", "system@cleansia.test"),
            new FixedTenantProvider(null));

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
