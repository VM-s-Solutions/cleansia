using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.EmployeeDocuments;

/// <summary>
/// What the stored document says it is.
///
/// <para>The recorded content type is not decoration: it is handed back verbatim as the
/// <c>Content-Type</c> of every download (<c>DownloadMyDocument</c>, <c>DownloadEmployeeDocument</c>,
/// and the admin route), so whatever decides it decides a response header on three routes. It used to
/// be the client's own <c>ContentType</c> string, falling back to the file extension — two values the
/// uploader picks freely and neither of which is evidence about the bytes.</para>
/// </summary>
public class SaveMyDocumentsHandlerTests
{
    private const string UserEmail = "cleaner@cleansia.cz";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmployeeDocumentRepository> _documentRepository = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IBlobContainerClientFactory> _blobFactory = new();
    private readonly Mock<IBlobContainerClient> _blobClient = new();
    private readonly List<EmployeeDocument> _added = [];

    private SaveMyDocuments.Handler CreateHandler()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        user.Id = "user-1";
        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _userRepository
            .Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _documentRepository
            .Setup(r => r.GetLatestByFileNameAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeDocument?)null);
        _documentRepository
            .Setup(r => r.Add(It.IsAny<EmployeeDocument>()))
            .Callback<EmployeeDocument>(_added.Add);

        _blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobClient.Object);
        _blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobClient
            .Setup(c => c.GetBlobUri(It.IsAny<string>()))
            .Returns(new Uri("https://storage.invalid/employee-documents/blob"));

        return new SaveMyDocuments.Handler(
            _employeeRepository.Object,
            _documentRepository.Object,
            _userRepository.Object,
            _session.Object,
            _blobFactory.Object);
    }

    private static SaveMyDocuments.Command Upload(byte[] content, string fileName, string? declaredContentType) =>
        new()
        {
            Documents =
            [
                new SaveMyDocuments.DocumentToSave
                {
                    DocumentType = DocumentType.Passport,
                    File = new BlobFileDto(fileName, Convert.ToBase64String(content), declaredContentType)
                }
            ]
        };

    private static byte[] Jpeg(long size)
    {
        var bytes = new byte[size];
        new byte[] { 0xFF, 0xD8, 0xFF }.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>
    /// Both client-supplied claims point at PDF and the bytes are a JPEG. Whichever of the two the
    /// handler believed, the stored object would be served as <c>application/pdf</c>.
    /// </summary>
    [Fact]
    public async Task Stored_ContentType_Comes_From_The_Bytes_Not_The_Declared_Type_Or_The_Extension()
    {
        var command = Upload(Jpeg(2048), fileName: "payslip.pdf", declaredContentType: "application/pdf");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("image/jpeg", Assert.Single(_added).ContentType);
    }
}
