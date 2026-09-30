using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// Owner rulings 2026-09-28 on how a justified complaint is settled and who pays for it.
/// <list type="bullet">
///   <item>The settlement is a card refund unless the customer chose credit when filing. Credit expires
///   and is never paid out, so it is never the settlement a customer did not ask for, and the
///   administrator decides the amount, not the tender.</item>
///   <item>A cleaner's pay drops only on an administrator's explicit finding that they were at fault:
///   a deduction on their pay for the disputed order, linked to the dispute, with the reason on the pay
///   record they read. A refund alone never touches it.</item>
/// </list>
/// </summary>
public sealed class DisputeSettlementAndCleanerChargeTests
{
    private const string DisputeId = "dispute-settle-1";
    private const string OrderId = "order-settle-1";
    private const string CustomerId = "customer-settle-1";
    private const string CleanerId = "employee-settle-1";
    private const string ActorId = "admin-settle";

    private readonly Mock<IDisputeRepository> _disputes = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IRefundService> _refunds = new();
    private readonly Mock<IRefundRepository> _refundRows = new();
    private readonly Mock<ICreditAccountRepository> _creditAccounts = new();
    private readonly Mock<IOrderEmployeePayRepository> _pays = new();

    public DisputeSettlementAndCleanerChargeTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(ActorId);
        _refunds
            .Setup(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefundRequest r, CancellationToken _) => BusinessResult.Success(new RefundResult(
                "refund-1", $"refund:{OrderId}:dispute:{DisputeId}", r.Amount, RefundStatus.Succeeded, false)));
    }

    [Fact]
    public async Task A_Customer_Who_Chose_Credit_Is_Settled_In_Credit_And_The_Card_Is_Untouched()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.Credit);
        var account = ArrangeCreditAccount();

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, 300m, "justified"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _refunds.VerifyNoOtherCalls();
        Assert.Equal(300m, account.Balance);
        var granted = Assert.Single(account.Transactions);
        Assert.Equal(CreditTransactionReason.DisputeSettlement, granted.Reason);
        Assert.Equal(DisputeId, granted.DisputeId);
        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
        Assert.Equal(300m, dispute.CreditReturnedAmount);
        Assert.Null(dispute.CardRefundedAmount);
    }

    [Fact]
    public async Task A_Customer_Who_Did_Not_Choose_Credit_Is_Refunded_To_The_Card()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.CardRefund);

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, 300m, "justified"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _refunds.Verify(s => s.IssueRefundAsync(
            It.Is<RefundRequest>(r => r.Amount == 300m && r.DisputeId == DisputeId), It.IsAny<CancellationToken>()), Times.Once);
        AssertNoCreditGranted();
        Assert.Equal(300m, dispute.CardRefundedAmount);
    }

    [Fact]
    public async Task A_Credit_Settlement_Above_The_Order_Total_Is_Refused_And_Grants_Nothing()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.Credit);

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, 1500m, "justified"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidRefundAmount, result.Error!.Message);
        AssertNoCreditGranted();
        Assert.Equal(DisputeStatus.Pending, dispute.Status);
    }

    /// <summary>
    /// A resolved dispute does not stop a new one on the same order. The order was refunded to the card
    /// in full, so a later complaint settled in credit has nothing left to give back.
    /// </summary>
    [Fact]
    public async Task A_Credit_Settlement_On_An_Order_Already_Refunded_To_The_Card_Is_Refused()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.Credit);
        ArrangeCreditAccount();
        _refundRows
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m);

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, 1000m, "justified"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.InvalidRefundAmount, result.Error!.Message);
        AssertNoCreditGranted();
        _refunds.VerifyNoOtherCalls();
        Assert.Equal(DisputeStatus.Pending, dispute.Status);
    }

    /// <summary>
    /// 600 back to the card, 100 of the credit tender returned and 200 settled in credit by an earlier
    /// dispute leave 100 of the 1000 order. That much can still be settled, a cent more cannot.
    /// </summary>
    [Theory]
    [InlineData(100.00, true)]
    [InlineData(100.01, false)]
    public async Task A_Credit_Settlement_Counts_Everything_The_Order_Already_Gave_Back(decimal amount, bool settled)
    {
        ArrangeDispute(DisputeSettlementPreference.Credit);
        var account = ArrangeCreditAccount();
        _refundRows
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(600m);
        _creditAccounts
            .Setup(r => r.GetReturnedTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(100m);
        _creditAccounts
            .Setup(r => r.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(200m);

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, amount, "justified"), CancellationToken.None);

        Assert.Equal(settled, result.IsSuccess);
        Assert.Equal(settled ? amount : 0m, account.Balance);
    }

    /// <summary>
    /// An earlier complaint on the order was settled in credit, which is on neither tender the refund
    /// seam bounds. Once the whole order went back that way, the next complaint has nothing left to
    /// refund to the card.
    /// </summary>
    [Fact]
    public async Task A_Card_Settlement_After_The_Whole_Order_Was_Settled_In_Credit_Is_Refused()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.CardRefund);
        ArrangeSettledInCredit(1000m);

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, 1000m, "justified"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.RefundNothingRefundable, result.Error!.Message);
        _refunds.Verify(s => s.IssueRefundAsync(It.IsAny<RefundRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(DisputeStatus.Pending, dispute.Status);
    }

    [Fact]
    public async Task A_Card_Settlement_After_Part_Of_The_Order_Was_Settled_In_Credit_Refunds_Only_What_Is_Left()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.CardRefund);
        ArrangeSettledInCredit(400m);

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, 1000m, "justified"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _refunds.Verify(s => s.IssueRefundAsync(
            It.Is<RefundRequest>(r => r.Amount == 600m && r.DisputeId == DisputeId), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1000m, dispute.RefundAmount);
        Assert.Equal(600m, dispute.CardRefundedAmount);
    }

    /// <summary>
    /// The card refund went through and the resolution was lost, so the same resolution runs again. What
    /// is left now counts this dispute's own refund, but the seam answers the replay with that refund and
    /// moves nothing, so the replay is not held to it.
    /// </summary>
    [Fact]
    public async Task A_Replay_Of_The_Disputes_Own_Card_Refund_Still_Resolves()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.CardRefund);
        ArrangeSettledInCredit(400m);
        _refundRows
            .Setup(r => r.GetSucceededRefundTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(600m);
        var ownRefund = Refund.Create(
                OrderId, $"refund:{OrderId}:dispute:{DisputeId}", 600m, "CZK",
                RefundReason.DisputeResolution, RefundSource.AppRefund, disputeId: DisputeId)
            .MarkSucceeded(stripeRefundId: null, confirmedOnUtc: DateTimeOffset.UtcNow);
        _refundRows
            .Setup(r => r.GetByRefundKeyAsync($"refund:{OrderId}:dispute:{DisputeId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownRefund);

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, 600m, "justified"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _refunds.Verify(s => s.IssueRefundAsync(
            It.Is<RefundRequest>(r => r.DisputeId == DisputeId), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
    }

    /// <summary>
    /// Credit on an erased account is forfeited, so a customer who chose credit and was then erased is
    /// refunded to the card, as one who never chose would be.
    /// </summary>
    [Fact]
    public async Task An_Erased_Customer_Who_Chose_Credit_Is_Refunded_To_The_Card()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.Credit);
        _creditAccounts
            .Setup(r => r.EnsureForUserAsync(CustomerId, "currency-czk", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreditAccount?)null);

        var result = await Handler().Handle(new ResolveDispute.Command(DisputeId, 300m, "justified"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        _refunds.Verify(s => s.IssueRefundAsync(
            It.Is<RefundRequest>(r => r.Amount == 300m && r.DisputeId == DisputeId), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(300m, dispute.CardRefundedAmount);
        Assert.Equal(DisputeStatus.Resolved, dispute.Status);
    }

    [Fact]
    public async Task A_Refund_Alone_Never_Touches_The_Cleaners_Pay()
    {
        ArrangeDispute(DisputeSettlementPreference.CardRefund);

        await Handler().Handle(new ResolveDispute.Command(DisputeId, 300m, "justified"), CancellationToken.None);

        _pays.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Finding_Of_Fault_Deducts_From_The_Cleaners_Pay_With_The_Reason_They_Read()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.CardRefund);
        var pay = ArrangePay(totalPay: 600m);

        var result = await Handler().Handle(
            new ResolveDispute.Command(DisputeId, 300m, "justified",
                new ResolveDispute.CleanerCharge(CleanerId, 150m, "Bathroom left uncleaned")),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(150m, pay.DeductionPay);
        Assert.Equal(450m, pay.TotalPay);
        Assert.Equal(dispute.Id, pay.DeductionDisputeId);
        Assert.Equal("Bathroom left uncleaned", pay.MapToDto().DeductionReason);
    }

    [Fact]
    public async Task A_Pay_Already_On_An_Invoice_Is_Not_Charged_And_No_Money_Moves()
    {
        var dispute = ArrangeDispute(DisputeSettlementPreference.CardRefund);
        var pay = ArrangePay(totalPay: 600m);
        pay.AssignToInvoice("invoice-1");

        var result = await Handler().Handle(
            new ResolveDispute.Command(DisputeId, 300m, "justified",
                new ResolveDispute.CleanerCharge(CleanerId, 150m, "Bathroom left uncleaned")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeCleanerChargeNotChargeable, result.Error!.Message);
        _refunds.VerifyNoOtherCalls();
        Assert.Equal(0m, pay.DeductionPay);
        Assert.Equal(DisputeStatus.Pending, dispute.Status);
    }

    [Fact]
    public async Task A_Charge_Larger_Than_The_Pay_Is_Not_Chargeable()
    {
        ArrangeDispute(DisputeSettlementPreference.CardRefund);
        ArrangePay(totalPay: 100m);

        var result = await Handler().Handle(
            new ResolveDispute.Command(DisputeId, null, "at fault",
                new ResolveDispute.CleanerCharge(CleanerId, 150m, "Bathroom left uncleaned")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeCleanerChargeNotChargeable, result.Error!.Message);
    }

    [Fact]
    public async Task A_Cleaner_With_No_Pay_On_The_Order_Is_Not_Chargeable()
    {
        ArrangeDispute(DisputeSettlementPreference.CardRefund);

        var result = await Handler().Handle(
            new ResolveDispute.Command(DisputeId, null, "at fault",
                new ResolveDispute.CleanerCharge("employee-never-on-it", 50m, "Late")),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.DisputeCleanerChargeNotChargeable, result.Error!.Message);
    }

    [Fact]
    public void A_Charge_Needs_A_Reason_For_The_Cleaner()
    {
        var result = new ResolveDispute.Validator().Validate(new ResolveDispute.Command(
            DisputeId, null, "at fault", new ResolveDispute.CleanerCharge(CleanerId, 50m, "")));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    /// <summary>
    /// The deduction and the pay total are each stored to the cent; a charge of 10.005 would round them
    /// apart and the cleaner's invoice line would no longer add up.
    /// </summary>
    [Fact]
    public void A_Charge_Is_Whole_Cents()
    {
        var result = new ResolveDispute.Validator().Validate(new ResolveDispute.Command(
            DisputeId, null, "at fault", new ResolveDispute.CleanerCharge(CleanerId, 10.005m, "Late")));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.DisputeCleanerChargeNotWholeMinorUnits, error.ErrorMessage);
    }

    private void ArrangeSettledInCredit(decimal amount) =>
        _creditAccounts
            .Setup(r => r.GetDisputeSettledTotalForOrderAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(amount);

    private void AssertNoCreditGranted() =>
        _creditAccounts.Verify(
            r => r.EnsureForUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    private ResolveDispute.Handler Handler() =>
        new(_disputes.Object, _session.Object, _refunds.Object, _refundRows.Object, _creditAccounts.Object, _pays.Object,
            Mock.Of<INotificationProducer>(), new AuditContext());

    private Dispute ArrangeDispute(DisputeSettlementPreference preference)
    {
        var dispute = new Dispute(OrderId, CustomerId, DisputeReason.QualityIssue, "Bathroom not cleaned", CustomerId, preference)
        {
            Id = DisputeId,
        };
        var order = Order.Create(
            customerName: "Cust",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 2,
            bathrooms: 1,
            cleaningDateTime: DateTime.UtcNow.AddDays(-1),
            paymentType: PaymentType.Card,
            totalPrice: 1000m,
            currencyId: "currency-czk",
            paymentStatus: PaymentStatus.Paid,
            userId: CustomerId);
        order.Id = OrderId;
        typeof(Dispute).GetProperty(nameof(Dispute.Order))!.SetValue(dispute, order);
        _disputes.Setup(r => r.GetForUpdateAsync(DisputeId, It.IsAny<CancellationToken>())).ReturnsAsync(dispute);
        return dispute;
    }

    private CreditAccount ArrangeCreditAccount()
    {
        var account = CreditAccount.Create(CustomerId, "currency-czk", "system");
        _creditAccounts
            .Setup(r => r.EnsureForUserAsync(CustomerId, "currency-czk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        return account;
    }

    private OrderEmployeePay ArrangePay(decimal totalPay)
    {
        var pay = OrderEmployeePay.Create(OrderId, CleanerId, "period-1", "currency-czk", basePay: totalPay, totalPay: totalPay);
        _pays.Setup(r => r.GetByOrderAndEmployeeAsync(OrderId, CleanerId, It.IsAny<CancellationToken>())).ReturnsAsync(pay);
        return pay;
    }
}
