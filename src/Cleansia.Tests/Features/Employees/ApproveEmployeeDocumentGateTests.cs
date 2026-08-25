using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// Approval now requires the work country's documents to exist AND be approved.
///
/// <para><b>What this closes.</b> Approval consulted <c>IsProfileComplete()</c>, which excludes
/// documents deliberately — its own comment says they are "handled separately by the registration
/// lock". Nothing else checked them, so an admin could approve a cleaner who had uploaded nothing, or
/// whose every document had been rejected. "Approved" meant a button had been pressed, not that the
/// paperwork existed.</para>
///
/// <para><b>Keyed on the WORK country</b>, not the cleaner's home address: the paperwork is a function
/// of the jurisdiction they will work in, which is the same thing <c>WorkCountryId</c> already decides
/// currency, language and VAT by.</para>
/// </summary>
public class ApproveEmployeeDocumentGateTests
{
    private const string EmployeeId = "emp-1";
    private const string CountryId = "cze";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<ICountryRepository> _countries = new();
    private readonly Mock<IServiceRepository> _services = new();
    private readonly Mock<IPackageRepository> _packages = new();
    private readonly Mock<IEmployeePayConfigRepository> _payConfigs = new();
    private readonly Mock<IEmployeeDocumentRequirementRepository> _requirements = new();

    private readonly Employee _employee;

    public ApproveEmployeeDocumentGateTests()
    {
        _employee = CompleteEmployee();

        _employees.Setup(r => r.ExistsAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _employees.Setup(r => r.GetByIdAsync(EmployeeId, It.IsAny<CancellationToken>())).ReturnsAsync(_employee);
        _employees.Setup(r => r.GetQueryable()).Returns(new[] { _employee }.AsQueryable().BuildMock());

        _countries.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // An empty catalogue, so the pay-coverage rule finds no gaps and stays out of the way. These
        // cases are about DOCUMENTS; ApproveEmployeePayCoverageTests owns pay. The mocks still have to
        // return async-capable queryables or that rule throws rather than passing.
        _services.Setup(r => r.GetAll()).Returns(Array.Empty<Service>().AsQueryable().BuildMock());
        _packages.Setup(r => r.GetAll()).Returns(Array.Empty<Package>().AsQueryable().BuildMock());
        _payConfigs.Setup(r => r.GetAll())
            .Returns(Array.Empty<EmployeePayConfig>().AsQueryable().BuildMock());
    }

    private void Require(params DocumentType[] types) =>
        _requirements
            .Setup(r => r.GetForCountryAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(types
                .Select((t, i) => EmployeeDocumentRequirement.Create(CountryId, t, true, i, "system"))
                .ToList());

    private void RequireNothing() =>
        _requirements
            .Setup(r => r.GetForCountryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

    private void Holds(DocumentType type, DocumentStatus status, bool active = true)
    {
        var document = EmployeeDocument.Create(
            EmployeeId, $"{type}.pdf", $"path/{type}", "application/pdf", 1024, type, null, "system");

        if (status == DocumentStatus.Approved)
        {
            document.Approve("admin");
        }
        else if (status == DocumentStatus.Rejected)
        {
            document.Reject("admin");
        }

        if (!active)
        {
            document.SoftDelete("system");
        }

        AttachToEmployee(document);
    }

    /// <summary>
    /// <c>Employee.Documents</c> is a read-only projection over a private field and the aggregate
    /// offers no adder — the collection is EF's to fill. Reflection is the only way to put a document
    /// on an in-memory employee, and the alternative (widening the aggregate so a test can reach it)
    /// would be a production change made for a test's convenience.
    /// </summary>
    private void AttachToEmployee(EmployeeDocument document)
    {
        var field = typeof(Employee).GetField(
            "_documents",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(field);
        var documents = (ICollection<EmployeeDocument>)field!.GetValue(_employee)!;
        documents.Add(document);
    }

    private async Task<bool> ApprovalIsAllowed()
    {
        var validator = new ApproveEmployee.Validator(
            _employees.Object, _countries.Object, _services.Object, _packages.Object,
            _payConfigs.Object, _requirements.Object);

        var result = await validator.ValidateAsync(
            new ApproveEmployee.Command(EmployeeId, CountryId), CancellationToken.None);

        return !result.Errors.Any(
            e => e.ErrorMessage == BusinessErrorMessage.EmployeeDocumentsNotApproved);
    }

    [Fact]
    public async Task Approval_Is_Refused_When_A_Required_Document_Was_Never_Uploaded()
    {
        Require(DocumentType.IdentityCard);

        Assert.False(await ApprovalIsAllowed());
    }

    /// <summary>
    /// The case that motivated the whole gate: the paperwork is there, an admin looked at it and said
    /// no, and the cleaner could still be approved.
    /// </summary>
    [Fact]
    public async Task Approval_Is_Refused_When_The_Required_Document_Was_REJECTED()
    {
        Require(DocumentType.IdentityCard);
        Holds(DocumentType.IdentityCard, DocumentStatus.Rejected);

        Assert.False(await ApprovalIsAllowed());
    }

    /// <summary>Uploaded is not reviewed. Pending is the ordinary state of a fresh upload.</summary>
    [Fact]
    public async Task Approval_Is_Refused_While_The_Required_Document_Is_Still_Pending()
    {
        Require(DocumentType.IdentityCard);
        Holds(DocumentType.IdentityCard, DocumentStatus.Pending);

        Assert.False(await ApprovalIsAllowed());
    }

    /// <summary>
    /// A soft-deleted document is gone as far as every other read is concerned, so it cannot satisfy a
    /// requirement either — otherwise an approved-then-removed document would keep the gate open.
    /// </summary>
    [Fact]
    public async Task A_Soft_Deleted_Document_Does_Not_Satisfy_A_Requirement()
    {
        Require(DocumentType.IdentityCard);
        Holds(DocumentType.IdentityCard, DocumentStatus.Approved, active: false);

        Assert.False(await ApprovalIsAllowed());
    }

    [Fact]
    public async Task Approval_Is_Allowed_Once_Every_Required_Document_Is_Approved()
    {
        Require(DocumentType.IdentityCard, DocumentType.TaxDocument);
        Holds(DocumentType.IdentityCard, DocumentStatus.Approved);
        Holds(DocumentType.TaxDocument, DocumentStatus.Approved);

        Assert.True(await ApprovalIsAllowed());
    }

    /// <summary>Every one of them, not any of them.</summary>
    [Fact]
    public async Task One_Approved_Document_Does_Not_Satisfy_Two_Requirements()
    {
        Require(DocumentType.IdentityCard, DocumentType.TaxDocument);
        Holds(DocumentType.IdentityCard, DocumentStatus.Approved);

        Assert.False(await ApprovalIsAllowed());
    }

    /// <summary>
    /// A country that configures nothing gates nothing. This is what keeps the rule additive: a market
    /// whose requirements have not been entered yet behaves exactly as it did before the gate existed,
    /// rather than locking every cleaner in it out of approval.
    /// </summary>
    [Fact]
    public async Task A_Country_With_No_Requirements_Configured_Gates_Nothing()
    {
        RequireNothing();

        Assert.True(await ApprovalIsAllowed());
    }

    /// <summary>
    /// An OPTIONAL requirement is a prompt on the upload screen, not a gate. Both live in the same
    /// table so the cleaner can be asked for something the platform will not block on.
    /// </summary>
    [Fact]
    public async Task An_Optional_Requirement_Is_A_Prompt_Not_A_Gate()
    {
        _requirements
            .Setup(r => r.GetForCountryAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                EmployeeDocumentRequirement.Create(
                    CountryId, DocumentType.Certificate, isRequired: false, 0, "system")
            ]);

        Assert.True(await ApprovalIsAllowed());
    }

    private static Employee CompleteEmployee()
    {
        var user = User.CreateWithPassword("cleaner@example.com", "Password1", "Clea", "Ner");
        user.Id = "user-1";
        user.Update("Clea", "Ner", "+420111222333", new DateOnly(1990, 1, 1));

        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;
        employee.UpdateEmployeeDetails(
            EmployeeEntityType.NaturalPerson,
            registrationNumber: "12345678",
            vatNumber: null,
            legalEntityName: null,
            nationalityId: CountryId,
            passportId: "AB1234567",
            address: Address.Create("Main St 1", "Praha", "11000", CountryId),
            availability: new Dictionary<string, List<TimeRange>>(),
            emergencyContactName: null,
            emergencyContactPhone: null);
        employee.UpdateBankDetails("CZ6508000000192000145399");

        return employee;
    }
}
