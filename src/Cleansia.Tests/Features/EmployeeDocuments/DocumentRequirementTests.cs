using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.EmployeeDocuments;

/// <summary>
/// The per-country requirements, and the checklist a cleaner reads them through.
///
/// <para><b>The checklist is what replaced the empty box.</b> A cleaner opening the documents screen
/// used to find nothing that named which papers we wanted, so the first step of onboarding was
/// contacting support to ask.</para>
/// </summary>
public class DocumentRequirementTests
{
    private const string CountryId = "cze";
    private const string UserEmail = "cleaner@cleansia.cz";

    private readonly Mock<IEmployeeDocumentRequirementRepository> _requirements = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly List<EmployeeDocumentRequirement> _added = [];

    public DocumentRequirementTests()
    {
        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _session.Setup(s => s.GetUserId()).Returns("admin-1");
        _requirements
            .Setup(r => r.Add(It.IsAny<EmployeeDocumentRequirement>()))
            .Callback<EmployeeDocumentRequirement>(_added.Add);
    }

    private void Configured(params EmployeeDocumentRequirement[] rows) =>
        _requirements
            .Setup(r => r.GetForCountryAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

    private SaveDocumentRequirement.Handler SaveHandler() =>
        new(_requirements.Object, _session.Object);

    /// <summary>
    /// The unique index on (CountryId, DocumentType) means a second row for the same pair is not a
    /// variant of the rule — it is two rules disagreeing, and whichever the query happened to read
    /// first would win. Saving the same pair twice therefore has to EDIT.
    /// </summary>
    [Fact]
    public async Task Saving_The_Same_Country_And_Type_Twice_Edits_Rather_Than_Adding()
    {
        var existing = EmployeeDocumentRequirement.Create(
            CountryId, DocumentType.IdentityCard, isRequired: true, 0, "system");
        Configured(existing);

        var result = await SaveHandler().Handle(
            new SaveDocumentRequirement.Command(CountryId, DocumentType.IdentityCard, false, 3),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_added);
        Assert.False(existing.IsRequired);
        Assert.Equal(3, existing.SortOrder);
    }

    [Fact]
    public async Task A_Type_The_Country_Has_Never_Configured_Is_Added()
    {
        Configured();

        var result = await SaveHandler().Handle(
            new SaveDocumentRequirement.Command(CountryId, DocumentType.WorkPermit, true, 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentType.WorkPermit, Assert.Single(_added).DocumentType);
    }

    /// <summary>
    /// The checklist reports the newest ACTIVE document of each type. A replaced document leaves its
    /// superseded version behind, and reading the older one would report a stale status — an approved
    /// v1 telling the cleaner the pending v2 has already been accepted.
    /// </summary>
    [Fact]
    public async Task The_Checklist_Reports_The_Newest_Version_Not_The_First()
    {
        var employee = EmployeeWith(
            Held(DocumentType.IdentityCard, DocumentStatus.Approved, version: 1),
            Held(DocumentType.IdentityCard, DocumentStatus.Pending, version: 2));

        Configured(EmployeeDocumentRequirement.Create(CountryId, DocumentType.IdentityCard, true, 0, "system"));

        var row = Assert.Single(await Checklist(employee));

        Assert.Equal(DocumentStatus.Pending, row.Status);
    }

    /// <summary>Nothing uploaded yet is the state this screen exists for: a null status, not an
    /// absent row.</summary>
    [Fact]
    public async Task A_Type_With_Nothing_Uploaded_Is_Listed_With_No_Status()
    {
        var employee = EmployeeWith();

        Configured(EmployeeDocumentRequirement.Create(CountryId, DocumentType.Passport, true, 0, "system"));

        var row = Assert.Single(await Checklist(employee));

        Assert.Null(row.Status);
        Assert.Null(row.DocumentId);
        Assert.True(row.IsRequired);
    }

    /// <summary>An OPTIONAL requirement still appears — that is the difference between "we would like
    /// this" and "you cannot start without this", and both are worth telling somebody.</summary>
    [Fact]
    public async Task Optional_Requirements_Are_On_The_Checklist_Too()
    {
        var employee = EmployeeWith();

        Configured(
            EmployeeDocumentRequirement.Create(CountryId, DocumentType.IdentityCard, true, 0, "system"),
            EmployeeDocumentRequirement.Create(CountryId, DocumentType.WorkPermit, false, 1, "system"));

        var rows = (await Checklist(employee)).ToList();

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.DocumentType == DocumentType.WorkPermit && !r.IsRequired);
    }

    /// <summary>
    /// Keyed on the WORK country, falling back to the address country. Work country is the jurisdiction
    /// the requirements belong to, but it is only set at APPROVAL — and this screen exists precisely for
    /// people who are not approved yet, so with no fallback it would be empty for everyone who needs it.
    /// </summary>
    [Fact]
    public async Task The_Address_Country_Answers_Until_A_Work_Country_Is_Assigned()
    {
        var employee = EmployeeWith();

        Assert.Null(employee.WorkCountryId);
        Configured(EmployeeDocumentRequirement.Create(CountryId, DocumentType.IdentityCard, true, 0, "system"));

        Assert.Single(await Checklist(employee));
    }

    /// <summary>
    /// A cleaner with no country at all gets an empty list rather than an exception. This screen is a
    /// prompt, not a gate, so the cost of not knowing is a blank checklist, not a refusal.
    /// </summary>
    [Fact]
    public async Task A_Cleaner_With_No_Country_At_All_Gets_An_Empty_Checklist()
    {
        var employee = EmployeeWith(withAddress: false);

        Assert.Empty(await Checklist(employee));
    }

    private async Task<IEnumerable<Core.AppServices.Features.EmployeeDocuments.DTOs.MyDocumentRequirementDto>>
        Checklist(Employee employee)
    {
        _employees.Setup(r => r.GetQueryable()).Returns(new[] { employee }.AsQueryable().BuildMock());

        var handler = new GetMyDocumentRequirements.Handler(
            _employees.Object, _requirements.Object, _session.Object);

        return await handler.Handle(new GetMyDocumentRequirements.Request(), CancellationToken.None);
    }

    private static EmployeeDocument Held(DocumentType type, DocumentStatus status, int version)
    {
        var document = EmployeeDocument.Create(
            "emp-1", $"{type}-v{version}.pdf", $"path/{type}-v{version}", "application/pdf", 1024,
            type, null, "system");

        // Version is the aggregate's own, set by CreateNewVersion in production. Reflection puts a
        // v2 on an in-memory document without a blob round-trip; the alternative is a setter that
        // exists only for tests.
        typeof(EmployeeDocument)
            .GetProperty(nameof(EmployeeDocument.Version))!
            .SetValue(document, version);

        if (status == DocumentStatus.Approved)
        {
            document.Approve("admin-1");
        }

        return document;
    }

    private static Employee EmployeeWith(params EmployeeDocument[] documents)
        => EmployeeWith(true, documents);

    private static Employee EmployeeWith(bool withAddress, params EmployeeDocument[] documents)
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "Clea", "Ner");
        user.Id = "user-1";

        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";

        if (withAddress)
        {
            employee.UpdateAddress(Address.Create("Main St 1", "Praha", "11000", CountryId));
        }

        var field = typeof(Employee).GetField(
            "_documents",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var collection = (ICollection<EmployeeDocument>)field.GetValue(employee)!;
        foreach (var document in documents)
        {
            collection.Add(document);
        }

        return employee;
    }
}
