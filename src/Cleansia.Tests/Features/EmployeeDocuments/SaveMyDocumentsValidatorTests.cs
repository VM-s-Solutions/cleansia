using System.Security.Cryptography;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.EmployeeDocuments;

/// <summary>
/// The command-level half of the document-upload bound: that the per-file rule is actually WIRED to
/// every item of the list, and that the list itself is capped.
///
/// <para>A per-item cap on an uncapped list is not a bound — the host body limit buys thousands of
/// small items, each of which is a blob upload and a row.</para>
/// </summary>
public class SaveMyDocumentsValidatorTests
{
    private const long TenMebibytes = 10L * 1024 * 1024;
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string EmployeeId = "emp-1";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmployeeDocumentRepository> _documentRepository = new();
    private readonly List<EmployeeDocument> _stored = [];

    public SaveMyDocumentsValidatorTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        user.ConfirmEmail();
        user.Id = "user-1";
        var employee = Employee.CreateWithUser(user);
        employee.Id = EmployeeId;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _documentRepository
            .Setup(r => r.GetByEmployeeIdAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string employeeId, bool includeInactive, CancellationToken _) =>
                _stored.Where(d => d.EmployeeId == employeeId && (includeInactive || d.IsActive)).ToList());
    }

    private SaveMyDocuments.Validator CreateValidator() =>
        new(_userRepository.Object, _session.Object, _employeeRepository.Object, _documentRepository.Object);

    private static byte[] Pdf(long size)
    {
        var bytes = new byte[size];
        "%PDF-"u8.CopyTo(bytes);
        return bytes;
    }

    private static SaveMyDocuments.DocumentToSave Document(
        byte[] content, DocumentType documentType = DocumentType.Passport, string fileName = "passport.pdf") => new()
    {
        DocumentType = documentType,
        File = new BlobFileDto(fileName, Convert.ToBase64String(content), "application/pdf")
    };

    private EmployeeDocument Stored(byte[] content, DocumentType documentType = DocumentType.Passport)
    {
        var document = EmployeeDocument.Create(
            EmployeeId, "passport.pdf", $"path/{_stored.Count}.pdf", "application/pdf", content.Length,
            Convert.ToHexStringLower(SHA256.HashData(content)), documentType, null, "user-1");
        _stored.Add(document);
        return document;
    }

    private static async Task<bool> RefusedAsDuplicate(SaveMyDocuments.Validator validator, SaveMyDocuments.Command command)
    {
        var result = await validator.ValidateAsync(command);
        return result.Errors.Any(e => e.ErrorMessage == BusinessErrorMessage.EmployeeDocumentDuplicateFile);
    }

    private static SaveMyDocuments.Command CommandOf(params SaveMyDocuments.DocumentToSave[] documents) =>
        new() { Documents = [.. documents] };

    [Fact]
    public async Task Valid_Documents_Pass()
    {
        var result = await CreateValidator().ValidateAsync(CommandOf(Document(Pdf(2048)), Document(Pdf(4096))));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(DocumentType.Passport)]
    [InlineData(DocumentType.IdentityCard)]
    public async Task The_Bytes_Of_An_Active_Pending_Document_Are_Refused_Under_Any_Type(DocumentType uploadedAs)
    {
        Stored(Pdf(2048), DocumentType.Passport);

        Assert.True(await RefusedAsDuplicate(CreateValidator(), CommandOf(Document(Pdf(2048), uploadedAs))));
    }

    [Fact]
    public async Task The_Bytes_Of_An_Approved_Document_Are_Refused()
    {
        Stored(Pdf(2048)).Approve("admin-1");

        Assert.True(await RefusedAsDuplicate(CreateValidator(), CommandOf(Document(Pdf(2048)))));
    }

    /// <summary>
    /// A passport uploaded as an ID card and rejected for the wrong type. Replacing cannot change the
    /// type, so uploading the same bytes again under the right one is the only way to correct it.
    /// </summary>
    [Fact]
    public async Task The_Bytes_Of_A_Rejected_Document_May_Be_Uploaded_Again_Under_Another_Type()
    {
        Stored(Pdf(2048), DocumentType.IdentityCard).Reject("admin-1", "This is a passport");

        var result = await CreateValidator().ValidateAsync(CommandOf(Document(Pdf(2048), DocumentType.Passport)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task The_Bytes_Of_A_Retired_Document_May_Be_Uploaded_Again()
    {
        Stored(Pdf(2048)).SoftDelete("user-1");

        var result = await CreateValidator().ValidateAsync(CommandOf(Document(Pdf(2048))));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task The_Same_Bytes_Twice_In_One_Upload_Are_Refused()
    {
        var command = CommandOf(
            Document(Pdf(2048), DocumentType.IdentityCard, "front.pdf"),
            Document(Pdf(2048), DocumentType.WorkPermit, "permit.pdf"));

        Assert.True(await RefusedAsDuplicate(CreateValidator(), command));
    }

    [Fact]
    public async Task Files_Sharing_A_Name_With_Different_Bytes_Are_Accepted()
    {
        Stored(Pdf(1024));

        var result = await CreateValidator().ValidateAsync(CommandOf(Document(Pdf(2048)), Document(Pdf(4096))));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Document_Over_TenMebibytes_Fails_The_Whole_Command()
    {
        var command = CommandOf(Document(Pdf(2048)), Document(Pdf(TenMebibytes + 1024)));

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileSizeExceeded);
    }

    [Fact]
    public async Task More_Documents_Than_The_Cap_Fails_With_FileCountExceeded()
    {
        var command = CommandOf([.. Enumerable.Range(0, 11).Select(_ => Document(Pdf(2048)))]);

        var result = await CreateValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.FileCountExceeded);
    }

    /// <summary>
    /// The collection-level mirror of the per-file ordering rule: an over-long list must be refused
    /// without every one of its items being decoded first, which is the cost the cap exists to refuse.
    /// A second, per-item error in the result means the item rules ran anyway.
    /// </summary>
    [Fact]
    public async Task Over_Long_List_Is_Refused_Without_Validating_Its_Items()
    {
        var command = CommandOf([.. Enumerable.Range(0, 11).Select(_ => Document("<html>"u8.ToArray()))]);

        var result = await CreateValidator().ValidateAsync(command);

        var failure = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.FileCountExceeded, failure.ErrorMessage);
    }
}
