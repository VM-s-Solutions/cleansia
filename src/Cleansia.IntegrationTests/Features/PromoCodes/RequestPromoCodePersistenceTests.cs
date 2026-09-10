using System.Text.Json;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Features.PromoCodes;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Loyalty;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using Cleansia.Infra.Database.Repositories;
using Cleansia.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;

namespace Cleansia.IntegrationTests.Features.PromoCodes;

[Collection("PostgresCollection")]
public class RequestPromoCodePersistenceTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string Email = "visitor@example.com";
    private static readonly JsonSerializerOptions WireJson = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task First_request_persists_the_code_and_its_email_together()
    {
        await ResetAsync();

        var result = await SendAsync("  VISITOR@Example.COM ");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Accepted);
        await using var verification = NewContext();
        var promo = Assert.Single(await verification.PromoCodes.ToListAsync());
        var outbox = Assert.Single(await verification.OutboxMessages.ToListAsync());
        Assert.Equal(QueueNames.SendEmail, outbox.QueueName);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
        var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(outbox.Body, WireJson);
        Assert.NotNull(envelope);
        Assert.Equal(EmailType.PromoCode, envelope.Payload.EmailType);
        Assert.Equal(Email, envelope.Payload.Email);
        Assert.Equal(promo.Code, envelope.Payload.Code);
    }

    [Fact]
    public async Task Repeated_request_in_a_new_scope_reports_already_sent_without_another_outbox_row()
    {
        await ResetAsync();
        Assert.True((await SendAsync(Email)).IsSuccess);

        var result = await SendAsync("  VISITOR@Example.COM ");

        AssertAlreadySent(result);
        await using var verification = NewContext();
        Assert.Single(await verification.PromoCodes.ToListAsync());
        Assert.Single(await verification.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_first_requests_accept_one_and_report_already_sent_for_the_others()
    {
        await ResetAsync();
        var gate = new LookupGate(participants: 4);

        var results = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => SendAsync(Email, gate)));

        Assert.Single(results, result => result.IsSuccess);
        Assert.All(results.Where(result => result.IsFailure), AssertAlreadySent);
        await using var verification = NewContext();
        Assert.Single(await verification.PromoCodes.ToListAsync());
        Assert.Single(await verification.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Failed_outbox_insert_rolls_back_the_code_so_a_later_request_can_retry()
    {
        await ResetAsync();

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            SendAsync(Email, failOutboxInsert: true));

        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation,
            Assert.IsType<PostgresException>(exception.InnerException).SqlState);
        await using (var verification = NewContext())
        {
            Assert.Empty(await verification.PromoCodes.ToListAsync());
            Assert.Empty(await verification.OutboxMessages.ToListAsync());
        }

        Assert.True((await SendAsync(Email)).IsSuccess);
        await using var retryVerification = NewContext();
        Assert.Single(await retryVerification.PromoCodes.ToListAsync());
        Assert.Single(await retryVerification.OutboxMessages.ToListAsync());
    }

    private async Task<BusinessResult<RequestPromoCode.Response>> SendAsync(
        string email, LookupGate? gate = null, bool failOutboxInsert = false)
    {
        await using var context = NewContext();
        IPromoCodeRepository repository = gate is null
            ? new PromoCodeRepository(context)
            : new GatedPromoCodeRepository(context, gate);
        IPendingDispatch pending = new OutboxPendingDispatch(context);
        if (failOutboxInsert)
        {
            pending = new InvalidQueueDispatch(pending);
        }

        var handler = new RequestPromoCode.Handler(repository, pending);
        var command = new RequestPromoCode.Command(email);
        var pipeline = new UnitOfWorkPipelineBehavior<
            RequestPromoCode.Command, BusinessResult<RequestPromoCode.Response>>(context);
        return await pipeline.Handle(command, ct => handler.Handle(command, ct), CancellationToken.None);
    }

    private static void AssertAlreadySent(BusinessResult<RequestPromoCode.Response> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal("Email", result.Error!.Code);
        Assert.Equal("promo.already_sent", result.Error.Message);
    }

    private CleansiaDbContext NewContext() => new(
        new DbContextOptionsBuilder<CleansiaDbContext>()
            .UseNpgsql(Fixture.GetConnectionString()).Options,
        new TestUserSessionProvider("system", "system@cleansia.test"),
        new TenantProvider(new HttpContextAccessor()));

    private async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(Fixture.GetConnectionString());
        await connection.OpenAsync();
        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToExclude = ["pg_catalog", "information_schema"]
        });
        await respawner.ResetAsync(connection);
    }

    private sealed class GatedPromoCodeRepository(CleansiaDbContext context, LookupGate gate)
        : PromoCodeRepository(context), IPromoCodeRepository
    {
        async Task<PromoCode?> IPromoCodeRepository.GetByCodeAsync(string code, CancellationToken cancellationToken)
        {
            var existing = await base.GetByCodeAsync(code, cancellationToken);
            Assert.Null(existing);
            // Every independent connection has observed absence before any request inserts.
            await gate.WaitAsync(cancellationToken);
            return existing;
        }
    }

    private sealed class LookupGate(int participants)
    {
        private int _remaining = participants;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Decrement(ref _remaining) == 0)
            {
                _ready.TrySetResult();
            }

            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private sealed class InvalidQueueDispatch(IPendingDispatch inner) : IPendingDispatch
    {
        // PostgreSQL rejects varchar(128); the real outbox writer and transaction still execute.
        public void Enqueue<T>(string queueName, T message, string messageKey) =>
            inner.Enqueue(new string('x', 129), message, messageKey);

        public IReadOnlyList<PendingMessage> Drain() => inner.Drain();
    }
}
