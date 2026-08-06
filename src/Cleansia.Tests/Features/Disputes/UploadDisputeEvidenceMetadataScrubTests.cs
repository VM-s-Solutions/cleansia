using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Blobs.Abstractions.Extensions;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Tests.Common.Media;
using Microsoft.Extensions.Logging;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// ADR-0043 D4 on the surface the panel ruled non-deferrable: here the uploader IS the disputing
/// customer (<c>UploadDisputeEvidence</c> refuses anyone else), the counterparty is a cleaner and the
/// outcome is money. A client-side strip is not a control against a party whose interest is to not
/// strip, so the scrub is enforceability rather than durability.
///
/// <para>Every assertion reads the bytes handed to the blob client, never "the scrub was called": what
/// reaches storage is the only thing an adjudicator can later download.</para>
/// </summary>
public class UploadDisputeEvidenceMetadataScrubTests
{
    private const string DisputeId = "dispute-scrub-1";
    private const string OwnerUserId = "user-scrub-1";

    [Fact]
    public async Task A_Photograph_Submitted_As_Evidence_Reaches_Storage_Without_Its_Coordinates()
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6));

        var stored = await UploadAsync(photo, "evidence.jpg", "image/jpeg");

        Assert.False(ByteSequence.Contains(stored, SyntheticJpeg.GpsCoordinateBytes()));
        Assert.False(ByteSequence.Contains(stored, SyntheticJpeg.DeviceSentinel));
        Assert.True(ByteSequence.Contains(stored, SyntheticJpeg.ImageBody));
    }

    /// <summary>
    /// The declared content type says PNG and the bytes say JPEG. Dispatching on the caller's string
    /// would run the PNG walker over a JPEG — it finds no signature, bails, and the coordinates survive
    /// under a test that still says "scrubbed".
    /// </summary>
    [Theory]
    [InlineData("image/png", "evidence.png")]
    [InlineData("application/pdf", "evidence.pdf")]
    [InlineData("image/webp", "evidence.webp")]
    public async Task The_Callers_Declared_Type_Never_Chooses_The_Walker(string declaredType, string fileName)
    {
        var photo = SyntheticJpeg.Photo(SyntheticJpeg.ExifSegment(orientation: 6));

        var stored = await UploadAsync(photo, fileName, declaredType);

        Assert.False(ByteSequence.Contains(stored, SyntheticJpeg.GpsCoordinateBytes()));
    }

    [Fact]
    public async Task A_Png_Submitted_As_Evidence_Loses_Its_Text_Chunks()
    {
        var image = SyntheticPng.Image(SyntheticPng.ExifChunk(), SyntheticPng.TextChunk("Model", SyntheticPng.DeviceSentinel));

        var stored = await UploadAsync(image, "evidence.png", "image/png");

        Assert.False(ByteSequence.Contains(stored, SyntheticPng.LocationSentinel));
        Assert.False(ByteSequence.Contains(stored, SyntheticPng.DeviceSentinel));
    }

    /// <summary>
    /// A PDF is accepted evidence and is deliberately NOT rewritten (ADR-0043 D8 — an object-graph
    /// rewrite is the thing the no-decoder ruling refuses). It must therefore arrive at storage exactly
    /// as it was sent: an evidence file this platform quietly mangled is an adjudication problem.
    /// </summary>
    [Fact]
    public async Task A_Pdf_Is_Stored_Byte_For_Byte()
    {
        byte[] pdf = [.. "%PDF-1.7\n"u8, .. "/Author (SentinelCam)"u8, .. "\n%%EOF"u8];

        var stored = await UploadAsync(pdf, "evidence.pdf", "application/pdf");

        Assert.Equal(pdf, stored);
    }

    private static async Task<byte[]> UploadAsync(byte[] fileData, string fileName, string declaredContentType)
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

        byte[]? uploaded = null;
        var blobClient = new Mock<IBlobContainerClient>();
        blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Metadata?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, Metadata?, CancellationToken>((_, stream, _, _) =>
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                uploaded = copy.ToArray();
            })
            .Returns(Task.CompletedTask);
        blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<ServedContentType>()))
            .Returns(new Uri("https://account.blob.core.windows.net/dispute-evidence/x?sig=x"));

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
        Assert.NotNull(uploaded);
        return uploaded!;
    }
}
