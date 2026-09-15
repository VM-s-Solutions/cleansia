using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Auditing;

/// <summary>
/// ADR-0062 D5 (Verification #6) against real Postgres, through <c>GdprDeletionService</c>'s REAL walk —
/// the <see cref="GdprDeleteAuditSurvivesErasureTests"/> shape for the customer table.
///
/// <para>The subject's rows SURVIVE the erasure with <c>UserId</c>, <c>ResourceId</c> and the payload
/// intact — they are the defence-of-claims record — and exactly three columns are blanked: the IP
/// address, the device label and the device id. A bystander's rows are untouched. And because the
/// blanking is a tracked write riding the erasure's single commit, an erasure whose commit throws leaves
/// every row exactly as it was: the trail is never blanked for a customer who still exists.</para>
///
/// <para>Also covers the retention delete the repository exposes for the retention task: per row, by its own age,
/// for the ambient company alone, and never the admin or employee tables.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CustomerActionAuditErasureTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string BystanderId = "user-keep-audit-1";
    private const string SubjectIp = "203.0.113.9";
    private const string SubjectDevice = "iPhone 15 / iOS 17.4";
    private const string SubjectDeviceId = "device-abc-123";
    private const string Payload = "{\"feeRate\": 0.5, \"hasBeenAccepted\": true}";

    [Fact]
    public async Task Erasure_Keeps_Every_Row_And_Blanks_Exactly_The_Three_Request_Metadata_Columns()
    {
        await TestMethod(
            arrange: SeedSubjectWithRows,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new DeleteUserAccount.Command());
            },
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess);

                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.StartsWith("deleted_", user.Email);

                var subjectRows = await context.CustomerActionAudits.IgnoreQueryFilters()
                    .Where(a => a.UserId == SubjectId)
                    .OrderBy(a => a.ResourceId)
                    .ToListAsync();
                Assert.Equal(3, subjectRows.Count);
                Assert.All(subjectRows, row =>
                {
                    Assert.Null(row.IpAddress);
                    Assert.Null(row.DeviceLabel);
                    Assert.Null(row.DeviceId);
                    Assert.Equal(SubjectId, row.UserId);
                    Assert.Equal("Order", row.ResourceType);
                    Assert.Equal(TestTenants.Default, row.TenantId);
                    Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
                });
                Assert.Equal(["ORD-1", "ORD-2", "ORD-3"], subjectRows.Select(r => r.ResourceId));
                Assert.Equal(new[] { true, false, true }, subjectRows.Select(r => r.Success));
                Assert.Contains("\"feeRate\": 0.5", subjectRows[0].PayloadJson);
                Assert.Equal("order.in_progress_cannot_cancel", subjectRows[1].ErrorCode);

                var bystander = await context.CustomerActionAudits.IgnoreQueryFilters().SingleAsync(a => a.UserId == BystanderId);
                Assert.Equal("198.51.100.7", bystander.IpAddress);
                Assert.Equal("Pixel 8", bystander.DeviceLabel);
                Assert.Equal("device-keep-1", bystander.DeviceId);
            });
    }

    [Fact]
    public async Task An_Erasure_Whose_Commit_Throws_Leaves_The_Rows_Unpseudonymised()
    {
        await TestMethod(
            arrange: SeedSubjectWithRows,
            act: async provider =>
            {
                var service = provider.GetRequiredService<IGdprDeletionService>();
                var context = provider.GetRequiredService<CleansiaDbContext>();

                var result = await service.DeleteUserAccountAsync(
                    SubjectId,
                    GdprAuditReasons.SelfDeletion,
                    user => (user.Email, null),
                    deferEmployeeErasure: true,
                    CancellationToken.None);
                Assert.True(result.IsSuccess);

                // Everything the walk staged is unsaved here; a row the commit cannot take (ActorId over its
                // 26-char column) makes the erasure's single SaveChangesAsync throw.
                context.AdminActionAudits.Add(new AdminActionAudit
                {
                    ActorId = new string('x', 40),
                    Action = "poison",
                    ActorProfile = UserProfile.Administrator,
                    Success = true,
                    TenantId = TestTenants.Default
                });

                await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                var user = await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == SubjectId);
                Assert.Equal(TestConstants.TestUserSession.TestUserEmail, user.Email);

                var subjectRows = await context.CustomerActionAudits.IgnoreQueryFilters()
                    .Where(a => a.UserId == SubjectId)
                    .ToListAsync();
                Assert.Equal(3, subjectRows.Count);
                Assert.All(subjectRows, row =>
                {
                    Assert.Equal(SubjectIp, row.IpAddress);
                    Assert.Equal(SubjectDevice, row.DeviceLabel);
                    Assert.Equal(SubjectDeviceId, row.DeviceId);
                });
            },
            transactional: false);
    }

    /// <summary>
    /// The delete is the ambient company's alone: the retention job calls it once per company under that
    /// company's override, because each company keeps its own window. The second company's expired row
    /// survives a delete run under the first.
    /// </summary>
    [Fact]
    public async Task The_Retention_Delete_Removes_The_Ambient_Companys_Rows_By_Their_Own_Age_And_Nothing_Else()
    {
        var cutoff = DateTimeOffset.UtcNow.AddYears(-3);

        await TestMethod(
            arrange: async context =>
            {
                context.CustomerActionAudits.AddRange(
                    RowAt(cutoff.AddDays(-1), "cust-old", "ORD-OLD", TestTenants.Default),
                    RowAt(cutoff.AddDays(1), "cust-old", "ORD-YOUNG", TestTenants.Default),
                    RowAt(cutoff.AddDays(-1), null, "ORD-GUEST", TestTenants.Default),
                    RowAt(cutoff.AddDays(-1), "cust-sk", "ORD-SK", TestTenants.Second));
                context.AdminActionAudits.Add(new AdminActionAudit
                {
                    ActorId = "admin-1", Action = "order.refund", ActorProfile = UserProfile.Administrator,
                    Success = true, OccurredOn = cutoff.AddYears(-1), TenantId = TestTenants.Default
                });
                await Task.CompletedTask;
            },
            act: async provider =>
            {
                var repository = provider.GetRequiredService<ICustomerActionAuditRepository>();
                return await repository.DeleteExpiredAsync(cutoff, CancellationToken.None);
            },
            assert: async (CleansiaDbContext context, int deleted) =>
            {
                Assert.Equal(2, deleted);

                var remaining = await context.CustomerActionAudits.IgnoreQueryFilters()
                    .Select(a => a.ResourceId)
                    .ToListAsync();
                Assert.Equal(["ORD-SK", "ORD-YOUNG"], remaining.OrderBy(r => r, StringComparer.Ordinal));

                Assert.Equal(1, await context.AdminActionAudits.IgnoreQueryFilters().CountAsync());
            });
    }

    private static CustomerActionAudit RowAt(DateTimeOffset occurredOn, string? userId, string resourceId, string tenantId)
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: JwtAudiences.Customer, ipAddress: null, deviceLabel: null, deviceId: null,
            action: "customer.test.act", resourceType: "Order", resourceId: resourceId, success: true, errorCode: null,
            payloadJson: null, correlationId: null);
        row.TenantId = tenantId;
        typeof(CustomerActionAudit).GetProperty(nameof(CustomerActionAudit.OccurredOn))!.SetValue(row, occurredOn);
        return row;
    }

    private static CustomerActionAudit SubjectRow(string resourceId, bool success, string? errorCode, string? payloadJson)
    {
        var row = CustomerActionAudit.Create(
            userId: SubjectId, clientAudience: JwtAudiences.Customer, ipAddress: SubjectIp, deviceLabel: SubjectDevice,
            deviceId: SubjectDeviceId, action: "customer.test.act", resourceType: "Order", resourceId: resourceId,
            success: success, errorCode: errorCode, payloadJson: payloadJson, correlationId: null);
        row.TenantId = TestTenants.Default;
        return row;
    }

    private static async Task SeedSubjectWithRows(CleansiaDbContext context)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
            await context.SaveChangesAsync();
        }

        var subject = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        subject.Id = SubjectId;
        subject.ConfirmEmail();
        context.Users.Add(subject);

        var bystander = User.CreateWithPassword("tomas.svoboda@cleansia.test", "Seed-Password-123", "Tomas", "Svoboda");
        bystander.Id = BystanderId;
        bystander.ConfirmEmail();
        context.Users.Add(bystander);
        await context.CommitAsync(CancellationToken.None);

        context.CustomerActionAudits.AddRange(
            SubjectRow("ORD-1", success: true, errorCode: null, Payload),
            SubjectRow("ORD-2", success: false, "order.in_progress_cannot_cancel", payloadJson: null),
            SubjectRow("ORD-3", success: true, errorCode: null, Payload));

        var bystanderRow = CustomerActionAudit.Create(
            userId: BystanderId, clientAudience: JwtAudiences.Customer, ipAddress: "198.51.100.7", deviceLabel: "Pixel 8",
            deviceId: "device-keep-1", action: "customer.test.act", resourceType: "Order", resourceId: "ORD-9",
            success: true, errorCode: null, payloadJson: null, correlationId: null);
        bystanderRow.TenantId = TestTenants.Default;
        context.CustomerActionAudits.Add(bystanderRow);
        await context.SaveChangesAsync();
    }
}
