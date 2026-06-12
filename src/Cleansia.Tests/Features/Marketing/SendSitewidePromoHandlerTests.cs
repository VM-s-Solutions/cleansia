using Cleansia.Core.AppServices.Features.Marketing;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Moq;

namespace Cleansia.Tests.Features.Marketing;

/// <summary>
/// LG-SEC-09 -- the producer side of the sitewide-promo fan-out. A retried/double-submitted admin action
/// must NOT start a second full fan-out: the second invocation for the same campaign identity
/// short-circuits idempotently (returns Success, enqueues nothing), so the entire opted-in base is not
/// double-pushed. Idempotency is keyed on the deterministic CampaignId (S7a/S7b -- claim-before-act on a
/// content-derived key, never a Guid), with the outbox unique (QueueName, MessageKey) index as the
/// concurrent backstop.
///
/// Test-first: RED until the handler looks the campaign up in the outbox before enqueuing.
/// </summary>
public class SendSitewidePromoHandlerTests
{
    private readonly Mock<IPendingDispatch> _pendingDispatch = new();
    private readonly Mock<IOutboxMessageRepository> _outboxRepository = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();

    private SendSitewidePromo.Handler CreateHandler()
    {
        _tenantProvider.Setup(t => t.GetCurrentTenantId()).Returns("TENANT-A");
        return new SendSitewidePromo.Handler(
            _pendingDispatch.Object,
            _outboxRepository.Object,
            _tenantProvider.Object);
    }

    private static SendSitewidePromo.Command ValidCommand() => new(
        TitleEn: "Spring sale", TitleCs: "Jaro", TitleSk: "Jar", TitleUk: "Vesna", TitleRu: "Vesna",
        BodyEn: "20% off", BodyCs: "Sleva", BodySk: "Zlava", BodyUk: "Znyzhka", BodyRu: "Skidka");

    [Fact]
    public async Task First_Submit_Enqueues_The_Fanout_Message()
    {
        _outboxRepository
            .Setup(r => r.GetByQueueAndKeyAsync(QueueNames.SitewidePromoFanout, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutboxMessage?)null);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _pendingDispatch.Verify(
            p => p.Enqueue(
                QueueNames.SitewidePromoFanout,
                It.IsAny<QueueEnvelope<SendSitewidePromoMessage>>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task Double_Submit_Of_The_Same_Campaign_Enqueues_Nothing()
    {
        var existing = OutboxMessage.Create(
            QueueNames.SitewidePromoFanout, "promo:TENANT-A:somehash", "{}", "TENANT-A");
        _outboxRepository
            .Setup(r => r.GetByQueueAndKeyAsync(QueueNames.SitewidePromoFanout, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _pendingDispatch.Verify(
            p => p.Enqueue(
                It.IsAny<string>(),
                It.IsAny<QueueEnvelope<SendSitewidePromoMessage>>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task The_Stamped_Message_Carries_The_Deterministic_CampaignId()
    {
        _outboxRepository
            .Setup(r => r.GetByQueueAndKeyAsync(QueueNames.SitewidePromoFanout, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutboxMessage?)null);

        SendSitewidePromoMessage? captured = null;
        _pendingDispatch
            .Setup(p => p.Enqueue(
                QueueNames.SitewidePromoFanout,
                It.IsAny<QueueEnvelope<SendSitewidePromoMessage>>(),
                It.IsAny<string>()))
            .Callback<string, QueueEnvelope<SendSitewidePromoMessage>, string>(
                (_, envelope, _2) => captured = envelope.Payload);

        await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.NotNull(captured);
        var command = ValidCommand();
        var expected = SendSitewidePromoMessage.DeriveCampaignId(
            "TENANT-A",
            new Dictionary<string, string>
            {
                ["en"] = command.TitleEn, ["cs"] = command.TitleCs, ["sk"] = command.TitleSk,
                ["uk"] = command.TitleUk, ["ru"] = command.TitleRu,
            },
            new Dictionary<string, string>
            {
                ["en"] = command.BodyEn, ["cs"] = command.BodyCs, ["sk"] = command.BodySk,
                ["uk"] = command.BodyUk, ["ru"] = command.BodyRu,
            });
        Assert.Equal(expected, captured!.CampaignId);
    }
}
