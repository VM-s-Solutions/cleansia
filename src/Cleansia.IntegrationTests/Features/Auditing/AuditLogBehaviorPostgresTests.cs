using System.Security.Claims;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Auditing;
using Cleansia.TestUtilities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Auditing;

/// <summary>
/// ADR-0012 D2/D2.1/D2.2 against a REAL Postgres DbContext (Testcontainers) — the only place the
/// atomicity and out-of-band-sink behavior is provable on the production provider.
///
/// <para>TC-AUDIT-ATOMIC: a successful admin command writes exactly one AdminActionAudit row in the SAME
/// transaction as the action (the outer UnitOfWorkPipelineBehavior's single SaveChangesAsync); a forced
/// audit-insert failure rolls the ACTION back (no orphan success row, no action row).</para>
///
/// <para>TC-AUDIT-FAILURE: a business-failure and a thrown handler each produce a Success=false row via
/// the out-of-band sink (its OWN committed scope), which survives even though the action transaction
/// committed nothing.</para>
/// </summary>
[Collection("PostgresCollection")]
public class AuditLogBehaviorPostgresTests : BaseIntegrationTest
{
    public AuditLogBehaviorPostgresTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    public sealed record AdminRefundOrderCommand(string OrderId) : IRequest<BusinessResult>;

    private CleansiaDbContext NewContext() =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).Options,
            AdminSession(),
            new FixedTenantProvider(tenantId: null));

    private static IUserSessionProvider AdminSession() =>
        new TestUserSessionProvider("admin-1", "admin@cleansia.test",
            [new Claim(ClaimTypes.Role, UserProfile.Administrator.ToString())]);

    // The pipeline as production nests it: UnitOfWork (outer, owns the single commit) → AuditLog (inner,
    // adds the success row to the same scoped context) → handler (adds the action row to that context).
    private async Task<BusinessResult> RunThroughPipelineAsync(
        CleansiaDbContext context,
        IAuditWriter writer,
        IAuditFailureSink sink,
        RequestHandlerDelegate<BusinessResult> handler)
    {
        var session = AdminSession();
        var audit = new AuditLogBehavior<AdminRefundOrderCommand, BusinessResult>(
            session, new AuditContext(), writer, sink, new AuditEntryFactory(session),
            NullLogger<AuditLogBehavior<AdminRefundOrderCommand, BusinessResult>>.Instance);
        var unitOfWork = new UnitOfWorkPipelineBehavior<AdminRefundOrderCommand, BusinessResult>(context);

        return await unitOfWork.Handle(
            new AdminRefundOrderCommand("ORD-1"),
            ct => audit.Handle(new AdminRefundOrderCommand("ORD-1"), handler, ct),
            CancellationToken.None);
    }

    // The FULL production nesting for the two failure shapes the inner AuditLog cannot see:
    // AuditFailureCapture (outer) → Validation → UnitOfWork → AuditLog (inner) → handler. The outer
    // AuditFailureCapture observes a validation reject (short-circuited before UnitOfWork/AuditLog) and a
    // commit-throw (raised from the OUTER UnitOfWork after AuditLog returned), and writes the failure row
    // out-of-band. AuditFailureCapture and AuditLog share ONE scoped AuditContext (the production latch).
    private async Task<BusinessResult> RunThroughFullPipelineAsync(
        CleansiaDbContext context,
        IAuditWriter writer,
        IAuditFailureSink sink,
        IValidator<AdminRefundOrderCommand> validator,
        RequestHandlerDelegate<BusinessResult> handler)
    {
        var session = AdminSession();
        var auditContext = new AuditContext();
        var factory = new AuditEntryFactory(session);

        var failureCapture = new AuditFailureCaptureBehavior<AdminRefundOrderCommand, BusinessResult>(
            session, auditContext, sink, factory,
            NullLogger<AuditFailureCaptureBehavior<AdminRefundOrderCommand, BusinessResult>>.Instance);
        var validation = new ValidationPipelineBehavior<AdminRefundOrderCommand, BusinessResult>(
            [validator], NullLogger<ValidationPipelineBehavior<AdminRefundOrderCommand, BusinessResult>>.Instance);
        var unitOfWork = new UnitOfWorkPipelineBehavior<AdminRefundOrderCommand, BusinessResult>(context);
        var audit = new AuditLogBehavior<AdminRefundOrderCommand, BusinessResult>(
            session, auditContext, writer, sink, factory,
            NullLogger<AuditLogBehavior<AdminRefundOrderCommand, BusinessResult>>.Instance);

        var command = new AdminRefundOrderCommand("ORD-1");
        return await failureCapture.Handle(command,
            ct1 => validation.Handle(command,
                ct2 => unitOfWork.Handle(command,
                    ct3 => audit.Handle(command, handler, ct3), ct2), ct1),
            CancellationToken.None);
    }

    private sealed class RejectingValidator : AbstractValidator<AdminRefundOrderCommand>
    {
        public RejectingValidator()
        {
            RuleFor(x => x.OrderId)
                .Must(_ => false)
                .WithErrorCode("admin.refund.rejected")
                .WithMessage("rejected by validator");
        }
    }

    private sealed class PassingValidator : AbstractValidator<AdminRefundOrderCommand>
    {
    }

    private IAuditFailureSink Sink() =>
        new OutOfBandAuditFailureSink(new SingleDbScopeFactory(Fixture.GetConnectionString()), new FixedTenantProvider(null));

    private static async Task<int> AuditRowCount(CleansiaDbContext ctx) =>
        await ctx.AdminActionAudits.IgnoreQueryFilters().CountAsync();

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

    [Fact]
    public async Task TC_AUDIT_ATOMIC_Success_Writes_One_Row_In_The_Same_Transaction_As_The_Action()
    {
        await ResetAsync();

        await using (var ctx = NewContext())
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(null));
            var result = await RunThroughPipelineAsync(ctx, writer, Sink(), ct =>
            {
                ctx.OutboxMessages.Add(OutboxMessage.Create(QueueNames.GenerateReceipt, "receipt:ORD-1", "{}", null));
                return Task.FromResult(BusinessResult.Success());
            });
            Assert.True(result.IsSuccess);
        }

        await using var verify = NewContext();
        var audit = Assert.Single(await verify.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.True(audit.Success);
        Assert.Equal("AdminRefundOrder", audit.Action);
        Assert.Equal("admin-1", audit.ActorId);
        Assert.Equal("ORD-1", audit.ResourceId);
        // The action row and its audit row committed together (one SaveChangesAsync).
        Assert.Equal(1, await verify.OutboxMessages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task TC_AUDIT_ATOMIC_A_Forced_Audit_Insert_Failure_Rolls_The_Action_Back()
    {
        await ResetAsync();

        await using (var ctx = NewContext())
        {
            // A writer that adds an oversized ActorId (> the 26-char column) so the single SaveChangesAsync
            // throws — proving the audit insert and the action share one transaction (both must roll back).
            var poisonWriter = new PoisonAuditWriter(ctx);
            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                RunThroughPipelineAsync(ctx, poisonWriter, Sink(), ct =>
                {
                    ctx.OutboxMessages.Add(OutboxMessage.Create(QueueNames.GenerateReceipt, "receipt:ORD-1", "{}", null));
                    return Task.FromResult(BusinessResult.Success());
                }));
        }

        await using var verify = NewContext();
        Assert.Equal(0, await AuditRowCount(verify));
        Assert.Equal(0, await verify.OutboxMessages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task TC_AUDIT_FAILURE_A_Business_Failure_Writes_A_Success_False_Row_OutOfBand()
    {
        await ResetAsync();

        await using (var ctx = NewContext())
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(null));
            var result = await RunThroughPipelineAsync(ctx, writer, Sink(), ct =>
                Task.FromResult(BusinessResult.Failure(new Error("refund.too_large", "exceeds total"))));
            Assert.True(result.IsFailure);
        }

        await using var verify = NewContext();
        var audit = Assert.Single(await verify.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.False(audit.Success);
        Assert.Equal("refund.too_large", audit.ErrorCode);
        Assert.Equal("AdminRefundOrder", audit.Action);
    }

    [Fact]
    public async Task TC_AUDIT_FAILURE_A_Thrown_Handler_Writes_A_Success_False_Row_OutOfBand_Then_Rethrows()
    {
        await ResetAsync();

        await using (var ctx = NewContext())
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(null));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RunThroughPipelineAsync(ctx, writer, Sink(),
                    _ => Task.FromException<BusinessResult>(new InvalidOperationException("boom"))));
        }

        await using var verify = NewContext();
        var audit = Assert.Single(await verify.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.False(audit.Success);
        Assert.Equal(nameof(InvalidOperationException), audit.ErrorCode);
    }

    [Fact]
    public async Task TC_AUDIT_FAILURE_A_ValidatorRejected_Admin_Command_Writes_A_Success_False_Row_OutOfBand()
    {
        await ResetAsync();

        await using (var ctx = NewContext())
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(null));
            var result = await RunThroughFullPipelineAsync(ctx, writer, Sink(), new RejectingValidator(), ct =>
            {
                // The handler must never run on a validation reject — proves the row came from the
                // out-of-band sink (outer AuditFailureCapture), not the in-band writer.
                ctx.OutboxMessages.Add(OutboxMessage.Create(QueueNames.GenerateReceipt, "receipt:ORD-1", "{}", null));
                return Task.FromResult(BusinessResult.Success());
            });
            Assert.True(result.IsFailure);
        }

        await using var verify = NewContext();
        var audit = Assert.Single(await verify.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.False(audit.Success);
        // ValidationPipelineBehavior collapses the rule failures into the ValidationResult sentinel
        // (BusinessResult.Error == IValidationResult.ValidationError), so the recorded ErrorCode is the
        // validation classification — the row marks the action a FAILURE, the trail is no longer empty.
        Assert.Equal("ValidationError", audit.ErrorCode);
        Assert.Equal("AdminRefundOrder", audit.Action);
        // The action transaction never committed (the handler never ran).
        Assert.Equal(0, await verify.OutboxMessages.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task TC_AUDIT_FAILURE_A_CommitThrow_On_A_Successful_Admin_Action_Writes_A_Success_False_Row_OutOfBand()
    {
        await ResetAsync();

        await using (var ctx = NewContext())
        {
            // The handler returns success and the inner AuditLog adds its in-band success row; the
            // PoisonAuditWriter makes the OUTER UnitOfWork.CommitAsync (single SaveChangesAsync) throw —
            // the commit-throw the inner AuditLog cannot catch (it already returned). The outer
            // AuditFailureCapture observes the propagating exception and writes the failure row out-of-band.
            var poisonWriter = new PoisonAuditWriter(ctx);
            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                RunThroughFullPipelineAsync(ctx, poisonWriter, Sink(), new PassingValidator(), ct =>
                {
                    ctx.OutboxMessages.Add(OutboxMessage.Create(QueueNames.GenerateReceipt, "receipt:ORD-1", "{}", null));
                    return Task.FromResult(BusinessResult.Success());
                }));
        }

        await using var verify = NewContext();
        // The action rolled back (no outbox row); the out-of-band failure row survives in its own scope.
        Assert.Equal(0, await verify.OutboxMessages.IgnoreQueryFilters().CountAsync());
        var audit = Assert.Single(await verify.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.False(audit.Success);
        Assert.Equal(nameof(DbUpdateException), audit.ErrorCode);
        Assert.Equal("AdminRefundOrder", audit.Action);
    }

    // A writer that adds an audit row with an oversized ActorId so the SaveChangesAsync throws (proving
    // the action and the audit insert ride one transaction).
    private sealed class PoisonAuditWriter(CleansiaDbContext context) : IAuditWriter
    {
        public void Add(AdminActionAudit entry)
        {
            context.AdminActionAudits.Add(new AdminActionAudit
            {
                ActorId = new string('x', 40),
                Action = "AdminRefundOrder",
                ActorProfile = UserProfile.Administrator,
                Success = true
            });
        }
    }

    private sealed class SingleDbScopeFactory(string connectionString) : IServiceScopeFactory, IServiceProvider, IServiceScope
    {
        public IServiceScope CreateScope() => this;
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
