using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Common.Validators.Auth;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.AppServices.Tenancy;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using System.Text.Json.Serialization;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// Admin-specific login command that returns a token with HasAdminAccess flag.
/// The flag indicates whether the user has Administrator or Employee role.
/// Frontend should check this flag and redirect unauthorized users.
/// </summary>
[AuditAction("admin.session.login", ResourceType = "User", AllowsAnonymousActor = true)]
public class AdminLogin
{
    public class Validator : LoginValidator<Command>
    {
        public Validator(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IRefreshTokenService refreshTokenService,
            IAuditContext auditContext)
            : base(userRepository, refreshTokenRepository, refreshTokenService, auditContext,
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
        // Admin is web-only: the trusted-device marker comes from the HttpOnly refresh cookie
        // server-side, never the body. JsonIgnore keeps it off the wire.
        [JsonIgnore]
        public string? TrustedDeviceToken { get; init; }

        // A sign-in names no market: a refusal for an unknown address is stamped with the default market's
        // operator (ADR-0061 D3), and a refusal on a known account is re-stamped by the failure sink with
        // that account's operator. Off the wire: the login form has no market to send.
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

            if (user.Profile != UserProfile.Administrator)
            {
                return BusinessResult.Failure<JwtTokenResponse>(
                    new Error(nameof(Command.Email), BusinessErrorMessage.InsufficientPrivileges));
            }

            user.ResetLoginThrottle();

            var tokenResponse = await tokenService.GenerateTokenAsync(user, command.RememberMe, hostAudience.Audience, cancellationToken);

            auditContext.RecordEvidence(
                "User",
                user.Id,
                new LoginEvidence(LoginEvidence.PasswordMethod, command.RememberMe, hostAudience.Audience, tokenResponse.IsEmailConfirmed),
                actorUserId: user.Id,
                actorProfile: user.Profile);

            return BusinessResult.Success(tokenResponse with { HasAdminAccess = true });
        }
    }
}
