using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.IntegrationTests.Features.Legal;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Constants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// ADR-0062 D4 through the real pipeline on real Postgres, as an anonymous caller: an e-mail
/// registration that asserts the tick leaves two versioned consent rows with the request's IP and
/// device, and one <c>customer.account.register</c> row keyed on the new user; one that asserts nothing
/// leaves no consent row, records "not asserted", and is not refused. A social registration leaves the
/// versioned consent rows and NO audit row — the command is marked as the sign-in and its handler
/// declines the row on the provisioning branch — and a social sign-in of an existing account leaves one
/// <c>customer.session.login</c> row naming the provider, and no consent.
/// </summary>
[Collection("PostgresCollection")]
public class RegistrationConsentAndAuditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Ip = "203.0.113.9";
    private const string DeviceLabel = "Chrome/Windows";
    private const string DeviceId = "device-abc-123";

    /// <summary>No claim at all: the anonymous path as the hosts run it (the ambient tenant override stays).</summary>
    private static Task AnonymousWithRequestContext(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            new TestClaimsPrincipalUser(new ClaimsPrincipal(new ClaimsIdentity())))));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(Ip, DeviceLabel, DeviceId)));
        services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => new Mock<IEmailService>().Object));
        return Task.CompletedTask;
    }

    private static async Task SeedLanguageAsync(Infra.Database.CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));
        await context.SaveChangesAsync();
        await LegalSeed.SeedAsync(context);
    }

    private static Register.Command RegisterCommand(bool? termsAccepted) =>
        new(
            Email: Constants.TestUserSession.TestUserEmail,
            Password: Constants.TestUserSession.TestUserPassword,
            FirstName: Constants.TestUserSession.TestFirstName,
            LastName: Constants.TestUserSession.TestLastName,
            Language: "en",
            TermsAccepted: termsAccepted);

    private static async Task<List<UserConsent>> ConsentsOf(Infra.Database.CleansiaDbContext context, string userId) =>
        await context.UserConsents.IgnoreQueryFilters().Where(c => c.UserId == userId).OrderBy(c => c.ConsentType).ToListAsync();

    private static async Task<List<CustomerActionAudit>> CustomerRows(Infra.Database.CleansiaDbContext context) =>
        await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();

    private static async Task AssertVersionedLegalConsents(Infra.Database.CleansiaDbContext context, List<UserConsent> consents)
    {
        Assert.Equal(2, consents.Count);
        var terms = Assert.Single(consents, c => c.ConsentType == ConsentType.TermsOfService);
        var privacy = Assert.Single(consents, c => c.ConsentType == ConsentType.PrivacyPolicy);
        foreach (var consent in new[] { terms, privacy })
        {
            Assert.True(consent.IsGranted);
            Assert.NotNull(consent.GrantedAt);
            Assert.Equal(Ip, consent.IpAddress);
            Assert.Equal(DeviceLabel, consent.UserAgent);
            Assert.Equal(TestTenants.Default, consent.TenantId);
        }

        var termsDocument = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService);
        var privacyDocument = await LegalSeed.PlatformWideAsync(context, LegalDocumentType.PrivacyPolicy);
        Assert.Equal(termsDocument.Version, terms.DocumentVersion);
        Assert.Equal(termsDocument.Id, terms.LegalDocumentId);
        Assert.Equal(privacyDocument.Version, privacy.DocumentVersion);
        Assert.Equal(privacyDocument.Id, privacy.LegalDocumentId);
    }

    [Fact]
    public async Task An_Email_Registration_With_The_Tick_Writes_Two_Versioned_Consents_And_One_Register_Row_On_The_New_User()
    {
        await TestMethod(
            setup: AnonymousWithRequestContext,
            arrange: SeedLanguageAsync,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(RegisterCommand(termsAccepted: true)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == Constants.TestUserSession.TestUserEmail);

                await AssertVersionedLegalConsents(context, await ConsentsOf(context, user.Id));

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.account.register", row.Action);
                Assert.True(row.Success);
                Assert.Equal(user.Id, row.UserId);
                Assert.Equal("User", row.ResourceType);
                Assert.Equal(user.Id, row.ResourceId);
                Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(DeviceLabel, row.DeviceLabel);
                Assert.Equal(TestTenants.Default, row.TenantId);

                var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
                Assert.True(payload.GetProperty("termsAccepted").GetBoolean());
                Assert.Equal((await LegalSeed.PlatformWideAsync(context, LegalDocumentType.TermsOfService)).Version, payload.GetProperty("termsVersion").GetString());
                Assert.Equal((await LegalSeed.PlatformWideAsync(context, LegalDocumentType.PrivacyPolicy)).Version, payload.GetProperty("privacyVersion").GetString());
                Assert.Equal("Email", payload.GetProperty("method").GetString());
                var members = payload.EnumerateObject().Select(p => p.Name).ToList();
                Assert.DoesNotContain(members, m => m.Contains("name", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(members, m => m.Contains("email", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(Constants.TestUserSession.TestUserEmail, row.PayloadJson);
                Assert.DoesNotContain(Constants.TestUserSession.TestFirstName, row.PayloadJson);

                Assert.Equal(0, await context.AdminActionAudits.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Email_Registration_From_A_Client_That_Sends_No_Tick_Writes_No_Consent_Records_Not_Asserted_And_Is_Not_Refused()
    {
        await TestMethod(
            setup: AnonymousWithRequestContext,
            arrange: SeedLanguageAsync,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(RegisterCommand(termsAccepted: null)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == Constants.TestUserSession.TestUserEmail);

                Assert.Empty(await ConsentsOf(context, user.Id));

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.account.register", row.Action);
                Assert.True(row.Success);
                Assert.Equal(user.Id, row.UserId);
                var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
                Assert.Equal(JsonValueKind.Null, payload.GetProperty("termsAccepted").ValueKind);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Google_SignIn_Of_An_Existing_Account_Writes_One_Session_Row_Naming_The_Provider_And_No_Consent()
    {
        await TestMethod(
            setup: async services =>
            {
                await AnonymousWithRequestContext(services);
                services.Replace(ServiceDescriptor.Scoped<IGoogleTokenVerifier>(_ => GoogleVerifier("google-existing")));
            },
            arrange: async context =>
            {
                await SeedLanguageAsync(context);
                var existing = User.CreateWithGoogle(Constants.TestUserSession.TestUserEmail, "Exi", "Sting", "google-existing");
                existing.Created("seed", DateTime.UtcNow);
                context.Users.Add(existing);
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(GoogleCommand("google-existing", termsAccepted: false)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.NotEmpty(result.Value.Token);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.GoogleId == "google-existing");
                Assert.Empty(await ConsentsOf(context, user.Id));

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.session.login", row.Action);
                Assert.True(row.Success);
                Assert.Equal(user.Id, row.UserId);
                Assert.Equal(user.Id, row.ResourceId);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(LoginEvidence.GoogleMethod, JsonDocument.Parse(row.PayloadJson!).RootElement.GetProperty("method").GetString());
                Assert.DoesNotContain(Constants.TestUserSession.TestUserEmail, row.PayloadJson);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_New_Google_Identity_With_The_Tick_Writes_Two_Versioned_Consents_And_Still_No_Audit_Row()
    {
        await TestMethod(
            setup: async services =>
            {
                await AnonymousWithRequestContext(services);
                services.Replace(ServiceDescriptor.Scoped<IGoogleTokenVerifier>(_ => GoogleVerifier("google-brand-new")));
            },
            arrange: SeedLanguageAsync,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(GoogleCommand("google-brand-new", termsAccepted: true)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.GoogleId == "google-brand-new");
                Assert.Equal(AuthenticationType.Google, user.AuthenticationType);
                await AssertVersionedLegalConsents(context, await ConsentsOf(context, user.Id));
                Assert.Empty(await CustomerRows(context));
            },
            transactional: false);
    }

    [Fact]
    public async Task A_New_Apple_Identity_With_The_Tick_Writes_Two_Versioned_Consents_And_No_Audit_Row()
    {
        await TestMethod(
            setup: async services =>
            {
                await AnonymousWithRequestContext(services);
                var verifier = new Mock<IAppleTokenVerifier>();
                verifier
                    .Setup(v => v.VerifyAsync("apple-token", "raw-nonce", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AppleVerifiedClaims("apple-sub-new", Constants.TestUserSession.TestUserEmail, EmailVerified: true));
                services.Replace(ServiceDescriptor.Scoped<IAppleTokenVerifier>(_ => verifier.Object));
            },
            arrange: SeedLanguageAsync,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(
                new AppleAuth.Command("apple-token", "raw-nonce", "Ap", "Ple", TermsAccepted: true)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.AppleId == "apple-sub-new");
                await AssertVersionedLegalConsents(context, await ConsentsOf(context, user.Id));
                Assert.Empty(await CustomerRows(context));
            },
            transactional: false);
    }

    private static IGoogleTokenVerifier GoogleVerifier(string subject)
    {
        var verifier = new Mock<IGoogleTokenVerifier>();
        verifier
            .Setup(v => v.VerifyAsync("google-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GoogleVerifiedClaims(subject, Constants.TestUserSession.TestUserEmail, EmailVerified: true));
        return verifier.Object;
    }

    private static GoogleAuth.Command GoogleCommand(string subject, bool termsAccepted) =>
        new("google-token", subject, Constants.TestUserSession.TestUserEmail, "Goo", "Gle", TermsAccepted: termsAccepted);
}
