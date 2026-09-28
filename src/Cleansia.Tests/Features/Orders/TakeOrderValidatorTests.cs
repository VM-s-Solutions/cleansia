using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using Cleansia.TestUtilities;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The order-action gate must let only an
/// <see cref="ContractStatus.Approved"/> cleaner take an order. A cleaner
/// the admin rejected (or who is still pending / terminated) must be turned
/// away with <see cref="BusinessErrorMessage.EmployeeNotApproved"/> and no
/// assignment created.
/// </summary>
public class TakeOrderValidatorTests
{
    private readonly Mock<IOrderRepository> _orderRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IOrderAccessService> _accessService = new();
    private readonly Mock<ILegalDocumentResolver> _legalDocuments = new();
    private readonly Mock<IUserConsentRepository> _consents = new();
    private readonly TakeOrder.Validator _validator;

    private const string OrderId = "order-1";
    private const string EmployeeId = "emp-1";

    public TakeOrderValidatorTests()
    {
        _validator = new TakeOrder.Validator(
            _orderRepository.Object,
            _employeeRepository.Object,
            _accessService.Object,
            ValidatorTestHelpers.CurrencyResolver(),
            WorkContractTestData.LegalDocumentRepository().Object,
            _legalDocuments.Object,
            _consents.Object);
    }

    [Theory]
    [InlineData(ContractStatus.Rejected)]   // rejected cleaner cannot take
    [InlineData(ContractStatus.Pending)]    // pending cleaner cannot take
    [InlineData(ContractStatus.Terminated)] // terminated cleaner cannot take
    public async Task When_Cleaner_Not_Approved_Then_EmployeeNotApproved(ContractStatus status)
    {
        ArrangeTakeableOrder(employeeStatus: status);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeNotApproved);
    }

    [Fact]
    public async Task When_Cleaner_Approved_And_All_Rules_Pass_Then_Valid()
    {
        // approved cleaner satisfying every existing rule still passes.
        ArrangeTakeableOrder(employeeStatus: ContractStatus.Approved);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Owner ruling 2026-09-28: while a cleaner document is in force for the cleaner's market, the take is refused
    /// until its current version is accepted.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_Who_Has_Not_Accepted_A_Document_In_Force_Is_Refused()
    {
        ArrangeTakeableOrder(employeeStatus: ContractStatus.Approved);
        FrameworkContractInForce(new DateOnly(2026, 12, 1));
        _consents
            .Setup(r => r.GetByUserIdNoTrackingAsync(EmployeeId + "-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn));

        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.EmployeeLegalDocumentsNotAccepted, error.ErrorMessage);
    }

    [Fact]
    public async Task A_Cleaner_Who_Accepted_An_Older_Version_Is_Refused()
    {
        ArrangeTakeableOrder(employeeStatus: ContractStatus.Approved);
        FrameworkContractInForce(new DateOnly(2027, 1, 1));
        AcceptedFrameworkContract("2026-12-01");

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn));

        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.EmployeeLegalDocumentsNotAccepted);
    }

    [Fact]
    public async Task A_Cleaner_Holding_The_Current_Version_Takes()
    {
        ArrangeTakeableOrder(employeeStatus: ContractStatus.Approved);
        FrameworkContractInForce(new DateOnly(2026, 12, 1));
        AcceptedFrameworkContract("2026-12-01");

        var result = await _validator.ValidateAsync(new TakeOrder.Command(OrderId, WorkContractTestData.TextIdEn));

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    private readonly Dictionary<DateOnly, LegalDocument> _frameworkContracts = new();

    // One document per date, so an acceptance and the text in force name the same row exactly when the
    // test says they are the same version.
    private LegalDocument FrameworkContract(DateOnly effectiveFrom)
    {
        if (!_frameworkContracts.TryGetValue(effectiveFrom, out var document))
        {
            document = LegalDocument.Create(
                LegalDocumentAudience.Employee, LegalDocumentType.CleanerFrameworkContract, null, effectiveFrom);
            document.AddText("en", "Framework contract", "## Terms");
            _frameworkContracts[effectiveFrom] = document;
        }

        return document;
    }

    private void FrameworkContractInForce(DateOnly effectiveFrom) =>
        _legalDocuments
            .Setup(r => r.ResolveInForceAsync(
                LegalDocumentAudience.Employee, LegalDocumentType.CleanerFrameworkContract, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FrameworkContract(effectiveFrom));

    private void AcceptedFrameworkContract(string version)
    {
        var document = FrameworkContract(DateOnly.Parse(version));
        _consents
            .Setup(r => r.GetByUserIdNoTrackingAsync(EmployeeId + "-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync([UserConsent.Grant(EmployeeId + "-user", ConsentType.CleanerFrameworkContract, "203.0.113.9", "Android", document.Version, document.Id)]);
    }

    private void ArrangeTakeableOrder(ContractStatus employeeStatus)
    {
        // Confirmed order with an open spot, NOT yet assigned to this cleaner.
        var order = ValidatorTestHelpers.BuildEmptyOrder(OrderId, OrderStatus.New, maxEmployees: 2);
        var employee = ValidatorTestHelpers.BuildEmployee(EmployeeId, employeeStatus, withAddress: true);

        _orderRepository.Setup(r => r.ExistsAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _orderRepository.Setup(r => r.GetQueryable()).Returns(new[] { order }.AsQueryable().BuildMock());
        _orderRepository
            .Setup(r => r.GetEmployeeOrderCountThisWeekAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _orderRepository
            .Setup(r => r.HasOverlappingOrderAsync(
                EmployeeId, It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _employeeRepository.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _employeeRepository.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        _accessService.Setup(s => s.GetCallerEmployeeIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(EmployeeId);
    }
}
