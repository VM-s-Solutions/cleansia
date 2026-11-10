using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Refunds;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Cleansia.TestUtilities;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Refunds;

/// <summary>
/// Recording how a refund Stripe failed or bounced was repaid by hand, through the real refund seam with
/// mocked repositories. A failed refund starts counting as given back: it reads Repaid, the order moves to
/// Partly refunded or Refunded, its credit share goes back and the clawback runs once, all staged for the
/// pipeline's one commit. A bounced refund already counted, so it is only stamped. Stripe is never called.
/// </summary>
public sealed class RecordRefundRepaymentTests
{
    private const string OrderId = "order-repaid";
    private const string UserId = "user-repaid";
    private const string AdminId = "admin-repaid";

    private readonly Mock<IRefundRepository> _refunds = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<IStripeClient> _stripe = new(MockBehavior.Strict);
    private readonly Mock<ILoyaltyService> _loyalty = new();

    private RecordRefundRepayment.Handler CreateHandler()
    {
        var factory = new Mock<IStripeClientFactory>();
        factory.Setup(f => f.CreateClient()).Returns(_stripe.Object);
        var service = new RefundService(
            _refunds.Object, _orders.Object, _credit.Object, factory.Object,
            new Mock<IAdminNotifier>().Object, new Mock<IUserNotificationRepository>().Object,
            NullLogger<RefundService>.Instance);
        return new RecordRefundRepayment.Handler(
            _refunds.Object, _orders.Object, _credit.Object, service, _loyalty.Object,
            new TestUserSessionProvider(AdminId, "admin@cleansia.test"));
    }

    private Order ArrangeOrder(decimal credit = 0m, string? userId = UserId)
    {
        var order = OrderMockFactory.GenerateWithStatusHistory([OrderStatus.Completed], totalPrice: 1000m, orderId: OrderId, userId: userId!);
        if (credit > 0m)
        {
            order.ApplyCredit(credit, UserId);
        }

        _orders.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        return order;
    }

    private Refund ArrangeFailed(decimal amount = 400m, string requestId = "lines-1", bool bounced = false)
    {
        var refund = Refund.Create(
            OrderId,
            RefundService.BuildRefundKey(new RefundRequest(OrderId, amount, RefundReason.AdminDiscretion, AdminId, RefundRequestId: requestId)),
            amount, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
        if (bounced)
        {
            refund.MarkSucceeded("re_bounced", DateTimeOffset.UtcNow);
        }

        refund.MarkFailedByStripe("re_failed", DateTimeOffset.UtcNow);
        _refunds.Setup(r => r.GetByIdAsync(refund.Id, It.IsAny<CancellationToken>())).ReturnsAsync(refund);
        return refund;
    }

    private CreditAccount ArrangeAccount()
    {
        var account = CreditAccount.Create(UserId, "currency-czk", UserId);
        _credit.Setup(c => c.EnsureForUserIgnoringTenantAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        return account;
    }

    private Task<BusinessResult<RecordRefundRepayment.Response>> RepayAsync(
        Refund refund, RefundRepaymentMethod method = RefundRepaymentMethod.BankTransfer, string? reference = "VS-2026-11", string orderId = OrderId) =>
        CreateHandler().Handle(new RecordRefundRepayment.Command(orderId, refund.Id, method, reference), CancellationToken.None);

    private void VerifyNothingCommitted()
    {
        _refunds.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _credit.Verify(c => c.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _orders.Verify(o => o.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Failed_400_Refund_On_A_1000_Card_Order_Repaid_By_Transfer_Reads_Repaid_The_Order_Partly_Refunded_And_Claws_Back_Once()
    {
        var order = ArrangeOrder();
        var refund = ArrangeFailed();

        var result = await RepayAsync(refund);

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RefundState.Repaid), result.Value!.State.Name);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.Equal(400m, refund.Amount);
        Assert.Equal((RefundRepaymentMethod.BankTransfer, "VS-2026-11"), (refund.RepaymentMethod!.Value, refund.RepaymentReference));
        Assert.NotNull(refund.RepaidOn);
        Assert.Equal(PaymentStatus.PartiallyRefunded, order.PaymentStatus);
        _loyalty.Verify(l => l.RevokeForRefundAsync(OrderId, 400m, refund.RefundKey, AdminId, It.IsAny<CancellationToken>()), Times.Once);
        _credit.Verify(c => c.TryReturnIgnoringTenantAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        VerifyNothingCommitted();
    }

    [Fact]
    public async Task On_An_Order_That_Took_Credit_Its_Credit_Share_Goes_Back_Once_Under_The_Customers_Credit_Lock()
    {
        var order = ArrangeOrder(credit: 250m);
        var refund = ArrangeFailed(amount: 300m);
        _credit.Setup(c => c.TryReturnIgnoringTenantAsync(
                UserId, order.CurrencyId, 100m, $"credit-return:{refund.RefundKey}", AdminId,
                It.IsAny<CancellationToken>(), OrderId, null))
            .ReturnsAsync(true);
        _credit.SetupSequence(c => c.GetReturnedAmountAsync($"credit-return:{refund.RefundKey}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m)
            .ReturnsAsync(100m);

        var result = await RepayAsync(refund);

        Assert.True(result.IsSuccess);
        _credit.Verify(c => c.LockForUserAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
        _credit.Verify(c => c.TryReturnIgnoringTenantAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Once);
        _loyalty.Verify(l => l.RevokeForRefundAsync(OrderId, 400m, refund.RefundKey, AdminId, It.IsAny<CancellationToken>()), Times.Once);
        VerifyNothingCommitted();
    }

    [Fact]
    public async Task When_A_Later_Full_Refund_Already_Took_That_Money_It_Is_Nothing_Refundable_And_Nothing_Changes()
    {
        var order = ArrangeOrder();
        var refund = ArrangeFailed();
        _refunds.Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(1000m);

        var result = await RepayAsync(refund);

        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
        Assert.Equal((RefundStatus.Failed, 400m), (refund.Status, refund.Amount));
        Assert.Null(refund.RepaidOn);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        _loyalty.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Bounced_Refund_Is_Only_Stamped_And_The_Payment_Status_And_Points_Do_Not_Move()
    {
        var order = ArrangeOrder();
        var refund = ArrangeFailed(bounced: true);

        var result = await RepayAsync(refund);

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(RefundState.Repaid), result.Value!.State.Name);
        Assert.Equal(RefundStatus.Succeeded, refund.Status);
        Assert.NotNull(refund.RepaidOn);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        _loyalty.VerifyNoOtherCalls();
        _credit.Verify(c => c.TryReturnIgnoringTenantAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task A_Second_Repayment_Is_Not_Owed()
    {
        ArrangeOrder();
        var refund = ArrangeFailed();

        Assert.True((await RepayAsync(refund)).IsSuccess);
        var second = await RepayAsync(refund, reference: "VS-again");

        Assert.Equal(BusinessErrorMessage.RefundNotOwed, second.Error!.Message);
        Assert.Equal("VS-2026-11", refund.RepaymentReference);
    }

    [Fact]
    public async Task A_Refund_Not_Sent_Refunded_Or_Closed_Is_Not_Owed()
    {
        ArrangeOrder();
        var pending = Refund.Create(OrderId, $"refund:{OrderId}:admin:p", 100m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund);
        var refunded = Refund.Create(OrderId, $"refund:{OrderId}:admin:r", 100m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund)
            .MarkSucceeded("re_r", DateTimeOffset.UtcNow);
        var closed = Refund.Create(OrderId, $"refund:{OrderId}:admin:c", 100m, "CZK", RefundReason.AdminDiscretion, RefundSource.AppRefund)
            .MarkFailed();
        foreach (var refund in new[] { pending, refunded, closed })
        {
            _refunds.Setup(r => r.GetByIdAsync(refund.Id, It.IsAny<CancellationToken>())).ReturnsAsync(refund);
            Assert.Equal(BusinessErrorMessage.RefundNotOwed, (await RepayAsync(refund)).Error!.Message);
        }
    }

    [Fact]
    public async Task Credit_Adds_One_Goodwill_Row_Keyed_On_The_Refund_With_The_Order_As_Provenance()
    {
        ArrangeOrder();
        var refund = ArrangeFailed();
        var account = ArrangeAccount();

        var result = await RepayAsync(refund, RefundRepaymentMethod.Credit, reference: null);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(account.Transactions);
        Assert.Equal(
            (CreditTransactionReason.Goodwill, 400m, $"{refund.RefundKey}:credit", (string?)OrderId),
            (row.Reason, row.Amount, row.IdempotencyKey, row.OrderId));
        Assert.Equal(RefundRepaymentMethod.Credit, refund.RepaymentMethod);
    }

    [Fact]
    public async Task Credit_Is_Unavailable_For_A_Guest_An_Erased_Account_And_Frozen_Books_And_Nothing_Is_Stamped()
    {
        ArrangeOrder(userId: null);
        var guest = ArrangeFailed(requestId: "guest");
        Assert.Equal(BusinessErrorMessage.RefundCreditUnavailable,
            (await RepayAsync(guest, RefundRepaymentMethod.Credit, null)).Error!.Message);
        Assert.Null(guest.RepaidOn);

        ArrangeOrder();
        var erased = ArrangeFailed(requestId: "erased");
        _credit.Setup(c => c.EnsureForUserIgnoringTenantAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreditAccount?)null);
        Assert.Equal(BusinessErrorMessage.RefundCreditUnavailable,
            (await RepayAsync(erased, RefundRepaymentMethod.Credit, null)).Error!.Message);
        Assert.Null(erased.RepaidOn);

        ArrangeAccount();
        var frozen = ArrangeFailed(requestId: "frozen");
        _credit.Setup(c => c.IsOnFrozenCompanyBooksIgnoringTenantAsync(UserId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Assert.Equal(BusinessErrorMessage.RefundCreditUnavailable,
            (await RepayAsync(frozen, RefundRepaymentMethod.Credit, null)).Error!.Message);
        Assert.Null(frozen.RepaidOn);
    }

    [Fact]
    public async Task A_Refund_On_Another_Order_Is_Not_Found()
    {
        ArrangeOrder();
        var refund = ArrangeFailed();

        Assert.Equal(BusinessErrorMessage.RefundNotFound, (await RepayAsync(refund, orderId: "another-order")).Error!.Message);
        Assert.Null(refund.RepaidOn);
    }

    [Fact]
    public async Task A_Failure_After_The_Stamp_Escapes_Before_Any_Commit_So_The_Pipeline_Rolls_The_Stamp_And_The_Credit_Back()
    {
        ArrangeOrder();
        var refund = ArrangeFailed();
        ArrangeAccount();
        _loyalty.Setup(l => l.RevokeForRefundAsync(It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("loyalty store down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => RepayAsync(refund, RefundRepaymentMethod.Credit, null));

        VerifyNothingCommitted();
    }

    [Theory]
    [InlineData(RefundRepaymentMethod.BankTransfer, null, BusinessErrorMessage.Required)]
    [InlineData(RefundRepaymentMethod.BankTransfer, "  ", BusinessErrorMessage.Required)]
    [InlineData((RefundRepaymentMethod)9, "VS-1", BusinessErrorMessage.InvalidEnumValue)]
    public void The_Validator_Refuses_A_Transfer_Without_A_Reference_And_An_Unknown_Method(
        RefundRepaymentMethod method, string? reference, string expected)
    {
        var result = new RecordRefundRepayment.Validator().Validate(new RecordRefundRepayment.Command(OrderId, "refund-1", method, reference));

        Assert.Contains(result.Errors, e => e.ErrorMessage == expected);
    }

    [Fact]
    public void The_Validator_Refuses_A_Reference_Over_100_Characters_And_Accepts_Credit_Without_One()
    {
        var validator = new RecordRefundRepayment.Validator();

        Assert.Contains(
            validator.Validate(new RecordRefundRepayment.Command(OrderId, "refund-1", RefundRepaymentMethod.BankTransfer, new string('x', 101))).Errors,
            e => e.ErrorMessage == BusinessErrorMessage.MaxLength);
        Assert.True(validator.Validate(new RecordRefundRepayment.Command(OrderId, "refund-1", RefundRepaymentMethod.Credit, null)).IsValid);
    }
}
