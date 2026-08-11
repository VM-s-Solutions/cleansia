using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
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

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();

    public SaveMyDocumentsValidatorTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        user.ConfirmEmail();
        user.Id = "user-1";

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Employee.CreateWithUser(user));
    }

    private SaveMyDocuments.Validator CreateValidator() =>
        new(_userRepository.Object, _session.Object, _employeeRepository.Object);

    private static byte[] Pdf(long size)
    {
        var bytes = new byte[size];
        "%PDF-"u8.CopyTo(bytes);
        return bytes;
    }

    private static SaveMyDocuments.DocumentToSave Document(byte[] content) => new()
    {
        DocumentType = DocumentType.Passport,
        File = new BlobFileDto("passport.pdf", Convert.ToBase64String(content), "application/pdf")
    };

    private static SaveMyDocuments.Command CommandOf(params SaveMyDocuments.DocumentToSave[] documents) =>
        new() { Documents = [.. documents] };

    [Fact]
    public async Task Valid_Documents_Pass()
    {
        var result = await CreateValidator().ValidateAsync(CommandOf(Document(Pdf(2048)), Document(Pdf(2048))));

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
