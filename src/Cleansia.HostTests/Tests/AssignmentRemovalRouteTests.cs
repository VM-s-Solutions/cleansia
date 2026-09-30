using System.Net.Http.Json;
using System.Text.Json;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.HostTests.Infrastructure;

namespace Cleansia.HostTests.Tests;

/// <summary>
/// Owner ruling 2026-09-28: the cleaner an administrator took off a job reads the written reason on both
/// partner hosts; a colleague asking about the same job is answered not-found, and a customer token is
/// refused at the gate.
/// </summary>
public sealed class AssignmentRemovalRouteTests(HostTestPostgresFixture db) : AuthzHostTestBase(db)
{
    private const string OrderId = "order-removal-route";
    private const string RemovedEmail = "removed-cleaner@hosttests.local";
    private const string ColleagueEmail = "colleague-cleaner@hosttests.local";
    private const string Reason = "The customer asked for another cleaner.";

    private static string Route => $"/api/Order/GetMyAssignmentRemoval?orderId={OrderId}";

    private async Task<(string RemovedEmployeeId, string ColleagueEmployeeId)> ArrangeAsync()
    {
        string removedEmployeeId = "", colleagueEmployeeId = "";

        await SeedAsync(async ctx =>
        {
            await DomainSeed.EnsureReferenceDataAsync(ctx);
            var removedUser = DomainSeed.EmployeeUser(RemovedEmail);
            var colleagueUser = DomainSeed.EmployeeUser(ColleagueEmail);
            ctx.Users.AddRange(removedUser, colleagueUser);
            var removed = DomainSeed.ApprovedEmployee(removedUser);
            var colleague = DomainSeed.ApprovedEmployee(colleagueUser);
            ctx.Employees.AddRange(removed, colleague);
            ctx.EmployeeDocuments.AddRange(DomainSeed.ActiveDocument(removed.Id), DomainSeed.ActiveDocument(colleague.Id));

            ctx.AdminActionAudits.Add(new AdminActionAudit
            {
                TenantId = HostTestTenants.Default,
                ActorId = "admin-1",
                ActorProfile = UserProfile.Administrator,
                Action = "order.reassign",
                ResourceType = "Order",
                ResourceId = OrderId,
                Success = true,
                OccurredOn = DateTimeOffset.UtcNow,
                Reason = Reason,
                BeforeJson = JsonSerializer.Serialize(new { orderId = OrderId, employeeId = removed.Id }),
                AfterJson = JsonSerializer.Serialize(new { orderId = OrderId, employeeId = colleague.Id }),
            });

            removedEmployeeId = removed.Id;
            colleagueEmployeeId = colleague.Id;
        });

        return (removedEmployeeId, colleagueEmployeeId);
    }

    [Fact]
    public async Task The_Removed_Cleaner_Reads_The_Reason_On_Both_Partner_Hosts()
    {
        var (removedEmployeeId, _) = await ArrangeAsync();

        foreach (var client in new[]
                 {
                     PartnerClient(TestJwtFactory.Mint(PartnerAudience, "u-removed", RemovedEmail, UserProfile.Employee, employeeId: removedEmployeeId)),
                     MobileClient(TestJwtFactory.Mint(MobileAudience, "u-removed", RemovedEmail, UserProfile.Employee, employeeId: removedEmployeeId)),
                 })
        {
            var response = await client.GetAsync(Route);

            HttpAssert.IsOk(response);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(Reason, body.GetProperty("reason").GetString());
        }
    }

    [Fact]
    public async Task A_Colleague_Asking_About_The_Same_Job_Is_Answered_Not_Found()
    {
        var (_, colleagueEmployeeId) = await ArrangeAsync();

        var response = await PartnerClient(
                TestJwtFactory.Mint(PartnerAudience, "u-colleague", ColleagueEmail, UserProfile.Employee, employeeId: colleagueEmployeeId))
            .GetAsync(Route);

        await HttpAssert.RejectedAsync(response, BusinessErrorMessage.OrderNotFound);
    }

    [Fact]
    public async Task A_Customer_Token_Is_Refused_At_The_Gate()
    {
        await ArrangeAsync();

        var response = await PartnerClient(
                TestJwtFactory.Mint(PartnerAudience, "u-customer", "customer@hosttests.local", UserProfile.Customer))
            .GetAsync(Route);

        HttpAssert.IsForbidden(response);
    }
}
