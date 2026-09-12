using System.Reflection;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Extras;
using Cleansia.Core.AppServices.Features.Extras.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Infra.Common.Validations;
using Microsoft.EntityFrameworkCore;
using MockQueryable;
using Moq;
using System.Linq.Expressions;

namespace Cleansia.Tests.Features.Extras;

/// <summary>
/// The rest of the admin lifecycle for an extra: the slug cannot be renamed (order lines snapshot
/// it), an extra any order has bought cannot be deleted (FK Restrict, answered as extra.in_use rather
/// than a 500), the admin list shows an unpriced entry so it can be priced, and the detail carries one
/// price per currency CODE with no key for a currency nobody has priced (T-0698).
/// </summary>
public class ExtraAdminLifecycleTests
{
    private const string ExtraId = "ext-1";
    private const string CzkId = "cur-czk";

    private readonly Mock<IExtraRepository> _extraRepository = new();
    private readonly Mock<IExtraPriceRepository> _extraPriceRepository = new();
    private readonly Currency _czk;
    private readonly ICurrencyRepository _currencyRepository;

    public ExtraAdminLifecycleTests()
    {
        _czk = Currency.Create("CZK", "Kc", "Czech koruna");
        _czk.Id = CzkId;
        _czk.IsActive = true;
        _czk.SetAsDefault(true);
        _currencyRepository = CataloguePriceDoubles.DefaultCurrency(_czk);
    }

    // ---------------------------------------------------------------- slug immutability

    [Fact]
    public void The_Update_Command_Cannot_Carry_A_Slug()
    {
        Assert.Null(typeof(UpdateExtra.Command).GetProperty("Slug"));
        Assert.NotNull(typeof(CreateExtra.Command).GetProperty("Slug"));
    }

    [Fact]
    public async Task Handling_An_Update_Leaves_The_Slug_Alone()
    {
        var extra = ArrangeExtra();
        _extraPriceRepository.Setup(r => r.GetAll()).Returns(Array.Empty<ExtraPrice>().AsQueryable().BuildMock());

        var result = await InvokeUpdate(new UpdateExtra.Command(ExtraId, "Oven, inside", "Renamed", 3, null, null));

        Assert.True(result.IsSuccess);
        Assert.Equal("inside-oven", extra.Slug);
        Assert.Equal("Oven, inside", extra.Name);
    }

    // ---------------------------------------------------------------- delete

    [Fact]
    public async Task In_Use_Extra_Is_Rejected_With_ExtraInUse_And_Not_Removed()
    {
        ArrangeExtra();
        _extraRepository.Setup(r => r.IsInUseAsync(ExtraId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new DeleteExtra.Handler(_extraRepository.Object)
            .Handle(new DeleteExtra.Command(ExtraId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.ExtraInUse, result.Error!.Message);
        _extraRepository.Verify(r => r.Remove(It.IsAny<Extra>()), Times.Never);
    }

    [Fact]
    public async Task Not_In_Use_Extra_Is_Removed()
    {
        var extra = ArrangeExtra();
        _extraRepository.Setup(r => r.IsInUseAsync(ExtraId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await new DeleteExtra.Handler(_extraRepository.Object)
            .Handle(new DeleteExtra.Command(ExtraId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _extraRepository.Verify(r => r.Remove(extra), Times.Once);
    }

    [Theory]
    [InlineData("23001")] // restrict_violation (explicit ON DELETE RESTRICT)
    [InlineData("23503")] // foreign_key_violation (NO ACTION)
    public async Task Reference_Racing_Past_The_Check_Maps_Restrict_Violation_To_ExtraInUse(string sqlState)
    {
        ArrangeExtra();
        _extraRepository.Setup(r => r.IsInUseAsync(ExtraId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _extraRepository
            .Setup(r => r.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("fk", new FakePostgresException(sqlState)));

        var result = await new DeleteExtra.Handler(_extraRepository.Object)
            .Handle(new DeleteExtra.Command(ExtraId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessErrorMessage.ExtraInUse, result.Error!.Message);
    }

    // ---------------------------------------------------------------- the reads

    [Fact]
    public async Task An_Unpriced_Extra_Is_Listed_At_Zero_Rather_Than_Withheld()
    {
        var priced = Extra.Create("inside-oven", "Inside oven", null, 1);
        priced.Id = ExtraId;
        var unpriced = Extra.Create("inside-fridge", "Inside fridge", null, 2);
        unpriced.Id = "ext-2";
        _extraRepository
            .Setup(r => r.GetPagedSort<ExtraSort>(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Expression<Func<Extra, bool>>>(), It.IsAny<IEnumerable<SortDefinition>>()))
            .Returns(new[] { priced, unpriced }.AsQueryable().BuildMock());
        _extraRepository
            .Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Extra, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var prices = CataloguePriceDoubles.Extras(_czk, (ExtraId, 200m));

        var handlerType = typeof(GetPagedExtras).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public)!;
        var handler = Activator.CreateInstance(handlerType, _extraRepository.Object, prices, _currencyRepository)!;
        var result = await (Task<PagedData<ExtraListItem>>)handlerType
            .GetMethod("Handle")!
            .Invoke(handler, [new GetPagedExtras.Request { Offset = 0, Limit = 10 }, CancellationToken.None])!;

        Assert.Equal(2, result.Data.Count());
        Assert.Equal(200m, result.Data.Single(e => e.Id == ExtraId).Price);
        Assert.Equal(0m, result.Data.Single(e => e.Id == "ext-2").Price);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task Detail_Returns_A_Price_Per_Currency_Code_And_No_Key_For_An_Unpriced_Currency()
    {
        ArrangeExtra();
        var row = ExtraPrice.Create(ExtraId, CzkId, 200m);
        typeof(ExtraPrice).GetProperty(nameof(ExtraPrice.Currency))!.SetValue(row, _czk);
        _extraPriceRepository.Setup(r => r.GetAll()).Returns(new[] { row }.AsQueryable().BuildMock());

        var handlerType = typeof(GetExtraById).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public)!;
        var handler = Activator.CreateInstance(handlerType, _extraRepository.Object, _extraPriceRepository.Object)!;
        var result = await (Task<BusinessResult<AdminExtraDetailDto>>)handlerType
            .GetMethod("Handle")!
            .Invoke(handler, [new GetExtraById.Query(ExtraId), CancellationToken.None])!;

        Assert.True(result.IsSuccess);
        Assert.Equal("inside-oven", result.Value!.Slug);
        Assert.Equal(200m, result.Value.Prices["CZK"]);
        Assert.False(result.Value.Prices.ContainsKey("EUR"));
    }

    // ---------------------------------------------------------------- arrangement

    private Extra ArrangeExtra()
    {
        var extra = Extra.Create("inside-oven", "Inside oven", null, 10);
        extra.Id = ExtraId;
        _extraRepository
            .Setup(r => r.GetByIdAsync(ExtraId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(extra);
        return extra;
    }

    private async Task<BusinessResult<UpdateExtra.Response>> InvokeUpdate(UpdateExtra.Command command)
    {
        var handlerType = typeof(UpdateExtra).GetNestedType("Handler", BindingFlags.NonPublic | BindingFlags.Public)!;
        var handler = Activator.CreateInstance(
            handlerType, _extraRepository.Object, _extraPriceRepository.Object, _currencyRepository)!;
        return await (Task<BusinessResult<UpdateExtra.Response>>)handlerType
            .GetMethod("Handle")!
            .Invoke(handler, [command, CancellationToken.None])!;
    }

    private sealed class FakePostgresException(string sqlState) : Exception
    {
        public string SqlState { get; } = sqlState;
    }
}
