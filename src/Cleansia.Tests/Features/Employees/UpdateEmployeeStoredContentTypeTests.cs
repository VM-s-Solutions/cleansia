using Cleansia.Core.AppServices.Features.Employees;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.Files;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.Employees;

/// <summary>
/// What a document uploaded through the PROFILE route says it is.
///
/// <para>The row this handler writes is read back by the same three download routes as
/// <c>SaveMyDocuments</c>' — <c>DownloadMyDocument</c> on both partner hosts and the admin's
/// <c>DownloadEmployeeDocument</c> — so whatever decides the stored type decides a response header. It
/// used to be <c>document.ContentType ?? "application/octet-stream"</c>: the client's own string, with a
/// fallback that was already unreachable because the old validator refused a null one.</para>
/// </summary>
public class UpdateEmployeeStoredContentTypeTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string CountryId = "cz";

    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IEmployeeDocumentRepository> _documentRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IBlobContainerClientFactory> _blobFactory = new();
    private readonly Mock<IBlobContainerClient> _blobClient = new();
    private readonly Mock<IAddressGeocoder> _geocoder = new();
    private readonly Mock<IConsentService> _consentService = new();
    private readonly List<EmployeeDocument> _added = [];

    private UpdateEmployee.Handler CreateHandler()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "First", "Last");
        user.Id = "user-1";
        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _employeeRepository
            .Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _documentRepository
            .Setup(r => r.Add(It.IsAny<EmployeeDocument>()))
            .Callback<EmployeeDocument>(_added.Add);

        _blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobClient.Object);
        _blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new UpdateEmployee.Handler(
            _employeeRepository.Object,
            _documentRepository.Object,
            _session.Object,
            _blobFactory.Object,
            _geocoder.Object,
            _consentService.Object);
    }

    private static UpdateEmployee.Command Upload(byte[] content, string fileName, string? declaredContentType) => new(
        EmployeeId: "emp-1",
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
        Documents: [new BlobFileDto(fileName, Convert.ToBase64String(content), declaredContentType)]);

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
