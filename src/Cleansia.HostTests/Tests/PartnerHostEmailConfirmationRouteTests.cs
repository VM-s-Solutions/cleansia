using System.Net;
using System.Net.Http.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Users;
using Cleansia.HostTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// End to end on both partner hosts with their real <c>IHostAudienceProvider</c>: <c>PUT
/// api/Auth/ConfirmUserEmail</c> confirms a cleaner's code and opens the session, and refuses a Customer's
/// code with the key <c>PartnerLogin</c> uses, leaving the address unconfirmed — with the partner
/// <c>Register</c> route gone this was the last anonymous route on which a Customer could be minted a
/// partner-audience session.
/// </summary>
public sealed class PartnerHostEmailConfirmationRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string EmployeeEmail = "otp-cleaner@hosttests.local";
    private const string CustomerEmail = "otp-customer@hosttests.local";

    private HttpClient AnonymousClientFor(string hostAudience) =>
        hostAudience == MobileAudience ? MobileClientAnonymous() : PartnerClientAnonymous();

    private async Task<(string employeeOtp, string customerOtp)> SeedUnconfirmedAccountsAsync()
    {
        var employee = User.CreateWithPassword(EmployeeEmail, "Secret-123!", "Otp", "Cleaner", UserProfile.Employee);
        var customer = User.CreateWithPassword(CustomerEmail, "Secret-123!", "Otp", "Customer");
        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            ctx.Users.Add(employee);
            ctx.Users.Add(customer);
        });
        return (employee.RawConfirmationToken!, customer.RawConfirmationToken!);
    }

    private Task<bool> IsConfirmedAsync(string email) =>
        QueryAsync(ctx => ctx.Users.IgnoreQueryFilters().Where(u => u.Email == email).Select(u => u.IsEmailConfirmed).SingleAsync());

    [Theory]
    [InlineData(PartnerAudience)]
    [InlineData(MobileAudience)]
    public async Task A_Cleaners_Code_Confirms_The_Address_And_Opens_The_Session(string hostAudience)
    {
        var (employeeOtp, _) = await SeedUnconfirmedAccountsAsync();

        var response = await AnonymousClientFor(hostAudience)
            .PutAsJsonAsync("/api/Auth/ConfirmUserEmail", new { code = employeeOtp, email = EmployeeEmail });

        HttpAssert.IsOk(response);
        var session = await response.Content.ReadFromJsonAsync<SessionBody>();
        Assert.True(session!.IsEmailConfirmed);
        Assert.Equal("Employee", session.Role);
        Assert.True(await IsConfirmedAsync(EmployeeEmail));
    }

    [Theory]
    [InlineData(PartnerAudience)]
    [InlineData(MobileAudience)]
    public async Task A_Customers_Code_Is_Refused_And_The_Address_Stays_Unconfirmed(string hostAudience)
    {
        var (_, customerOtp) = await SeedUnconfirmedAccountsAsync();

        var response = await AnonymousClientFor(hostAudience)
            .PutAsJsonAsync("/api/Auth/ConfirmUserEmail", new { code = customerOtp, email = CustomerEmail });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(BusinessErrorMessage.InsufficientPrivileges, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.False(await IsConfirmedAsync(CustomerEmail));
    }

    private sealed record SessionBody(bool IsEmailConfirmed, string? Role);
}
