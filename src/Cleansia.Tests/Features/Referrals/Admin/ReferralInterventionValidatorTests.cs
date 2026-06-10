using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Referrals.Admin;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Referrals.Admin;

/// <summary>
/// The reverse / force-qualify commands require an existing referral id and a
/// non-empty bounded reason (mirrors the manual grant/revoke Reason contract).
/// Written TEST-FIRST.
/// </summary>
public class ReferralInterventionValidatorTests
{
    private const string ReferralId = "ref-1";

    private static Mock<IReferralRepository> RepoWithExisting()
    {
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.ExistsAsync(ReferralId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return repo;
    }

    [Fact]
    public async Task Reverse_MissingReferral_Fails_NotFound()
    {
        var repo = new Mock<IReferralRepository>();
        repo.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var validator = new ReverseReferral.Validator(repo.Object);

        var result = await validator.ValidateAsync(new ReverseReferral.Command("missing", "reason"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ReverseReferral.Command.ReferralId)
            && e.ErrorMessage == BusinessErrorMessage.ReferralNotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Reverse_MissingReason_Fails_ReasonRequired(string? reason)
    {
        var validator = new ReverseReferral.Validator(RepoWithExisting().Object);

        var result = await validator.ValidateAsync(new ReverseReferral.Command(ReferralId, reason!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ReverseReferral.Command.Reason)
            && e.ErrorMessage == BusinessErrorMessage.ReferralReasonRequired);
    }

    [Fact]
    public async Task Reverse_Valid_Passes()
    {
        var validator = new ReverseReferral.Validator(RepoWithExisting().Object);

        var result = await validator.ValidateAsync(new ReverseReferral.Command(ReferralId, "self-referral ring"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ForceQualify_MissingReason_Fails_ReasonRequired(string? reason)
    {
        var validator = new ForceQualifyReferral.Validator(RepoWithExisting().Object);

        var result = await validator.ValidateAsync(new ForceQualifyReferral.Command(ReferralId, reason!));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ForceQualifyReferral.Command.Reason)
            && e.ErrorMessage == BusinessErrorMessage.ReferralReasonRequired);
    }

    [Fact]
    public async Task ForceQualify_Valid_Passes()
    {
        var validator = new ForceQualifyReferral.Validator(RepoWithExisting().Object);

        var result = await validator.ValidateAsync(new ForceQualifyReferral.Command(ReferralId, "legit qualifying order"));

        Assert.True(result.IsValid);
    }
}
