using System.Text.Json;
using Cleansia.Core.AppServices.Features.Auth;
using Cleansia.Core.AppServices.Features.Users;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Users;
using Cleansia.Core.Queue.Abstractions;
using Cleansia.Core.Queue.Abstractions.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cleansia.Tests.Features.Auth;

/// <summary>
/// The account-creation / password-reset email is recorded as post-commit dispatch intent, never sent
/// inline on the request path. These pin the producer side of the move:
///   • each of the four handlers <c>Enqueue</c>s a <c>send-email</c> message — and, structurally, no
///     longer depends on the email service at all (so it cannot send inline);
///   • the enqueued message carries the deterministic key and preserves the per-handler language
///     selection (<c>command.Language</c> for register/resend; <c>user.PreferredLanguageCode</c> ??
///     <c>command.Language</c> for password-reset).
///
/// The cross-cutting outage-still-succeeds and nothing-on-failure guarantees are properties of
/// <c>PostCommitDispatchBehavior</c> + the recording seam and are pinned in
/// <see cref="Cleansia.Tests.Dispatch.EmailDispatchPipelineTests"/>.
/// </summary>
public class RegistrationEmailDispatchTests
{
    private const string Email = "new.user@example.com";
    private const string Language = "cs";

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<ICartRepository> _cartRepository = new();
    private readonly Recorder _pending = new();

    private static SendEmailMessage Decode(PendingMessage message)
    {
        var envelope = JsonSerializer.Deserialize<QueueEnvelope<SendEmailMessage>>(
            message.Body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return envelope!.Payload;
    }

    [Fact]
    public async Task Register_Enqueues_Confirmation_Email_With_Deterministic_Key()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var referralService = new Mock<IReferralService>();

        var handler = new Register.Handler(
            _cartRepository.Object, _userRepository.Object, referralService.Object, _pending,
            NullLogger<Register.Handler>.Instance);

        var result = await handler.Handle(
            new Register.Command(Email, "Password1!@abc", "John", "Doe", Language), CancellationToken.None);

        Assert.True(result.IsSuccess);

        var message = Assert.Single(_pending.Messages);
        Assert.Equal(QueueNames.SendEmail, message.QueueName);
        var payload = Decode(message);
        Assert.Equal(EmailType.ConfirmationEmail, payload.EmailType);
        Assert.Equal(Email, payload.Email);
        Assert.Equal(Language, payload.LanguageCode);
        Assert.False(string.IsNullOrEmpty(payload.Code));
        Assert.Equal(
            MessageKeys.Email(EmailType.ConfirmationEmail, payload.UserId, MessageKeys.HashCode(payload.Code)),
            message.MessageKey);
    }

    [Fact]
    public async Task RegisterEmployee_Enqueues_Confirmation_Email()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var employeeRepository = new Mock<IEmployeeRepository>();

        var handler = new RegisterEmployee.Handler(
            _cartRepository.Object, _userRepository.Object, employeeRepository.Object, _pending);

        var result = await handler.Handle(
            new RegisterEmployee.Command(Email, "Password1!@abc", "John", "Doe", Language), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Decode(Assert.Single(_pending.Messages));
        Assert.Equal(EmailType.ConfirmationEmail, payload.EmailType);
        Assert.Equal(Language, payload.LanguageCode);
    }

    [Fact]
    public async Task ResendConfirmationEmail_Enqueues_Confirmation_Email()
    {
        var user = User.CreateWithPassword(Email, "Password1!@abc", "John", "Doe");
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new ResendConfirmationEmail.Handler(_userRepository.Object, _pending);

        var result = await handler.Handle(new ResendConfirmationEmail.Command(Email, Language), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Decode(Assert.Single(_pending.Messages));
        Assert.Equal(EmailType.ConfirmationEmail, payload.EmailType);
        Assert.Equal(Language, payload.LanguageCode);
    }

    [Fact]
    public async Task RequestPasswordChange_Enqueues_Reset_Email_Preferring_User_Language()
    {
        var user = User.CreateWithPassword(Email, "Password1!@abc", "John", "Doe");
        user.UpdateLanguagePreference("uk");
        _userRepository.Setup(r => r.GetByEmailIgnoringTenantAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new RequestPasswordChange.Handler(_userRepository.Object, _pending);

        var result = await handler.Handle(new RequestPasswordChange.Command(Email, Language), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var payload = Decode(Assert.Single(_pending.Messages));
        Assert.Equal(EmailType.ResetPassword, payload.EmailType);
        Assert.Equal("uk", payload.LanguageCode);
    }

    [Fact]
    public async Task RequestPasswordChange_Falls_Back_To_Command_Language_When_User_Has_No_Preference()
    {
        var user = User.CreateWithPassword(Email, "Password1!@abc", "John", "Doe");
        user.UpdateLanguagePreference(null);
        _userRepository.Setup(r => r.GetByEmailIgnoringTenantAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new RequestPasswordChange.Handler(_userRepository.Object, _pending);

        await handler.Handle(new RequestPasswordChange.Command(Email, Language), CancellationToken.None);

        var payload = Decode(Assert.Single(_pending.Messages));
        Assert.Equal(Language, payload.LanguageCode);
    }

    // A real in-memory recording seam so the test reads the buffered, serialized envelope exactly as
    // the post-commit dispatcher would.
    private sealed class Recorder : IPendingDispatch
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private readonly List<PendingMessage> _buffer = [];
        public IReadOnlyList<PendingMessage> Messages => _buffer;
        public void Enqueue<T>(string queueName, T message, string messageKey) =>
            _buffer.Add(new PendingMessage(queueName, JsonSerializer.Serialize(message, JsonOptions), messageKey));
        public IReadOnlyList<PendingMessage> Drain() => _buffer;
    }
}
