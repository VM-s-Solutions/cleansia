using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Owner ruling 2026-09-15, end to end on both partner hosts with their real <c>IHostAudienceProvider</c>
/// and a Google verifier that vouches for the test identities: <c>POST api/Auth/GoogleAuth</c> signs in
/// an existing Employee, refuses a Customer account with the key <c>PartnerLogin</c> uses, and refuses a
/// new identity with <c>auth.social_account_not_found</c> without writing a user row — whatever the terms
/// tick says. The partner <c>Register</c> route itself is gone (<c>SessionAuditRouteTests</c>).
/// </summary>
public sealed class PartnerHostGoogleSignInRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string EmployeeEmail = "google-cleaner@hosttests.local";
    private const string CustomerEmail = "google-customer@hosttests.local";
    private const string NewEmail = "google-newcomer@hosttests.local";

    private const string EmployeeToken = "employee-token";
    private const string CustomerToken = "customer-token";
    private const string NewcomerToken = "newcomer-token";

    private static readonly StubGoogleTokenVerifier Verifier = new(new Dictionary<string, GoogleVerifiedClaims>
    {
        [EmployeeToken] = new("sub-cleaner", EmployeeEmail, EmailVerified: true),
        [CustomerToken] = new("sub-customer", CustomerEmail, EmailVerified: true),
        [NewcomerToken] = new("sub-newcomer", NewEmail, EmailVerified: true),
    });

    protected override void ConfigurePartnerHostServices(IServiceCollection services) => UseStubVerifier(services);

    protected override void ConfigureMobileHostServices(IServiceCollection services) => UseStubVerifier(services);

    private static void UseStubVerifier(IServiceCollection services)
    {
        services.RemoveAll<IGoogleTokenVerifier>();
        services.AddSingleton<IGoogleTokenVerifier>(Verifier);
    }

    private HttpClient AnonymousClientFor(string hostAudience) =>
        hostAudience == MobileAudience ? MobileClientAnonymous() : PartnerClientAnonymous();

    private async Task SeedGoogleAccountsAsync()
    {
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            ctx.Users.Add(User.CreateWithGoogle(EmployeeEmail, "Google", "Cleaner", "sub-cleaner").UpgradeToEmployee());
            ctx.Users.Add(User.CreateWithGoogle(CustomerEmail, "Google", "Customer", "sub-customer"));
        });
    }

    private static object GoogleSignIn(string token, string email) => new
    {
        token,
        googleId = "client-supplied-and-ignored",
        email,
        firstName = "Any",
        lastName = "Name",
        termsAccepted = true,
    };

    private Task<int> AccountsWithAsync(string email) =>
        QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().CountAsync(u => u.Email == email));

    private static async Task AssertRefusedWithAsync(HttpResponseMessage response, string expectedKey)
    {
        // The handler's own verdict is a 401 carrying the key in the errors bag — the slot every client
        // resolves its translation from (SocialAuthLegacyBodyBindingTests pins the same shape).
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(expectedKey, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PartnerAudience)]
    [InlineData(MobileAudience)]
    public async Task An_Existing_Employee_Signs_In_With_Google_On_A_Partner_Host(string hostAudience)
    {
        await SeedGoogleAccountsAsync();

        var response = await AnonymousClientFor(hostAudience)
            .PostAsJsonAsync("/api/Auth/GoogleAuth", GoogleSignIn(EmployeeToken, EmployeeEmail));

        // The web partner host moves the JWT into an HttpOnly cookie and blanks it in the body, so the
        // session's proof here is the confirmed flag and the role the mint stamped, present on both hosts.
        HttpAssert.IsOk(response);
        var session = await response.Content.ReadFromJsonAsync<SessionBody>();
        Assert.True(session!.IsEmailConfirmed);
        Assert.Equal("Employee", session.Role);
    }

    [Theory]
    [InlineData(PartnerAudience)]
    [InlineData(MobileAudience)]
    public async Task A_Customer_Account_Is_Refused_On_A_Partner_Host_As_PartnerLogin_Refuses_It(string hostAudience)
    {
        await SeedGoogleAccountsAsync();

        var response = await AnonymousClientFor(hostAudience)
            .PostAsJsonAsync("/api/Auth/GoogleAuth", GoogleSignIn(CustomerToken, CustomerEmail));

        await AssertRefusedWithAsync(response, BusinessErrorMessage.InsufficientPrivileges);
    }

    [Theory]
    [InlineData(PartnerAudience)]
    [InlineData(MobileAudience)]
    public async Task A_New_Google_Identity_Is_Refused_On_A_Partner_Host_And_No_Account_Is_Created(string hostAudience)
    {
        await SeedGoogleAccountsAsync();

        var response = await AnonymousClientFor(hostAudience)
            .PostAsJsonAsync("/api/Auth/GoogleAuth", GoogleSignIn(NewcomerToken, NewEmail));

        await AssertRefusedWithAsync(response, BusinessErrorMessage.SocialAccountNotFound);
        Assert.Equal(0, await AccountsWithAsync(NewEmail));
    }

    private sealed record SessionBody(bool IsEmailConfirmed, string? Role);

    private sealed class StubGoogleTokenVerifier(IReadOnlyDictionary<string, GoogleVerifiedClaims> claimsByToken) : IGoogleTokenVerifier
    {
        public Task<GoogleVerifiedClaims?> VerifyAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(claimsByToken.GetValueOrDefault(token));
    }
}
