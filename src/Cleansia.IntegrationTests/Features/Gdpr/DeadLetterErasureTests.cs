using System.Text.Json;
using Cleansia.Core.AppServices.Features.Gdpr;
using Cleansia.Core.Domain.DeadLettering;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Internationalization;
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
/// T-0583 — the dead-letter half of an erasure, through <c>GdprDeletionService</c>'s REAL query shape.
///
/// <para>This runs against real Postgres for the same reason <see cref="PayoutDetailsErasureTests"/> does,
/// plus one of its own: a <see cref="DeadLetter"/> is written by a poison consumer that derives no tenant
/// from the body, so the row carries whatever tenant was ambient on the Functions host — routinely NOT the
/// erasing request's. The walk therefore has to read past the global tenant filter, and a fixture whose
/// rows are all null-stamped passes over exactly that bug. The subject's row here is stamped with a real
/// tenant id the erasing request does not carry.</para>
/// </summary>
[Collection("PostgresCollection")]
public class DeadLetterErasureTests(PostgresContainerFixture fixture) : BaseIntegrationTest(fixture)
{
    private const string PoisonTenantId = "tenant-alpha";
    private const string BystanderUserId = "user-keep-dl-int";
    private const string BystanderEmail = "tomas.svoboda@cleansia.test";

    private static readonly JsonSerializerOptions QueueJson =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task Erasure_Removes_The_Subjects_Send_Email_Row_Even_When_It_Carries_Another_Tenant()
    {
        await TestMethod(
            arrange: SeedUserWithPoisonedRows,
            act: async provider =>
            {
                var mediator = provider.GetRequiredService<IMediator>();
                return await mediator.Send(new DeleteUserAccount.Command());
            },
            assert: async (CleansiaDbContext context, BusinessResult result) =>
            {
                Assert.True(result.IsSuccess);

                var rows = await context.DeadLetters.IgnoreQueryFilters().ToListAsync();

                Assert.DoesNotContain(rows, r =>
                    r.RawBody.Contains(TestConstants.TestUserSession.TestUserEmail, StringComparison.Ordinal));
                Assert.DoesNotContain(rows, r =>
                    r.RawBody.Contains(TestConstants.TestUserSession.TestUserId, StringComparison.Ordinal)
                    && r.SourceQueue == QueueNames.SendEmail);

                Assert.Contains(rows, r => r.SourceQueue == QueueNames.NotificationsDispatch);
                Assert.Contains(rows, r =>
                    r.SourceQueue == QueueNames.SendEmail
                    && r.RawBody.Contains(BystanderEmail, StringComparison.Ordinal));
            });
    }

    private static async Task SeedUserWithPoisonedRows(CleansiaDbContext context)
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

        var subject = DeadLetter.Create(
            QueueNames.SendEmail,
            SendEmailBody(
                TestConstants.TestUserSession.TestUserId,
                TestConstants.TestUserSession.TestUserEmail,
                PoisonTenantId));
        subject.TenantId = PoisonTenantId;
        context.DeadLetters.Add(subject);

        context.DeadLetters.Add(DeadLetter.Create(
            QueueNames.SendEmail,
            SendEmailBody(BystanderUserId, BystanderEmail, tenantId: null)));

        context.DeadLetters.Add(DeadLetter.Create(
            QueueNames.NotificationsDispatch,
            PushBody(TestConstants.TestUserSession.TestUserId)));

        await context.CommitAsync(CancellationToken.None);
    }

    private static string SendEmailBody(string userId, string email, string? tenantId)
    {
        const string code = "9f2c41ae-reset-token";
        var payload = new SendEmailMessage(
            EmailType.ResetPassword, email, "Milada Novotna", code, "cs", userId, tenantId);

        return JsonSerializer.Serialize(
            new QueueEnvelope<SendEmailMessage>(
                MessageKeys.Email(EmailType.ResetPassword, userId, MessageKeys.HashCode(code)), tenantId, payload),
            QueueJson);
    }

    private static string PushBody(string userId)
    {
        const string eventKey = "order.completed";
        var payload = new SendPushNotificationMessage(
            userId, eventKey, new Dictionary<string, string> { ["orderNumber"] = "CL-2026-0042" }, null);

        return JsonSerializer.Serialize(
            new QueueEnvelope<SendPushNotificationMessage>(
                MessageKeys.Push(userId, eventKey, "order-dl-int"), null, payload),
            QueueJson);
    }
}
