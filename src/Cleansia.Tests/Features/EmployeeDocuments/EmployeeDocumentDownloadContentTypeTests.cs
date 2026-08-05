using Cleansia.Core.AppServices.Features.EmployeeDocuments;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Documents;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.EmployeeDocuments;

/// <summary>
/// The rows that were already there.
///
/// <para>Hardening an intake retypes nothing that was stored before it, and every row written by either
/// employee-document intake before this sprint carries whatever content type its uploader claimed —
/// including values no intake would accept today. That residue is a CLOSED set on the read path: two
/// handlers serve these blobs and nothing else does, so deriving the served type from the bytes there
/// fixes the rows already written without a backfill, exactly as <c>ServedContentType</c> did for order
/// photos. The signature table is the intake's, not a second one, so the two answers cannot drift.</para>
///
/// <para>It also removes this path's dependence on <c>Content-Disposition</c>. The three-argument
/// <c>File(bytes, type, name)</c> overload the hosts use emits <c>attachment</c>, which is why a stored
/// <c>text/html</c> downloaded rather than rendered; the two-argument overload emits no disposition at
/// all, so that mitigation was one call-site edit from gone. The byte-derived type holds either way.</para>
/// </summary>
public class EmployeeDocumentDownloadContentTypeTests
{
    private const string DocumentId = "doc-1";
    private const string EmployeeId = "emp-1";

    private static readonly byte[] Pdf = "%PDF-1.7\n%âãÏÓ"u8.ToArray();
    private static readonly byte[] Markup = "<html><script>alert(1)</script></html>"u8.ToArray();

    /// <summary>
    /// Each row is one a legacy uploader could have written: the recorded type is a client string, and
    /// the bytes are whatever was actually posted under it.
    /// </summary>
    public static TheoryData<string, byte[], string> LegacyRows() => new()
    {
        { "text/html", Pdf, "application/pdf" },
        { "image/svg+xml", Markup, "application/octet-stream" },
        { "application/pdf", Markup, "application/octet-stream" },
        { "application/octet-stream", Pdf, "application/pdf" }
    };

    [Theory]
    [MemberData(nameof(LegacyRows))]
    public async Task DownloadMyDocument_Serves_What_The_Bytes_Say(string recorded, byte[] stored, string expected)
    {
        var (repository, blobFactory) = Arrange(recorded, stored);

        var result = await new DownloadMyDocument.Handler(repository, blobFactory)
            .Handle(new DownloadMyDocument.Query(DocumentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value!.ContentType);
    }

    [Theory]
    [MemberData(nameof(LegacyRows))]
    public async Task DownloadEmployeeDocument_Serves_What_The_Bytes_Say(string recorded, byte[] stored, string expected)
    {
        var (repository, blobFactory) = Arrange(recorded, stored);

        var result = await new DownloadEmployeeDocument.Handler(repository, blobFactory)
            .Handle(new DownloadEmployeeDocument.Query(DocumentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value!.ContentType);
    }

    private static (IEmployeeDocumentRepository, IBlobContainerClientFactory) Arrange(string recorded, byte[] stored)
    {
        var document = EmployeeDocument.Create(
            EmployeeId, "contract.pdf", "employees/emp-1/documents/contract.pdf", recorded,
            stored.Length, DocumentType.Other, null, "seed");
        document.Id = DocumentId;

        var repository = new Mock<IEmployeeDocumentRepository>();
        repository
            .Setup(r => r.GetByIdAsync(DocumentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.DownloadAsync(document.FilePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BlobFile(new MemoryStream(stored), recorded));

        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        return (repository.Object, blobFactory.Object);
    }
}
