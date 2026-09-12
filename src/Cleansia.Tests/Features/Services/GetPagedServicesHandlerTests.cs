using Cleansia.Core.Domain.Internationalization;
using System.Linq.Expressions;
using System.Reflection;
using Cleansia.Core.AppServices.Features.Services;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Features.Services.Filters;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Services;

/// <summary>
/// Characterization of the admin services paged list across the A6 read-path
/// canonicalization (materialize-then-map -> Include/AsNoTracking/Select-in-query).
/// Pins the row projection (incl. the joined category) and page metadata, and that
/// the search/active filter reaches the spec — the projection result must be
/// byte-identical whether mapped before or after materialization.
/// </summary>
public class GetPagedServicesHandlerTests
{
    private const string CurrencyId = "cur-czk";
    private const string ServiceId = "svc-1";

    private readonly Mock<IServiceRepository> _repository = new();
    private readonly Mock<IServicePriceRepository> _prices = new();
    private readonly ICurrencyRepository _currencies;

    public GetPagedServicesHandlerTests()
    {
        var currency = Currency.Create("CZK", "Kč", "Czech Koruna");
        currency.Id = CurrencyId;
        currency.SetAsDefault(true);
        _currencies = CataloguePriceDoubles.DefaultCurrency(currency);

        // The row the admin list must READ. The numbers are the ones this suite has always pinned;
        // what moved is where they live — a price row in the default currency rather than a column on
        // the service — so an assertion that still sees 999 is the projection reaching the right table.
        ArrangePrices(ServicePrice.Create(ServiceId, CurrencyId, basePrice: 999m, perRoomPrice: 120m));
    }

    private void ArrangePrices(params ServicePrice[] prices) =>
        _prices.Setup(r => r.GetAll()).Returns(prices.AsQueryable().BuildMock());

    private Task<PagedData<ServiceListItem>> Handle(GetPagedServices.Request request)
    {
        var handlerType = typeof(GetPagedServices).GetNestedType("Handler", BindingFlags.NonPublic)!;
        var handler = Activator.CreateInstance(
            handlerType, _repository.Object, _prices.Object, _currencies)!;
        var method = handlerType.GetMethod("Handle")!;
        return (Task<PagedData<ServiceListItem>>)method.Invoke(handler, [request, CancellationToken.None])!;
    }

    private static Service ServiceWithCategory()
    {
        var category = ServiceCategory.Create("home", "Home", "Home cleaning", 1);
        category.Id = "cat-1";
        var service = Service.Create("cat-1", "Deep Clean", "Thorough", 90);
        service.Id = ServiceId;
        var prop = typeof(Service).GetProperty(nameof(Service.Category))!;
        prop.SetValue(service, category);
        return service;
    }

    [Fact]
    public async Task Projects_Row_With_Category_And_PageMetadata()
    {
        var service = ServiceWithCategory();
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Service, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(8);
        _repository
            .Setup(r => r.GetPagedSort<ServiceSort>(
                0, 50, It.IsAny<Expression<Func<Service, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());

        var result = await Handle(new GetPagedServices.Request());

        Assert.Equal(8, result.Total);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(50, result.PageSize);

        var row = Assert.Single(result.Data);
        Assert.Equal("svc-1", row.Id);
        Assert.Equal("Deep Clean", row.Name);
        Assert.Equal("Thorough", row.Description);
        Assert.Equal(999m, row.BasePrice);
        Assert.Equal(120m, row.PerRoomPrice);
        Assert.NotNull(row.Category);
        Assert.Equal("cat-1", row.Category.Id);
        Assert.Equal("Home", row.Category.Name);
    }

    [Fact]
    public async Task SearchTerm_And_IsActive_Filter_Reach_Specification()
    {
        Expression<Func<Service, bool>>? captured = null;
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Service, bool>>>(), It.IsAny<CancellationToken>()))
            .Callback<Expression<Func<Service, bool>>?, CancellationToken>((f, _) => captured = f)
            .ReturnsAsync(0);
        _repository
            .Setup(r => r.GetPagedSort<ServiceSort>(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<Service, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(Array.Empty<Service>().AsQueryable().BuildMock());

        var request = new GetPagedServices.Request
        {
            Filter = new ServiceFilter { SearchTerm = "deep", IsActive = true }
        };
        await Handle(request);

        Assert.NotNull(captured);
        var predicate = captured!.Compile();

        var matchActive = Service.Create("c", "Deep Clean", "x");
        var matchInactive = Service.Create("c", "Deep Clean", "x");
        matchInactive.IsActive = false;
        var noMatch = Service.Create("c", "Window Wash", "y");

        Assert.True(predicate(matchActive));
        Assert.False(predicate(matchInactive));
        Assert.False(predicate(noMatch));
    }

    /// <summary>
    /// The admin list does NOT withhold an unpriced entry, unlike the customer catalogue: an admin has
    /// to be able to SEE one in order to price it. It reports 0 rather than omitting the row or
    /// throwing — the one place in the system where a missing price is not a refusal.
    /// </summary>
    [Fact]
    public async Task An_Unpriced_Service_Is_Still_Listed_At_Zero()
    {
        var service = ServiceWithCategory();
        ArrangePrices();
        _repository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Service, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository
            .Setup(r => r.GetPagedSort<ServiceSort>(
                0, 50, It.IsAny<Expression<Func<Service, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { service }.AsQueryable().BuildMock());

        var result = await Handle(new GetPagedServices.Request());

        var row = Assert.Single(result.Data);
        Assert.Equal(ServiceId, row.Id);
        Assert.Equal(0m, row.BasePrice);
        Assert.Equal(0m, row.PerRoomPrice);
    }
}
