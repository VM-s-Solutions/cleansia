using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Legal;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// ADR-0062 D4 through the real pipeline on real Postgres. The same <c>GrantConsent</c> command is
/// routed on both Partner hosts: an employee's terms row stays unversioned and leaves no customer audit
/// row. A customer who granted under an older version and grants again moves the row to the version in
/// force and leaves a <c>customer.consent.grant</c> row carrying it; one who grants again under the same
/// version leaves the row untouched, is refused as already granted, and STILL leaves a
/// <c>customer.consent.grant</c> row — the refusal, out of band — so the trail has the attempt.
/// </summary>
[Collection("PostgresCollection")]
public class ConsentVersioningTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string OlderVersion = "2025-01-old";
    private const string Ip = "203.0.113.9";
    private const string DeviceLabel = "Chrome/Windows";
    private const string SeededIp = "198.51.100.1";
    private const string SeededDevice = "Firefox";

    private static User NewUser(UserProfile profile)
    {
        var user = User.CreateWithPassword($"{profile.ToString().ToLowerInvariant()}@cleansia.test", "Password1!@abc", "Con", "Sent", profile);
        user.Created("seed", DateTime.UtcNow);
        return user;
    }

    private static Task SessionOf(IServiceCollection services, User user, UserProfile role, string hostAudience)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            user.Id, user.Email, [new Claim(ClaimTypes.Role, role.ToString())])));
        services.Replace(ServiceDescriptor.Scoped<IRequestMetadataProvider>(_ => new TestRequestMetadataProvider(Ip, DeviceLabel)));
        services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider(hostAudience));
        return Task.CompletedTask;
    }

    private static UserConsent SeededTermsRow(User user, string? documentVersion)
    {
        var consent = UserConsent.Grant(user.Id, ConsentType.TermsOfService, SeededIp, SeededDevice, documentVersion);
        consent.Created("seed", DateTime.UtcNow);
        return consent;
    }

    private static async Task<UserConsent> TermsRowOf(CleansiaDbContext context, string userId) =>
        await context.UserConsents.IgnoreQueryFilters().SingleAsync(c => c.UserId == userId && c.ConsentType == ConsentType.TermsOfService);

    private static async Task<List<CustomerActionAudit>> CustomerRows(CleansiaDbContext context) =>
        await context.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();

    [Fact]
    public async Task An_Employee_On_The_Partner_Host_Gets_An_Unversioned_Row_And_No_Customer_Audit_Row()
    {
        var employee = NewUser(UserProfile.Employee);

        await TestMethod(
            setup: services => SessionOf(services, employee, UserProfile.Employee, JwtAudiences.Partner),
            arrange: context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                context.Users.Add(employee);
                return Task.CompletedTask;
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new GrantConsent.Command(ConsentType.TermsOfService)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);

                var row = await TermsRowOf(context, employee.Id);
                Assert.True(row.IsGranted);
                Assert.Null(row.DocumentVersion);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(DeviceLabel, row.UserAgent);

                Assert.Empty(await CustomerRows(context));
                Assert.Equal(0, await context.AdminActionAudits.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Regranting_Under_A_Newer_Version_Moves_The_Row_And_Leaves_A_Grant_Row_With_The_New_Version()
    {
        var customer = NewUser(UserProfile.Customer);

        await TestMethod(
            setup: services => SessionOf(services, customer, UserProfile.Customer, JwtAudiences.Customer),
            arrange: context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                context.Users.Add(customer);
                context.UserConsents.Add(SeededTermsRow(customer, OlderVersion));
                return Task.CompletedTask;
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new GrantConsent.Command(ConsentType.TermsOfService)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);

                var row = await TermsRowOf(context, customer.Id);
                Assert.True(row.IsGranted);
                Assert.Equal(LegalDocumentVersions.CustomerTerms, row.DocumentVersion);
                Assert.Equal(Ip, row.IpAddress);
                Assert.Equal(DeviceLabel, row.UserAgent);

                var audit = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.consent.grant", audit.Action);
                Assert.True(audit.Success);
                Assert.Equal(customer.Id, audit.UserId);
                Assert.Equal("User", audit.ResourceType);
                Assert.Equal(customer.Id, audit.ResourceId);
                Assert.Equal(JwtAudiences.Customer, audit.ClientAudience);
                var payload = JsonDocument.Parse(audit.PayloadJson!).RootElement;
                Assert.Equal(LegalDocumentVersions.CustomerTerms, payload.GetProperty("documentVersion").GetString());
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Regranting_Under_The_Same_Version_Leaves_The_Row_Untouched_And_Still_Leaves_A_Grant_Row()
    {
        var customer = NewUser(UserProfile.Customer);

        await TestMethod(
            setup: services => SessionOf(services, customer, UserProfile.Customer, JwtAudiences.Customer),
            arrange: context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                context.Users.Add(customer);
                context.UserConsents.Add(SeededTermsRow(customer, LegalDocumentVersions.CustomerTerms));
                return Task.CompletedTask;
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new GrantConsent.Command(ConsentType.TermsOfService)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsFailure);
                Assert.Equal(BusinessErrorMessage.ConsentAlreadyGranted, result.Error!.Message);

                var row = await TermsRowOf(context, customer.Id);
                Assert.Equal(LegalDocumentVersions.CustomerTerms, row.DocumentVersion);
                Assert.Equal(SeededIp, row.IpAddress);
                Assert.Equal(SeededDevice, row.UserAgent);

                var audit = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.consent.grant", audit.Action);
                Assert.False(audit.Success);
                Assert.Equal(BusinessErrorMessage.ConsentAlreadyGranted, audit.ErrorCode);
                Assert.Equal(customer.Id, audit.UserId);
                Assert.Null(audit.PayloadJson);
            },
            transactional: false);
    }

    [Fact]
    public async Task A_Customer_Withdrawal_Leaves_A_Withdraw_Row_Carrying_The_Version_The_Row_Was_Granted_Under()
    {
        var customer = NewUser(UserProfile.Customer);

        await TestMethod(
            setup: services => SessionOf(services, customer, UserProfile.Customer, JwtAudiences.Customer),
            arrange: context =>
            {
                context.Languages.Add(Language.Create("en", "English"));
                context.Users.Add(customer);
                context.UserConsents.Add(SeededTermsRow(customer, OlderVersion));
                return Task.CompletedTask;
            },
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new WithdrawConsent.Command(ConsentType.TermsOfService)),
            assert: async (context, result) =>
            {
                Assert.True(result.IsSuccess);

                var row = await TermsRowOf(context, customer.Id);
                Assert.False(row.IsGranted);
                Assert.NotNull(row.WithdrawnAt);
                Assert.Equal(OlderVersion, row.DocumentVersion);

                var audit = Assert.Single(await CustomerRows(context));
                Assert.Equal("customer.consent.withdraw", audit.Action);
                Assert.True(audit.Success);
                Assert.Equal(customer.Id, audit.ResourceId);
                Assert.Equal(OlderVersion, JsonDocument.Parse(audit.PayloadJson!).RootElement.GetProperty("documentVersion").GetString());
            },
            transactional: false);
    }
}
