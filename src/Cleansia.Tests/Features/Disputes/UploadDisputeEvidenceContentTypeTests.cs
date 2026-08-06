using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// The evidence row records no content type, so the only place a later read can recover one from is the
/// blob NAME — and its extension used to be <c>Path.GetExtension(command.FileName)</c>, i.e. the caller's
/// own string, carried onto the served <c>Content-Type</c> of every subsequent read. Both the name and
/// the upload response's SAS header are derived from the bytes now.
///
/// <para>This intake was invisible to the upload roster for two tickets because it arrives as an
/// <c>IFormFile</c> rather than a <c>BlobFileDto</c> — see <c>UploadIntakeRosterTests</c>.</para>
/// </summary>
public class UploadDisputeEvidenceContentTypeTests
{
    private const string DisputeId = "dispute-ct-1";
    private const string OwnerUserId = "user-ct-1";

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37, 0x0A, 0x25, 0x00, 0x00];

    private static byte[] WebpBytes()
    {
        var bytes = new byte[64];
        "RIFF"u8.ToArray().CopyTo(bytes, 0);
        "WEBP"u8.ToArray().CopyTo(bytes, 8);
        return bytes;
    }

    [Theory]
    [InlineData("evidence.pdf", "image/png", ".png")]
    [InlineData("evidence.png", "application/pdf", ".pdf")]
    public async Task The_Blob_Name_Extension_Comes_From_The_Bytes_Not_The_File_Name(
        string fileName, string declaredContentType, string expectedExtension)
    {
        var bytes = expectedExtension == ".png" ? PngBytes : PdfBytes;

        var blobName = await UploadAsync(bytes, fileName, declaredContentType);

        Assert.EndsWith(expectedExtension, blobName);
    }

    /// <summary>
    /// The SAS on the upload response pins the response header, so it must carry the byte-derived answer
    /// rather than the caller's declared string.
    /// </summary>
    [Fact]
    public async Task The_Served_Type_On_The_Response_Comes_From_The_Bytes()
    {
        ServedContentType? servedAs = null;

        await UploadAsync(PngBytes, "evidence.pdf", "application/pdf", captureServedAs: s => servedAs = s);

        Assert.NotNull(servedAs);
        Assert.Equal("image/png", servedAs!.Value);
    }

    /// <summary>
    /// The read path is a different call site (<see cref="DisputeMappers"/>) and must resolve the SAME
    /// answer, or the type a client is told on upload and the type it receives on every later fetch
    /// disagree. Resolving the stored PATH is what makes that hold: its extension is server-minted, while
    /// <c>FileName</c> stays the caller's own string for display.
    /// </summary>
    [Fact]
    public async Task The_Read_Path_Resolves_The_Same_Type_From_The_Stored_Path()
    {
        var blobName = await UploadAsync(PngBytes, "evidence.pdf", "application/pdf");

        var evidence = new DisputeEvidence(DisputeId, "evidence.pdf", blobName, OwnerUserId);

        ServedContentType? servedAs = null;
        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ServedContentType>()))
            .Returns<string, TimeSpan, ServedContentType>((name, _, served) =>
            {
                servedAs = served;
                return new Uri($"https://account.blob.core.windows.net/dispute-evidence/{name}?sig=x");
            });

        evidence.MapToDto(blobClient.Object);

        Assert.NotNull(servedAs);
        Assert.Equal("image/png", servedAs!.Value);
    }

    [Theory]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 })] // RIFF….WAVE
    [InlineData(new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C, 0x3E, 0x0A, 0x00 })] // <html>
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00 })] // a zip / docx
    public async Task A_Payload_That_Is_Not_Accepted_Evidence_Is_Refused(byte[] fileData)
    {
        var result = await Validate(fileData);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == BusinessErrorMessage.InvalidFileType);
    }

    /// <summary>
    /// Every content type this intake's accepted set names, reached through a genuine payload of that
    /// format. A set member with no matching row in the signature table is unreachable — the intake would
    /// refuse a format the evidence picker offers — and nothing but this pairing would say so.
    /// </summary>
    public static TheoryData<string, byte[]> AcceptedFormats() => new()
    {
        { ".jpg", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01] },
        { ".png", PngBytes },
        { ".webp", WebpBytes() },
        { ".pdf", PdfBytes }
    };

    [Theory]
    [MemberData(nameof(AcceptedFormats))]
    public async Task Every_Format_The_Clients_Offer_Is_Accepted(string expectedExtension, byte[] fileData)
    {
        Assert.True((await Validate(fileData)).IsValid, $"{expectedExtension} was refused, but the picker offers it.");

        var blobName = await UploadAsync(fileData, "evidence.bin", "application/octet-stream");

        Assert.EndsWith(expectedExtension, blobName);
    }

    private static async Task<FluentValidation.Results.ValidationResult> Validate(byte[] fileData)
    {
        var disputeRepository = new Mock<IDisputeRepository>();
        disputeRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return await new UploadDisputeEvidence.Validator(disputeRepository.Object)
            .ValidateAsync(new UploadDisputeEvidence.Command(DisputeId, "evidence.pdf", "application/pdf", fileData));
    }

    private static async Task<string> UploadAsync(
        byte[] fileData,
        string fileName,
        string declaredContentType,
        Action<ServedContentType>? captureServedAs = null)
    {
        var session = new Mock<IUserSessionProvider>();
        session.Setup(s => s.GetUserId()).Returns(OwnerUserId);

        var dispute = new Dispute(
            orderId: "order-1",
            userId: OwnerUserId,
            reason: DisputeReason.Other,
            description: "x",
            createdBy: OwnerUserId)
        {
            Id = DisputeId,
        };

        var disputeRepository = new Mock<IDisputeRepository>();
        disputeRepository.Setup(r => r.GetQueryable()).Returns(new[] { dispute }.AsQueryable().BuildMock());

        string? uploadedBlobName = null;
        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<Cleansia.Core.Blobs.Abstractions.Extensions.Metadata?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, Cleansia.Core.Blobs.Abstractions.Extensions.Metadata?, CancellationToken>(
                (name, _, _, _) => uploadedBlobName = name)
            .Returns(Task.CompletedTask);
        blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ServedContentType>()))
            .Returns<string, TimeSpan, ServedContentType>((name, _, served) =>
            {
                captureServedAs?.Invoke(served);
                return new Uri($"https://account.blob.core.windows.net/dispute-evidence/{name}?sig=x");
            });

        var blobFactory = new Mock<IBlobContainerClientFactory>();
        blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(blobClient.Object);

        var handler = new UploadDisputeEvidence.Handler(
            disputeRepository.Object,
            session.Object,
            blobFactory.Object,
            new Mock<ILogger<UploadDisputeEvidence.Handler>>().Object);

        var result = await handler.Handle(
            new UploadDisputeEvidence.Command(DisputeId, fileName, declaredContentType, fileData),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(uploadedBlobName);
        return uploadedBlobName!;
    }
}
