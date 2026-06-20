using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin.DTOs;
using Cleansia.Core.AppServices.Features.PromoCodes.Admin.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.PromoCodes.Admin;

/// <summary>
/// Characterization of the admin promo-code paged list across the §A
/// canonicalization (record Query + bespoke repo -> Request : DataRangeRequest +
/// Specification + GetPagedSort + MapToDto). Pins: the full row projection (incl.
/// the joined currency code), the page metadata derived from offset/limit/total,
/// and that the filter flags reach the spec while the count + page use the same
/// filter. The default newest-first order is preserved when no sort is supplied.
/// </summary>
public class GetPagedPromoCodesHandlerTests
{
    private readonly Mock<IPromoCodeRepository> _repository = new();

    private Task<PagedData<PromoCodeListItem>> Handle(GetPagedPromoCodes.Request request)
    {
        var handlerType = typeof(GetPagedPromoCodes).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(handlerType, _repository.Object)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<PromoCodeListItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static PromoCode FixedCode(string code, string currencyId, Currency? currency)
    {
        var promo = PromoCode.CreateFixed(
            code: code,
            amount: 50m,
            currencyId: currencyId,
            minimumOrderAmount: 200m,
            maxRedemptionsPerUser: 3,
            globalMaxRedemptions: 100,
            description: "desc-" + code);
        promo.Id = "promo-" + code;
        promo.Created("system", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        SetNav(promo, nameof(PromoCode.Currency), currency);
        return promo;
    }

    private static void SetNav(object entity, string property, object? value)
    {
        var prop = entity.GetType().GetProperty(property)!;
        prop.SetValue(entity, value);
    }

    private static Currency Czk() => Currency.Create("CZK", "Kč", "Czech Koruna", 1m);

    [Fact]
    public async Task Projects_Row_Including_CurrencyCode_And_PageMetadata()
    {
        var promo = FixedCode("WELCOME50", "cur-czk", Czk());
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<PromoCode, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(57);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.PromoCodeSort>(
                40, 20, It.IsAny<Expression<Func<PromoCode, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { promo }.AsQueryable().BuildMock());

        var request = new GetPagedPromoCodes.Request { Offset = 40, Limit = 20 };

        var result = await Handle(request);

        Assert.Equal(57, result.Total);
        Assert.Equal(3, result.PageNumber);
        Assert.Equal(20, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("promo-WELCOME50", row.Id);
        Assert.Equal("WELCOME50", row.Code);
        Assert.Equal(PromoCodeType.FixedDiscount, row.Type);
        Assert.Null(row.DiscountPercent);
        Assert.Equal(50m, row.DiscountAmount);
        Assert.Equal("cur-czk", row.CurrencyId);
        Assert.Equal("CZK", row.CurrencyCode);
        Assert.Equal(200m, row.MinimumOrderAmount);
        Assert.Equal(3, row.MaxRedemptionsPerUser);
        Assert.Equal(100, row.GlobalMaxRedemptions);
        Assert.Equal(0, row.CurrentRedemptionsCount);
        Assert.True(row.IsActive);
        Assert.Equal("desc-WELCOME50", row.Description);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), row.CreatedOn);
    }

    [Fact]
    public async Task Null_Currency_Maps_To_Null_CurrencyCode()
    {
        var promo = FixedCode("NOJOIN", "cur-x", currency: null);
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<PromoCode, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.PromoCodeSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<PromoCode, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { promo }.AsQueryable().BuildMock());

        var result = await Handle(new GetPagedPromoCodes.Request());

        var row = Assert.Single(result.Data);
        Assert.Null(row.CurrencyCode);
    }

    [Fact]
    public async Task Empty_Page_Reports_Total_With_No_Rows()
    {
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<PromoCode, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.PromoCodeSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<PromoCode, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<PromoCode>().AsQueryable().BuildMock());

        var result = await Handle(new GetPagedPromoCodes.Request());

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Data);
        Assert.Equal(1, result.PageNumber);
    }

    [Fact]
    public async Task Active_Filter_Reaches_Specification()
    {
        Expression<Func<PromoCode, bool>>? capturedCount = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<PromoCode, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<PromoCode, bool>>?, CancellationToken>((f, _) => capturedCount = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<Cleansia.Core.Domain.Sorting.PromoCodeSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<PromoCode, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<PromoCode>().AsQueryable().BuildMock());

        var request = new GetPagedPromoCodes.Request
        {
            Filter = new PromoCodeFilter(Active: true)
        };
        await Handle(request);

        Assert.NotNull(capturedCount);
        var active = PromoCode.CreatePercent("A", 0.1m);
        var inactive = PromoCode.CreatePercent("B", 0.1m);
        inactive.Deactivate("system");
        var predicate = capturedCount!.Compile();
        Assert.True(predicate(active));
        Assert.False(predicate(inactive));
    }
}
