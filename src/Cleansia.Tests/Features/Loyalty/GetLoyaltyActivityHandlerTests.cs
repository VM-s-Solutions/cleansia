using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Loyalty;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// Characterization of the customer loyalty activity ledger across the §A
/// canonicalization (record Query + bespoke GetForAccountAsync/CountForAccountAsync
/// -> Request : DataRangeRequest + LoyaltyTransactionSpecification + GetPagedSort +
/// MapToDto). Pins the account-not-found short-circuit, the row projection, page
/// metadata, that the account id reaches the spec, and that the order-display-number
/// enrichment still runs POST-materialization. Default order (OccurredOn desc) preserved.
/// </summary>
public class GetLoyaltyActivityHandlerTests
{
    private const string UserId = "user-1";
    private const string AccountId = "acct-1";
    private const string OrderId = "order-1";

    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();

    private Task<PagedData<GetLoyaltyActivity.ActivityItem>> Handle(GetLoyaltyActivity.Request request)
    {
        var handlerType = typeof(GetLoyaltyActivity).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(
            handlerType,
            _accountRepository.Object,
            _transactionRepository.Object,
            _orderRepository.Object,
            _session.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<GetLoyaltyActivity.ActivityItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static LoyaltyTransaction Transaction(string? orderId) =>
        LoyaltyTransaction.Create(AccountId, LoyaltyTransactionType.Earn, 50, LoyaltyEarnSource.OrderCompleted, orderId);

    private static Order OrderWith(string id)
    {
        var order = Order.Create(
            customerName: "C",
            customerEmail: "c@x.test",
            customerPhone: "+420123456789",
            customerAddress: null!,
            rooms: 1,
            bathrooms: 1,
            extras: new Dictionary<string, bool>(),
            cleaningDateTime: DateTime.UtcNow.AddDays(1),
            paymentType: PaymentType.Cash,
            totalPrice: 100m,
            currencyId: "currency-1",
            paymentStatus: PaymentStatus.Paid,
            userId: UserId);
        order.Id = id;
        return order;
    }

    [Fact]
    public async Task Missing_Account_Short_Circuits_To_Empty_Page()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _accountRepository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyAccount?)null);

        var result = await Handle(new GetLoyaltyActivity.Request { Offset = 0, Limit = 20 });

        Assert.Equal(0, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        Assert.Empty(result.Data);

        _transactionRepository.Verify(
            r => r.GetCountAsync(It.IsAny<Expression<Func<LoyaltyTransaction, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Projects_Rows_And_Enriches_DisplayNumber_Post_Materialization()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        var account = LoyaltyAccount.Create(UserId);
        account.Id = AccountId;
        _accountRepository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var order = OrderWith(OrderId);
        var withOrder = Transaction(OrderId);
        var withoutOrder = Transaction(null);

        _transactionRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<LoyaltyTransaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _transactionRepository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.LoyaltyTransactionSort>(
                0, 20, It.IsAny<Expression<Func<LoyaltyTransaction, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { withOrder, withoutOrder }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());

        var result = await Handle(new GetLoyaltyActivity.Request { Offset = 0, Limit = 20 });

        Assert.Equal(2, result.Total);
        var data = result.Data.ToList();
        Assert.Equal(2, data.Count);

        var enriched = data.Single(d => d.OrderId == OrderId);
        Assert.Equal(order.DisplayOrderNumber, enriched.OrderDisplayNumber);
        Assert.Equal(LoyaltyTransactionType.Earn, enriched.Type);
        Assert.Equal(50, enriched.Points);
        Assert.Equal(LoyaltyEarnSource.OrderCompleted, enriched.Source);

        var plain = data.Single(d => d.OrderId == null);
        Assert.Null(plain.OrderDisplayNumber);
    }

    [Fact]
    public async Task Account_Id_Reaches_Specification()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        var account = LoyaltyAccount.Create(UserId);
        account.Id = AccountId;
        _accountRepository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        Expression<Func<LoyaltyTransaction, bool>>? captured = null;
        _transactionRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<LoyaltyTransaction, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<LoyaltyTransaction, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _transactionRepository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.LoyaltyTransactionSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<LoyaltyTransaction, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<LoyaltyTransaction>().AsQueryable().BuildMock());

        await Handle(new GetLoyaltyActivity.Request());

        Assert.NotNull(captured);
        var predicate = captured!.Compile();
        var mine = LoyaltyTransaction.Create(AccountId, LoyaltyTransactionType.Earn, 1, LoyaltyEarnSource.OrderCompleted, null);
        var other = LoyaltyTransaction.Create("other-acct", LoyaltyTransactionType.Earn, 1, LoyaltyEarnSource.OrderCompleted, null);
        Assert.True(predicate(mine));
        Assert.False(predicate(other));
    }
}
