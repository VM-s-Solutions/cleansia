using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// The fourth intake into <c>employee-documents</c>.
///
/// <para>It writes the same container and the same <c>EmployeeDocument</c> table as
/// <c>SaveMyDocuments</c>, from the same two partner hosts, so the container's content invariant is the
/// weaker of the two paths' rules — not the stronger. Its previous rule constrained the content type the
/// client <i>declared</i> to seven values, which bounds a claim and not a payload: arbitrary bytes
/// labelled <c>image/jpeg</c> passed, and the label is what was stored and later served.</para>
/// </summary>
public class UpdateEmployeeDocumentIntakeTests
{
    private const long TenMebibytes = 10L * 1024 * 1024;
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string EmployeeId = "emp-1";
    private const string CountryId = "cz";

    private readonly Mock<ICountryRepository> _countryRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<ITaxIdValidator> _taxIdValidator = new();

    public UpdateEmployeeDocumentIntakeTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _countryRepository.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countryRepository.Setup(r => r.IsServicedAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _taxIdValidator
            .Setup(v => v.ValidateRegistrationNumberAsync(
                It.IsAny<string>(), It.IsAny<EmployeeEntityType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Valid());
        _taxIdValidator
            .Setup(v => v.ValidateVatNumberAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TaxIdValidationResult.Valid());
    }

    private UpdateEmployee.Validator CreateValidator() => new(
        _countryRepository.Object,
        _employeeRepository.Object,
        _session.Object,
        _taxIdValidator.Object);

    private static byte[] Headed(byte[] header, long size)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    private static byte[] Pdf(long size) => Headed("%PDF-"u8.ToArray(), size);

    private static BlobFileDto Document(
        byte[] content,
        string fileName = "contract.pdf",
        string? declaredContentType = "application/pdf") =>
        new(fileName, Convert.ToBase64String(content), declaredContentType);

    private static UpdateEmployee.Command CommandWith(params BlobFileDto?[] documents) => new(
        EmployeeId: EmployeeId,
        FirstName: "First",
        LastName: "Last",
        BirthDate: new DateOnly(1990, 1, 1),
        Street: "Main Street 10",
        City: "Prague",
        ZipCode: "11000",
        CountryId: CountryId,
        State: null,
        NationalityId: CountryId,
        Phone: "+420123456789",
        PassportId: "AB12345",
        EntityType: EmployeeEntityType.NaturalPerson,
        RegistrationNumber: "12345678",
        VatNumber: null,
        LegalEntityName: null,
        EmergencyName: null,
        EmergencyPhone: null,
        Consent: true,
        Documents: [.. documents!]);

    [Fact]
    public async Task Valid_Documents_Pass()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(Document(Pdf(2048)), Document(Pdf(2048))));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// The declared-type allowlist this replaces admitted exactly this payload: the seven permitted
    /// strings say what a caller may CLAIM, and <c>image/jpeg</c> is one of them whatever the bytes are.
    /// </summary>
    [Fact]
    public async Task Bytes_That_Are_Not_A_Permitted_Document_Are_Refused_However_They_Are_Declared()
    {
        var markup = Document(Headed("<html>"u8.ToArray(), 2048), declaredContentType: "image/jpeg");

        var result = await CreateValidator().ValidateAsync(CommandWith(markup));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileTypeNotAllowed);
    }

    [Fact]
    public async Task More_Documents_Than_The_Cap_Fails_With_FileCountExceeded()
    {
        var command = CommandWith([.. Enumerable.Range(0, 11).Select(_ => Document(Pdf(2048)))]);

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileCountExceeded);
    }

    /// <summary>
    /// The cost half of the cap: an over-long list must be refused without every item being sniffed and
    /// decoded first. A second, per-item error in the result means the item rules ran anyway.
    /// </summary>
    [Fact]
    public async Task Over_Long_List_Is_Refused_Without_Validating_Its_Items()
    {
        var command = CommandWith([.. Enumerable.Range(0, 11).Select(_ => Document("<html>"u8.ToArray()))]);

        var result = await CreateValidator().ValidateAsync(command);

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.FileCountExceeded, failure.ErrorMessage);
    }

    /// <summary>
    /// FluentValidation skips a child validator for a null element, so a <c>[null]</c> entry used to pass
    /// validation entirely and be dereferenced by the handler — a 500 on a one-line request body.
    /// </summary>
    [Fact]
    public async Task A_Null_Entry_In_The_List_Is_Refused_Rather_Than_Reaching_The_Handler()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(Document(Pdf(2048)), null));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
    }

    /// <summary>
    /// Blank content used to reach the handler and be silently skipped, so the caller was told the upload
    /// succeeded and nothing was stored. It is now refused, and the message has to be <c>file.required</c>
    /// rather than the size rule's — the two rules both reject blank, which is what makes asserting only
    /// "it was rejected" a test neither rule's deletion can kill.
    /// </summary>
    [Fact]
    public async Task A_Blank_Document_Is_Refused_Rather_Than_Silently_Skipped()
    {
        var result = await CreateValidator().ValidateAsync(
            CommandWith(new BlobFileDto("contract.pdf", string.Empty, "application/pdf")));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.Required, failure.ErrorMessage);
    }

    [Fact]
    public async Task Document_Over_TenMebibytes_Fails_With_FileSizeExceeded()
    {
        var result = await CreateValidator().ValidateAsync(CommandWith(Document(Pdf(TenMebibytes + 1024))));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileSizeExceeded);
    }

    /// <summary>
    /// <c>EmployeeDocument.FileName</c> is <c>varchar(255)</c>, and this path stored the client's name
    /// verbatim with no bound — so an over-long name surfaced as a <c>DbUpdateException</c> from the
    /// pipeline's commit, i.e. a 500 rather than a rejected upload.
    /// </summary>
    [Fact]
    public async Task FileName_Longer_Than_Its_Column_Fails_MaxLength()
    {
        var result = await CreateValidator().ValidateAsync(
            CommandWith(Document(Pdf(2048), fileName: new string('x', 252) + ".pdf")));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.MaxLength);
    }
}
