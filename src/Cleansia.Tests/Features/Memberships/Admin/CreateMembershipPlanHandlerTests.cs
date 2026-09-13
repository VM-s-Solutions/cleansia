using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Memberships.Admin;
using Cleansia.Core.AppServices.Features.Memberships.Admin.DTOs;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Memberships.Admin;

/// <summary>
/// Admin membership-plan creation. Happy-path persists the plan via MembershipPlan.Create (code
/// upper-cased) and one price row per currency sent; a case-insensitive duplicate code resolved
/// through GetByCodeAsync is rejected with MembershipPlanCodeAlreadyExists and nothing is added.
/// </summary>
public class CreateMembershipPlanHandlerTests
{
    private readonly Mock<IMembershipPlanRepository> _planRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<ICurrencyRepository> _currencyRepository = new();

    public CreateMembershipPlanHandlerTests()
    {
        _currencyRepository
            .Setup(r => r.GetAll())
            .Returns(new[] { MembershipPricingMockFactory.Czk(), MembershipPricingMockFactory.Eur() }.AsQueryable().BuildMock());
    }

    private CreateMembershipPlan.Handler CreateHandler() =>
        new(_planRepository.Object, _priceRepository.Object, _currencyRepository.Object);

    private static CreateMembershipPlan.Command ValidCommand(string code = "PLUS_MONTHLY") =>
        new(
            Code: code,
            Name: "Plus Monthly",
            BillingInterval: BillingInterval.Monthly,
            Prices: new Dictionary<string, MembershipPlanPriceInput> { ["CZK"] = new(199m, "price_plus_monthly") },
            DiscountPercentage: 5m,
            FreeCancellationWindowHours: 4,
            TrialPeriodDays: 0,
            AllowsExpressUpgrade: true);

    [Fact]
    public async Task Create_UniqueCode_PersistsNewPlan_AndOnePriceRowPerCurrencySent()
    {
        _planRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPlan?)null);

        MembershipPlan? added = null;
        _planRepository.Setup(r => r.Add(It.IsAny<MembershipPlan>()))
            .Callback<MembershipPlan>(p => added = p);
        var prices = new List<MembershipPlanPrice>();
        _priceRepository.Setup(r => r.Add(It.IsAny<MembershipPlanPrice>())).Callback<MembershipPlanPrice>(prices.Add);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("PLUS_MONTHLY", added!.Code);
        Assert.True(added.IsActive);
        var price = Assert.Single(prices);
        Assert.Equal(added.Id, price.MembershipPlanId);
        Assert.Equal(MembershipPricingMockFactory.CzkCurrencyId, price.CurrencyId);
        Assert.Equal(199m, price.Price);
        Assert.Equal("price_plus_monthly", price.StripePriceId);
    }

    [Fact]
    public async Task Create_WithNoPrices_PersistsThePlanAndNoRow()
    {
        _planRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPlan?)null);

        var result = await CreateHandler().Handle(ValidCommand() with { Prices = null }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _planRepository.Verify(r => r.Add(It.IsAny<MembershipPlan>()), Times.Once);
        _priceRepository.Verify(r => r.Add(It.IsAny<MembershipPlanPrice>()), Times.Never);
    }

    [Fact]
    public async Task Create_DuplicateCode_IsRejected_NothingAdded()
    {
        var existing = MembershipPlan.Create(
            code: "PLUS_MONTHLY",
            name: "Plus Monthly",
            discountPercentage: 5m,
            freeCancellationWindowHours: 4,
            allowsExpressUpgrade: true,
            billingInterval: BillingInterval.Monthly,
            trialPeriodDays: 0);

        _planRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(ValidCommand("plus_monthly"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.MembershipPlanCodeAlreadyExists, result.Error!.Message);
        Assert.Equal(nameof(CreateMembershipPlan.Command.Code), result.Error.Code);
        _planRepository.Verify(r => r.Add(It.IsAny<MembershipPlan>()), Times.Never);
        _priceRepository.Verify(r => r.Add(It.IsAny<MembershipPlanPrice>()), Times.Never);
    }

    [Fact]
    public async Task Create_Handler_DoesNotCommit()
    {
        _planRepository
            .Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MembershipPlan?)null);

        await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        _planRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        _priceRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
