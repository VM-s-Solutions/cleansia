using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Loyalty.Admin;

/// <summary>
/// Characterization of the admin user-explicit loyalty ledger across the §A
/// canonicalization (record Query + bespoke GetForAccountAsync/CountForAccountAsync ->
/// Request : DataRangeRequest + LoyaltyTransactionSpecification + GetPagedSort + MapToDto).
/// Pins the empty-user and account-not-found short-circuits, the row projection (incl. Id +
/// Description), page metadata, that the account id reaches the spec, and the POST-materialization
/// display-number enrichment. Default order (OccurredOn desc) preserved.
/// </summary>
public class GetUserLoyaltyActivityHandlerTests
{
    private const string UserId = "user-1";
    private const string AccountId = "acct-1";
    private const string OrderId = "order-1";

    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<IOrderRepository> _orderRepository = new();

    private Task<PagedData<GetUserLoyaltyActivity.ActivityItem>> Handle(GetUserLoyaltyActivity.Request request)
    {
        var handlerType = typeof(GetUserLoyaltyActivity).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(
            handlerType,
            _accountRepository.Object,
            _transactionRepository.Object,
            _orderRepository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<GetUserLoyaltyActivity.ActivityItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static LoyaltyTransaction Transaction(string id, string? orderId)
    {
        var tx = LoyaltyTransaction.Create(
            AccountId, LoyaltyTransactionType.Earn, 50, LoyaltyEarnSource.OrderCompleted, orderId, "manual grant");
        tx.Id = id;
        return tx;
    }

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
    public async Task Empty_UserId_Short_Circuits_To_Empty_Page()
    {
        var result = await Handle(new GetUserLoyaltyActivity.Request { UserId = string.Empty, Offset = 0, Limit = 20 });

        Assert.Equal(0, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        Assert.Empty(result.Data);

        _accountRepository.Verify(
            r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Missing_Account_Short_Circuits_To_Empty_Page()
    {
        _accountRepository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyAccount?)null);

        var result = await Handle(new GetUserLoyaltyActivity.Request { UserId = UserId, Offset = 0, Limit = 20 });

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task Projects_Rows_With_Id_And_Description_And_Enriches_DisplayNumber()
    {
        var account = LoyaltyAccount.Create(UserId);
        account.Id = AccountId;
        _accountRepository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var order = OrderWith(OrderId);
        var withOrder = Transaction("tx-1", OrderId);
        var withoutOrder = Transaction("tx-2", null);

        _transactionRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<LoyaltyTransaction, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        _transactionRepository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.LoyaltyTransactionSort>(
                10, 5, It.IsAny<Expression<Func<LoyaltyTransaction, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { withOrder, withoutOrder }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetQueryable())
            .Returns(new[] { order }.AsQueryable().BuildMock());

        var result = await Handle(new GetUserLoyaltyActivity.Request { UserId = UserId, Offset = 10, Limit = 5 });

        Assert.Equal(7, result.Total);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(5, result.PageSize);

        var data = result.Data.ToList();
        var enriched = data.Single(d => d.Id == "tx-1");
        Assert.Equal(OrderId, enriched.OrderId);
        Assert.Equal(order.DisplayOrderNumber, enriched.OrderDisplayNumber);
        Assert.Equal("manual grant", enriched.Description);

        var plain = data.Single(d => d.Id == "tx-2");
        Assert.Null(plain.OrderDisplayNumber);
    }

    [Fact]
    public async Task Account_Id_Reaches_Specification()
    {
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

        await Handle(new GetUserLoyaltyActivity.Request { UserId = UserId });

        Assert.NotNull(captured);
        var predicate = captured!.Compile();
        var mine = LoyaltyTransaction.Create(AccountId, LoyaltyTransactionType.Earn, 1, LoyaltyEarnSource.OrderCompleted, null);
        var other = LoyaltyTransaction.Create("other-acct", LoyaltyTransactionType.Earn, 1, LoyaltyEarnSource.OrderCompleted, null);
        Assert.True(predicate(mine));
        Assert.False(predicate(other));
    }
}
