using Cleansia.Core.AppServices.Abstractions;
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
/// The door that needs no admin, because the slot never empties.
///
/// <para><b>The ordering is the feature.</b> The replacement is added BEFORE the old version is
/// retired, so no read between the two sees the cleaner with one fewer document —
/// <c>AreDocumentsUploaded</c> never dips and the registration lock never re-engages. That is the
/// entire reason replacing is allowed where deleting is not.</para>
/// </summary>
public class ReplaceMyDocumentTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string DocumentId = "doc-1";

    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IEmployeeDocumentRepository> _documents = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IBlobContainerClientFactory> _blobFactory = new();
    private readonly Mock<IBlobContainerClient> _blobClient = new();

    private readonly List<EmployeeDocument> _added = [];
    private readonly EmployeeDocument _previous;

    /// <summary>
    /// Sampled at the moment the replacement is handed to the repository. Observing the old version
    /// from inside the <c>Add</c> callback is what makes the ORDERING assertable without widening the
    /// aggregate to announce its own writes.
    /// </summary>
    private bool? _previousWasStillActiveWhenTheReplacementLanded;

    public ReplaceMyDocumentTests()
    {
        var user = User.CreateWithPassword(UserEmail, "Password1", "Clea", "Ner");
        user.Id = "user-1";

        var employee = Employee.CreateWithUser(user);
        employee.Id = "emp-1";

        _previous = EmployeeDocument.Create(
            employee.Id, "id.pdf", "path/id.pdf", "application/pdf", 1024,
            DocumentType.IdentityCard, "My ID", user.Id);
        _previous.Id = DocumentId;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _users.Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _employees.Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _documents.Setup(r => r.GetByIdAsync(DocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(_previous);
        _documents
            .Setup(r => r.Add(It.IsAny<EmployeeDocument>()))
            .Callback<EmployeeDocument>(d =>
            {
                _added.Add(d);
                _previousWasStillActiveWhenTheReplacementLanded = _previous.IsActive;
            });

        _blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobClient.Object);
        _blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private ReplaceMyDocument.Handler Handler() =>
        new(_employees.Object, _documents.Object, _users.Object, _session.Object, _blobFactory.Object);

    private static ReplaceMyDocument.Command Replacement(string? description = null) =>
        new(DocumentId, new BlobFileDto("new-id.jpg", Convert.ToBase64String(Jpeg()), "image/jpeg"), description);

    private static byte[] Jpeg()
    {
        var bytes = new byte[2048];
        new byte[] { 0xFF, 0xD8, 0xFF }.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>
    /// The count never dips. If the old version were retired first, a read landing between the two
    /// writes would see zero documents of that type and the lock would re-engage on a cleaner who was
    /// mid-upload.
    /// </summary>
    [Fact]
    public async Task The_New_Version_Exists_Before_The_Old_One_Is_Retired()
    {
        var result = await Handler().Handle(Replacement(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_previousWasStillActiveWhenTheReplacementLanded);
        Assert.False(_previous.IsActive);
    }

    [Fact]
    public async Task The_Replacement_Is_The_Next_Version_Of_The_Same_Chain()
    {
        var result = await Handler().Handle(Replacement(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(_previous.Version + 1, result.Value.Version);
        Assert.Equal(DocumentId, Assert.Single(_added).PreviousVersionId);
    }

    /// <summary>
    /// Replacing is "here is a newer one of these". Letting the caller pick the type would let a cleaner
    /// satisfy a requirement by relabelling a document an admin already approved as something else.
    /// </summary>
    [Fact]
    public async Task The_Document_Type_Is_Carried_Over_Never_Taken_From_The_Caller()
    {
        await Handler().Handle(Replacement(), CancellationToken.None);

        Assert.Equal(DocumentType.IdentityCard, Assert.Single(_added).DocumentType);
    }

    /// <summary>
    /// The new version is new evidence and has not been looked at. It is also why this cannot be used
    /// to dodge review: replacing an approved document costs its approved status.
    /// </summary>
    [Fact]
    public async Task Replacing_An_Approved_Document_Yields_A_Pending_One()
    {
        _previous.Approve("admin-1");

        await Handler().Handle(Replacement(), CancellationToken.None);

        Assert.Equal(DocumentStatus.Pending, Assert.Single(_added).Status);
    }

    /// <summary>
    /// Sniffed, never trusted from the client — the same rule the upload path follows. The declared
    /// type here is a lie about PDF bytes that are really a JPEG.
    /// </summary>
    [Fact]
    public async Task The_Stored_Content_Type_Comes_From_The_BYTES_Not_The_Claim()
    {
        var command = new ReplaceMyDocument.Command(
            DocumentId, new BlobFileDto("new-id.pdf", Convert.ToBase64String(Jpeg()), "application/pdf"), null);

        await Handler().Handle(command, CancellationToken.None);

        Assert.Equal("image/jpeg", Assert.Single(_added).ContentType);
    }
}
