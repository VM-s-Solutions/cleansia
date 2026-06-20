using Cleansia.Core.AppServices.Features.Disputes;
using Cleansia.Core.Blobs.Abstractions;
using Cleansia.Core.Domain.Disputes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging;
using MockQueryable;
using MockQueryable.Moq;
using Moq;

namespace Cleansia.Tests.Features.Disputes;

/// <summary>
/// DA-12 — the empty <c>catch { }</c> around dispute-evidence SAS-URI generation hid the failure.
/// Pins the graceful-degradation contract: when SAS generation throws AFTER the blob upload + evidence
/// row are persisted, the command still SUCCEEDS with a null <c>BlobUrl</c> (behavior preserved) and
/// the failure is LOGGED (S6 — no silent swallow), never re-thrown.
/// </summary>
public class UploadDisputeEvidenceSasFailureTests
{
    private const string DisputeId = "dispute-1";
    private const string OwnerUserId = "user-1";

    private readonly Mock<IDisputeRepository> _disputeRepository = new();
    private readonly Mock<IUserSessionProvider> _session = new();
    private readonly Mock<IBlobContainerClientFactory> _blobFactory = new();
    private readonly Mock<IBlobContainerClient> _blobClient = new();
    private readonly Mock<ILogger<UploadDisputeEvidence.Handler>> _logger = new();

    private UploadDisputeEvidence.Handler CreateHandler()
    {
        _session.Setup(s => s.GetUserId()).Returns(OwnerUserId);

        var dispute = new Dispute(
            orderId: "order-1",
            userId: OwnerUserId,
            reason: DisputeReason.Other,
            description: "x",
            createdBy: OwnerUserId)
        {
            Id = DisputeId,
        };
        _disputeRepository.Setup(r => r.GetQueryable())
            .Returns(new[] { dispute }.AsQueryable().BuildMock());

        _blobFactory.Setup(f => f.GetBlobContainerClient(It.IsAny<string>())).Returns(_blobClient.Object);
        _blobClient
            .Setup(c => c.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Cleansia.Core.Blobs.Abstractions.Extensions.Metadata?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _blobClient
            .Setup(c => c.GenerateSasUri(It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .Throws(new InvalidOperationException("SAS unavailable"));

        return new UploadDisputeEvidence.Handler(
            _disputeRepository.Object, _session.Object, _blobFactory.Object, _logger.Object);
    }

    private static UploadDisputeEvidence.Command ValidCommand() =>
        new(DisputeId, "evidence.png", "image/png", new byte[] { 1, 2, 3 });

    [Fact]
    public async Task SasGenerationThrows_UploadStillSucceeds_WithNullBlobUrl()
    {
        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.BlobUrl);
        Assert.Equal("evidence.png", result.Value.FileName);
    }

    [Fact]
    public async Task SasGenerationThrows_FailureIsLogged_NotSwallowed()
    {
        await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(e => e is InvalidOperationException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
