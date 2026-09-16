using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The <c>company-archive</c> consumer's classification (ADR-0064 D3): the envelope's tenant becomes
/// the override and the build runs for it with the freeze instant the message names; a body naming
/// no company is permanent and acked; what the build discards is acked; a transient failure throws
/// so the runtime redelivers into the same folder and, past <c>maxDequeueCount</c>, the poison twin
/// dead-letters the body while the company stays frozen and un-archived.
/// </summary>
public sealed class CompanyArchiveHandlerTests
{
    private const string TenantId = "cleansia-sk";
    private static readonly DateTimeOffset RequestedOn = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
    private const string Envelope =
        "{\"messageKey\":\"archive:cleansia-sk:20260916100000\",\"tenantId\":\"cleansia-sk\",\"payload\":{\"tenantId\":\"cleansia-sk\",\"requestedOn\":\"2026-09-16T10:00:00+00:00\"}}";

    private readonly Mock<ICompanyArchiveService> _service = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IDeadLetterStore> _deadLetters = new();

    private CompanyArchiveHandler Handler() =>
        new(_service.Object, _tenantProvider.Object, NullLogger<CompanyArchiveHandler>.Instance);

    [Fact]
    public async Task The_Envelopes_Tenant_Is_Set_As_The_Override_And_The_Build_Runs_For_The_Named_Request()
    {
        _service.Setup(s => s.RunAsync(TenantId, RequestedOn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyArchiveRunSummary(true, null, "cleansia-sk/20260916T100000Z", 22, new string('a', 64)));

        await Handler().HandleAsync(Envelope, CancellationToken.None);

        _tenantProvider.Verify(p => p.SetTenantOverride(TenantId), Times.Once);
        _service.Verify(s => s.RunAsync(TenantId, RequestedOn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("{\"messageKey\":\"archive::x\",\"tenantId\":null,\"payload\":{\"tenantId\":\"\",\"requestedOn\":\"2026-09-16T10:00:00+00:00\"}}")]
    [InlineData("not json at all")]
    public async Task A_Body_Naming_No_Company_Is_Discarded_As_Permanent(string body)
    {
        await Handler().HandleAsync(body, CancellationToken.None);

        _service.Verify(s => s.RunAsync(It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _tenantProvider.Verify(p => p.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task What_The_Build_Discards_Is_Acked_Not_Redelivered()
    {
        _service.Setup(s => s.RunAsync(TenantId, RequestedOn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyArchiveRunSummary.Skipped("the company is already archived"));

        await Handler().HandleAsync(Envelope, CancellationToken.None);

        _service.Verify(s => s.RunAsync(TenantId, RequestedOn, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Transient_Failure_Throws_For_Redelivery()
    {
        _service.Setup(s => s.RunAsync(TenantId, RequestedOn, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("blob storage unreachable"));

        await Assert.ThrowsAsync<TimeoutException>(() => Handler().HandleAsync(Envelope, CancellationToken.None));
    }

    [Fact]
    public async Task The_Poison_Twin_Records_The_Body_Under_Its_Queue_And_Does_Not_Throw()
    {
        var poison = new CompanyArchivePoisonHandler(_deadLetters.Object, NullLogger<CompanyArchivePoisonHandler>.Instance);

        await poison.HandleAsync(Envelope, CancellationToken.None);

        _deadLetters.Verify(
            s => s.RecordAsync(QueueNames.CompanyArchive, Envelope, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
