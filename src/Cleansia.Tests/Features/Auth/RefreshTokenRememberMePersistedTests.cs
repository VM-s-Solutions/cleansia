using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RefreshTokenEntity = Cleansia.Core.Domain.Users.RefreshToken;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// G-03 — rotation carries the session's <c>rememberMe</c> across by READING it, not by re-deriving it.
///
/// THE HOLE: rotation used to infer the flag by measuring <c>ExpiresAt - CreatedOn</c> against
/// <c>RefreshTokenShortExpDays + 0.5</c>. That is correct for every shipped configuration — 30 days
/// versus 1, and 90 versus 1 on Mobile.Customer — but it couples a security property to the GAP between
/// two settings that are independently tunable. Bring them within half a day of each other and every
/// rotation silently downgrades the session to short-lived, with nothing to notice: the failure is
/// fail-safe in direction (shorter, not longer) and completely silent in operation.
///
/// THE FIX: <c>RefreshToken.RememberMe</c> is persisted at issue and read at rotation. The arithmetic
/// survives only as the fallback for rows written before the column existed, and those self-heal the
/// first time they rotate, because the row rotation writes stores the flag.
/// </summary>
public sealed class RefreshTokenRememberMePersistedTests
{
    private const string UserId = "user-g03";
    private const string Audience = "cleansia.partner";

    private readonly Mock<IRefreshTokenRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IJwtSettings> _jwt = new();
    private readonly List<RefreshTokenEntity> _added = [];

    public RefreshTokenRememberMePersistedTests()
    {
        _jwt.SetupGet(s => s.RefreshTokenExpDays).Returns(30);
        _jwt.SetupGet(s => s.RefreshTokenShortExpDays).Returns(1);
        _repository.Setup(r => r.Add(It.IsAny<RefreshTokenEntity>()))
            .Callback<RefreshTokenEntity>(_added.Add);
    }

    private RefreshTokenService CreateService() =>
        new(_repository.Object,
            _unitOfWork.Object,
            _jwt.Object,
            NullLogger<RefreshTokenService>.Instance,
            TimeProvider.System);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Issue_persists_the_flag_it_was_asked_for(bool rememberMe)
    {
        CreateService().Issue(UserId, rememberMe, Audience);

        var record = Assert.Single(_added);
        Assert.Equal(rememberMe, record.RememberMe);
    }

    /// <summary>
    /// The regression this closes. With the two lifetimes configured a day apart — which the arithmetic
    /// cannot distinguish, since it tests <c>&gt; RefreshTokenShortExpDays + 0.5</c> — a long session
    /// still reports as long, because the answer is read rather than measured.
    /// </summary>
    [Fact]
    public void A_Long_Session_Survives_Lifetimes_Configured_Close_Together()
    {
        _jwt.SetupGet(s => s.RefreshTokenExpDays).Returns(2);
        _jwt.SetupGet(s => s.RefreshTokenShortExpDays).Returns(1.6);

        var issued = CreateService().Issue(UserId, rememberMe: true, Audience);

        // The old arithmetic would answer FALSE here: a 2-day lifetime is not > 1.6 + 0.5.
        var derived = (issued.Record.ExpiresAt - issued.Record.CreatedOn).TotalDays > 1.6 + 0.5;
        Assert.False(derived);

        // The stored flag is unaffected by how the two settings were configured relative to each other.
        Assert.True(issued.Record.RememberMe);
    }

    /// <summary>
    /// A row written before the column existed carries null, and must still resolve — via the old
    /// arithmetic — rather than defaulting a long session to short on its first rotation after deploy.
    /// </summary>
    [Fact]
    public void A_Pre_Existing_Row_Is_Distinguishable_From_One_Issued_As_Short()
    {
        var legacy = RefreshTokenEntity.Create(
            userId: UserId,
            tokenHash: "hash",
            expiresAt: DateTimeOffset.UtcNow.AddDays(30),
            audience: Audience,
            deviceLabel: null,
            ipAddress: null);

        Assert.Null(legacy.RememberMe);

        var issuedShort = CreateService().Issue(UserId, rememberMe: false, Audience);
        Assert.False(issuedShort.Record.RememberMe);
    }
}
