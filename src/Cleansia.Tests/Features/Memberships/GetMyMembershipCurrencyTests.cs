using Cleansia.Core.AppServices.Features.Memberships;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Memberships;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities.MockDataFactories.Memberships;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// The management screen shows the subscription in the currency it is billed in — its own row's
/// figures and its own code — never the market the customer happens to be browsing. A membership
/// whose plan row has since gone renders no price rather than a wrong one.
/// </summary>
public class GetMyMembershipCurrencyTests
{
    private const string UserId = "user-1";

    private readonly Mock<IUserMembershipRepository> _membershipRepository = new();
    private readonly Mock<IMembershipPlanPriceRepository> _priceRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IExpressWaiverResolver> _waiver = new();
    private readonly UserMembership _membership;

    public GetMyMembershipCurrencyTests()
    {
        _session.Setup(s => s.GetUserId()).Returns(UserId);
        _membership = UserMembershipMockFactory.Paid(UserId);
        _membershipRepository
            .Setup(r => r.GetActiveForUserNoTrackingAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_membership);
        _waiver
            .Setup(w => w.ResolveForUserAsync(UserId, null, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExpressWaiver.None("2026-09"));
    }

    private GetMyMembership.Handler Handler() =>
        new(_membershipRepository.Object, _priceRepository.Object, _session.Object, _waiver.Object,
            NullLogger<GetMyMembership.Handler>.Instance);

    [Fact]
    public async Task TheMembershipsOwnRow_AndItsOwnCode_AreRendered()
    {
        _priceRepository.PriceIn(_membership.MembershipPlanId, MembershipPricingMockFactory.CzkCurrencyId, "price_czk", 199m);
        _priceRepository.PriceIn(_membership.MembershipPlanId, MembershipPricingMockFactory.EurCurrencyId, "price_eur", 7.99m);

        var result = await Handler().Handle(new GetMyMembership.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("CZK", result.Value.CurrencyCode);
        Assert.Equal(199m, result.Value.Price);
        Assert.Equal(199m, result.Value.MonthlyEquivalentPrice);
    }

    [Fact]
    public async Task AMembershipWhosePlanRowIsGone_RendersNoPrice_AndStillItsCode()
    {
        var result = await Handler().Handle(new GetMyMembership.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.HasMembership);
        Assert.Null(result.Value.Price);
        Assert.Null(result.Value.MonthlyEquivalentPrice);
        Assert.Equal("CZK", result.Value.CurrencyCode);
    }
}
