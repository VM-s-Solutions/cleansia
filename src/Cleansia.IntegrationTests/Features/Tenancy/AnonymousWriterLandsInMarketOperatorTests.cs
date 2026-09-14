using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.PromoCodes;
using Cleansia.Core.AppServices.Features.Referrals;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace Cleansia.IntegrationTests.Features.Tenancy;

/// <summary>
/// ADR-0061 D3 through the real pipeline with NO claim and NO override: an anonymous request naming a
/// market lands its rows in that market's operating company; naming none lands in the default market's;
/// naming a market nobody operates is refused <c>tenant.not_found</c> and writes nothing; naming a
/// country that is not a market is refused <c>country.not_serviced</c>, never <c>tenant.not_found</c>.
/// The seeded map: CZE (default market) → cleansia-cz, SVK → cleansia-sk, POL → nobody, DEU → not
/// serviced.
/// </summary>
[Collection("PostgresCollection")]
public sealed class AnonymousWriterLandsInMarketOperatorTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Czechia = "country-cze-anon";
    private const string Slovakia = "country-svk-anon";
    private const string Poland = "country-pol-anon";
    private const string Germany = "country-deu-anon";
    private static readonly string Password = TestUtilities.Constants.TestUserSession.TestUserPassword;

    private static readonly JsonSerializerOptions WireJson = new() { PropertyNameCaseInsensitive = true };

    [Theory]
    [InlineData(Slovakia, TestTenants.Second)]
    [InlineData(null, TestTenants.Default)]
    public async Task Register_Lands_The_User_And_Cart_In_The_Named_Markets_Operator(string? countryId, string expectedTenant)
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedAndCommitAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new Register.Command("visitor@cleansia.test", Password, "Vi", "Sitor", "en", CountryId: countryId, TermsAccepted: true)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "visitor@cleansia.test");
                Assert.Equal(expectedTenant, user.TenantId);
                Assert.Equal(expectedTenant, (await context.Carts.IgnoreQueryFilters().SingleAsync(c => c.UserId == user.Id)).TenantId);
            },
            transactional: false);
    }

    [Fact]
    public async Task RegisterEmployee_Lands_The_User_Cart_And_Employee_In_The_Named_Markets_Operator()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedAndCommitAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new RegisterEmployee.Command("cleaner@cleansia.test", Password, "Clea", "Ner", "en", CountryId: Slovakia)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "cleaner@cleansia.test");
                Assert.Equal(TestTenants.Second, user.TenantId);
                Assert.Equal(TestTenants.Second, (await context.Carts.IgnoreQueryFilters().SingleAsync(c => c.UserId == user.Id)).TenantId);
                Assert.Equal(TestTenants.Second, (await context.Employees.IgnoreQueryFilters().SingleAsync(e => e.UserId == user.Id)).TenantId);
                Assert.Empty(await context.Users.IgnoreQueryFilters().Where(u => u.TenantId == TestTenants.Default).ToListAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task GoogleAuth_Provisions_The_Account_In_The_Named_Markets_Operator()
    {
        await TestMethod(
            setup: services =>
            {
                Anonymous(services);
                var verifier = new Mock<IGoogleTokenVerifier>();
                verifier.Setup(v => v.VerifyAsync("google-token", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GoogleVerifiedClaims("google-sub-1", "google@cleansia.test", EmailVerified: true));
                services.Replace(ServiceDescriptor.Scoped<IGoogleTokenVerifier>(_ => verifier.Object));
                return Task.CompletedTask;
            },
            arrange: SeedAndCommitAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GoogleAuth.Command("google-token", "ignored", "ignored@x", "Goo", "Gle", TermsAccepted: true, CountryId: Slovakia)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "google@cleansia.test");
                Assert.Equal(TestTenants.Second, user.TenantId);
                Assert.Equal(TestTenants.Second, (await context.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.UserId == user.Id)).TenantId);
            },
            transactional: false);
    }

    /// <summary>
    /// ADR-0061 D4: an EXISTING account of another company signing in with no market keeps its own
    /// operator — TokenService re-scopes to the user before the RefreshToken row is added, replacing
    /// the default operator the scope behaviour set first.
    /// </summary>
    [Fact]
    public async Task GoogleAuth_Of_An_Existing_Slovak_Account_With_No_Market_Stamps_The_Token_With_The_Slovak_Company()
    {
        await TestMethod(
            setup: services =>
            {
                Anonymous(services);
                var verifier = new Mock<IGoogleTokenVerifier>();
                verifier.Setup(v => v.VerifyAsync("google-token", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new GoogleVerifiedClaims("google-sub-sk", "existing-sk@cleansia.test", EmailVerified: true));
                services.Replace(ServiceDescriptor.Scoped<IGoogleTokenVerifier>(_ => verifier.Object));
                return Task.CompletedTask;
            },
            arrange: async context =>
            {
                await SeedMarketsAsync(context);
                var existing = User.CreateWithGoogle("existing-sk@cleansia.test", "Exi", "Sting", "google-sub-sk");
                existing.TenantId = TestTenants.Second;
                context.Users.Add(existing);
                await CommitSeedAsync(context);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new GoogleAuth.Command("google-token", "ignored", "ignored@x", "Exi", "Sting")),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                var user = Assert.Single(await context.Users.IgnoreQueryFilters().Where(u => u.Email == "existing-sk@cleansia.test").ToListAsync());
                Assert.Equal(TestTenants.Second, user.TenantId);
                var token = await context.RefreshTokens.IgnoreQueryFilters().SingleAsync(t => t.UserId == user.Id);
                Assert.Equal(TestTenants.Second, token.TenantId);
                Assert.Empty(await context.RefreshTokens.IgnoreQueryFilters().Where(t => t.TenantId == TestTenants.Default).ToListAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task AppleAuth_Provisions_The_Account_In_The_Named_Markets_Operator()
    {
        await TestMethod(
            setup: services =>
            {
                Anonymous(services);
                var verifier = new Mock<IAppleTokenVerifier>();
                verifier.Setup(v => v.VerifyAsync("apple-token", "nonce", It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new AppleVerifiedClaims("apple-sub-1", "apple@cleansia.test", EmailVerified: true));
                services.Replace(ServiceDescriptor.Scoped<IAppleTokenVerifier>(_ => verifier.Object));
                return Task.CompletedTask;
            },
            arrange: SeedAndCommitAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new AppleAuth.Command("apple-token", "nonce", "App", "Le", TermsAccepted: true, CountryId: Slovakia)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == "apple@cleansia.test");
                Assert.Equal(TestTenants.Second, user.TenantId);
            },
            transactional: false);
    }

    [Fact]
    public async Task RequestPromoCode_Lands_The_Code_And_Its_Envelope_In_The_Named_Markets_Operator()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedAndCommitAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new RequestPromoCode.Command("prospect@cleansia.test", "en", CountryId: Slovakia)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                var code = Assert.Single(await context.PromoCodes.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(TestTenants.Second, code.TenantId);

                var outbox = Assert.Single(await context.OutboxMessages.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(TestTenants.Second, outbox.TenantId);
                var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(outbox.Body, WireJson);
                Assert.Equal(TestTenants.Second, envelope!.TenantId);
            },
            transactional: false);
    }

    [Theory]
    [InlineData(Slovakia, true)]
    [InlineData(Czechia, false)]
    public async Task ValidateReferral_Reads_The_Named_Markets_Codes(string countryId, bool expectedValid)
    {
        await TestMethod(
            setup: Anonymous,
            arrange: async context =>
            {
                await SeedMarketsAsync(context);
                var referrer = User.CreateWithPassword("referrer-sk@cleansia.test", Password, "Ref", "Errer");
                referrer.ConfirmEmail();
                referrer.TenantId = TestTenants.Second;
                context.Users.Add(referrer);
                var code = ReferralCode.Generate(referrer.Id, "SKCODE1", "system");
                code.TenantId = TestTenants.Second;
                context.ReferralCodes.Add(code);
                await CommitSeedAsync(context);
            },
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new ValidateReferral.Query("SKCODE1", CountryId: countryId)),
            assert: (_, result) =>
            {
                Assert.True(result.IsSuccess, Describe(result));
                Assert.Equal(expectedValid, result.Value.IsValid);
                return Task.CompletedTask;
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Market_Nobody_Operates_Is_Refused_As_A_Configuration_Defect_And_Writes_Nothing()
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedAndCommitAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new Register.Command("nobody@cleansia.test", Password, "No", "Body", "en", CountryId: Poland, TermsAccepted: true)),
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                var error = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
                Assert.Equal(BusinessErrorMessage.TenantNotFound, error.Message);
                Assert.Equal("CountryId", error.Code);
                Assert.Empty(await context.Users.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    [Theory]
    [InlineData(Germany)]
    [InlineData("country-that-does-not-exist")]
    public async Task A_Country_That_Is_Not_A_Market_Is_Refused_As_User_Input_And_Writes_Nothing(string countryId)
    {
        await TestMethod(
            setup: Anonymous,
            arrange: SeedAndCommitAsync,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new Register.Command("nowhere@cleansia.test", Password, "No", "Where", "en", CountryId: countryId, TermsAccepted: true)),
            assert: async (context, result) =>
            {
                Assert.False(result.IsSuccess);
                var error = Assert.Single(Assert.IsAssignableFrom<IValidationResult>(result).Errors);
                Assert.Equal(BusinessErrorMessage.CountryNotServiced, error.Message);
                Assert.NotEqual(BusinessErrorMessage.TenantNotFound, error.Message);
                Assert.Empty(await context.Users.IgnoreQueryFilters().ToListAsync());
            },
            transactional: false);
    }

    private static string Describe(BusinessResult result) =>
        result is IValidationResult validation
            ? string.Join("; ", validation.Errors.Select(e => $"{e.Code}={e.Message}"))
            : result.Error?.Message ?? "success";

    /// <summary>No claim, no override, no e-mail transport: the anonymous path as the hosts run it.</summary>
    private static Task Anonymous(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            new TestClaimsPrincipalUser(new ClaimsPrincipal(new ClaimsIdentity())))));
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        services.Replace(ServiceDescriptor.Scoped<IEmailService>(_ => new Mock<IEmailService>().Object));
        return Task.CompletedTask;
    }

    private static async Task SeedAndCommitAsync(CleansiaDbContext context)
    {
        await SeedMarketsAsync(context);
        await CommitSeedAsync(context);
    }

    /// <summary>
    /// The anonymous path's provider answers null, so the seed is stamped by hand with the default
    /// company and committed through CommitAsync (which also stamps CreatedBy) rather than left to the
    /// harness's bare save.
    /// </summary>
    private static async Task CommitSeedAsync(CleansiaDbContext context)
    {
        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static Task SeedMarketsAsync(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.IsActive = true;
        czk.SetAsDefault(true);
        var eur = Currency.Create("EUR", "€", "Euro");
        eur.IsActive = true;
        var pln = Currency.Create("PLN", "zł", "Złoty");
        pln.IsActive = true;
        context.Currencies.AddRange(czk, eur, pln);

        foreach (var (id, name, iso, serviced, code, operatorTenantId, isDefault) in new[]
                 {
                     (Czechia, "Czechia", "CZE", true, "CZK", TestTenants.Default, true),
                     (Slovakia, "Slovakia", "SVK", true, "EUR", TestTenants.Second, false),
                     (Poland, "Poland", "POL", true, "PLN", (string?)null, false),
                     (Germany, "Germany", "DEU", false, "EUR", (string?)null, false),
                 })
        {
            var country = Country.Create(name, iso, iso[..2], serviced);
            country.Id = id;
            context.Countries.Add(country);
            context.CountryConfigurations.Add(
                CountryConfiguration.Create(id, code, "en", 0.2m)
                    .AssignOperator(operatorTenantId)
                    .SetAsDefaultMarket(isDefault));
        }

        return Task.CompletedTask;
    }
}
