using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The <c>send-email</c> consumer that realizes the registration / password-reset email post-commit.
/// Covers:
///   • "safe to run twice" — two invocations with the same message send the email EXACTLY ONCE (the
///     guard claims the deterministic key before the terminal send);
///   • dual-read at the deploy boundary — an enveloped body and a bare in-flight body are both
///     processed (the bare key synthesized from the payload), neither poisoned;
///   • template-by-type + language preservation — confirmation vs reset routes to the right
///     IEmailService method with the message's language;
///   • failure classification — a malformed body acks (no throw); an infra fault throws (so the
///     runtime retries).
/// </summary>
public class SendEmailHandlerTests
{
    private const string UserId = "USER-1";
    private const string Email = "user@example.com";
    private const string UserName = "John Doe";
    private const string RawCode = "raw-token-123";

    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<ITenantProvider> _tenantProvider = new();
    private readonly InMemoryIdempotencyGuard _guard = new();

    private SendEmailHandler CreateHandler() => new(
        _emailService.Object,
        _guard,
        _tenantProvider.Object,
        NullLogger<SendEmailHandler>.Instance);

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static string SerializeEnvelope(SendEmailMessage message, string? tenantId = null) =>
        JsonSerializer.Serialize(
            new QueueEnvelope<SendEmailMessage>(
                MessageKeys.Email(message.EmailType, message.UserId, MessageKeys.HashCode(message.Code)),
                tenantId, message),
            JsonOptions);

    private static string SerializeBare(SendEmailMessage message) =>
        JsonSerializer.Serialize(message, JsonOptions);

    private static SendEmailMessage Confirmation(string language = "cs") =>
        new(EmailType.ConfirmationEmail, Email, UserName, RawCode, language, UserId, TenantId: null);

    private static SendEmailMessage Reset(string language = "uk") =>
        new(EmailType.ResetPassword, Email, UserName, RawCode, language, UserId, TenantId: null);

    [Fact]
    public async Task Twice_With_Same_Message_Sends_Confirmation_Email_Exactly_Once()
    {
        _emailService
            .Setup(s => s.SendEmailConfirmationAsync(Email, UserName, RawCode, "cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();
        var body = SerializeEnvelope(Confirmation());

        await handler.HandleAsync(body, CancellationToken.None);
        await handler.HandleAsync(body, CancellationToken.None);

        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Confirmation_Routes_To_Confirmation_Template_With_Message_Language()
    {
        _emailService
            .Setup(s => s.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();

        await handler.HandleAsync(SerializeEnvelope(Confirmation("sk")), CancellationToken.None);

        _emailService.Verify(s => s.SendEmailConfirmationAsync(Email, UserName, RawCode, "sk", It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(s => s.SendResetPasswordEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reset_Routes_To_Reset_Template_With_Message_Language()
    {
        _emailService
            .Setup(s => s.SendResetPasswordEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();

        await handler.HandleAsync(SerializeEnvelope(Reset("uk")), CancellationToken.None);

        _emailService.Verify(s => s.SendResetPasswordEmailAsync(Email, It.IsAny<string>(), RawCode, "uk", It.IsAny<CancellationToken>()), Times.Once);
        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DualRead_Enveloped_Body_Is_Processed()
    {
        _emailService
            .Setup(s => s.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();

        await handler.HandleAsync(SerializeEnvelope(Confirmation()), CancellationToken.None);

        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            Email, UserName, RawCode, "cs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DualRead_Bare_Body_Is_Processed_Not_Poisoned()
    {
        _emailService
            .Setup(s => s.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();

        await handler.HandleAsync(SerializeBare(Confirmation()), CancellationToken.None);

        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            Email, UserName, RawCode, "cs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Envelope_Tenant_Sets_The_Cross_Tenant_Override()
    {
        _emailService
            .Setup(s => s.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();

        await handler.HandleAsync(SerializeEnvelope(Confirmation(), tenantId: "TENANT-A"), CancellationToken.None);

        _tenantProvider.Verify(t => t.SetTenantOverride("TENANT-A"), Times.Once);
    }

    [Fact]
    public async Task Malformed_Body_Acks_Without_Throwing()
    {
        var handler = CreateHandler();

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync("}{ not json", CancellationToken.None));

        Assert.Null(ex);
        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_Required_Fields_Acks_Without_Throwing()
    {
        var handler = CreateHandler();

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync("{}", CancellationToken.None));

        Assert.Null(ex);
        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Transport_Fault_During_Send_Throws_So_Runtime_Retries()
    {
        _emailService
            .Setup(s => s.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SendGrid unreachable"));
        var handler = CreateHandler();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => handler.HandleAsync(SerializeEnvelope(Confirmation()), CancellationToken.None));
    }

    // Wave-0 in-memory backing for IIdempotencyGuard — the same instance is shared across the two
    // invocations in the "twice" test, so the second claim short-circuits before the send.
    private sealed class InMemoryIdempotencyGuard : IIdempotencyGuard
    {
        private readonly HashSet<string> _claimed = [];
        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(!_claimed.Add(messageKey));
    }
}
