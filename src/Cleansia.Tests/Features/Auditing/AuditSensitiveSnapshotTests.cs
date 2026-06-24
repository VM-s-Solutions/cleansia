using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Features.PayConfig;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Auditing;

/// <summary>
/// ADR-0012 D4/D4.1 (TC-AUDIT-SNAPSHOT) — each of the sensitive (five + dispute) admin handlers emits
/// exactly one typed, producer-redacted before/after snapshot through <see cref="IAuditContext"/> on its
/// success path. Asserted at the producer: a real <see cref="AuditContext"/> is injected, the handler
/// runs, and the DRAINED snapshot's serialized payload carries the changed money/state fields + ids ONLY
/// — never raw subject PII (name/email/address/card). The GDPR-delete snapshot carries scope + subject id
/// only, the lawful-to-retain set that survives the subject's erasure (AC3, integration-proven separately).
/// </summary>
public sealed class AuditSensitiveSnapshotTests
{
    private const string AdminId = "admin-1";
    private const string CustomerName = "Jane Doe";
    private const string CustomerEmail = "jane.doe@example.test";

    private static IUserSessionProvider AdminSession()
    {
        var mock = new Mock<IUserSessionProvider>();
        mock.Setup(s => s.GetUserId()).Returns(AdminId);
        mock.Setup(s => s.GetUserEmail()).Returns("admin@cleansia.test");
        return mock.Object;
    }

    private static void AssertNoSubjectPii(AuditSnapshot snapshot)
    {
        foreach (var json in new[] { snapshot.BeforeJson, snapshot.AfterJson })
        {
            if (json is null)
            {
                continue;
            }

            Assert.DoesNotContain(CustomerName, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(CustomerEmail, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("@", json);
        }
    }

    // ── AdminOverrideOrderStatus ──────────────────────────────────────────────

    [Fact]
    public async Task OverrideOrderStatus_Emits_Before_After_Status_With_OrderId_And_No_Pii()
    {
        var auditContext = new AuditContext();
        var orderRepository = new Mock<IOrderRepository>();
        var order = BuildOrder("order-ovr", OrderStatus.Confirmed);
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        var handler = new AdminOverrideOrderStatus.Handler(orderRepository.Object, AdminSession(), auditContext);
        var result = await handler.Handle(
            new AdminOverrideOrderStatus.Command("order-ovr", OrderStatus.OnTheWay), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("Order", snapshot!.ResourceType);
        Assert.Equal("order-ovr", snapshot.ResourceId);
        Assert.Contains("\"orderId\":\"order-ovr\"", snapshot.BeforeJson);
        Assert.Contains($"\"status\":{(int)OrderStatus.Confirmed}", snapshot.BeforeJson);
        Assert.Contains($"\"status\":{(int)OrderStatus.OnTheWay}", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── AdminRefundOrder (full refund) ────────────────────────────────────────

    [Fact]
    public async Task AdminRefundOrder_Emits_Consumed_Before_After_With_Amount_And_No_Pii()
    {
        var auditContext = new AuditContext();
        var orderRepository = new Mock<IOrderRepository>();
        var order = BuildOrder("order-ref", OrderStatus.Confirmed, totalPrice: 1000m);
        order.AssignStripeSessionId("cs_test_1");
        orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());

        var refundRepository = new Mock<IRefundRepository>();
        refundRepository.SetupSequence(r => r.GetSucceededRefundTotalForOrderAsync("order-ref", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m)       // consumedBefore
            .ReturnsAsync(1000m);   // consumedAfter

        var refundService = new Mock<IRefundService>();
        refundService.Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", "refund:order-ref:admin:full", 1000m, RefundStatus.Succeeded, false)));

        var handler = new AdminRefundOrder.Handler(
            orderRepository.Object, refundRepository.Object, refundService.Object,
            AdminSession(), Mock.Of<IPendingDispatch>(), auditContext);

        var result = await handler.Handle(new AdminRefundOrder.Command("order-ref"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("Order", snapshot!.ResourceType);
        Assert.Equal("order-ref", snapshot.ResourceId);
        Assert.Contains("\"consumedRefund\":0", snapshot.BeforeJson);
        Assert.Contains("\"consumedRefund\":1000", snapshot.AfterJson);
        Assert.Contains("\"orderTotal\":1000", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── IssuePartialRefund ────────────────────────────────────────────────────

    [Fact]
    public async Task PartialRefund_Emits_Amount_Consumed_And_No_Pii()
    {
        var auditContext = new AuditContext();
        var service = Service.Create("cat-1", "Deep clean", "", 1000m, 0m);
        service.Id = "svc-a";
        var order = BuildOrder("order-prt", OrderStatus.Completed, totalPrice: 1000m, completed: true);
        order.AddSelectedServices([OrderService.Create(order, service)]);

        var orderRepository = new Mock<IOrderRepository>();
        orderRepository.Setup(r => r.GetByIdAsync("order-prt", It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var refundRepository = new Mock<IRefundRepository>();
        refundRepository.SetupSequence(r => r.GetSucceededRefundTotalForOrderAsync("order-prt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m)       // consumedBefore
            .ReturnsAsync(1000m);   // consumedAfter

        var refundService = new Mock<IRefundService>();
        refundService.Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest req, CancellationToken _) => BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{req.OrderId}:admin:{req.RefundRequestId}", req.Amount, RefundStatus.Succeeded, false)));

        var loyaltyService = new Mock<ILoyaltyService>();
        var countryRepository = new Mock<ICountryConfigurationRepository>();

        var handler = new IssuePartialRefund.Handler(
            orderRepository.Object, refundRepository.Object, countryRepository.Object,
            refundService.Object, loyaltyService.Object, AdminSession(), auditContext,
            NullLogger<IssuePartialRefund.Handler>.Instance);

        var result = await handler.Handle(
            new IssuePartialRefund.Command(
                "order-prt",
                [new IssuePartialRefund.RefundLineSelection("svc-a", null)],
                RefundReason.ServiceNotRendered,
                null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("Order", snapshot!.ResourceType);
        Assert.Equal("order-prt", snapshot.ResourceId);
        Assert.Contains("\"consumedRefund\":0", snapshot.BeforeJson);
        Assert.Contains("\"consumedRefund\":1000", snapshot.AfterJson);
        Assert.Contains("\"refundAmount\":1000", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── ResolveDispute ────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveDispute_Emits_Before_After_Status_RefundAmount_And_DisputeId_No_Pii()
    {
        var auditContext = new AuditContext();
        var dispute = new Dispute("order-d", "customer-9", DisputeReason.Other, "x", "customer-9") { Id = "dispute-1" };

        var disputeRepository = new Mock<IDisputeRepository>();
        disputeRepository.Setup(r => r.GetForUpdateAsync("dispute-1", It.IsAny<CancellationToken>())).ReturnsAsync(dispute);

        var refundService = new Mock<IRefundService>();
        refundService.Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new RefundResult(
                "refund-1", "refund:order-d:dispute:dispute-1", 250m, RefundStatus.Succeeded, false)));

        var handler = new ResolveDispute.Handler(
            disputeRepository.Object, AdminSession(), refundService.Object, Mock.Of<IPendingDispatch>(), auditContext);

        var result = await handler.Handle(
            new ResolveDispute.Command("dispute-1", 250m, "approved by ops"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("Dispute", snapshot!.ResourceType);
        Assert.Equal("dispute-1", snapshot.ResourceId);
        Assert.Contains($"\"status\":{(int)DisputeStatus.Pending}", snapshot.BeforeJson);
        Assert.Contains($"\"status\":{(int)DisputeStatus.Resolved}", snapshot.AfterJson);
        Assert.Contains("\"refundAmount\":250", snapshot.AfterJson);
        // The free-text resolution notes are NOT in the snapshot (could carry subject PII).
        Assert.DoesNotContain("approved by ops", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── UpdatePayConfig ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePayConfig_Emits_Old_New_Rates_With_Ids_And_No_Pii()
    {
        var auditContext = new AuditContext();
        var config = EmployeePayConfig.CreateForService("svc-pay", basePay: 100m, currencyId: "czk", employeeId: "emp-7");
        config.Id = "paycfg-1";

        var payConfigRepository = new Mock<IEmployeePayConfigRepository>();
        payConfigRepository.Setup(r => r.GetByIdAsync("paycfg-1", It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var handler = new UpdatePayConfig.Handler(payConfigRepository.Object, auditContext);
        var result = await handler.Handle(
            new UpdatePayConfig.Command("paycfg-1", BasePay: 150m, ExtraPerRoom: 10m, ExtraPerBathroom: 5m,
                DistanceRatePerKm: 2m, MinimumPay: 0m, MaximumPay: 0m, Description: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("EmployeePayConfig", snapshot!.ResourceType);
        Assert.Equal("paycfg-1", snapshot.ResourceId);
        Assert.Contains("\"basePay\":100", snapshot.BeforeJson);
        Assert.Contains("\"basePay\":150", snapshot.AfterJson);
        Assert.Contains("\"employeeId\":\"emp-7\"", snapshot.AfterJson);
        Assert.Contains("\"serviceId\":\"svc-pay\"", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── GrantPointsManually / RevokePointsManually ────────────────────────────

    [Fact]
    public async Task GrantPoints_Emits_Positive_Delta_With_Subject_UserId_Only()
    {
        var auditContext = new AuditContext();
        var loyaltyService = new Mock<ILoyaltyService>();

        var handler = new GrantPointsManually.Handler(loyaltyService.Object, AdminSession(), auditContext);
        var result = await handler.Handle(
            new GrantPointsManually.Command("user-77", 500, "goodwill", "req-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("LoyaltyAccount", snapshot!.ResourceType);
        Assert.Equal("user-77", snapshot.ResourceId);
        Assert.Contains("\"pointsDelta\":0", snapshot.BeforeJson);
        Assert.Contains("\"pointsDelta\":500", snapshot.AfterJson);
        Assert.Contains("\"userId\":\"user-77\"", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    [Fact]
    public async Task RevokePoints_Emits_Negative_Delta_With_Subject_UserId_Only()
    {
        var auditContext = new AuditContext();
        var loyaltyService = new Mock<ILoyaltyService>();

        var handler = new RevokePointsManually.Handler(loyaltyService.Object, AdminSession(), auditContext);
        var result = await handler.Handle(
            new RevokePointsManually.Command("user-88", 300, "fraud reversal", "req-2"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("LoyaltyAccount", snapshot!.ResourceType);
        Assert.Equal("user-88", snapshot.ResourceId);
        Assert.Contains("\"pointsDelta\":-300", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    // ── AdminDeleteUserAccount (GDPR) ─────────────────────────────────────────

    [Fact]
    public async Task GdprDelete_Emits_Scope_And_Subject_Id_Only_No_Exported_Data()
    {
        var auditContext = new AuditContext();
        var deletionService = new Mock<IGdprDeletionService>();
        deletionService.Setup(s => s.DeleteUserAccountAsync(
                "subject-1", It.IsAny<string>(), It.IsAny<Func<Core.Domain.Users.User, (string, string?)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success());

        var handler = new AdminDeleteUserAccount.Handler(AdminSession(), deletionService.Object, auditContext);
        var result = await handler.Handle(new AdminDeleteUserAccount.Command("subject-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var snapshot = auditContext.DrainSnapshot();
        Assert.NotNull(snapshot);
        Assert.Equal("User", snapshot!.ResourceType);
        Assert.Equal("subject-1", snapshot.ResourceId);
        Assert.Contains("\"subjectUserId\":\"subject-1\"", snapshot.AfterJson);
        Assert.Contains("\"scope\":\"Deletion\"", snapshot.AfterJson);
        AssertNoSubjectPii(snapshot);
    }

    [Fact]
    public async Task GdprDelete_On_Failed_Deletion_Emits_No_Snapshot()
    {
        var auditContext = new AuditContext();
        var deletionService = new Mock<IGdprDeletionService>();
        deletionService.Setup(s => s.DeleteUserAccountAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Core.Domain.Users.User, (string, string?)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Failure(new Error("gdpr.blocked", "active order")));

        var handler = new AdminDeleteUserAccount.Handler(AdminSession(), deletionService.Object, auditContext);
        var result = await handler.Handle(new AdminDeleteUserAccount.Command("subject-2"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Null(auditContext.DrainSnapshot());
    }

    private static Order BuildOrder(
        string orderId,
        OrderStatus latestStatus,
        decimal totalPrice = 1000m,
        bool completed = false)
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna", 1m);
        var address = Address.Create("Street 1", "Prague", "11000", "cz");
        var order = Order.Create(
            customerName: CustomerName,
            customerEmail: CustomerEmail,
            customerPhone: "+420123456789",
            customerAddress: address,
            rooms: 2,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: totalPrice,
            currencyId: currency.Id,
            paymentStatus: PaymentStatus.Paid,
            userId: "owner-user");
        order.Id = orderId;
        order.SetCurrency(currency);
        if (completed)
        {
            order.CompleteOrder(actualCompletionTime: 120);
        }
        else
        {
            order.AddOrderStatus(OrderStatusTrack.Create(latestStatus, order));
        }

        return order;
    }
}
