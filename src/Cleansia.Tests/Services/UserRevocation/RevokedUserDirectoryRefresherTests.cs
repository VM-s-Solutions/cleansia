using Cleansia.Config.Services.DeviceRevocation;
using Cleansia.Config.Services.UserRevocation;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting.Common;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace Cleansia.Tests.Services.UserRevocation;

/// <summary>
/// TC-REVOKE-USER-4: the refresher fails open on the last snapshot, warns past 3x the interval, and
/// survives consecutive poll faults (nothing escapes into the host). Plus the perf pin: the
/// request-path check does zero repository I/O.
/// </summary>
public class RevokedUserDirectoryRefresherTests
{
    private static readonly DateTimeOffset Start = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Successful_poll_fills_the_snapshot()
    {
        var repo = new CountingRefreshTokenRepository
        {
            Result = [new UserPasswordReset("u1", Start.AddMinutes(-1))],
        };
        var (refresher, directory, _, _) = Build(repo, refreshSeconds: 30);

        await refresher.RefreshOnceAsync(CancellationToken.None);

        Assert.True(directory.IsRevoked("u1", Start.AddMinutes(-5)));
        Assert.Equal(Start, directory.LastPolledAt);
    }

    [Fact]
    public async Task Poll_fault_keeps_the_last_snapshot_serving()
    {
        var repo = new CountingRefreshTokenRepository
        {
            Result = [new UserPasswordReset("u1", Start.AddMinutes(-1))],
        };
        var (refresher, directory, time, _) = Build(repo, refreshSeconds: 30);

        await refresher.RefreshOnceAsync(CancellationToken.None);
        Assert.True(directory.IsRevoked("u1", Start.AddMinutes(-5)));

        repo.Throw = true;
        time.Advance(TimeSpan.FromSeconds(30));
        await refresher.RefreshOnceAsync(CancellationToken.None);

        // Still serving the good snapshot - fail-open, never fail-closed.
        Assert.True(directory.IsRevoked("u1", Start.AddMinutes(-5)));
    }

    [Fact]
    public async Task Staleness_past_three_intervals_escalates_to_a_warning()
    {
        var repo = new CountingRefreshTokenRepository { Result = [] };
        var (refresher, _, time, logger) = Build(repo, refreshSeconds: 30);

        await refresher.RefreshOnceAsync(CancellationToken.None);

        repo.Throw = true;

        // Within 3x interval: no warning.
        time.Advance(TimeSpan.FromSeconds(60));
        await refresher.RefreshOnceAsync(CancellationToken.None);
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Warning);

        // Past 3x interval (90s): warning fires.
        time.Advance(TimeSpan.FromSeconds(40));
        await refresher.RefreshOnceAsync(CancellationToken.None);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task Refresher_survives_consecutive_faults_and_keeps_attempting()
    {
        var repo = new CountingRefreshTokenRepository { Result = [], Throw = true };
        var (refresher, _, time, _) = Build(repo, refreshSeconds: 30);

        // Initial fill throws, then two more consecutive faulting ticks - none escapes.
        await refresher.RefreshOnceAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(30));
        await refresher.RefreshOnceAsync(CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(30));
        await refresher.RefreshOnceAsync(CancellationToken.None);

        // The loop kept calling the repository across every fault (the pump is un-killable).
        Assert.Equal(3, repo.CallCount);
    }

    [Fact]
    public void Request_path_check_never_touches_the_repository()
    {
        var repo = new CountingRefreshTokenRepository { Result = [] };
        var (_, directory, _, _) = Build(repo, refreshSeconds: 30);
        directory.Replace([new RevokedUserEntry("u1", Start)], Start);

        for (var i = 0; i < 1000; i++)
        {
            directory.IsRevoked("u1", Start.AddSeconds(-1));
            directory.IsRevoked("u9", Start.AddSeconds(-1));
        }

        Assert.Equal(0, repo.CallCount);
    }

    private static (RevokedUserDirectoryRefresher Refresher, RevokedUserDirectory Directory, MutableTimeProvider Time, CapturingLogger Logger)
        Build(CountingRefreshTokenRepository repo, int refreshSeconds)
    {
        var directory = new RevokedUserDirectory();
        var time = new MutableTimeProvider(Start);
        var logger = new CapturingLogger();
        var jwt = new StubJwtSettings { AccessTokenExpMinutes = 30 };
        var options = Options.Create(new DeviceRevocationOptions { Enabled = true, RefreshSeconds = refreshSeconds });
        var scopeFactory = new SingleRepoScopeFactory(repo);

        var refresher = new RevokedUserDirectoryRefresher(directory, scopeFactory, jwt, options, time, logger);
        return (refresher, directory, time, logger);
    }

    private sealed class CountingRefreshTokenRepository : IRefreshTokenRepository
    {
        public IReadOnlyList<UserPasswordReset> Result { get; set; } = [];
        public bool Throw { get; set; }
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<UserPasswordReset>> GetPasswordResetsSinceAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
        {
            CallCount++;
            if (Throw) throw new InvalidOperationException("simulated poll fault");
            return Task.FromResult(Result);
        }

        public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RefreshToken?> GetByIdIgnoringTenantAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> RevokeChainAsync(string rootTokenId, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> BulkRevokeIgnoringConcurrencyAsync(RefreshTokenRevocationScope scope, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void DetachModifiedTracked() => throw new NotSupportedException();
        public Task<int> DeleteStaleAsync(DateTimeOffset olderThan, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> ExistWithIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RefreshToken?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetByIds(IEnumerable<string> ids) => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetPaged(int offset, int limit) => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetPaged(int offset, int limit, Expression<Func<RefreshToken, bool>> filter) => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetPagedSort<TSort>(int offset, int limit, Expression<Func<RefreshToken, bool>> filter, SortDefinition sort) where TSort : BaseSort<RefreshToken> => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetPagedSort<TSort>(int offset, int limit, Expression<Func<RefreshToken, bool>>? filter, IEnumerable<SortDefinition> sortDefinitions) where TSort : BaseSort<RefreshToken> => throw new NotSupportedException();
        public Task<int> GetCountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetCountAsync(Expression<Func<RefreshToken, bool>>? filter, CancellationToken cancellationToken) => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetFiltered(Expression<Func<RefreshToken, bool>> filter) => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetAll() => throw new NotSupportedException();
        public void Add(RefreshToken entity) => throw new NotSupportedException();
        public void AddRange(IEnumerable<RefreshToken> entities) => throw new NotSupportedException();
        public void Deactivate(RefreshToken entity) => throw new NotSupportedException();
        public void DeactivateRange(IEnumerable<RefreshToken> entities) => throw new NotSupportedException();
        public void Remove(RefreshToken entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<RefreshToken> entities) => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetQueryable() => throw new NotSupportedException();
        public IQueryable<RefreshToken> GetQueryableIgnoringTenant() => throw new NotSupportedException();
        public Task CommitAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Rollback() => throw new NotSupportedException();
        public void Dispose() { }
    }

    private sealed class SingleRepoScopeFactory(IRefreshTokenRepository repo) : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public object? GetService(Type serviceType) => serviceType == typeof(IRefreshTokenRepository) ? repo : null;
        public void Dispose() { }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class StubJwtSettings : IJwtSettings
    {
        public string Secret { get; set; } = "secret";
        public double AccessTokenExpMinutes { get; set; }
        public double RefreshTokenExpDays { get; set; }
        public double RefreshTokenShortExpDays { get; set; }
        public string Issuer => "cleansia";
    }

    private sealed class CapturingLogger : ILogger<RevokedUserDirectoryRefresher>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
