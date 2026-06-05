using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Loyalty.Admin;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Loyalty;

/// <summary>
/// T-0112 (LG-SEC-06) AC3 — the manual grant/revoke <c>RequestId</c> (the client-supplied idempotency
/// token, S7a) is a REQUIRED, bounded-length field, validated through the EXISTING FluentValidation
/// validators (which already cover UserId / Points / Reason). Asserts on <see cref="BusinessErrorMessage"/>
/// constants. Written TEST-FIRST (predates the validator rule). Covers BOTH commands (grant + revoke).
/// </summary>
public class GrantPointsManuallyValidatorTests
{
    private const string ValidUserId = "user-1";
    private const string ValidReason = "Goodwill credit for service issue";
    private const int ValidPoints = 100;

    // RequestId is bounded to the same 80-char ceiling as the persisted IdempotencyKey column.
    private const int MaxRequestIdLength = 80;

    private static GrantPointsManually.Validator GrantValidator()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.ExistsAsync(ValidUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new GrantPointsManually.Validator(userRepo.Object);
    }

    private static RevokePointsManually.Validator RevokeValidator()
    {
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.ExistsAsync(ValidUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return new RevokePointsManually.Validator(userRepo.Object);
    }

    // ── AC3 (grant) — missing RequestId ⇒ Required ──
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Grant_When_RequestId_Missing_Then_Fails_With_Required(string? requestId)
    {
        var command = new GrantPointsManually.Command(ValidUserId, ValidPoints, ValidReason, requestId!);

        var result = await GrantValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == nameof(GrantPointsManually.Command.RequestId)).ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
    }

    // ── AC3 (grant) — oversized RequestId ⇒ MaxLength ──
    [Fact]
    public async Task Grant_When_RequestId_Oversized_Then_Fails_With_MaxLength()
    {
        var oversized = new string('a', MaxRequestIdLength + 1);
        var command = new GrantPointsManually.Command(ValidUserId, ValidPoints, ValidReason, oversized);

        var result = await GrantValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == nameof(GrantPointsManually.Command.RequestId)).ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.MaxLength, errors[0].ErrorMessage);
    }

    // ── AC3 (grant) — valid RequestId ⇒ passes ──
    [Fact]
    public async Task Grant_When_RequestId_Valid_Then_Passes_For_RequestId()
    {
        var command = new GrantPointsManually.Command(ValidUserId, ValidPoints, ValidReason, "req-idem-abc");

        var result = await GrantValidator().ValidateAsync(command);

        Assert.Empty(result.Errors.Where(e => e.PropertyName == nameof(GrantPointsManually.Command.RequestId)));
    }

    // ── AC3 (revoke) — missing RequestId ⇒ Required ──
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Revoke_When_RequestId_Missing_Then_Fails_With_Required(string? requestId)
    {
        var command = new RevokePointsManually.Command(ValidUserId, ValidPoints, ValidReason, requestId!);

        var result = await RevokeValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == nameof(RevokePointsManually.Command.RequestId)).ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.Required, errors[0].ErrorMessage);
    }

    // ── AC3 (revoke) — oversized RequestId ⇒ MaxLength ──
    [Fact]
    public async Task Revoke_When_RequestId_Oversized_Then_Fails_With_MaxLength()
    {
        var oversized = new string('a', MaxRequestIdLength + 1);
        var command = new RevokePointsManually.Command(ValidUserId, ValidPoints, ValidReason, oversized);

        var result = await RevokeValidator().ValidateAsync(command);

        Assert.False(result.IsValid);
        var errors = result.Errors.Where(e => e.PropertyName == nameof(RevokePointsManually.Command.RequestId)).ToList();
        Assert.Single(errors);
        Assert.Equal(BusinessErrorMessage.MaxLength, errors[0].ErrorMessage);
    }

    // ── AC3 (revoke) — valid RequestId ⇒ passes ──
    [Fact]
    public async Task Revoke_When_RequestId_Valid_Then_Passes_For_RequestId()
    {
        var command = new RevokePointsManually.Command(ValidUserId, ValidPoints, ValidReason, "req-idem-abc");

        var result = await RevokeValidator().ValidateAsync(command);

        Assert.Empty(result.Errors.Where(e => e.PropertyName == nameof(RevokePointsManually.Command.RequestId)));
    }
}
