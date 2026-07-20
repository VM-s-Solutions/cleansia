using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.SeedWork;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Services;

public class RefreshTokenServiceIssueClockTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<IRefreshTokenRepository> _repository = new();
    private readonly Mock<IJwtSettings> _jwtSettings = new();
    private readonly StubTimeProvider _timeProvider = new(Now);

    public RefreshTokenServiceIssueClockTests()
    {
        _jwtSettings.SetupGet(s => s.RefreshTokenExpDays).Returns(30);
        _jwtSettings.SetupGet(s => s.RefreshTokenShortExpDays).Returns(1);
    }

    private RefreshTokenService CreateSut() => new(
        _repository.Object,
        Mock.Of<IUnitOfWork>(),
        _jwtSettings.Object,
        NullLogger<RefreshTokenService>.Instance,
        _timeProvider);

    [Fact]
    public void Issue_RememberMe_RowExpiryRidesTheInjectedClock()
    {
        var issued = CreateSut().Issue("user-1", rememberMe: true, JwtAudiences.Customer);

        Assert.Equal(Now.AddDays(30), issued.Record.ExpiresAt);
    }

    [Fact]
    public void Issue_ShortLived_RowExpiryRidesTheInjectedClock()
    {
        var issued = CreateSut().Issue("user-1", rememberMe: false, JwtAudiences.Customer);

        Assert.Equal(Now.AddDays(1), issued.Record.ExpiresAt);
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
