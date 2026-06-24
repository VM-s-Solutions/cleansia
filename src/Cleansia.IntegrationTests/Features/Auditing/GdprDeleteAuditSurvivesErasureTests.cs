using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Auditing;
using Cleansia.TestUtilities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Auditing;

/// <summary>
/// ADR-0012 D4.1/D6 (TC-AUDIT-SNAPSHOT GDPR / AC3) against a REAL Postgres DbContext — the
/// accountability record of an admin GDPR deletion SURVIVES the subject's own erasure.
///
/// <para>The <c>AdminDeleteUserAccount</c> handler emits a scope+subject-id-only snapshot; the production
/// <c>AuditLogBehavior</c> (inner to <c>UnitOfWorkPipelineBehavior</c>) drains it into one append-only
/// <c>AdminActionAudit</c> row that rides the same <c>SaveChangesAsync</c> as the deletion. The row has NO
/// foreign key to the subject, so a subsequent hard-delete of the subject user leaves it intact — it holds
/// the actor's identity + the subject's id + the scope, never the subject's personal data (verifiable in
/// the persisted jsonb).</para>
/// </summary>
[Collection("PostgresCollection")]
public class GdprDeleteAuditSurvivesErasureTests : BaseIntegrationTest
{
    private const string SubjectId = "gdpr-subject-1";
    private const string SubjectEmail = "erase.me@example.test";
    private const string SubjectFirstName = "Erase";
    private const string SubjectLastName = "Me";

    public GdprDeleteAuditSurvivesErasureTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    private static IUserSessionProvider AdminSession() =>
        new TestUserSessionProvider("admin-1", "admin@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);

    private CleansiaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).Options,
            AdminSession(),
            new FixedTenantProvider(tenantId: null));

    // The production nesting for the success path: UnitOfWork (outer, the single commit) → AuditLog
    // (inner, drains the handler snapshot and adds the audit row to the same scoped context) → handler.
    private async Task RunDeleteThroughPipelineAsync(CleansiaDbContext context, IGdprDeletionService deletionService)
    {
        var session = AdminSession();
        var auditContext = new AuditContext();
        var factory = new AuditEntryFactory(session);
        var writer = new DbContextAuditWriter(context, new FixedTenantProvider(null));
        var sink = new OutOfBandAuditFailureSink(
            new SingleDbScopeFactory(Fixture.GetConnectionString()), new FixedTenantProvider(null));

        var handler = new AdminDeleteUserAccount.Handler(session, deletionService, auditContext);
        var audit = new AuditLogBehavior<AdminDeleteUserAccount.Command, BusinessResult>(
            session, auditContext, writer, sink, factory,
            NullLogger<AuditLogBehavior<AdminDeleteUserAccount.Command, BusinessResult>>.Instance);
        var unitOfWork = new UnitOfWorkPipelineBehavior<AdminDeleteUserAccount.Command, BusinessResult>(context);

        var command = new AdminDeleteUserAccount.Command(SubjectId);
        var result = await unitOfWork.Handle(
            command,
            ct => audit.Handle(command, _ => handler.Handle(command, ct), ct),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GdprDelete_Audit_Row_Survives_Subject_Hard_Delete_With_Scope_And_Ids_Only()
    {
        await ResetAsync();

        await using (var seed = NewContext())
        {
            seed.Languages.Add(Language.Create("en", "English"));
            var subject = User.CreateWithPassword(SubjectEmail, "Seed-Password-123", SubjectFirstName, SubjectLastName);
            subject.Id = SubjectId;
            subject.ConfirmEmail();
            subject.Created("seed", DateTimeOffset.UtcNow);
            seed.Users.Add(subject);
            await seed.CommitAsync(CancellationToken.None);
        }

        await using (var ctx = NewContext())
        {
            // A success-returning deletion service stand-in: the deletion itself is covered by the GDPR
            // service's own suite — this test pins the AUDIT-survives-erasure interaction, so the handler's
            // success-path snapshot is what must land atomically.
            await RunDeleteThroughPipelineAsync(ctx, new SucceedingDeletionService());
        }

        // The subject is erased from the database entirely (hard delete) — the lawful worst case for the
        // accountability record: even with the row gone, the audit must remain (no cascading FK).
        await using (var erase = NewContext())
        {
            await erase.Users.IgnoreQueryFilters().Where(u => u.Id == SubjectId).ExecuteDeleteAsync();
        }

        await using var verify = NewContext();
        Assert.Equal(0, await verify.Users.IgnoreQueryFilters().CountAsync(u => u.Id == SubjectId));

        var audit = Assert.Single(await verify.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.Equal("gdpr.user.delete", audit.Action);
        Assert.Equal("User", audit.ResourceType);
        Assert.Equal(SubjectId, audit.ResourceId);
        Assert.Equal("admin-1", audit.ActorId);
        Assert.True(audit.Success);

        // Scope + subject id ONLY — never the erased subject's personal data.
        Assert.NotNull(audit.AfterJson);
        Assert.Contains(SubjectId, audit.AfterJson);
        Assert.Contains("Deletion", audit.AfterJson);
        Assert.DoesNotContain(SubjectEmail, audit.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SubjectFirstName, audit.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(SubjectLastName, audit.AfterJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", audit.AfterJson);
    }

    private async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(Fixture.GetConnectionString());
        await conn.OpenAsync();
        var respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(conn);
    }

    private sealed class SucceedingDeletionService : IGdprDeletionService
    {
        public Task<BusinessResult> DeleteUserAccountAsync(
            string userId,
            string deactivationReason,
            Func<User, (string ProcessedBy, string? Notes)> resolveAuditActor,
            CancellationToken cancellationToken) =>
            Task.FromResult(BusinessResult.Success());
    }

    private sealed class SingleDbScopeFactory(string connectionString)
        : Microsoft.Extensions.DependencyInjection.IServiceScopeFactory, IServiceProvider, Microsoft.Extensions.DependencyInjection.IServiceScope
    {
        public Microsoft.Extensions.DependencyInjection.IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public void Dispose() { }

        public object? GetService(Type serviceType) =>
            serviceType == typeof(CleansiaDbContext)
                ? new CleansiaDbContext(
                    new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(connectionString).Options,
                    new TestUserSessionProvider("admin-1", "admin@cleansia.test"),
                    new FixedTenantProvider(null))
                : null;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }
}
