using Cleansia.Core.AppServices.Features.Marketing;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Moq;

namespace Cleansia.Tests.Features.Marketing;

/// <summary>
/// LG-13 (B1) — the admin send action now returns a typed <see cref="SendSitewidePromo.Response"/>
/// instead of a bare <c>ICommand</c>. The enqueued fan-out behavior is unchanged; this pins that the
/// success carries the response carrying whether a new fan-out was enqueued.
/// </summary>
public class SendSitewidePromoResponseShapeTests
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
    public async Task First_Submit_Returns_Enqueued_Response()
    {
        _outboxRepository
            .Setup(r => r.GetByQueueAndKeyAsync(QueueNames.SitewidePromoFanout, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutboxMessage?)null);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Enqueued);
    }

    [Fact]
    public async Task Double_Submit_Returns_NotEnqueued_Response()
    {
        var existing = OutboxMessage.Create(
            QueueNames.SitewidePromoFanout, "promo:TENANT-A:somehash", "{}", "TENANT-A");
        _outboxRepository
            .Setup(r => r.GetByQueueAndKeyAsync(QueueNames.SitewidePromoFanout, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Enqueued);
    }
}
