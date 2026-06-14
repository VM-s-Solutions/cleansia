using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// Unit tests for <see cref="OrderLateReferralAcceptor"/> — the late-referral-acceptance collaborator
/// extracted from <c>CreateOrder.Handler</c>. Covers every guard the collaborator owns (no code / no
/// user / already-referred skip the accept) and the best-effort never-block semantics (a rejected
/// accept and a thrown accept are both swallowed), so the extraction carries the same behavior the
/// handler characterization suite pins.
/// </summary>
public class OrderLateReferralAcceptorTests
{
    private const string UserId = "user-1";
    private const string ReferralCode = "FRIEND6";

    private readonly Mock<IReferralService> _referralService = new();
    private readonly Mock<IReferralRepository> _referralRepository = new();

    private OrderLateReferralAcceptor CreateAcceptor() =>
        new(_referralService.Object, _referralRepository.Object,
            NullLogger<OrderLateReferralAcceptor>.Instance);

    [Fact]
    public async Task NoReferralCode_DoesNotLookUpOrAccept()
    {
        await CreateAcceptor().AcceptIfPresentAsync(null, UserId, CancellationToken.None);

        _referralRepository.Verify(
            r => r.GetByReferredUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _referralService.Verify(
            s => s.AcceptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NoUser_DoesNotLookUpOrAccept()
    {
        await CreateAcceptor().AcceptIfPresentAsync(ReferralCode, string.Empty, CancellationToken.None);

        _referralRepository.Verify(
            r => r.GetByReferredUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _referralService.Verify(
            s => s.AcceptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AlreadyReferred_DoesNotAccept()
    {
        _referralRepository
            .Setup(r => r.GetByReferredUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Referral.CreateAccepted("referrer-1", UserId, "code-1", "actor-1"));

        await CreateAcceptor().AcceptIfPresentAsync(ReferralCode, UserId, CancellationToken.None);

        _referralService.Verify(
            s => s.AcceptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotYetReferred_CallsAccept()
    {
        _referralRepository
            .Setup(r => r.GetByReferredUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Referral?)null);
        _referralService
            .Setup(s => s.AcceptAsync(ReferralCode, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReferralAcceptResult(true, null));

        await CreateAcceptor().AcceptIfPresentAsync(ReferralCode, UserId, CancellationToken.None);

        _referralService.Verify(
            s => s.AcceptAsync(ReferralCode, UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AcceptRejected_DoesNotThrow()
    {
        _referralRepository
            .Setup(r => r.GetByReferredUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Referral?)null);
        _referralService
            .Setup(s => s.AcceptAsync(ReferralCode, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReferralAcceptResult(false, ReferralValidationError.NotFound));

        var ex = await Record.ExceptionAsync(() => CreateAcceptor().AcceptIfPresentAsync(
            ReferralCode, UserId, CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task AcceptThrows_IsSwallowed()
    {
        _referralRepository
            .Setup(r => r.GetByReferredUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Referral?)null);
        _referralService
            .Setup(s => s.AcceptAsync(ReferralCode, UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("referral store down"));

        var ex = await Record.ExceptionAsync(() => CreateAcceptor().AcceptIfPresentAsync(
            ReferralCode, UserId, CancellationToken.None));

        Assert.Null(ex);
    }
}
