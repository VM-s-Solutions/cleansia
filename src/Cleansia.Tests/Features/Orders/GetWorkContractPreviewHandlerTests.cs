using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0068 D3 — the preview renders the ORDER's document (never the one in force today), in the
/// requested language or the fallback, with the order's currency in the copy and the facts the
/// acceptance will freeze; an order with no document is refused as a missing legal document; and the
/// order must be open to the caller exactly as the take requires, so a stranger to a held order is told
/// nothing.
/// </summary>
public sealed class GetWorkContractPreviewHandlerTests
{
    private const string OrderId = "order-preview-1";
    private const string EmployeeId = "emp-preview-1";
    private const string StrangerId = "emp-preview-stranger";

    private static readonly WorkContractFacts Facts = new(
        "ORD-PREVIEW", new DateTime(2026, 9, 22, 9, 0, 0, DateTimeKind.Utc), 120, 1000m, "CZK", "Prague · 110 xx", "cz", 1, 1, [], [], []);

    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<IWorkContractFactsBuilder> _factsBuilder = new();

    private GetWorkContractPreview.Handler CreateHandler() =>
        new(_orderRepository.Object, WorkContractTestData.LegalDocumentRepository().Object, _factsBuilder.Object);

    private GetWorkContractPreview.Validator CreateValidator() =>
        new(_orderRepository.Object, _accessService.Object, ValidatorTestHelpers.CurrencyResolver());

    private void Arrange(Order order, string caller = EmployeeId)
    {
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caller);
        _factsBuilder.Setup(b => b.BuildAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(Facts);
    }

    [Fact]
    public async Task The_Preview_Renders_The_Orders_Document_With_Its_Text_Id_And_The_Orders_Currency()
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New));

        var result = await CreateHandler().Handle(new GetWorkContractPreview.Query(OrderId, "cs-CZ"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var dto = result.Value!;
        Assert.Equal(WorkContractTestData.TextIdCs, dto.LegalDocumentTextId);
        Assert.Equal(WorkContractTestData.DocumentId, dto.LegalDocumentId);
        Assert.Equal(WorkContractTestData.Version, dto.Version);
        Assert.Equal(WorkContractTestData.EffectiveFrom, dto.EffectiveFrom);
        Assert.Equal("cs", dto.Language);
        Assert.Equal("Smlouva o dílo", dto.Title);
        Assert.Contains("CZK", dto.ContentHtml);
        Assert.DoesNotContain("{{", dto.ContentHtml);
        Assert.Contains("<h2>", dto.ContentHtml);
        Assert.Same(Facts, dto.Facts);
        Assert.Null(dto.Acceptance);
    }

    [Fact]
    public async Task An_Unknown_Language_Falls_Back_To_English()
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New));

        var result = await CreateHandler().Handle(new GetWorkContractPreview.Query(OrderId, "de"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkContractTestData.TextIdEn, result.Value!.LegalDocumentTextId);
        Assert.Equal("en", result.Value.Language);
    }

    [Fact]
    public async Task An_Order_With_No_Document_Is_Refused_As_A_Missing_Legal_Document()
    {
        var unstamped = Order.Create(
            "Test Customer", "test@example.com", "+420000000000",
            Core.Domain.Users.Address.Create("123 Main St", "Prague", "11000", "cz"),
            1, 1, ValidatorTestHelpers.DefaultCleaningTime, PaymentType.Cash, 1000m, ValidatorTestHelpers.CurrencyId, PaymentStatus.Pending);
        unstamped.Id = OrderId;
        Arrange(unstamped);

        var result = await CreateHandler().Handle(new GetWorkContractPreview.Query(OrderId, "en"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.LegalDocumentNotFound, result.Error!.Message);
    }

    [Fact]
    public async Task The_Beneficiary_Of_A_Held_Order_May_Preview_And_A_Stranger_Is_Told_Nothing()
    {
        var held = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New);
        held.GrantPreferredHold(EmployeeId, DateTime.UtcNow.AddHours(6), DateTime.UtcNow, 3);

        Arrange(held, caller: EmployeeId);
        var beneficiary = await CreateValidator().ValidateAsync(new GetWorkContractPreview.Query(OrderId));

        Arrange(held, caller: StrangerId);
        var stranger = await CreateValidator().ValidateAsync(new GetWorkContractPreview.Query(OrderId));

        Assert.True(beneficiary.IsValid, string.Join("; ", beneficiary.Errors.Select(e => e.ErrorMessage)));
        Assert.False(stranger.IsValid);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(stranger.Errors).ErrorMessage);
    }

    [Fact]
    public async Task A_Missing_Order_Is_Not_Found()
    {
        Arrange(ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New));

        var result = await CreateValidator().ValidateAsync(new GetWorkContractPreview.Query("order-nowhere"));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(result.Errors).ErrorMessage);
    }
}
