using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Enums;

namespace Cleansia.Core.AppServices.Features.Auth;

/// <summary>
/// The single mapping from the AuthenticationType an account ACTUALLY uses to the message that tells the
/// caller how to sign in. Its call sites must never disagree about the same row: the password reset
/// (an external account asked for or completed one) and the Apple/Google handlers (a social sign-in
/// collided with an account owned by a different provider). The password sign-in names no provider.
/// Keeping one switch is what stops a provider added later from being named correctly on one path and
/// mis-named on the other.
/// </summary>
internal static class AuthTypeErrorMessages
{
    internal static string For(AuthenticationType authenticationType)
    {
        return authenticationType switch
        {
            AuthenticationType.Internal => BusinessErrorMessage.InternalAuthTypeError,
            AuthenticationType.Google => BusinessErrorMessage.GoogleAuthTypeError,
            AuthenticationType.Apple => BusinessErrorMessage.AppleAuthTypeError,
            // Every AuthenticationType added in future lands here — a provider-neutral message rather
            // than a wrong one. Adding a provider without adding its arm degrades the wording; it can
            // never mis-name the provider. (The Internal arm is unreachable from the password reset, where
            // Internal is the passing case, but the social handlers reject exactly that account.)
            _ => BusinessErrorMessage.ExternalAuthTypeError,
        };
    }
}
