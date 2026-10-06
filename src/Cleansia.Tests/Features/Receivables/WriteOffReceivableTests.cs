using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Receivables;
using Cleansia.Core.Clients.Abstractions.Stripe;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StripeException = Stripe.StripeException;

namespace Cleansia.Tests.Features.Receivables;

/// <summary>
/// An administrator writes off an open receivable (owner ruling 2026-09-28, decision 18): it records who,
/// when and why; one already written off, or one the company does not hold, is refused and left as it is.
/// Its pay link is closed at Stripe first (owner ruling 2026-10-06), so the customer cannot pay what was
/// just written off; a link Stripe reports paid, or a Stripe that cannot be reached, refuses the write-off.
/// </summary>
public sealed class WriteOffReceivableTests
{
    private const string AdminId = "admin-write-off";
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IReceivableRepository> _receivables = new();
    private readonly Mock<IStripeClient> _stripe = new();

    [Fact]
    public async Task Writing_Off_An_Open_Receivable_Records_Who_When_And_Why()
    {
        var receivable = Owed();

        var result = await Handler().Handle(new WriteOffReceivable.Command(receivable.Id, "Goodwill after a complaint"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(new WriteOffReceivable.Response(receivable.Id, ReceivableStatus.WrittenOff), result.Value);
        Assert.Equal(ReceivableStatus.WrittenOff, receivable.Status);
        Assert.Equal((AdminId, Now, "Goodwill after a complaint"),
            (receivable.WrittenOffByUserId, receivable.WrittenOffOn, receivable.WriteOffNote));
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task A_Receivable_Already_Written_Off_Is_Refused_And_Keeps_Its_First_Record()
    {
        var receivable = Owed();
        receivable.RecordPayLink("cs_already_written_off");
        receivable.WriteOff("first-admin", "First note", Now.AddDays(-1));

        var result = await Handler().Handle(new WriteOffReceivable.Command(receivable.Id, "Second note"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReceivableNotOpen, result.Error!.Message);
        Assert.Equal(nameof(WriteOffReceivable.Command.ReceivableId), result.Error.Code);
        Assert.Equal(("first-admin", "First note"), (receivable.WrittenOffByUserId, receivable.WriteOffNote));
        _stripe.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Writing_Off_Closes_The_Pay_Link_At_Stripe_Before_The_Receivable()
    {
        var receivable = Owed();
        receivable.RecordPayLink("cs_write_off");
        _stripe.Setup(s => s.ExpireReceivableCheckoutSessionAsync("cs_write_off", It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(ReceivableStatus.Open, receivable.Status))
            .ReturnsAsync(true);

        var result = await Handler().Handle(new WriteOffReceivable.Command(receivable.Id, "Goodwill"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(ReceivableStatus.WrittenOff, receivable.Status);
        _stripe.Verify(s => s.ExpireReceivableCheckoutSessionAsync("cs_write_off", It.IsAny<CancellationToken>()), Times.Once);
        _stripe.VerifyNoOtherCalls();
    }

    /// <summary>Stripe answers that the pay link was paid: the debt is settled online and its webhook has not landed yet.</summary>
    [Fact]
    public async Task A_Pay_Link_Stripe_Reports_Paid_Refuses_The_Write_Off()
    {
        var receivable = Owed();
        receivable.RecordPayLink("cs_paid");
        _stripe.Setup(s => s.ExpireReceivableCheckoutSessionAsync("cs_paid", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await Handler().Handle(new WriteOffReceivable.Command(receivable.Id, "Goodwill"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReceivableNotOpen, result.Error!.Message);
        Assert.Equal(nameof(WriteOffReceivable.Command.ReceivableId), result.Error.Code);
        Assert.Equal(ReceivableStatus.Open, receivable.Status);
        Assert.Null(receivable.WrittenOffOn);
    }

    [Fact]
    public async Task An_Unreachable_Stripe_Refuses_The_Write_Off_And_Leaves_The_Receivable_Open()
    {
        var receivable = Owed();
        receivable.RecordPayLink("cs_unreachable");
        _stripe.Setup(s => s.ExpireReceivableCheckoutSessionAsync("cs_unreachable", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new StripeException("Stripe is unavailable"));

        var result = await Handler().Handle(new WriteOffReceivable.Command(receivable.Id, "Goodwill"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.PaymentGatewayUnavailable, result.Error!.Message);
        Assert.Equal(nameof(WriteOffReceivable.Command.ReceivableId), result.Error.Code);
        Assert.Equal(ReceivableStatus.Open, receivable.Status);
        Assert.Null(receivable.WrittenOffOn);
    }

    [Fact]
    public async Task A_Receivable_The_Company_Does_Not_Hold_Is_Not_Found()
    {
        var result = await Handler().Handle(new WriteOffReceivable.Command("receivable-elsewhere", "Note"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReceivableNotFound, result.Error!.Message);
        Assert.Equal(nameof(WriteOffReceivable.Command.ReceivableId), result.Error.Code);
    }

    [Theory]
    [InlineData("", "Note", BusinessErrorMessage.Required)]
    [InlineData("receivable-1", "", BusinessErrorMessage.Required)]
    public async Task The_Receivable_And_A_Note_Are_Required(string receivableId, string note, string expected)
    {
        var result = await new WriteOffReceivable.Validator().ValidateAsync(new WriteOffReceivable.Command(receivableId, note));

        Assert.Equal(expected, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Note_Longer_Than_500_Characters_Is_Refused()
    {
        var result = await new WriteOffReceivable.Validator().ValidateAsync(
            new WriteOffReceivable.Command("receivable-1", new string('x', 501)));

        Assert.Equal(BusinessErrorMessage.MaxLength, Assert.Single(result.Errors).ErrorMessage);
    }

    private Receivable Owed()
    {
        var order = OrderMockFactory.Generate(new OrderMockFactory.OrderPartial
        {
            UserId = "customer-owing",
            PaymentType = PaymentType.Cash,
            PaymentStatus = PaymentStatus.Pending,
            TotalPrice = 1000m,
        });
        var receivable = Receivable.ForCashCancellationFee(order, 250m);
        _receivables.Setup(r => r.GetByIdAsync(receivable.Id, It.IsAny<CancellationToken>())).ReturnsAsync(receivable);
        return receivable;
    }

    private WriteOffReceivable.Handler Handler()
    {
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(AdminId);
        return new WriteOffReceivable.Handler(
            _receivables.Object,
            _stripe.Object,
            session.Object,
            new StubTimeProvider(Now),
            NullLogger<WriteOffReceivable.Handler>.Instance);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
