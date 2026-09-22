using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The <c>company-wind-down</c> consumer's classification (ADR-0064 D2): the envelope's
/// tenant becomes the override and the sweep runs for it; a body naming no company is permanent and
/// acked; what the sweep discards is acked; a transient failure throws so the runtime redelivers and,
/// past <c>maxDequeueCount</c>, the poison twin dead-letters the body.
/// </summary>
public sealed class CompanyWindDownHandlerTests
{
    private const string TenantId = "cleansia-sk";
    private const string Envelope =
        "{\"messageKey\":\"wind-down:cleansia-sk:20260916100000\",\"tenantId\":\"cleansia-sk\",\"payload\":{\"tenantId\":\"cleansia-sk\"}}";

    private readonly Mock<ICompanyWindDownService> _service = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly Mock<IDeadLetterStore> _deadLetters = new();

    private CompanyWindDownHandler Handler() =>
        new(_service.Object, _tenantProvider.Object, NullLogger<CompanyWindDownHandler>.Instance);

    [Fact]
    public async Task The_Envelopes_Tenant_Is_Set_As_The_Override_And_The_Sweep_Runs_For_It()
    {
        _service.Setup(s => s.RunAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CompanyWindDownRunSummary(true, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, null));

        await Handler().HandleAsync(Envelope, CancellationToken.None);

        _tenantProvider.Verify(p => p.SetTenantOverride(TenantId), Times.Once);
        _service.Verify(s => s.RunAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("{\"messageKey\":\"wind-down::x\",\"tenantId\":null,\"payload\":{\"tenantId\":\"\"}}")]
    [InlineData("not json at all")]
    public async Task A_Body_Naming_No_Company_Is_Discarded_As_Permanent(string body)
    {
        await Handler().HandleAsync(body, CancellationToken.None);

        _service.Verify(s => s.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _tenantProvider.Verify(p => p.SetTenantOverride(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task What_The_Sweep_Discards_Is_Acked_Not_Redelivered()
    {
        _service.Setup(s => s.RunAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CompanyWindDownRunSummary.Skipped("the company is frozen for archive"));

        await Handler().HandleAsync(Envelope, CancellationToken.None);

        _service.Verify(s => s.RunAsync(TenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Transient_Failure_Throws_For_Redelivery()
    {
        _service.Setup(s => s.RunAsync(TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("database unreachable"));

        await Assert.ThrowsAsync<TimeoutException>(() => Handler().HandleAsync(Envelope, CancellationToken.None));
    }

    [Fact]
    public async Task The_Poison_Twin_Records_The_Body_Under_Its_Queue_And_Does_Not_Throw()
    {
        var poison = new CompanyWindDownPoisonHandler(_deadLetters.Object, NullLogger<CompanyWindDownPoisonHandler>.Instance);

        await poison.HandleAsync(Envelope, CancellationToken.None);

        _deadLetters.Verify(
            s => s.RecordAsync(QueueNames.CompanyWindDown, Envelope, It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
