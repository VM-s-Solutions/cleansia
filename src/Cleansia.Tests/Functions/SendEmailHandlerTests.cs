using System.Text.Json;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Cleansia.Functions.Core.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Functions;

/// <summary>
/// The <c>send-email</c> consumer that realizes the registration / password-reset email post-commit.
/// Covers:
///   • "safe to run twice" — two invocations with the same message send the email EXACTLY ONCE;
///   • act-then-claim (at-least-once, ADR-0023's TC-EMAIL-ALO contract) — a FAILED send leaves the
///     key unclaimed so the queue retry genuinely retries (the claim-before-send regression: a send
///     failing after the claim was permanently lost); only a successful send claims; a claim-write
///     failure after a successful send logs a Warning and acks (no throw — throwing would guarantee
///     a duplicate);
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

    [Fact]
    public async Task Failed_Send_Leaves_Key_Unclaimed_So_A_Redelivery_Retries_The_Send()
    {
        // The SendGrid config-gap regression: claim-before-send permanently lost any email whose send
        // failed after the claim. A failed send must leave the key unclaimed so the queue's redelivery
        // genuinely re-attempts the send — and succeeds.
        _emailService
            .SetupSequence(s => s.SendEmailConfirmationAsync(Email, UserName, RawCode, "cs", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("SendGrid unreachable"))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();
        var body = SerializeEnvelope(Confirmation());

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.HandleAsync(body, CancellationToken.None));
        Assert.Empty(_guard.ClaimedKeys);

        await handler.HandleAsync(body, CancellationToken.None);

        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            Email, UserName, RawCode, "cs", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Successful_Send_Claims_The_Key_So_A_Redelivery_Skips_Without_Sending()
    {
        _emailService
            .Setup(s => s.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");
        var handler = CreateHandler();
        var message = Confirmation();
        var key = MessageKeys.Email(message.EmailType, message.UserId, MessageKeys.HashCode(message.Code));

        await handler.HandleAsync(SerializeEnvelope(message), CancellationToken.None);
        Assert.Contains(key, _guard.ClaimedKeys);

        await handler.HandleAsync(SerializeEnvelope(message), CancellationToken.None);

        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Already_Claimed_Key_Skips_Without_Sending()
    {
        var handler = CreateHandler();
        var message = Confirmation();
        _guard.PreClaim(MessageKeys.Email(message.EmailType, message.UserId, MessageKeys.HashCode(message.Code)));

        await handler.HandleAsync(SerializeEnvelope(message), CancellationToken.None);

        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Claim_Write_Failure_After_Successful_Send_Logs_Warning_And_Acks()
    {
        // The email IS sent; throwing on a failed claim-write would force an immediate redelivery — a
        // guaranteed duplicate. The handler must log a Warning and ack (a later redelivery duplicating
        // the email is the accepted worst case), never re-send within the invocation.
        _emailService
            .Setup(s => s.SendEmailConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("msg-id");
        _guard.MarkFails = true;
        var logger = new CapturingLogger();
        var handler = new SendEmailHandler(_emailService.Object, _guard, _tenantProvider.Object, logger);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(SerializeEnvelope(Confirmation()), CancellationToken.None));

        Assert.Null(ex);
        _emailService.Verify(s => s.SendEmailConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.DoesNotContain(RawCode, warning.Message);
        Assert.DoesNotContain(Email, warning.Message);
    }

    // In-memory backing for IIdempotencyGuard — shared across the invocations within a test, so a claim
    // from one delivery is visible to the next. MarkFails simulates a claim-write infra fault AFTER the
    // successful send; PreClaim/ClaimedKeys expose the claim state for the at-least-once assertions.
    private sealed class InMemoryIdempotencyGuard : IIdempotencyGuard
    {
        private readonly HashSet<string> _claimed = [];
        public bool MarkFails { get; set; }
        public IReadOnlyCollection<string> ClaimedKeys => _claimed;
        public void PreClaim(string key) => _claimed.Add(key);

        public Task<bool> AlreadyProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(!_claimed.Add(messageKey));

        public Task<bool> HasProcessedAsync(string messageKey, CancellationToken ct = default) =>
            Task.FromResult(_claimed.Contains(messageKey));

        public Task MarkProcessedAsync(string messageKey, CancellationToken ct = default)
        {
            if (MarkFails)
            {
                throw new InvalidOperationException("simulated claim-write infra fault");
            }
            _claimed.Add(messageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingLogger : ILogger<SendEmailHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        IDisposable? ILogger.BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
