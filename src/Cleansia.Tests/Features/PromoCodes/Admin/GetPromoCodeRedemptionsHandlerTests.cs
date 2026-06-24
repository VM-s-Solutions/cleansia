using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.PromoCodes.Admin;

/// <summary>
/// Characterization of the per-code redemption log across the §A canonicalization
/// (record Query + bespoke GetPagedByPromoCodeAsync/CountByPromoCodeAsync ->
/// Request : DataRangeRequest + PromoCodeRedemptionSpecification + GetPagedSort + MapToDto).
/// Pins the empty-id short-circuit, the row projection (incl. joined user email), page
/// metadata, and that the promo-code id reaches the spec. Default order (RedeemedOn desc) preserved.
/// </summary>
public class GetPromoCodeRedemptionsHandlerTests
{
    private const string PromoCodeId = "promo-1";

    private readonly Mock<IPromoCodeRedemptionRepository> _repository = new();

    private Task<PagedData<PromoCodeRedemptionListItem>> Handle(GetPromoCodeRedemptions.Request request)
    {
        var handlerType = typeof(GetPromoCodeRedemptions).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<PromoCodeRedemptionListItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static PromoCodeRedemption Redemption(User? user)
    {
        var redemption = PromoCodeRedemption.CreateReserved(PromoCodeId, "user-1", "order-1", 25m, 0);
        redemption.Id = "redemption-1";
        var prop = typeof(PromoCodeRedemption).GetProperty(nameof(PromoCodeRedemption.User))!;
        prop.SetValue(redemption, user);
        return redemption;
    }

    [Fact]
    public async Task Empty_PromoCodeId_Short_Circuits_To_Empty_Page()
    {
        var result = await Handle(new GetPromoCodeRedemptions.Request { PromoCodeId = string.Empty, Offset = 0, Limit = 20 });

        Assert.Equal(0, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(20, result.PageSize);
        Assert.Empty(result.Data);

        _repository.Verify(
            r => r.GetCountAsync(It.IsAny<Expression<Func<PromoCodeRedemption, bool>>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Projects_Row_With_User_Email_And_PageMetadata()
    {
        var user = User.CreateWithPassword("redeemer@x.test", "Passw0rd!", "Red", "Eemer");
        var redemption = Redemption(user);

        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<PromoCodeRedemption, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(12);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.PromoCodeRedemptionSort>(
                20, 10, It.IsAny<Expression<Func<PromoCodeRedemption, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { redemption }.AsQueryable().BuildMock());

        var result = await Handle(new GetPromoCodeRedemptions.Request { PromoCodeId = PromoCodeId, Offset = 20, Limit = 10 });

        Assert.Equal(12, result.Total);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(10, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("redemption-1", row.Id);
        Assert.Equal(PromoCodeId, row.PromoCodeId);
        Assert.Equal("user-1", row.UserId);
        Assert.Equal("redeemer@x.test", row.UserEmail);
        Assert.Equal("order-1", row.OrderId);
        Assert.Equal(25m, row.AppliedDiscount);
    }

    [Fact]
    public async Task Null_User_Maps_To_Null_Email()
    {
        var redemption = Redemption(user: null);
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<PromoCodeRedemption, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.PromoCodeRedemptionSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<PromoCodeRedemption, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { redemption }.AsQueryable().BuildMock());

        var result = await Handle(new GetPromoCodeRedemptions.Request { PromoCodeId = PromoCodeId });

        var row = Assert.Single(result.Data);
        Assert.Null(row.UserEmail);
    }

    [Fact]
    public async Task PromoCodeId_Reaches_Specification()
    {
        Expression<Func<PromoCodeRedemption, bool>>? captured = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<PromoCodeRedemption, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<PromoCodeRedemption, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.PromoCodeRedemptionSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<PromoCodeRedemption, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<PromoCodeRedemption>().AsQueryable().BuildMock());

        await Handle(new GetPromoCodeRedemptions.Request { PromoCodeId = PromoCodeId });

        Assert.NotNull(captured);
        var predicate = captured!.Compile();
        var mine = PromoCodeRedemption.CreateReserved(PromoCodeId, "u", "o1", 1m, 0);
        var other = PromoCodeRedemption.CreateReserved("other-code", "u", "o2", 1m, 0);
        Assert.True(predicate(mine));
        Assert.False(predicate(other));
    }
}
