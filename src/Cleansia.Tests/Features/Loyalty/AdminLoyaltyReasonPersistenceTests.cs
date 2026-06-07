using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

public class AdminLoyaltyReasonPersistenceTests
{
    private const string UserId = "user-1";
    private const string ActorId = "admin-1";
    private const string RequestId = "req-idem-abc";
    private const string Reason = "goodwill credit #4821";
    private const int Points = 250;

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<ILoyaltyAccountRepository> _accountRepository = new();
    private readonly Mock<ILoyaltyTierConfigRepository> _tierConfigRepository = new();
    private readonly Mock<ILoyaltyTransactionRepository> _transactionRepository = new();
    private readonly Mock<IPendingDispatch> _pendingDispatch = new();

    private LoyaltyService CreateService() =>
        new(
            _orderRepository.Object,
            _accountRepository.Object,
            _tierConfigRepository.Object,
            _transactionRepository.Object,
            _pendingDispatch.Object,
            NullLogger<LoyaltyService>.Instance);

    private LoyaltyAccount ArrangeAccount()
    {
        var account = LoyaltyAccount.Create(UserId);
        account.Id = "acct-1";
        _accountRepository
            .Setup(r => r.EnsureForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _accountRepository
            .Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        return account;
    }

    private void ArrangeNoExistingKey()
    {
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoyaltyTransaction?)null);
    }

    private void ArrangeTierConfigs()
    {
        var configs = new[]
        {
            LoyaltyTierConfig.Create(LoyaltyTier.SilverMopper, 500, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.GoldPolisher, 2000, 0m, null, "[]"),
            LoyaltyTierConfig.Create(LoyaltyTier.PlatinumSparkler, 5000, 0m, null, "[]"),
        };
        _tierConfigRepository
            .Setup(r => r.GetAllForTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);
    }

    [Fact]
    public async Task Grant_PersistsReason_AsLedgerDescription()
    {
        var account = ArrangeAccount();
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService().GrantPointsManuallyAsync(
            UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);

        Assert.Equal(Reason, account.Transactions.Single().Description);
    }

    [Fact]
    public async Task Revoke_PersistsReason_AsLedgerDescription()
    {
        var account = ArrangeAccount();
        account.GrantPoints(1000, LoyaltyEarnSource.OrderCompleted, "order-x", "system", DefaultThresholds());
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService().RevokePointsManuallyAsync(
            UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);

        var revokeRow = account.Transactions.Single(t => t.IdempotencyKey == RequestId);
        Assert.Equal(Reason, revokeRow.Description);
    }

    [Fact]
    public void OrderDrivenGrant_KeepsNullDescription()
    {
        var account = LoyaltyAccount.Create(UserId);

        account.GrantPoints(100, LoyaltyEarnSource.OrderCompleted, "order-1", "system", DefaultThresholds());

        Assert.Null(account.Transactions.Single().Description);
    }

    [Fact]
    public async Task Grant_DoubleSubmitSameRequestId_LandsOnceWithReason()
    {
        var account = ArrangeAccount();
        ArrangeTierConfigs();

        LoyaltyTransaction? existing = null;
        _transactionRepository
            .Setup(r => r.GetByIdempotencyKeyAsync(RequestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => existing);
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => existing ??= account.Transactions.LastOrDefault())
            .Returns(Task.CompletedTask);

        var service = CreateService();
        await service.GrantPointsManuallyAsync(
            UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);
        await service.GrantPointsManuallyAsync(
            UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None);

        var surviving = Assert.Single(account.Transactions);
        Assert.Equal(Points, account.LifetimePoints);
        Assert.Equal(Reason, surviving.Description);
        Assert.Equal(RequestId, surviving.IdempotencyKey);
    }

    [Fact]
    public async Task Grant_ConcurrentSameRequestId_UniqueViolation_Collapses_WithoutThrow()
    {
        ArrangeAccount();
        ArrangeNoExistingKey();
        ArrangeTierConfigs();
        _transactionRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint",
                new FakePostgresUniqueViolationException()));

        var ex = await Record.ExceptionAsync(() =>
            CreateService().GrantPointsManuallyAsync(
                UserId, Points, LoyaltyEarnSource.ManualGrant, null, ActorId, Reason, RequestId, CancellationToken.None));

        Assert.Null(ex);
        _transactionRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static LoyaltyTierThresholds DefaultThresholds() =>
        new(Silver: 500, Gold: 2000, Platinum: 5000);

    private sealed class FakePostgresUniqueViolationException : Exception
    {
        public string SqlState => "23505";
    }
}
