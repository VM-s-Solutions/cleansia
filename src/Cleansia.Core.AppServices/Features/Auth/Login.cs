using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using System.Text.Json.Serialization;

namespace Cleansia.Core.AppServices.Features.Auth;

[AuditAction("customer.session.login", Audience = AuditAudience.Customer, ResourceType = "User", AllowsAnonymousActor = true)]
public class Login
{
    public class Validator : LoginValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IRefreshTokenService refreshTokenService)
            : base(userRepository, refreshTokenRepository, refreshTokenService,
                c => c.Email, c => c.Password, c => c.RememberMe, c => c.TrustedDeviceToken)
        {
        }
    }

    public record Command(
        string Email,
        string Password,
        bool RememberMe)
        : ICommand<JwtTokenResponse>, IOperatorScopedRequest
    {
        // Web hosts derive the trusted-device marker from the HttpOnly refresh cookie server-side
        // (the body never carries it), so JsonIgnore keeps it off the wire. The mobile login path
        // (MobileLogin/MobilePartnerLogin) carries it in the body instead.
        [JsonIgnore]
        public string? TrustedDeviceToken { get; init; }

        // A sign-in names no market, so its refusal row is stamped with the default market's operator —
        // the same answer a registration that names none gets. Off the wire: the login form has no
        // market to send.
        string? IOperatorScopedRequest.CountryId => null;
    }

    internal class Handler(
        ITokenService tokenService,
        IUserRepository userRepository,
        IHostAudienceProvider hostAudience,
        IAuditContext auditContext)
        : ICommandHandler<Command, JwtTokenResponse>
    {
        public async Task<BusinessResult<JwtTokenResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByEmailIgnoringTenantAsync(command.Email, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Email), BusinessErrorMessage.InvalidPassword));
            }

            user.ResetLoginThrottle();

            var response = await tokenService.GenerateTokenAsync(user, command.RememberMe, hostAudience.Audience, cancellationToken);

            auditContext.RecordEvidence(
                "User",
                user.Id,
                new LoginEvidence(LoginEvidence.PasswordMethod, command.RememberMe, hostAudience.Audience, response.IsEmailConfirmed),
                actorUserId: user.Id);

            return BusinessResult.Success(response);
        }
    }
}