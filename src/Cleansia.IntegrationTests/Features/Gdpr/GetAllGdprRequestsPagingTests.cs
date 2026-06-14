using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// PERF-IDA-06 latent-correctness fix: the admin GDPR-request list used to apply OrderBy AFTER
/// Skip/Take, returning the WRONG page window. This pins the corrected canonical paged shape —
/// order-before-page, newest-first by default, a real total — over a real Postgres so the
/// DateTimeOffset ordering is exercised (SQLite has no native DateTimeOffset).
/// </summary>
[Collection("PostgresCollection")]
public class GetAllGdprRequestsPagingTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const int TotalRows = 5;

    [Fact]
    public async Task FirstPage_ReturnsNewestFirst_NotAnArbitraryWindow()
    {
        await TestMethod<PagedData<GdprRequestDto>>(
            arrange: SeedRequestsWithDistinctCreatedOn,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new GetAllGdprRequests.Request { Offset = 0, Limit = 2 });
            },
            assert: (CleansiaDbContext _, PagedData<GdprRequestDto> page) =>
            {
                Assert.Equal(TotalRows, page.Total);
                var data = page.Data.ToList();
                Assert.Equal(2, data.Count);
                // Default order is CreatedOn descending — the two NEWEST rows (request-4, request-3).
                Assert.Equal("request-4", data[0].UserId);
                Assert.Equal("request-3", data[1].UserId);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task SecondPage_ContinuesTheSameOrdering_WindowIsCorrect()
    {
        await TestMethod<PagedData<GdprRequestDto>>(
            arrange: SeedRequestsWithDistinctCreatedOn,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new GetAllGdprRequests.Request { Offset = 2, Limit = 2 });
            },
            assert: (CleansiaDbContext _, PagedData<GdprRequestDto> page) =>
            {
                Assert.Equal(TotalRows, page.Total);
                var data = page.Data.ToList();
                Assert.Equal(2, data.Count);
                Assert.Equal("request-2", data[0].UserId);
                Assert.Equal("request-1", data[1].UserId);
                return Task.CompletedTask;
            });
    }

    private static async Task SeedRequestsWithDistinctCreatedOn(CleansiaDbContext context)
    {
        var baseTime = DateTimeOffset.UtcNow.AddDays(-10);

        // User.PreferredLanguageCode defaults to "en" and FKs to Languages — seed it once.
        context.Languages.Add(Language.Create("en", "English"));

        for (var i = 0; i < TotalRows; i++)
        {
            // GdprRequest.UserId is an FK to Users — seed the owning user first (distinct email per row,
            // id pinned so the request's UserId resolves) and audit-stamp it to satisfy CreatedBy NOT NULL.
            var user = User.CreateWithPassword(
                email: $"gdpr-seed-{i}@example.com",
                password: "Seed-Password-123",
                firstName: "Seed",
                lastName: $"User{i}");
            user.Id = $"request-{i}";
            user.ConfirmEmail();
            user.Created("seed", baseTime.AddHours(i));
            context.Users.Add(user);

            var request = GdprRequest.Create($"request-{i}", "Export");
            // Set an explicit creation audit so CommitAsync keeps the staggered CreatedOn instead of
            // stamping every row "now" (CreatedBy already set → no auto-stamp).
            request.Created("seed", baseTime.AddHours(i));
            context.GdprRequests.Add(request);
        }

        await context.CommitAsync(CancellationToken.None);
    }
}
