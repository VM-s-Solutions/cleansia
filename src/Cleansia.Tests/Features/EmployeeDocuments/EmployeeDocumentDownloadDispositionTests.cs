using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Cleansia.Tests.Features.EmployeeDocuments;

/// <summary>
/// Every route that hands an employee document back to a browser, and which <c>File(...)</c> overload it
/// uses.
///
/// <para><c>File(bytes, contentType, fileName)</c> sets <c>Content-Disposition: attachment</c>; the
/// two-argument <c>File(bytes, contentType)</c> sets no disposition header at all. So the difference
/// between "a hostile stored type downloads" and "a hostile stored type renders with the API host's
/// origin" is one argument at a call site, in a file nobody would think of as security-relevant. The
/// served type is now derived from the bytes, so this is the second line and not the first — but it is
/// the line whose loss is invisible in review, which is why it is asserted on the returned result rather
/// than assumed from reading the call.</para>
///
/// <para>The set is these three and no others: nothing else reads the <c>employee-documents</c> container
/// for a client (no SAS is ever minted for it), and <c>GetMyDocuments</c> returns the blob PATH, which is
/// unusable without credentials.</para>
/// </summary>
public class EmployeeDocumentDownloadDispositionTests
{
    private const string DocumentId = "doc-1";

    [Fact]
    public async Task PartnerWeb_DownloadMyDocument_Is_An_Attachment()
    {
        var controller = new Cleansia.Web.Partner.Controllers.EmployeeController(MyDocumentMediator());

        var result = await controller.DownloadMyDocument(new DownloadMyDocument.Query(DocumentId), CancellationToken.None);

        AssertAttachment(result);
    }

    [Fact]
    public async Task PartnerMobile_DownloadMyDocument_Is_An_Attachment()
    {
        var controller = new Cleansia.Web.Mobile.Partner.Controllers.EmployeeController(MyDocumentMediator());

        var result = await controller.DownloadMyDocument(new DownloadMyDocument.Query(DocumentId), CancellationToken.None);

        AssertAttachment(result);
    }

    [Fact]
    public async Task Admin_DownloadEmployeeDocument_Is_An_Attachment()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<DownloadEmployeeDocument.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new DownloadEmployeeDocument.Response(
                [0x25, 0x50, 0x44, 0x46, 0x2D], "contract.pdf", "application/pdf")));

        var controller = new Cleansia.Web.Admin.Controllers.AdminEmployeeDocumentController(mediator.Object);

        var result = await controller.DownloadDocument(DocumentId, CancellationToken.None);

        AssertAttachment(result);
    }

    private static IMediator MyDocumentMediator()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<DownloadMyDocument.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new DownloadMyDocument.Response(
                [0x25, 0x50, 0x44, 0x46, 0x2D], "contract.pdf", "application/pdf")));

        return mediator.Object;
    }

    private static void AssertAttachment(IActionResult result)
    {
        var file = Assert.IsType<FileContentResult>(result);

        Assert.False(
            string.IsNullOrEmpty(file.FileDownloadName),
            "the two-argument File(bytes, contentType) overload emits no Content-Disposition, so a stored type is served for the browser to act on");
    }
}
