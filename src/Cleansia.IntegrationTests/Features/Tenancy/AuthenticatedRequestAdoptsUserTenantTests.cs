using System.IdentityModel.Tokens.Jwt;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0061 D4 over real Postgres: login and refresh are anonymous, and every RefreshToken row they add
/// is NOT NULL on TenantId — the request adopts the user's own operating company before the row is
/// added, and the JWT it mints says the same. One host is enough: all five call the same TokenService.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AuthenticatedRequestAdoptsUserTenantTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Email = "slovak-user@cleansia.test";

    [Fact]
    public async Task Login_Stamps_The_Refresh_Token_And_The_Jwt_With_The_Users_Company()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedSlovakUserAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new Login.Command(Email, TestUtilities.Constants.TestUserSession.TestUserPassword, RememberMe: true)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var claims = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token).Claims;
                Assert.Equal(TestTenants.Second, Assert.Single(claims, c => c.Type == "tenant_id").Value);

                var token = Assert.Single(await context.RefreshTokens.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(TestTenants.Second, token.TenantId);
            },
            transactional: false);
    }

    [Fact]
    public async Task Rotating_A_Refresh_Token_Anonymously_Stamps_The_New_Row_With_The_Users_Company()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedSlovakUserAsync,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var login = await mediator.Send(new Login.Command(Email, TestUtilities.Constants.TestUserSession.TestUserPassword, RememberMe: true));
                Assert.True(login.IsSuccess, login.Error?.Message);
                return await mediator.Send(new Cleansia.Core.AppServices.Features.Auth.RefreshToken.Command(login.Value.RefreshToken!));
            },
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                var tokens = await context.RefreshTokens.IgnoreQueryFilters().ToListAsync();
                Assert.Equal(2, tokens.Count);
                Assert.All(tokens, t => Assert.Equal(TestTenants.Second, t.TenantId));
                Assert.Equal(TestTenants.Second, Assert.Single(new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token).Claims, c => c.Type == "tenant_id").Value);
            },
            transactional: false);
    }

    private static async Task SeedSlovakUserAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        var user = User.CreateWithPassword(Email, TestUtilities.Constants.TestUserSession.TestUserPassword, "Slo", "Vak");
        user.ConfirmEmail();
        user.TenantId = TestTenants.Second;
        context.Users.Add(user);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Task Anonymous(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        return Task.CompletedTask;
    }
}
