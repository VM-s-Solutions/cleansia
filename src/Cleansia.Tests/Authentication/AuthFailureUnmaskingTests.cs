using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Cleansia.Tests.Authentication;

/// <summary>
/// A failed auth command (Apple/Google/login/refresh) must return 401 with the real business key carried
/// in the ProblemDetails <c>errors</c> dictionary — the shape every client reads to localize (iOS/Android
/// <c>firstErrorKey</c>, web <c>HttpErrorInterceptor</c> reads the first <c>errors</c> value). Before this
/// fix the auth arm returned a bare <c>Error{code,message}</c> with NO <c>errors</c> dict, so all three
/// clients fell back to a generic status message (iOS surfaced "session expired") and the true cause
/// (e.g. <c>auth.invalid_apple_token</c>) was silently discarded.
/// </summary>
public class AuthFailureUnmaskingTests
{
    private sealed class TestController(IMediator mediator) : CleansiaApiController(mediator)
    {
        public IActionResult Invoke(BusinessResult result) => HandleFailure<JwtTokenResponse>(result);
    }

    [Fact]
    public void Failed_auth_command_returns_401_with_the_business_key_in_the_errors_dict()
    {
        var controller = new TestController(Mock.Of<IMediator>());
        // The auth-handler idiom: Error(code = field name, message = dotted business key).
        var result = BusinessResult.Failure<JwtTokenResponse>(
            new Error("IdentityToken", "auth.invalid_apple_token"));

        var response = controller.Invoke(result);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(response);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(unauthorized.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);

        var errors = Assert.IsType<Dictionary<string, string>>(problem.Extensions["errors"]);
        // Clients read the FIRST errors value as the localization key — it must be the dotted business key,
        // not the field name and not a generic 401 message.
        Assert.Contains("auth.invalid_apple_token", errors.Values);
    }
}
