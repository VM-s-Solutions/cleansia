using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Moq;

namespace Cleansia.Tests.Features.EmployeeDocuments;

/// <summary>
/// Asking replaced doing, and only the answer removes anything.
///
/// <para><b>What this closes.</b> The partner delete button removed the document immediately, with no
/// confirmation on either mobile platform, and the soft-delete flipped <c>AreDocumentsUploaded</c> —
/// which re-engaged the registration lock. One tap cost a cleaner their access to work, and it was a
/// tap on documents the employer is required to hold.</para>
///
/// <para>The two halves are tested together because the property that matters spans them: the request
/// leaves the document ACTIVE, and only <c>ResolveDocumentDeletionRequest</c> with
/// <c>Approve: true</c> ever soft-deletes it.</para>
/// </summary>
public class DocumentDeletionRequestTests
{
    private const string UserEmail = "cleaner@cleansia.cz";
    private const string EmployeeId = "emp-1";
    private const string DocumentId = "doc-1";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmployeeRepository> _employees = new();
    private readonly Mock<IEmployeeDocumentRepository> _documents = new();
    private readonly Mock<IDocumentDeletionRequestRepository> _requests = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IAuditContext> _audit = new();

    private readonly List<DocumentDeletionRequest> _added = [];
    private readonly EmployeeDocument _document;
    private readonly User _user;

    public DocumentDeletionRequestTests()
    {
        _user = User.CreateWithPassword(UserEmail, "Password1", "Clea", "Ner");
        _user.Id = "user-1";
        _user.ConfirmEmail();

        var employee = Employee.CreateWithUser(_user);
        employee.Id = EmployeeId;

        _document = EmployeeDocument.Create(
            EmployeeId, "id.pdf", "path/id.pdf", "application/pdf", 1024,
            DocumentType.IdentityCard, null, _user.Id);
        _document.Id = DocumentId;

        _session.Setup(s => s.GetUserEmail()).Returns(UserEmail);
        _users.Setup(r => r.GetByEmailAsync(UserEmail, It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _employees.Setup(r => r.GetByUserEmailAsync(UserEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);
        _documents.Setup(r => r.ExistsAsync(DocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _documents.Setup(r => r.GetByIdAsync(DocumentId, It.IsAny<CancellationToken>())).ReturnsAsync(_document);
        _requests.Setup(r => r.GetOpenForDocumentAsync(DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentDeletionRequest?)null);
        _requests.Setup(r => r.Add(It.IsAny<DocumentDeletionRequest>()))
            .Callback<DocumentDeletionRequest>(_added.Add);
    }

    private RequestMyDocumentDeletion.Validator RequestValidator() =>
        new(_users.Object, _session.Object, _employees.Object, _documents.Object, _requests.Object);

    private RequestMyDocumentDeletion.Handler RequestHandler() =>
        new(_employees.Object, _documents.Object, _requests.Object, _session.Object);

    private ResolveDocumentDeletionRequest.Handler ResolveHandler() =>
        new(_requests.Object, _documents.Object, _users.Object, _session.Object, _audit.Object);

    private async Task<IReadOnlyList<string>> RequestErrors(string documentId, string reason)
    {
        var result = await RequestValidator().ValidateAsync(
            new RequestMyDocumentDeletion.Command(documentId, reason), CancellationToken.None);

        return result.Errors.Select(e => e.ErrorMessage).ToList();
    }

    /// <summary>
    /// The whole design in one assertion. A pending request is a message, not a state change — so a
    /// request nobody answers leaves the cleaner exactly as they were rather than locked out.
    /// </summary>
    [Fact]
    public async Task Requesting_Deletion_Leaves_The_Document_Active()
    {
        var result = await RequestHandler().Handle(
            new RequestMyDocumentDeletion.Command(DocumentId, "Wrong file"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(_document.IsActive);
        Assert.Single(_added);
        Assert.Equal(DocumentDeletionRequestStatus.Pending, _added[0].Status);
    }

    /// <summary>A second request is not more urgent — it is the same ask twice, and it would give an
    /// admin two rows to answer for one decision.</summary>
    [Fact]
    public async Task A_Second_Request_For_The_Same_Document_Is_Refused_While_The_First_Is_Open()
    {
        _requests
            .Setup(r => r.GetOpenForDocumentAsync(DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DocumentDeletionRequest.Create(DocumentId, EmployeeId, "Wrong file", _user.Id));

        Assert.Contains(
            BusinessErrorMessage.DocumentDeletionAlreadyRequested,
            await RequestErrors(DocumentId, "Wrong file again"));
    }

    /// <summary>
    /// An ANSWERED request does not block a new one. A cleaner refused once may have a better reason
    /// the second time, and a rejection that silently became permanent would be a refusal nobody
    /// decided on.
    /// </summary>
    [Fact]
    public async Task An_Answered_Request_Does_Not_Block_A_New_One()
    {
        var resolved = DocumentDeletionRequest.Create(DocumentId, EmployeeId, "Wrong file", _user.Id);
        resolved.Reject("admin-1", "We need this on file");

        // GetOpenForDocumentAsync is what the validator asks, and a rejected request is not open.
        _requests
            .Setup(r => r.GetOpenForDocumentAsync(DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DocumentDeletionRequest?)null);

        Assert.False(resolved.IsOpen);
        Assert.DoesNotContain(
            BusinessErrorMessage.DocumentDeletionAlreadyRequested,
            await RequestErrors(DocumentId, "It expired last week"));
    }

    /// <summary>
    /// The reason IS the request. Without one an admin is being asked to rule on nothing, and the whole
    /// point of routing this through a person is that they have something to judge.
    /// </summary>
    [Fact]
    public async Task A_Request_Without_A_Reason_Is_Refused()
    {
        Assert.Contains(BusinessErrorMessage.Required, await RequestErrors(DocumentId, string.Empty));
    }

    [Fact]
    public async Task A_Document_Belonging_To_Somebody_Else_Cannot_Be_Requested()
    {
        var theirs = EmployeeDocument.Create(
            "emp-2", "id.pdf", "path/theirs.pdf", "application/pdf", 1024,
            DocumentType.IdentityCard, null, "user-2");
        theirs.Id = "doc-2";

        _documents.Setup(r => r.ExistsAsync("doc-2", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _documents.Setup(r => r.GetByIdAsync("doc-2", It.IsAny<CancellationToken>())).ReturnsAsync(theirs);

        Assert.Contains(
            BusinessErrorMessage.EmployeeDocumentNotOwned,
            await RequestErrors("doc-2", "Not mine"));
    }

    /// <summary>Approving is the ONLY thing in the system that removes one of these.</summary>
    [Fact]
    public async Task Approving_The_Request_Is_What_Removes_The_Document()
    {
        var request = Pending();

        var result = await ResolveHandler().Handle(
            new ResolveDocumentDeletionRequest.Command(request.Id, Approve: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentDeletionRequestStatus.Approved, result.Value.Status);
        Assert.False(_document.IsActive);
    }

    [Fact]
    public async Task Rejecting_The_Request_Leaves_The_Document_Alone()
    {
        var request = Pending();

        var result = await ResolveHandler().Handle(
            new ResolveDocumentDeletionRequest.Command(request.Id, Approve: false, "We need this on file"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentDeletionRequestStatus.Rejected, result.Value.Status);
        Assert.True(_document.IsActive);
        Assert.Equal("We need this on file", request.ReviewNotes);
    }

    /// <summary>
    /// Approval speaks for itself — the document is gone. A refusal without a reason tells the cleaner
    /// only that somebody said no, so they either ask again or stop trusting the queue.
    /// </summary>
    [Fact]
    public async Task A_Rejection_Must_Say_Why_And_An_Approval_Need_Not()
    {
        var request = Pending();
        var validator = new ResolveDocumentDeletionRequest.Validator(_requests.Object);

        var rejection = await validator.ValidateAsync(
            new ResolveDocumentDeletionRequest.Command(request.Id, Approve: false), CancellationToken.None);
        var approval = await validator.ValidateAsync(
            new ResolveDocumentDeletionRequest.Command(request.Id, Approve: true), CancellationToken.None);

        Assert.Contains(rejection.Errors, e => e.ErrorMessage == BusinessErrorMessage.Required);
        Assert.True(approval.IsValid);
    }

    [Fact]
    public async Task An_Already_Answered_Request_Cannot_Be_Answered_Again()
    {
        var request = Pending();
        request.Approve("admin-1", null);

        var result = await new ResolveDocumentDeletionRequest.Validator(_requests.Object).ValidateAsync(
            new ResolveDocumentDeletionRequest.Command(request.Id, Approve: false, "Changed my mind"),
            CancellationToken.None);

        Assert.Contains(
            result.Errors,
            e => e.ErrorMessage == BusinessErrorMessage.DocumentDeletionAlreadyResolved);
    }

    /// <summary>
    /// Approving a request whose document has already gone is not an error — the outcome the cleaner
    /// asked for is the outcome they have, and answering the request anyway is what closes it out of the
    /// queue. Without this the row would sit in the admin's to-do list with no way to clear it.
    /// </summary>
    [Fact]
    public async Task Approving_A_Request_Whose_Document_Is_Already_Gone_Still_Closes_It()
    {
        var request = Pending();
        _documents
            .Setup(r => r.GetByIdAsync(DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeDocument?)null);

        var result = await ResolveHandler().Handle(
            new ResolveDocumentDeletionRequest.Command(request.Id, Approve: true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentDeletionRequestStatus.Approved, request.Status);
    }

    private DocumentDeletionRequest Pending()
    {
        var request = DocumentDeletionRequest.Create(DocumentId, EmployeeId, "Wrong file", _user.Id);
        request.Id = "req-1";

        _requests.Setup(r => r.ExistsAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _requests.Setup(r => r.GetByIdAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        return request;
    }
}
