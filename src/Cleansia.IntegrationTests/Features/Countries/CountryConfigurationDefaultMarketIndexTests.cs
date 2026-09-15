using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Infra.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Cleansia.IntegrationTests.Features.Countries;

/// <summary>
/// The partial unique index behind <c>CountryConfiguration.IsDefaultMarket</c> over real Postgres
/// (owner ruling 2026-09-13): at most one configuration carries the flag, and the filter is what lets
/// every unflagged configuration coexist beside it. The refusal names the index, so a renamed or
/// dropped index fails here rather than letting two default markets sit in the table.
/// </summary>
[Collection("PostgresCollection")]
public class CountryConfigurationDefaultMarketIndexTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CzeId = "country-cze-defaultmarket";
    private const string SvkId = "country-svk-defaultmarket";
    private const string PolId = "country-pol-defaultmarket";

    [Fact]
    public async Task The_Schema_Refuses_A_Second_Default_Market()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx),
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();

                ctx.CountryConfigurations.Add(CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m).SetAsDefaultMarket(true));
                await ctx.CommitAsync(CancellationToken.None);

                ctx.CountryConfigurations.Add(CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m).SetAsDefaultMarket(true));
                var secondDefault = await Assert.ThrowsAsync<DbUpdateException>(() => ctx.CommitAsync(CancellationToken.None));
                ctx.ChangeTracker.Clear();

                return (secondDefault.InnerException as PostgresException)?.ConstraintName;
            },
            assert: (CleansiaDbContext _, string? constraint) =>
            {
                Assert.Equal("IX_CountryConfigurations_IsDefaultMarket_Unique", constraint);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    /// <summary>
    /// The filter keeps this an index over ONE row rather than over the column: two unflagged
    /// configurations beside the default would collide with each other without it.
    /// </summary>
    [Fact]
    public async Task One_Default_Market_Beside_Two_Unflagged_Configurations_Is_Accepted()
    {
        await TestMethod(
            arrange: ctx => SeedAsync(ctx),
            act: async provider =>
            {
                var ctx = provider.GetRequiredService<CleansiaDbContext>();

                ctx.CountryConfigurations.Add(CountryConfiguration.Create(CzeId, "CZK", "cs", 0.21m).SetAsDefaultMarket(true));
                ctx.CountryConfigurations.Add(CountryConfiguration.Create(SvkId, "EUR", "sk", 0.20m));
                ctx.CountryConfigurations.Add(CountryConfiguration.Create(PolId, "PLN", "pl", 0.23m));
                await ctx.CommitAsync(CancellationToken.None);

                var rows = await ctx.CountryConfigurations
                    .OrderBy(c => c.CountryId)
                    .Select(c => new { c.CountryId, c.IsDefaultMarket })
                    .ToListAsync();
                return rows.Select(c => (c.CountryId, c.IsDefaultMarket)).ToList();
            },
            assert: (CleansiaDbContext _, List<(string CountryId, bool IsDefaultMarket)> rows) =>
            {
                Assert.Equal([(CzeId, true), (PolId, false), (SvkId, false)], rows);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedAsync(CleansiaDbContext ctx)
    {
        var cze = Country.Create("Czechia", "CZE", "CZ", isServiced: true);
        cze.Id = CzeId;
        var svk = Country.Create("Slovakia", "SVK", "SK", isServiced: true);
        svk.Id = SvkId;
        var pol = Country.Create("Poland", "POL", "PL", isServiced: true);
        pol.Id = PolId;
        ctx.Countries.AddRange(cze, svk, pol);

        await ctx.CommitAsync(CancellationToken.None);
    }
}
