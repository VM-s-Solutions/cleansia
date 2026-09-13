using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Features.Gdpr.DTOs;
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// ADR-0062 D5-export / D6-export through the real pipeline on real Postgres. The subject export carries
/// the customer's own trail as <c>customerActions</c>, and both exports finally leave a record: the
/// <c>GdprRequest("Export")</c> row each of them had been adding since the feature shipped was never
/// committed, because a Query has no commit for it to ride. As Commands they commit; the admin one is
/// additionally an audited admin act (<c>gdpr.user.export</c>) whose snapshot holds counts and the
/// subject id, never the exported data. A build that throws commits nothing and is recorded out-of-band
/// with the exception type — the test that would have caught the silent drop.
/// </summary>
[Collection("PostgresCollection")]
public class SubjectExportAuditTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string SubjectId = TestConstants.TestUserSession.TestUserId;
    private const string SubjectEmail = TestConstants.TestUserSession.TestUserEmail;
    private const string BystanderId = "user-bystander-export-1";
    private const string AdminId = "admin-1";
    private const string AdminEmail = "admin@cleansia.test";
    private const string SubjectIp = "203.0.113.9";
    private const string SubjectDevice = "iPhone 15 / iOS 17.4";
    private const string Payload = "{\"feeRate\": 0.5, \"hasBeenAccepted\": true}";

    private static Task AsAdministrator(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            AdminId, AdminEmail, [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())])));
        return Task.CompletedTask;
    }

    private static Task AsTheSubject(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider>(_ => new TestUserSessionProvider(
            SubjectId, SubjectEmail, [new Claim(ClaimTypes.Role, UserProfile.Customer.ToString())])));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_Admin_Export_Carries_Every_Row_Of_The_Subject_Commits_Its_Request_And_Is_Recorded_As_An_Admin_Act()
    {
        await TestMethod(
            setup: AsAdministrator,
            arrange: SeedSubjectAndBystanderWithRows,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new AdminExportUserData.Command(SubjectId)),
            assert: async (CleansiaDbContext context, BusinessResult<GdprExportDto> result) =>
            {
                Assert.True(result.IsSuccess);
                AssertSubjectTrail(result.Value.CustomerActions, expectRequestContext: true);
                Assert.Equal($"admin:{AdminEmail}", result.Value.Metadata.ExportedBy);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(SubjectId, request.UserId);
                Assert.Equal("Export", request.RequestType);
                Assert.Equal(GdprRequestStatus.Completed, request.Status);
                Assert.Equal(AdminEmail, request.ProcessedBy);

                var audit = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.Equal("gdpr.user.export", audit.Action);
                Assert.True(audit.Success);
                Assert.Equal("User", audit.ResourceType);
                Assert.Equal(SubjectId, audit.ResourceId);
                Assert.Equal(AdminId, audit.ActorId);
                Assert.NotNull(audit.AfterJson);
                // jsonb hands the document back normalised (key order and spacing are Postgres's), so read it as JSON.
                var snapshot = JsonDocument.Parse(audit.AfterJson!).RootElement;
                Assert.Equal(SubjectId, snapshot.GetProperty("subjectUserId").GetString());
                Assert.Equal("Export", snapshot.GetProperty("scope").GetString());
                Assert.Equal(0, snapshot.GetProperty("orderCount").GetInt32());
                Assert.Equal(2, snapshot.GetProperty("customerActionCount").GetInt32());
                Assert.Equal(4, snapshot.EnumerateObject().Count());
                Assert.DoesNotContain(SubjectEmail, audit.AfterJson, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(TestConstants.TestUserSession.TestFirstName, audit.AfterJson, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(TestConstants.TestUserSession.TestLastName, audit.AfterJson, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(SubjectIp, audit.AfterJson);
                Assert.DoesNotContain("@", audit.AfterJson);
                Assert.DoesNotContain("+", audit.AfterJson);

                Assert.Equal(3, await context.CustomerActionAudits.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Admin_Export_Whose_Build_Throws_Commits_No_Request_And_Is_Recorded_OutOfBand_With_The_Exception_Type()
    {
        await TestMethod(
            setup: async services =>
            {
                await AsAdministrator(services);
                services.Replace(ServiceDescriptor.Scoped<IGdprExportService>(_ => new ThrowingExportService()));
            },
            arrange: SeedSubjectAndBystanderWithRows,
            act: async provider =>
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    provider.GetRequiredService<IMediator>().Send(new AdminExportUserData.Command(SubjectId)));
                return true;
            },
            assert: async (CleansiaDbContext context, bool _) =>
            {
                Assert.Empty(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());

                var audit = Assert.Single(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
                Assert.Equal("gdpr.user.export", audit.Action);
                Assert.False(audit.Success);
                Assert.Equal(nameof(InvalidOperationException), audit.ErrorCode);
                Assert.Equal("User", audit.ResourceType);
                Assert.Equal(SubjectId, audit.ResourceId);
                Assert.Equal(AdminId, audit.ActorId);
                Assert.Null(audit.AfterJson);
            },
            transactional: false);
    }

    [Fact]
    public async Task The_Subjects_Own_Export_Carries_Only_Their_Rows_Commits_Its_Request_And_Writes_No_Admin_Row()
    {
        await TestMethod(
            setup: AsTheSubject,
            arrange: SeedSubjectAndBystanderWithRows,
            act: async provider => await provider.GetRequiredService<IMediator>().Send(new ExportUserData.Command()),
            assert: async (CleansiaDbContext context, BusinessResult<GdprExportDto> result) =>
            {
                Assert.True(result.IsSuccess);
                AssertSubjectTrail(result.Value.CustomerActions, expectRequestContext: true);
                Assert.Equal(SubjectEmail, result.Value.Metadata.ExportedBy);

                var request = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().ToListAsync());
                Assert.Equal(SubjectId, request.UserId);
                Assert.Equal("Export", request.RequestType);
                Assert.Equal(GdprRequestStatus.Completed, request.Status);
                Assert.Equal(SubjectEmail, request.ProcessedBy);

                Assert.Equal(0, await context.AdminActionAudits.IgnoreQueryFilters().CountAsync());
                Assert.Equal(3, await context.CustomerActionAudits.IgnoreQueryFilters().CountAsync());
            },
            transactional: false);
    }

    [Fact]
    public async Task An_Erased_Subject_Exports_With_The_Trail_Present_Its_Payloads_Intact_And_Its_Request_Context_Blanked()
    {
        await TestMethod(
            setup: AsAdministrator,
            arrange: SeedSubjectAndBystanderWithRows,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                var erased = await mediator.Send(new AdminDeleteUserAccount.Command(SubjectId));
                Assert.True(erased.IsSuccess);
                return await mediator.Send(new AdminExportUserData.Command(SubjectId));
            },
            assert: async (CleansiaDbContext context, BusinessResult<GdprExportDto> result) =>
            {
                Assert.True(result.IsSuccess);
                Assert.StartsWith("deleted_", result.Value.Profile.Email);
                AssertSubjectTrail(result.Value.CustomerActions, expectRequestContext: false);

                var export = Assert.Single(await context.GdprRequests.IgnoreQueryFilters().Where(r => r.RequestType == "Export").ToListAsync());
                Assert.Equal(GdprRequestStatus.Completed, export.Status);
                Assert.Contains(await context.AdminActionAudits.IgnoreQueryFilters().ToListAsync(),
                    a => a.Action == "gdpr.user.export" && a.ResourceId == SubjectId && a.Success);
            },
            transactional: false);
    }

    private static void AssertSubjectTrail(List<GdprExportCustomerActionDto> actions, bool expectRequestContext)
    {
        Assert.Equal(2, actions.Count);
        Assert.DoesNotContain(actions, a => a.ResourceId == "ORD-9");

        var cancelled = Assert.Single(actions, a => a.ResourceId == "ORD-1");
        Assert.Equal("customer.order.cancel", cancelled.Action);
        Assert.Equal("Order", cancelled.ResourceType);
        Assert.True(cancelled.Success);
        Assert.Null(cancelled.ErrorCode);
        Assert.Contains("\"feeRate\": 0.5", cancelled.PayloadJson);

        var refused = Assert.Single(actions, a => a.ResourceId == "ORD-2");
        Assert.False(refused.Success);
        Assert.Equal("order.in_progress_cannot_cancel", refused.ErrorCode);
        Assert.Null(refused.PayloadJson);

        Assert.All(actions, a =>
        {
            Assert.Equal(expectRequestContext ? SubjectIp : null, a.IpAddress);
            Assert.Equal(expectRequestContext ? SubjectDevice : null, a.DeviceLabel);
            Assert.NotEqual(default, a.OccurredOn);
        });
    }

    private static CustomerActionAudit Row(string userId, string resourceId, bool success, string? errorCode, string? payloadJson, string ip, string device)
    {
        var row = CustomerActionAudit.Create(
            userId: userId, clientAudience: JwtAudiences.Customer, ipAddress: ip, deviceLabel: device,
            deviceId: "device-abc-123", action: "customer.order.cancel", resourceType: "Order", resourceId: resourceId,
            success: success, errorCode: errorCode, payloadJson: payloadJson, correlationId: null);
        row.TenantId = TestTenants.Default;
        return row;
    }

    private static async Task SeedSubjectAndBystanderWithRows(CleansiaDbContext context)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
            await context.SaveChangesAsync();
        }

        var subject = User.CreateWithPassword(
            email: SubjectEmail,
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
            Row(SubjectId, "ORD-1", success: true, errorCode: null, Payload, SubjectIp, SubjectDevice),
            Row(SubjectId, "ORD-2", success: false, "order.in_progress_cannot_cancel", payloadJson: null, SubjectIp, SubjectDevice),
            Row(BystanderId, "ORD-9", success: true, errorCode: null, payloadJson: null, "198.51.100.7", "Pixel 8"));
        await context.SaveChangesAsync();
    }

    private sealed class ThrowingExportService : IGdprExportService
    {
        public Task<GdprExportDto> BuildAsync(string userId, string exportedBy, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("blob store down");
    }
}
