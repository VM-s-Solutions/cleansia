using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Auth;

/// <summary>
/// Q-AUD-L5 overruled, through the real pipeline on real Postgres, as the anonymous caller a sign-in
/// is: a password sign-in leaves one <c>customer.session.login</c> row keyed on the account with the
/// method and the audience, committed with the session; a sign-in with a wrong password or an unknown
/// address leaves one out-of-band failure row with the KEY, the IP and the device — stamped with the
/// default market's operator, because a sign-in names no market — and nowhere on it the address the
/// caller typed; a reset request for an unknown address is the same shape. The success row is stamped
/// with the account's own operator, the one the token was minted under.
/// </summary>
[Collection("PostgresCollection")]
public class SessionAuditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string CustomerId = "cust-session-audit";
    private const string CustomerEmail = "session-audit@cleansia.test";
    private const string CustomerPassword = "Seed-Password-123!";
    private const string UnknownEmail = "nobody-session-audit@cleansia.test";
    private const string CountryId = "country-cz-session-audit";
    private const string CurrencyId = "currency-czk-session-audit";
    private const string Ip = "203.0.113.42";
    private const string DeviceLabel = "Firefox/Linux";
    private const string DeviceId = "device-session-audit";

    /// <summary>No claim and no override: the anonymous path, where the scope behaviour sets the tenant from the default market.</summary>
    private static Task AnonymousSession(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            new TestClaimsPrincipalUser(new ClaimsPrincipal(new ClaimsIdentity())))));
        services.Replace(ServiceDescriptor.Scoped<ITenantProvider>(sp =>
            new TenantProvider(sp.GetRequiredService<IHttpContextAccessor>())));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(Ip, DeviceLabel, DeviceId)));
        return Task.CompletedTask;
    }

    private static async Task Seed(CleansiaDbContext context)
    {
        context.Languages.Add(Language.Create("en", "English"));

        var country = Country.Create("Czechia", "CZ", "CZ", isServiced: true);
        country.Id = CountryId;
        context.Countries.Add(country);
        context.CountryConfigurations.Add(
            CountryConfiguration.Create(CountryId, "CZK", "cs", 0.20m).AssignOperator(TestTenants.Default).SetAsDefaultMarket(true));

        var czk = Currency.Create("CZK", "Kč", "Czech koruna");
        czk.Id = CurrencyId;
        czk.IsActive = true;
        czk.SetAsDefault(true);
        context.Currencies.Add(czk);

        var customer = User.CreateWithPassword(CustomerEmail, CustomerPassword, "Session", "Audit", UserProfile.Customer);
        customer.Id = CustomerId;
        customer.ConfirmEmail();
        context.Users.Add(customer);

        StampUnstampedAdded(context, TestTenants.Default);
        await context.CommitAsync(CancellationToken.None);
    }

    private static async Task<List<CustomerActionAudit>> CustomerRows(CleansiaDbContext context) =>
        await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();

    private static void AssertCarriesNoAddress(CustomerActionAudit row, string email)
    {
        foreach (var value in new[] { row.UserId, row.ResourceId, row.ResourceType, row.ErrorCode, row.PayloadJson, row.DeviceLabel, row.DeviceId, row.CorrelationId })
        {
            Assert.DoesNotContain(email, value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task A_Password_SignIn_Leaves_One_Success_Row_Keyed_On_The_Account_With_The_Method_And_The_Audience()
    {
        await TestMethod(
            setup: AnonymousSession,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new Login.Command(CustomerEmail, CustomerPassword, RememberMe: true)),
            assert: async (CleansiaDbContext context, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);
                Assert.NotEmpty(result.Value.Token);

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.session.login", row.Action);
                Assert.True(row.Success);
                Assert.Null(row.ErrorCode);
                Assert.Equal(CustomerId, row.UserId);
                Assert.Equal("User", row.ResourceType);
                Assert.Equal(CustomerId, row.ResourceId);
                Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(DeviceLabel, row.DeviceLabel);
                Assert.Equal(DeviceId, row.DeviceId);
                Assert.Equal(TestTenants.Default, row.TenantId);

                var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
                Assert.Equal(LoginEvidence.PasswordMethod, payload.GetProperty("method").GetString());
                Assert.True(payload.GetProperty("rememberMe").GetBoolean());
                Assert.Equal(JwtAudiences.Customer, payload.GetProperty("clientAudience").GetString());
                Assert.True(payload.GetProperty("emailConfirmed").GetBoolean());
                AssertCarriesNoAddress(row, CustomerEmail);

                Assert.Equal(1, await context.RefreshTokens.IgnoreQueryFilters().CountAsync(t => t.UserId == CustomerId));
                Assert.Equal(0, await context.AdminActionAudits.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_SignIn_With_An_Unknown_Address_Leaves_One_Failure_Row_With_The_Key_The_IP_And_No_Trace_Of_The_Address()
    {
        await TestMethod(
            setup: AnonymousSession,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new Login.Command(UnknownEmail, CustomerPassword, RememberMe: false)),
            assert: async (CleansiaDbContext context, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Contains(((IValidationResult)result).Errors, e => e.Message == BusinessErrorMessage.NotExistingUserWithEmail);

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.session.login", row.Action);
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.NotExistingUserWithEmail, row.ErrorCode);
                Assert.Null(row.UserId);
                Assert.Null(row.ResourceId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(DeviceLabel, row.DeviceLabel);
                Assert.Equal(DeviceId, row.DeviceId);
                Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
                Assert.Equal(TestTenants.Default, row.TenantId);
                AssertCarriesNoAddress(row, UnknownEmail);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_SignIn_With_A_Wrong_Password_Leaves_One_Failure_Row_With_The_Key_And_No_User()
    {
        await TestMethod(
            setup: AnonymousSession,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new Login.Command(CustomerEmail, "Wrong-Password-999!", RememberMe: false)),
            assert: async (CleansiaDbContext context, BusinessResult<JwtTokenResponse> result) =>
            {
                Assert.True(result.IsFailure);

                var row = Assert.Single(await CustomerRows(context));
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.InvalidPassword, row.ErrorCode);
                Assert.Null(row.UserId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(Ip, row.IpAddress);
                AssertCarriesNoAddress(row, CustomerEmail);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == CustomerId);
                Assert.Equal(1, user.FailedLoginAttempts);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Reset_Request_For_An_Unknown_Address_Leaves_One_Failure_Row_With_No_User_And_No_Trace_Of_The_Address()
    {
        await TestMethod(
            setup: AnonymousSession,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new RequestPasswordChange.Command(UnknownEmail)),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsFailure);

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.password.reset_requested", row.Action);
                Assert.False(row.Success);
                Assert.Equal(BusinessErrorMessage.NotExistingUserWithEmail, row.ErrorCode);
                Assert.Null(row.UserId);
                Assert.Null(row.ResourceId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(Ip, row.IpAddress);
                AssertCarriesNoAddress(row, UnknownEmail);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Reset_Request_For_A_Known_Address_Leaves_One_Success_Row_Keyed_On_The_Account_With_No_Payload()
    {
        await TestMethod(
            setup: AnonymousSession,
            arrange: Seed,
            act: async provider => await provider.GetRequiredService<IMediator>()
                .Send(new RequestPasswordChange.Command(CustomerEmail)),
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess, result.Error?.Message);

                var row = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.password.reset_requested", row.Action);
                Assert.True(row.Success);
                Assert.Equal(CustomerId, row.UserId);
                Assert.Equal(CustomerId, row.ResourceId);
                Assert.Null(row.PayloadJson);
                Assert.Equal(TestTenants.Default, row.TenantId);
                AssertCarriesNoAddress(row, CustomerEmail);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == CustomerId);
                Assert.NotNull(user.ResetPasswordCode);
            },
            transactional: false);
    }
}
