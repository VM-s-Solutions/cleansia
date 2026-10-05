using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.Domain.Credit;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Referrals.Admin;

/// <summary>
/// The admin reversal takes back, per side, the referral credit the ledger shows was granted under that
/// side's key — and on a balance that no longer holds it all, only what is left (owner default
/// 2026-10-04): a balance never goes negative, and the debit row records what was taken. A retry on the
/// already-Reversed row is a guarded no-op.
/// </summary>
public class ReverseReferralHandlerTests
{
    private const string ReferralId = "ref-1";
    private const string ReferrerUserId = "referrer-1";
    private const string ReferredUserId = "referred-1";
    private const string CzkId = "czk";
    private const string ActorId = "admin-1";
    private const string Reason = "self-referral ring remediation #88";

    private readonly Mock<IReferralRepository> _referralRepository = new();
    private readonly Mock<ICreditAccountRepository> _credit = new();
    private readonly Mock<ICurrencyRepository> _currencies = new();
    private readonly Mock<IUserSessionProvider> _userSession = new();

    public ReverseReferralHandlerTests()
    {
        _userSession.Setup(s => s.GetUserId()).Returns(ActorId);
        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = CzkId;
        _currencies.Setup(c => c.GetByIdAsync(CzkId, It.IsAny<CancellationToken>())).ReturnsAsync(czk);
        _credit.Setup(c => c.TryDebitAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CreditTransactionReason>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
    }

    private ReverseReferral.Handler CreateHandler() => new(
        _referralRepository.Object,
        _credit.Object,
        _currencies.Object,
        _userSession.Object,
        NullLogger<ReverseReferral.Handler>.Instance);

    private Referral QualifiedReferral(decimal? toReferrer = 150m, decimal? toReferred = 150m)
    {
        var referral = Referral.CreateAccepted(ReferrerUserId, ReferredUserId, "code-1", "system");
        referral.MarkQualified("order-1", CzkId, toReferrer, toReferred, "system");
        referral.Id = ReferralId;
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);
        return referral;
    }

    private void Granted(string side, decimal amount) =>
        _credit.Setup(c => c.GetAmountAsync(
                $"referral:{ReferralId}:{side}", CreditTransactionReason.Referral, It.IsAny<CancellationToken>()))
            .ReturnsAsync(amount);

    private void Holds(string userId, decimal balance) =>
        _credit.Setup(c => c.GetSpendableAsync(userId, CzkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditSpendable($"account-{userId}", balance, CzkId, null));

    private void VerifyTaken(string userId, string side, decimal amount) =>
        _credit.Verify(c => c.TryDebitAsync(
            $"account-{userId}", amount, CreditTransactionReason.ReferralReversed,
            $"referral-reverse:{ReferralId}:{side}", ActorId, It.IsAny<CancellationToken>(), null, Reason), Times.Once);

    [Fact]
    public async Task Both_Grants_Are_Taken_Back_In_Full_And_The_Referral_Is_Reversed()
    {
        var referral = QualifiedReferral();
        Granted("referrer", 150m);
        Granted("referred", 150m);
        Holds(ReferrerUserId, 400m);
        Holds(ReferredUserId, 150m);

        var result = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new ReverseReferral.Response(ReferralId, 150m, 150m, "CZK"), result.Value);
        Assert.Equal(ReferralStatus.Reversed, referral.Status);
        VerifyTaken(ReferrerUserId, "referrer", 150m);
        VerifyTaken(ReferredUserId, "referred", 150m);
        _credit.Verify(c => c.LockForUserAsync(ReferrerUserId, It.IsAny<CancellationToken>()), Times.Once);
        _credit.Verify(c => c.LockForUserAsync(ReferredUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_Balance_That_No_Longer_Holds_The_Grant_Gives_Up_Only_What_Is_Left()
    {
        QualifiedReferral();
        Granted("referrer", 150m);
        Granted("referred", 150m);
        Holds(ReferrerUserId, 150m);
        Holds(ReferredUserId, 40m);

        var result = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.Equal(new ReverseReferral.Response(ReferralId, 150m, 40m, "CZK"), result.Value);
        VerifyTaken(ReferredUserId, "referred", 40m);
    }

    [Fact]
    public async Task An_Empty_Balance_Gives_Up_Nothing()
    {
        QualifiedReferral();
        Granted("referrer", 150m);
        Granted("referred", 150m);
        Holds(ReferrerUserId, 150m);
        Holds(ReferredUserId, 0m);

        var result = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.Equal(new ReverseReferral.Response(ReferralId, 150m, 0m, "CZK"), result.Value);
        _credit.Verify(c => c.TryDebitAsync(
            "account-referred-1", It.IsAny<decimal>(), It.IsAny<CreditTransactionReason>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    /// <summary>
    /// The ledger, not the row's recorded figure, decides what a side was given: a side erased at
    /// qualification has no grant under its key, so nothing is taken from it.
    /// </summary>
    [Fact]
    public async Task A_Side_With_No_Grant_On_The_Ledger_Gives_Up_Nothing()
    {
        QualifiedReferral(toReferrer: null);
        Granted("referred", 150m);
        Holds(ReferredUserId, 150m);

        var result = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.Equal(new ReverseReferral.Response(ReferralId, 0m, 150m, "CZK"), result.Value);
        _credit.Verify(c => c.LockForUserAsync(ReferrerUserId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_Referral_That_Paid_No_Credit_Is_Reversed_Without_Touching_A_Balance()
    {
        var referral = QualifiedReferral(toReferrer: null, toReferred: null);

        var result = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.Equal(new ReverseReferral.Response(ReferralId, 0m, 0m, null), result.Value);
        Assert.Equal(ReferralStatus.Reversed, referral.Status);
        _credit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reverse_RunTwice_SecondIsGuardedNoOp_NoDoubleClawback()
    {
        var referral = QualifiedReferral();
        Granted("referrer", 150m);
        Granted("referred", 150m);
        Holds(ReferrerUserId, 150m);
        Holds(ReferredUserId, 150m);

        var first = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Equal(ReferralStatus.Reversed, referral.Status);

        var second = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(second.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReferralNotQualified, second.Error!.Message);
        Assert.Equal(nameof(ReverseReferral.Command.ReferralId), second.Error.Code);
        _credit.Verify(c => c.TryDebitAsync(
            It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CreditTransactionReason>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Reverse_NonQualifiedReferral_IsRejected()
    {
        var referral = Referral.CreateAccepted(ReferrerUserId, ReferredUserId, "code-1", "system");
        referral.Id = ReferralId;
        _referralRepository.Setup(r => r.GetByIdAsync(ReferralId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(referral);

        var result = await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BusinessErrorMessage.ReferralNotQualified, result.Error!.Message);
        Assert.Equal(nameof(ReverseReferral.Command.ReferralId), result.Error.Code);
        _credit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reverse_Handler_DoesNotCommit()
    {
        QualifiedReferral();

        await CreateHandler().Handle(new ReverseReferral.Command(ReferralId, Reason), CancellationToken.None);

        _referralRepository.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
