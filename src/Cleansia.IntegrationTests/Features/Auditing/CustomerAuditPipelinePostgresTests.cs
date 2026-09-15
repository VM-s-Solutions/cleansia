using System.Security.Claims;
using System.Text.Json;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Extensions;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Infra.Common.Configuration.Interfaces;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Auditing;
using Cleansia.TestUtilities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.Auditing;

/// <summary>
/// ADR-0062 D1/D7 (Verification #3) against a REAL Postgres DbContext — the customer arm of the shipped
/// pipeline, driven by throw-away marked test commands (no production command is marked here; the
/// production commands are marked in a later ticket). The production nesting is reproduced by hand
/// exactly as <see cref="AuditLogBehaviorPostgresTests"/> does for the admin arm:
/// AuditFailureCapture (outer) → OperatorTenantScope → Validation → UnitOfWork → AuditLog (inner) → handler.
///
/// <para>What it proves: a Customer's success row rides the action's commit with the tenant stamped and
/// the request context filled; a rolled-back action leaves no row; a handler refusal and a validation
/// reject each leave one out-of-band row whose <c>ErrorCode</c> is the KEY — on both of the reject's
/// arms, returned as a result or thrown where the response cannot carry one; the latch keeps it to one; an
/// Employee lands nowhere; an Administrator lands in the admin table under the marker's admin label (an
/// administrator's act is an admin act, owner ruling 2026-09-15); an anonymous caller lands in the
/// customer table only where the marker allows it, with <c>ClientAudience</c> filled. For an anonymous
/// market-scoped act the tenant is the operator <c>OperatorTenantScopeBehavior</c> resolved before
/// validation — and a refusal raised BEFORE that resolution has no tenant at all: the sink skips that
/// row with one Warning instead of losing it to a NOT NULL violation logged as an error per probe.</para>
/// </summary>
[Collection("PostgresCollection")]
public class CustomerAuditPipelinePostgresTests : BaseIntegrationTest
{
    private const string CustomerId = "cust-audit-1";
    private const string Ip = "203.0.113.9";
    private const string DeviceLabel = "iPhone 15 / iOS 17.4";
    private const string DeviceId = "device-abc-123";

    public CustomerAuditPipelinePostgresTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [AuditAction("customer.test.act", Audience = AuditAudience.Customer, ResourceType = "Order", AdminAction = "admin.test.act")]
    public sealed record CustomerActCommand(string OrderId) : IRequest<BusinessResult>;

    [AuditAction("customer.test.list", Audience = AuditAudience.Customer, ResourceType = "Order")]
    public sealed record CustomerListCommand(string OrderId) : IRequest<PagedData<string>>;

    [AuditAction("customer.test.guest_act", Audience = AuditAudience.Customer, ResourceType = "Order", AllowsAnonymousActor = true)]
    public sealed record GuestActCommand(string OrderId) : IRequest<BusinessResult>;

    [AuditAction("customer.test.guest_market_act", Audience = AuditAudience.Customer, ResourceType = "Order", AllowsAnonymousActor = true)]
    public sealed record GuestMarketActCommand(string OrderId, string? CountryId) : IRequest<BusinessResult>, IOperatorScopedRequest;

    private sealed class Run
    {
        public IUserSessionProvider Session { get; init; } = CustomerSession();
        public string Audience { get; init; } = JwtAudiences.Customer;
        public IAuditContext AuditContext { get; } = new AuditContext();
    }

    private static IUserSessionProvider Session(string userId, UserProfile role) =>
        new TestUserSessionProvider(userId, $"{userId}@cleansia.test", [new Claim(ClaimTypes.Role, role.ToString())]);

    // A mobile token carries the device it was minted for; the row records that claim, not the header.
    private static IUserSessionProvider CustomerSession() =>
        new TestUserSessionProvider(CustomerId, $"{CustomerId}@cleansia.test",
        [
            new Claim(ClaimTypes.Role, UserProfile.Customer.ToString()),
            new Claim(AuthExtensions.DeviceIdClaimType, DeviceId)
        ]);

    private static IUserSessionProvider AnonymousSession() => new TestUserSessionProvider([]);

    private CleansiaDbContext NewContext(IUserSessionProvider? session = null) =>
        new(new DbContextOptionsBuilder<CleansiaDbContext>().UseNpgsql(Fixture.GetConnectionString()).Options,
            session ?? CustomerSession(),
            new FixedTenantProvider(TestTenants.Default));

    private IAuditFailureSink Sink(ITenantProvider? tenantProvider = null, ILogger<OutOfBandAuditFailureSink>? logger = null) =>
        new OutOfBandAuditFailureSink(
            new SingleDbScopeFactory(Fixture.GetConnectionString()),
            tenantProvider ?? new FixedTenantProvider(TestTenants.Default),
            logger ?? NullLogger<OutOfBandAuditFailureSink>.Instance);

    private static AuditEntryFactory Factory(Run run) =>
        new(run.Session, new TestRequestMetadataProvider(Ip, DeviceLabel, DeviceId), Host(run));

    private static IHostAudienceProvider Host(Run run) => new HostAudienceProvider(run.Audience);

    // UnitOfWork (outer, the single commit) → AuditLog (inner) → handler: the success placement.
    private async Task<BusinessResult> RunInnerPipelineAsync<TRequest>(
        CleansiaDbContext context,
        Run run,
        TRequest command,
        IAuditWriter writer,
        RequestHandlerDelegate<BusinessResult> handler)
        where TRequest : IRequest<BusinessResult>
    {
        var audit = new AuditLogBehavior<TRequest, BusinessResult>(
            run.Session, Host(run), run.AuditContext, writer, Sink(), Factory(run),
            NullLogger<AuditLogBehavior<TRequest, BusinessResult>>.Instance);
        var unitOfWork = new UnitOfWorkPipelineBehavior<TRequest, BusinessResult>(context);

        return await unitOfWork.Handle(command, ct => audit.Handle(command, handler, ct), CancellationToken.None);
    }

    // The FULL production nesting, both audit behaviors sharing ONE scoped AuditContext (the latch).
    private async Task<TResponse> RunFullPipelineAsync<TRequest, TResponse>(
        CleansiaDbContext context,
        Run run,
        TRequest command,
        IAuditWriter writer,
        IValidator<TRequest> validator,
        RequestHandlerDelegate<TResponse> handler)
        where TRequest : IRequest<TResponse>
    {
        var sink = Sink();
        var factory = Factory(run);

        var failureCapture = new AuditFailureCaptureBehavior<TRequest, TResponse>(
            run.Session, Host(run), run.AuditContext, sink, factory,
            NullLogger<AuditFailureCaptureBehavior<TRequest, TResponse>>.Instance);
        var validation = new ValidationPipelineBehavior<TRequest, TResponse>(
            [validator], NullLogger<ValidationPipelineBehavior<TRequest, TResponse>>.Instance);
        var unitOfWork = new UnitOfWorkPipelineBehavior<TRequest, TResponse>(context);
        var audit = new AuditLogBehavior<TRequest, TResponse>(
            run.Session, Host(run), run.AuditContext, writer, sink, factory,
            NullLogger<AuditLogBehavior<TRequest, TResponse>>.Instance);

        return await failureCapture.Handle(command,
            ct1 => validation.Handle(command,
                ct2 => unitOfWork.Handle(command,
                    ct3 => audit.Handle(command, handler, ct3), ct2), ct1),
            CancellationToken.None);
    }

    // The production nesting of an anonymous MARKET-scoped request: the operator scope resolves the ambient
    // tenant between the outer failure capture and validation. ONE tenant provider is shared by the scope
    // behavior, the sink and the writer, as the request scope shares it in production.
    private async Task<BusinessResult> RunMarketPipelineAsync<TRequest>(
        CleansiaDbContext context,
        Run run,
        TRequest command,
        ITenantProvider tenantProvider,
        OperatorResolution resolution,
        IValidator<TRequest> validator,
        RequestHandlerDelegate<BusinessResult> handler,
        List<(LogLevel Level, string Message)> behaviorEntries,
        List<(LogLevel Level, string Message)> sinkEntries)
        where TRequest : IRequest<BusinessResult>
    {
        var sink = Sink(tenantProvider, new CapturingLogger<OutOfBandAuditFailureSink>(sinkEntries));
        var factory = Factory(run);
        var writer = new DbContextAuditWriter(context, tenantProvider);

        var failureCapture = new AuditFailureCaptureBehavior<TRequest, BusinessResult>(
            run.Session, Host(run), run.AuditContext, sink, factory,
            new CapturingLogger<AuditFailureCaptureBehavior<TRequest, BusinessResult>>(behaviorEntries));
        var operatorScope = new OperatorTenantScopeBehavior<TRequest, BusinessResult>(tenantProvider, new FixedOperatorResolver(resolution));
        var validation = new ValidationPipelineBehavior<TRequest, BusinessResult>(
            [validator], NullLogger<ValidationPipelineBehavior<TRequest, BusinessResult>>.Instance);
        var unitOfWork = new UnitOfWorkPipelineBehavior<TRequest, BusinessResult>(context);
        var audit = new AuditLogBehavior<TRequest, BusinessResult>(
            run.Session, Host(run), run.AuditContext, writer, sink, factory,
            new CapturingLogger<AuditLogBehavior<TRequest, BusinessResult>>(behaviorEntries));

        return await failureCapture.Handle(command,
            ct1 => operatorScope.Handle(command,
                ct2 => validation.Handle(command,
                    ct3 => unitOfWork.Handle(command,
                        ct4 => audit.Handle(command, handler, ct4), ct3), ct2), ct1),
            CancellationToken.None);
    }

    private sealed class RejectingValidator<TRequest> : AbstractValidator<TRequest>
    {
        public RejectingValidator()
        {
            RuleFor(x => x)
                .Must(_ => false)
                .WithErrorCode("TotalPrice")
                .WithMessage(BusinessErrorMessage.TotalPriceNotMatch);
            RuleFor(x => x)
                .Must(_ => false)
                .WithErrorCode("CurrencyId")
                .WithMessage(BusinessErrorMessage.Required);
        }
    }

    private sealed class PassingValidator<TRequest> : AbstractValidator<TRequest>;

    private static RequestHandlerDelegate<BusinessResult> ActionThatAddsAnOutboxRow(CleansiaDbContext ctx, string orderId = "ORD-1") => _ =>
    {
        ctx.OutboxMessages.Add(OutboxMessage.Create(QueueNames.GenerateReceipt, $"receipt:{orderId}", "{}", null));
        return Task.FromResult(BusinessResult.Success());
    };

    private static async Task<List<CustomerActionAudit>> CustomerRows(CleansiaDbContext ctx) =>
        await ctx.CustomerActionAudits.IgnoreQueryFilters().ToListAsync();

    private static async Task<int> AdminRowCount(CleansiaDbContext ctx) =>
        await ctx.AdminActionAudits.IgnoreQueryFilters().CountAsync();

    private static async Task<int> OutboxCount(CleansiaDbContext ctx) =>
        await ctx.OutboxMessages.IgnoreQueryFilters().CountAsync();

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
        await SeedTenantRegistryAsync(conn);
    }

    // ── success rides the commit ───────────────────────────────────────────────

    [Fact]
    public async Task A_Customer_Success_Writes_One_Row_In_The_Same_Transaction_With_Tenant_Audience_And_Request_Context()
    {
        await ResetAsync();
        var run = new Run();

        await using (var ctx = NewContext())
        {
            run.AuditContext.RecordEvidence("Order", "ORD-1", new { feeRate = 0.5m, hasBeenAccepted = true });
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));
            var result = await RunInnerPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), writer, ActionThatAddsAnOutboxRow(ctx));
            Assert.True(result.IsSuccess);
        }

        await using var verify = NewContext();
        var row = Assert.Single(await CustomerRows(verify));
        Assert.True(row.Success);
        Assert.Null(row.ErrorCode);
        Assert.Equal("customer.test.act", row.Action);
        Assert.Equal(CustomerId, row.UserId);
        Assert.Equal(TestTenants.Default, row.TenantId);
        Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
        Assert.Equal(Ip, row.IpAddress);
        Assert.Equal(DeviceLabel, row.DeviceLabel);
        Assert.Equal(DeviceId, row.DeviceId);
        Assert.Equal("Order", row.ResourceType);
        Assert.Equal("ORD-1", row.ResourceId);
        var payload = JsonDocument.Parse(row.PayloadJson!).RootElement;
        Assert.Equal(0.5m, payload.GetProperty("feeRate").GetDecimal());
        Assert.True(payload.GetProperty("hasBeenAccepted").GetBoolean());
        Assert.Equal(0, await AdminRowCount(verify));
        // The action row and its audit row committed together (one SaveChangesAsync).
        Assert.Equal(1, await OutboxCount(verify));
    }

    [Fact]
    public async Task A_Rolled_Back_Action_Leaves_No_Customer_Row()
    {
        await ResetAsync();
        var run = new Run();

        await using (var ctx = NewContext())
        {
            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                RunInnerPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), new PoisonAuditWriter(ctx), ActionThatAddsAnOutboxRow(ctx)));
        }

        await using var verify = NewContext();
        Assert.Empty(await CustomerRows(verify));
        Assert.Equal(0, await OutboxCount(verify));
    }

    // ── refusals are written out-of-band, with the KEY ─────────────────────────

    [Fact]
    public async Task A_Handler_Refusal_Writes_One_OutOfBand_Row_With_The_Key_And_Commits_Nothing_Else()
    {
        await ResetAsync();
        var run = new Run();

        await using (var ctx = NewContext())
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));
            var result = await RunFullPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), writer, new PassingValidator<CustomerActCommand>(), _ =>
            {
                ctx.OutboxMessages.Add(OutboxMessage.Create(QueueNames.GenerateReceipt, "receipt:ORD-1", "{}", null));
                return Task.FromResult(BusinessResult.Failure(new Error("OrderId", BusinessErrorMessage.OrderInProgressCannotCancel)));
            });
            Assert.True(result.IsFailure);
        }

        await using var verify = NewContext();
        var row = Assert.Single(await CustomerRows(verify));
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.OrderInProgressCannotCancel, row.ErrorCode);
        Assert.Equal(CustomerId, row.UserId);
        Assert.Equal("ORD-1", row.ResourceId);
        Assert.Equal(TestTenants.Default, row.TenantId);
        Assert.Null(row.PayloadJson);
        Assert.Equal(0, await OutboxCount(verify));
    }

    [Fact]
    public async Task A_Validation_Reject_Writes_Exactly_One_OutOfBand_Row_With_The_First_Rules_Key()
    {
        await ResetAsync();
        var run = new Run();

        await using (var ctx = NewContext())
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));
            var result = await RunFullPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), writer,
                new RejectingValidator<CustomerActCommand>(), ActionThatAddsAnOutboxRow(ctx));
            Assert.True(result.IsFailure);
        }

        await using var verify = NewContext();
        var row = Assert.Single(await CustomerRows(verify));
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.TotalPriceNotMatch, row.ErrorCode);
        Assert.Equal("customer.test.act", row.Action);
        Assert.Equal(Ip, row.IpAddress);
        Assert.Equal(0, await OutboxCount(verify));
    }

    [Fact]
    public async Task A_Validation_Reject_That_Travels_As_A_Throw_Writes_Exactly_One_OutOfBand_Row_With_The_First_Rules_Key()
    {
        await ResetAsync();
        var run = new Run();

        await using (var ctx = NewContext())
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));
            await Assert.ThrowsAsync<RequestValidationException>(() =>
                RunFullPipelineAsync(ctx, run, new CustomerListCommand("ORD-1"), writer,
                    new RejectingValidator<CustomerListCommand>(), _ =>
                    {
                        ctx.OutboxMessages.Add(OutboxMessage.Create(QueueNames.GenerateReceipt, "receipt:ORD-1", "{}", null));
                        return Task.FromResult(new PagedData<string>(1, 50, 0, []));
                    }));
        }

        await using var verify = NewContext();
        var row = Assert.Single(await CustomerRows(verify));
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.TotalPriceNotMatch, row.ErrorCode);
        Assert.Equal("customer.test.list", row.Action);
        Assert.Equal(Ip, row.IpAddress);
        Assert.Equal(0, await OutboxCount(verify));
    }

    [Fact]
    public async Task A_Thrown_Handler_Writes_One_OutOfBand_Row_With_The_Exception_Type_Then_Rethrows()
    {
        await ResetAsync();
        var run = new Run();

        await using (var ctx = NewContext())
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RunFullPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), writer, new PassingValidator<CustomerActCommand>(),
                    _ => Task.FromException<BusinessResult>(new InvalidOperationException("stripe down"))));
        }

        await using var verify = NewContext();
        var row = Assert.Single(await CustomerRows(verify));
        Assert.False(row.Success);
        Assert.Equal(nameof(InvalidOperationException), row.ErrorCode);
    }

    [Fact]
    public async Task A_Commit_Throw_After_A_Successful_Handler_Writes_Exactly_One_OutOfBand_Failure_Row()
    {
        await ResetAsync();
        var run = new Run();

        await using (var ctx = NewContext())
        {
            await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                RunFullPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), new PoisonAuditWriter(ctx),
                    new PassingValidator<CustomerActCommand>(), ActionThatAddsAnOutboxRow(ctx)));
        }

        await using var verify = NewContext();
        Assert.Equal(0, await OutboxCount(verify));
        var row = Assert.Single(await CustomerRows(verify));
        Assert.False(row.Success);
        Assert.Equal(nameof(DbUpdateException), row.ErrorCode);
    }

    // ── who lands where ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_Employee_On_The_Partner_Host_Lands_Nowhere()
    {
        await ResetAsync();
        var run = new Run { Session = Session("emp-1", UserProfile.Employee), Audience = JwtAudiences.Partner };

        await using (var ctx = NewContext(run.Session))
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));
            var success = await RunFullPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), writer,
                new PassingValidator<CustomerActCommand>(), ActionThatAddsAnOutboxRow(ctx));
            Assert.True(success.IsSuccess);

            var refused = await RunFullPipelineAsync(ctx, run, new CustomerActCommand("ORD-2"), writer,
                new RejectingValidator<CustomerActCommand>(), ActionThatAddsAnOutboxRow(ctx, "ORD-2"));
            Assert.True(refused.IsFailure);
        }

        await using var verify = NewContext();
        Assert.Empty(await CustomerRows(verify));
        Assert.Equal(0, await AdminRowCount(verify));
        Assert.Equal(1, await OutboxCount(verify));
    }

    [Fact]
    public async Task An_Administrator_Running_A_Customer_Marked_Command_Lands_In_The_Admin_Table_Only_Under_The_Admin_Label()
    {
        await ResetAsync();
        var run = new Run { Session = Session("admin-1", UserProfile.Administrator), Audience = JwtAudiences.Admin };

        await using (var ctx = NewContext(run.Session))
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));
            var result = await RunFullPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), writer,
                new PassingValidator<CustomerActCommand>(), ActionThatAddsAnOutboxRow(ctx));
            Assert.True(result.IsSuccess);
        }

        await using var verify = NewContext();
        Assert.Empty(await CustomerRows(verify));
        var admin = Assert.Single(await verify.AdminActionAudits.IgnoreQueryFilters().ToListAsync());
        Assert.Equal("admin.test.act", admin.Action);
        Assert.Equal("admin-1", admin.ActorId);
        Assert.Equal("ORD-1", admin.ResourceId);
    }

    [Fact]
    public async Task An_Anonymous_Caller_Lands_In_The_Customer_Table_Only_Where_The_Marker_Allows_A_Guest()
    {
        await ResetAsync();
        var run = new Run { Session = AnonymousSession() };

        await using (var ctx = NewContext(run.Session))
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));

            var withoutGuest = await RunFullPipelineAsync(ctx, run, new CustomerActCommand("ORD-1"), writer,
                new PassingValidator<CustomerActCommand>(), ActionThatAddsAnOutboxRow(ctx));
            Assert.True(withoutGuest.IsSuccess);

            var guestRun = new Run { Session = AnonymousSession() };
            var withGuest = await RunFullPipelineAsync(ctx, guestRun, new GuestActCommand("ORD-2"), writer,
                new PassingValidator<GuestActCommand>(), ActionThatAddsAnOutboxRow(ctx, "ORD-2"));
            Assert.True(withGuest.IsSuccess);
        }

        await using var verify = NewContext();
        var row = Assert.Single(await CustomerRows(verify));
        Assert.Equal("customer.test.guest_act", row.Action);
        Assert.Null(row.UserId);
        Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
        Assert.Equal(TestTenants.Default, row.TenantId);
        Assert.Equal("ORD-2", row.ResourceId);
        Assert.Equal(0, await AdminRowCount(verify));
    }

    // ── the anonymous market-scoped act and its tenant ─────────────────────────

    [Fact]
    public async Task An_Anonymous_Refusal_Raised_Before_The_Operator_Is_Resolved_Is_Skipped_With_One_Warning_Not_Lost_As_An_Error()
    {
        await ResetAsync();
        var run = new Run { Session = AnonymousSession() };
        var ambientTenant = new FixedTenantProvider(null);
        var behaviorEntries = new List<(LogLevel Level, string Message)>();
        var sinkEntries = new List<(LogLevel Level, string Message)>();

        await using (var ctx = NewContext(run.Session))
        {
            var result = await RunMarketPipelineAsync(ctx, run, new GuestMarketActCommand("ORD-1", "XX"), ambientTenant,
                OperatorResolution.NotAMarket, new PassingValidator<GuestMarketActCommand>(), ActionThatAddsAnOutboxRow(ctx),
                behaviorEntries, sinkEntries);

            Assert.True(result.IsFailure);
            Assert.Equal(BusinessErrorMessage.CountryNotServiced, ((IValidationResult)result).Errors.Single().Message);
        }

        await using var verify = NewContext();
        Assert.Empty(await CustomerRows(verify));
        Assert.Equal(0, await OutboxCount(verify));
        Assert.DoesNotContain(behaviorEntries, e => e.Level == LogLevel.Error);
        var warning = Assert.Single(sinkEntries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("customer.test.guest_market_act", warning.Message);
        Assert.Contains(BusinessErrorMessage.CountryNotServiced, warning.Message);
    }

    [Fact]
    public async Task An_Anonymous_Refusal_Raised_After_The_Operator_Is_Resolved_Is_Stamped_With_That_Operator()
    {
        await ResetAsync();
        var run = new Run { Session = AnonymousSession() };
        var ambientTenant = new FixedTenantProvider(null);
        var behaviorEntries = new List<(LogLevel Level, string Message)>();
        var sinkEntries = new List<(LogLevel Level, string Message)>();

        await using (var ctx = NewContext(run.Session))
        {
            var result = await RunMarketPipelineAsync(ctx, run, new GuestMarketActCommand("ORD-1", "CZE"), ambientTenant,
                new OperatorResolution(true, TestTenants.Default), new RejectingValidator<GuestMarketActCommand>(), ActionThatAddsAnOutboxRow(ctx),
                behaviorEntries, sinkEntries);

            Assert.True(result.IsFailure);
        }

        await using var verify = NewContext();
        var row = Assert.Single(await CustomerRows(verify));
        Assert.False(row.Success);
        Assert.Equal(BusinessErrorMessage.TotalPriceNotMatch, row.ErrorCode);
        Assert.Null(row.UserId);
        Assert.Equal(TestTenants.Default, row.TenantId);
        Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
        Assert.Equal(0, await OutboxCount(verify));
        Assert.Empty(sinkEntries);
        Assert.DoesNotContain(behaviorEntries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task A_Registration_Style_Act_Carries_The_Snapshots_ActorUserId_When_The_Session_Has_None()
    {
        await ResetAsync();
        var run = new Run { Session = AnonymousSession() };

        await using (var ctx = NewContext(run.Session))
        {
            var writer = new DbContextAuditWriter(ctx, new FixedTenantProvider(TestTenants.Default));
            var result = await RunFullPipelineAsync(ctx, run, new GuestActCommand("ORD-1"), writer,
                new PassingValidator<GuestActCommand>(), _ =>
                {
                    run.AuditContext.RecordEvidence("User", "new-user-9", new { method = "email" }, actorUserId: "new-user-9");
                    return Task.FromResult(BusinessResult.Success());
                });
            Assert.True(result.IsSuccess);
        }

        await using var verify = NewContext();
        var row = Assert.Single(await CustomerRows(verify));
        Assert.Equal("new-user-9", row.UserId);
        Assert.Equal("User", row.ResourceType);
        Assert.Equal("new-user-9", row.ResourceId);
        Assert.Equal(JwtAudiences.Customer, row.ClientAudience);
    }

    /// <summary>
    /// A writer that adds a customer row too wide for its column, so the single SaveChangesAsync throws
    /// (proving the action and the audit insert ride one transaction).
    /// </summary>
    private sealed class PoisonAuditWriter(CleansiaDbContext context) : IAuditWriter
    {
        public void Add(AdminActionAudit entry) => throw new NotSupportedException();

        public void Add(CustomerActionAudit entry)
        {
            var poison = CustomerActionAudit.Create(
                userId: null, clientAudience: new string('x', 60), ipAddress: null, deviceLabel: null, deviceId: null,
                action: "customer.test.act", resourceType: null, resourceId: null, success: true, errorCode: null,
                payloadJson: null, correlationId: null);
            poison.TenantId = TestTenants.Default;
            context.CustomerActionAudits.Add(poison);
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
                    new TestUserSessionProvider("cust-audit-1", "cust@cleansia.test"),
                    new FixedTenantProvider(TestTenants.Default))
                : null;
    }

    private sealed class FixedTenantProvider(string? tenantId) : ITenantProvider
    {
        private string? _tenantId = tenantId;
        public string? GetCurrentTenantId() => _tenantId;
        public void SetTenantOverride(string tenantId) => _tenantId = tenantId;
        public void ClearTenantOverride() => _tenantId = null;
    }

    private sealed class FixedOperatorResolver(OperatorResolution resolution) : IOperatorTenantResolver
    {
        public Task<OperatorResolution> ResolveAsync(string? countryId, CancellationToken cancellationToken) => Task.FromResult(resolution);
    }

    private sealed class CapturingLogger<T>(List<(LogLevel Level, string Message)> entries) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => entries.Add((logLevel, formatter(state, exception)));
    }
}
