using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Receivables;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Payments;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Orders;
using Moq;

namespace Cleansia.Tests.Features.Receivables;

/// <summary>
/// An administrator writes off an open receivable (owner ruling 2026-09-28, decision 18): it records who,
/// when and why; one already written off, or one the company does not hold, is refused and left as it is.
/// </summary>
public sealed class WriteOffReceivableTests
{
    private const string AdminId = "admin-write-off";
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IReceivableRepository> _receivables = new();

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
    }

    [Fact]
    public async Task A_Receivable_Already_Written_Off_Is_Refused_And_Keeps_Its_First_Record()
    {
        var receivable = Owed();
        receivable.WriteOff("first-admin", "First note", Now.AddDays(-1));

        var result = await Handler().Handle(new WriteOffReceivable.Command(receivable.Id, "Second note"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReceivableNotOpen, result.Error!.Message);
        Assert.Equal(nameof(WriteOffReceivable.Command.ReceivableId), result.Error.Code);
        Assert.Equal(("first-admin", "First note"), (receivable.WrittenOffByUserId, receivable.WriteOffNote));
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
        return new WriteOffReceivable.Handler(_receivables.Object, session.Object, new StubTimeProvider(Now));
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
