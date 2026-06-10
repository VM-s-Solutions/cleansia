using Cleansia.Core.AppServices.Features.Referrals;
using Cleansia.Functions.Core.Handlers;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// AC4/AC5 — the timer Function shell's Core handler is reachable and drives the sweep through the
/// MediatR pipeline exactly once (so the UoW pipeline commits the domain transition). This is the
/// production caller that gives <c>ExpireStaleReferrals.Command</c> a non-test reference.
/// </summary>
public class ExpireStaleReferralsHandlerSmokeTests
{
    private readonly Mock<IMediator> _mediator = new();

    private ExpireStaleReferralsHandler CreateHandler() => new(
        _mediator.Object,
        NullLogger<ExpireStaleReferralsHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Is_Reachable_And_Drives_The_Sweep_Once()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<ExpireStaleReferrals.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BusinessResult.Success(new ExpireStaleReferrals.Response(ExpiredCount: 0)));

        var handler = CreateHandler();

        await handler.HandleAsync(CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<ExpireStaleReferrals.Command>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
