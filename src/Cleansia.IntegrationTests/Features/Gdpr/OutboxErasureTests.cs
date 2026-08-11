using System.Text.Json;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
using Cleansia.Core.Domain.Outbox;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Infra.Common.Validations;
using Cleansia.Infra.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestConstants = Cleansia.TestUtilities.Constants;

namespace Cleansia.IntegrationTests.Features.Gdpr;

/// <summary>
/// T-0586 — the outbox half of an erasure, through <c>GdprDeletionService</c>'s REAL query shape.
///
/// <para>Real Postgres for two reasons SQLite cannot answer. The match is a <c>Contains</c> narrowing plus a
/// prefix decision, and only the real provider says what that translates to. And an
/// <see cref="OutboxMessage"/> is stamped from the tenant carried in the envelope the producer wrote, so a
/// row enqueued for a tenanted user routinely carries a tenant the erasing request does not — the walk has
/// to read past the global filter, and a fixture whose rows are all null-stamped passes over exactly that
/// bug. The subject's row here is stamped with a real tenant id the erasing request does not carry.</para>
///
/// <para>The bystander's user id deliberately CONTAINS the subject's, so the narrowing hands his row to the
/// decision and only the frozen per-subject key prefix keeps it.</para>
/// </summary>
[Collection("PostgresCollection")]
public class OutboxErasureTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string EnqueueTenantId = "tenant-alpha";
    private const string BystanderUserId = "x-" + TestConstants.TestUserSession.TestUserId;
    private const string BystanderEmail = "tomas.svoboda@cleansia.test";

    private static readonly JsonSerializerOptions QueueJson =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task Erasure_Removes_The_Subjects_Send_Email_Rows_Even_When_They_Carry_Another_Tenant()
    {
        await TestMethod(
            arrange: SeedUserWithOutboxRows,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new DeleteUserAccount.Command());
            },
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess);

                var rows = await context.OutboxMessages.IgnoreQueryFilters().ToListAsync();

                Assert.DoesNotContain(rows, r =>
                    r.Body.Contains(TestConstants.TestUserSession.TestUserEmail, StringComparison.Ordinal));
                Assert.DoesNotContain(rows, r =>
                    r.QueueName == QueueNames.SendEmail
                    && r.MessageKey.Contains($":{TestConstants.TestUserSession.TestUserId}:", StringComparison.Ordinal));

                Assert.Contains(rows, r => r.QueueName == QueueNames.NotificationsDispatch);
                Assert.Contains(rows, r =>
                    r.QueueName == QueueNames.SendEmail
                    && r.Body.Contains(BystanderEmail, StringComparison.Ordinal));
            });
    }

    private static async Task SeedUserWithOutboxRows(CleansiaDbContext context)
    {
        if (!await context.Languages.AnyAsync())
        {
            context.Languages.Add(Language.Create("en", "English"));
            await context.SaveChangesAsync();
        }

        var user = User.CreateWithPassword(
            email: TestConstants.TestUserSession.TestUserEmail,
            password: TestConstants.TestUserSession.TestUserPassword,
            firstName: TestConstants.TestUserSession.TestFirstName,
            lastName: TestConstants.TestUserSession.TestLastName);
        user.Id = TestConstants.TestUserSession.TestUserId;
        user.ConfirmEmail();
        context.Users.Add(user);

        context.OutboxMessages.Add(SendEmailRow(
            TestConstants.TestUserSession.TestUserId,
            TestConstants.TestUserSession.TestUserEmail,
            EmailType.ResetPassword,
            EnqueueTenantId));

        var failed = SendEmailRow(
            TestConstants.TestUserSession.TestUserId,
            TestConstants.TestUserSession.TestUserEmail,
            EmailType.ConfirmationEmail,
            tenantId: null);
        failed.MarkFailed("smtp refused");
        context.OutboxMessages.Add(failed);

        context.OutboxMessages.Add(
            SendEmailRow(BystanderUserId, BystanderEmail, EmailType.ResetPassword, tenantId: null));

        context.OutboxMessages.Add(PushRow(TestConstants.TestUserSession.TestUserId));

        await context.CommitAsync(CancellationToken.None);
    }

    private static OutboxMessage SendEmailRow(string userId, string email, EmailType emailType, string? tenantId)
    {
        var code = $"{userId}-{emailType}-raw-token";
        var key = MessageKeys.Email(emailType, userId, MessageKeys.HashCode(code));
        var payload = new SendEmailMessage(emailType, email, "Milada Novotna", code, "cs", userId, tenantId);

        return OutboxMessage.Create(
            QueueNames.SendEmail,
            key,
            JsonSerializer.Serialize(new QueueEnvelope<SendEmailMessage>(key, tenantId, payload), QueueJson),
            tenantId);
    }

    private static OutboxMessage PushRow(string userId)
    {
        const string eventKey = "order.completed";
        var key = MessageKeys.Push(userId, eventKey, "order-ob-int");
        var payload = new SendPushNotificationMessage(
            userId, eventKey, new Dictionary<string, string> { ["orderNumber"] = "CL-2026-0042" }, null);

        return OutboxMessage.Create(
            QueueNames.NotificationsDispatch,
            key,
            JsonSerializer.Serialize(new QueueEnvelope<SendPushNotificationMessage>(key, null, payload), QueueJson),
            tenantId: null);
    }
}
