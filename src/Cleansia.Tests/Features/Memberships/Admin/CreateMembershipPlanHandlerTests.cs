using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// Admin membership-plan creation. Happy-path persists via MembershipPlan.Create
/// (code upper-cased); a case-insensitive duplicate code resolved through
/// GetByCodeAsync is rejected with MembershipPlanCodeAlreadyExists and no row is
/// added. Written TEST-FIRST (RED before the handler existed).
/// </summary>
public class CreateMembershipPlanHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();

    private CreateMembershipPlan.Handler CreateHandler() => new(_planRepository.Object);

    private static CreateMembershipPlan.Command ValidCommand(string code = "PLUS_MONTHLY") =>
        new(
            Code: code,
            Name: "Plus Monthly",
            BillingInterval: BillingInterval.Monthly,
            MonthlyPriceCzk: 199m,
            StripePriceId: "price_plus_monthly",
            DiscountPercentage: 5m,
            FreeCancellationWindowHours: 4,
            TrialPeriodDays: 0,
            AllowsExpressUpgrade: true);

    // AC2 — a unique code persists a new active plan via the entity factory.
    [Fact]
    public async Task Create_UniqueCode_PersistsNewPlan()
    {
        _planRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPlan?)null);

        MembershipPlan? added = null;
        _planRepository.Setup(r => r.Add(It.IsAny<MembershipPlan>()))
            .Callback<MembershipPlan>(p => added = p);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("PLUS_MONTHLY", added!.Code);
        Assert.True(added.IsActive);
        Assert.Equal("price_plus_monthly", added.StripePriceId);
        _planRepository.Verify(r => r.Add(It.IsAny<MembershipPlan>()), Times.Once);
    }

    // AC3 — a duplicate code (resolved case-insensitively via GetByCodeAsync) is rejected, no row added.
    [Fact]
    public async Task Create_DuplicateCode_IsRejected_NoRowAdded()
    {
        var existing = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            monthlyPriceCzk: 199m,
            stripePriceId: "price_existing",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);

        // The repo upper-cases the lookup, so a lowercase submission still collides.
        _planRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(ValidCommand("plus_monthly"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipPlanCodeAlreadyExists, result.Error!.Code);
        _planRepository.Verify(r => r.Add(It.IsAny<MembershipPlan>()), Times.Never);
    }

    // Handler is happy-path only — it never commits (the UoW pipeline owns the commit).
    [Fact]
    public async Task Create_Handler_DoesNotCommit()
    {
        _planRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPlan?)null);

        await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        _planRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
