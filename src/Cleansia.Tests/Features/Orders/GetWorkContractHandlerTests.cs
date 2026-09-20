using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Repositories;
using Cleansia.TestUtilities;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// ADR-0068 D4 (Verification #5) — the read is keyed on the acceptance: access is derived from the
/// row's order (owner-pinned for a customer, the company's for staff) and, for a cleaner, from the row
/// naming them — so the order's customer, the accepting cleaner (even after dropping) and an
/// administrator read it, and another customer or another cleaner answer not-found. The facts are the
/// STORED ones; the text is the requested language when the accepted document has it, else the
/// accepted text, with the accepted language on the DTO either way.
/// </summary>
public sealed class GetWorkContractHandlerTests
{
    private const string OrderId = "01ORDER00000000000000READ1";
    private const string SeatId = "01SEAT000000000000000READ1";
    private const string EmployeeId = "01EMP0000000000000000READ1";
    private const string OtherEmployeeId = "01EMP0000000000000000READ2";

    private static readonly WorkContractFacts StoredFacts = new(
        "ORD-STORED", new DateTime(2026, 9, 22, 9, 0, 0, DateTimeKind.Utc), 180, 1500m, "EUR", "Brno · 602 xx", "cz", 3, 2,
        [new WorkContractFactsLine("svc-1", "Deep Clean")], [], ["insideOven"]);

    private readonly Mock<IWorkContractAcceptanceRepository> _acceptanceRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();

    private static WorkContractAcceptance AcceptedInCzech() =>
        WorkContractAcceptance.Create(
            OrderId, SeatId, EmployeeId, WorkContractTestData.Document().TextFor("cs")!, WorkContractTestData.Version,
            "cleansia.mobile", "203.0.113.9", "Pixel 8", "device-1", StoredFacts.ToJson());

    private GetWorkContract.Handler CreateHandler() =>
        new(_acceptanceRepository.Object, WorkContractTestData.LegalDocumentRepository().Object);

    private GetWorkContract.Validator CreateValidator() =>
        new(_acceptanceRepository.Object, _accessService.Object);

    private WorkContractAcceptance Arrange(bool orderExistsForCaller, string? callerEmployeeId)
    {
        var acceptance = AcceptedInCzech();
        _acceptanceRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync(acceptance.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(acceptance);
        _accessService
            .Setup(s => s.OrderExistsForCallerAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderExistsForCaller);
        _accessService
            .Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(callerEmployeeId);
        return acceptance;
    }

    // ── Access, keyed on the acceptance ─────────────────────────────────────────────────────────

    [Fact]
    public async Task The_Orders_Customer_Reads_It()
    {
        var acceptance = Arrange(orderExistsForCaller: true, callerEmployeeId: null);

        var result = await CreateValidator().ValidateAsync(new GetWorkContract.Query(acceptance.Id));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Another_Customer_Is_Told_Nothing()
    {
        var acceptance = Arrange(orderExistsForCaller: false, callerEmployeeId: null);

        var result = await CreateValidator().ValidateAsync(new GetWorkContract.Query(acceptance.Id));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task The_Cleaner_Who_Accepted_Reads_It_Even_Off_The_Crew()
    {
        // The order is in the cleaner's company, so it exists for them whether or not they still hold
        // the seat; the row naming them is what admits them.
        var acceptance = Arrange(orderExistsForCaller: true, callerEmployeeId: EmployeeId);

        var result = await CreateValidator().ValidateAsync(new GetWorkContract.Query(acceptance.Id));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Another_Cleaner_Of_The_Same_Company_Is_Told_Nothing()
    {
        var acceptance = Arrange(orderExistsForCaller: true, callerEmployeeId: OtherEmployeeId);

        var result = await CreateValidator().ValidateAsync(new GetWorkContract.Query(acceptance.Id));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task An_Administrator_Reads_It()
    {
        var acceptance = Arrange(orderExistsForCaller: true, callerEmployeeId: null);

        var result = await CreateValidator().ValidateAsync(new GetWorkContract.Query(acceptance.Id));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task An_Unknown_Acceptance_Is_Not_Found()
    {
        _acceptanceRepository
            .Setup(r => r.GetByIdIgnoringTenantAsync("01NOWHERE0000000000000001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkContractAcceptance?)null);

        var result = await CreateValidator().ValidateAsync(new GetWorkContract.Query("01NOWHERE0000000000000001"));

        Assert.False(result.IsValid);
        Assert.Equal(BusinessErrorMessage.OrderNotFound, Assert.Single(result.Errors).ErrorMessage);
    }

    // ── The rendering ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_Read_Carries_The_Stored_Facts_And_The_Acceptance()
    {
        var acceptance = Arrange(orderExistsForCaller: true, callerEmployeeId: null);

        var result = await CreateHandler().Handle(new GetWorkContract.Query(acceptance.Id, "cs"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var dto = result.Value!;
        Assert.Equal("ORD-STORED", dto.Facts.OrderNumber);
        Assert.Equal(1500m, dto.Facts.TotalPrice);
        Assert.Equal("Brno · 602 xx", dto.Facts.LocationApproximate);
        Assert.Equal("Deep Clean", dto.Facts.Services.Single().Name);
        Assert.Equal(["insideOven"], dto.Facts.ExtraSlugs);
        Assert.NotNull(dto.Acceptance);
        Assert.Equal(acceptance.AcceptedOn, dto.Acceptance!.AcceptedOn);
        Assert.Equal(WorkContractTestData.Version, dto.Acceptance.DocumentVersion);
        Assert.Equal("cs", dto.Acceptance.AcceptedLanguage);
        Assert.Equal(SeatId, dto.Acceptance.OrderEmployeeId);
        Assert.Equal(EmployeeId, dto.Acceptance.EmployeeId);
        Assert.Equal(WorkContractTestData.TextIdCs, dto.LegalDocumentTextId);
        Assert.Equal("cs", dto.Language);
        Assert.Contains("EUR", dto.ContentHtml);
    }

    [Fact]
    public async Task The_Requested_Language_Is_Rendered_When_The_Accepted_Document_Has_It_And_The_Accepted_Language_Is_Still_Named()
    {
        var acceptance = Arrange(orderExistsForCaller: true, callerEmployeeId: null);

        var result = await CreateHandler().Handle(new GetWorkContract.Query(acceptance.Id, "en-GB"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("en", result.Value!.Language);
        Assert.Equal(WorkContractTestData.TextIdEn, result.Value.LegalDocumentTextId);
        Assert.Equal("cs", result.Value.Acceptance!.AcceptedLanguage);
    }

    [Fact]
    public async Task A_Language_The_Document_Lacks_Falls_Back_To_The_Accepted_Text_Not_To_English()
    {
        var acceptance = Arrange(orderExistsForCaller: true, callerEmployeeId: null);

        var result = await CreateHandler().Handle(new GetWorkContract.Query(acceptance.Id, "uk"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("cs", result.Value!.Language);
        Assert.Equal(WorkContractTestData.TextIdCs, result.Value.LegalDocumentTextId);
    }
}
