using System.Reflection;
using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Infra.Common.Validations;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Cleansia.Tests.Controllers;

/// <summary>
/// BE-1 — every arm of <see cref="CleansiaApiController"/>.HandleFailure must carry the business key in the
/// ProblemDetails <c>errors</c> dictionary. All six clients localize from the FIRST <c>errors</c> value
/// (iOS/Android <c>firstErrorKey</c>, web <c>HttpErrorInterceptor</c>); with an empty dict they fall through
/// to <c>detail</c>, which holds the raw dotted key, and the user is shown literal text such as
/// "address.already_exists". The auth arm was fixed for this; the default arm — every non-validation,
/// non-auth handler failure, i.e. most business errors — was not.
///
/// The cases are DISCOVERED by reflection over every concrete <see cref="BusinessResult"/> subtype rather
/// than hand-listed, so an arm added later for a new result type is covered without editing this test. The
/// limit of that: an arm discriminated by something other than a distinct result type (a property pattern,
/// say) still escapes — reflection cannot enumerate switch arms.
/// </summary>
public class HandleFailureErrorsContractTests
{
    /// The handler idiom: Error(code = the offending field, message = the dotted business key).
    private static readonly Error BusinessError = new("Street", "address.already_exists");

    /// Generic results are closed over JwtTokenResponse (the auth arm's discriminator) and over a plain
    /// payload (every other handler), so both those arms are reached.
    private static readonly Type[] PayloadTypes = [typeof(JwtTokenResponse), typeof(object)];

    private sealed class TestController(IMediator mediator) : CleansiaApiController(mediator)
    {
        public IActionResult Invoke(BusinessResult result) => HandleFailure<object>(result);
    }

    [Fact]
    public void Every_failure_arm_carries_the_business_key_in_the_errors_dict()
    {
        var controller = new TestController(Mock.Of<IMediator>());
        var cases = FailureResults().ToArray();

        // Guard the guard: a rename that stops the reflection finding anything must fail here, not pass
        // vacuously. Validation, auth and default arms => at least three discriminators.
        Assert.Contains(cases, c => c.Result is IValidationResult);
        Assert.Contains(cases, c => c.Result is BusinessResult<JwtTokenResponse>);
        Assert.Contains(cases, c => c.Result is not IValidationResult and not BusinessResult<JwtTokenResponse>);

        var armsWithoutErrors = new List<string>();

        foreach (var (name, result) in cases)
        {
            var objectResult = Assert.IsAssignableFrom<ObjectResult>(controller.Invoke(result));
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            var errors = Assert.IsType<Dictionary<string, string>>(problem.Extensions["errors"]);

            if (errors.Count == 0)
            {
                armsWithoutErrors.Add(name);
            }
        }

        Assert.Empty(armsWithoutErrors);
    }

    [Fact]
    public void Plain_business_failure_returns_400_with_the_dotted_key_as_the_first_errors_value()
    {
        var controller = new TestController(Mock.Of<IMediator>());

        var response = controller.Invoke(BusinessResult.Failure<object>(BusinessError));

        var badRequest = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);

        var errors = Assert.IsType<Dictionary<string, string>>(problem.Extensions["errors"]);
        // Clients read the first errors VALUE as the localization key — it must be the dotted business key,
        // not the field name that keys the entry.
        Assert.Equal("address.already_exists", Assert.Single(errors.Values));
    }

    private static IEnumerable<(string Name, BusinessResult Result)> FailureResults()
    {
        var resultTypes = typeof(BusinessResult).Assembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsPublic: true } && typeof(BusinessResult).IsAssignableFrom(type));

        foreach (var type in resultTypes)
        {
            if (!type.IsGenericTypeDefinition)
            {
                yield return (type.Name, CreateFailure(type));
                continue;
            }

            foreach (var payload in PayloadTypes)
            {
                yield return ($"{type.Name}<{payload.Name}>", CreateFailure(type.MakeGenericType(payload)));
            }
        }
    }

    private static BusinessResult CreateFailure(Type resultType)
    {
        // A validation result carries its own error list; everything else is a bare business failure. The
        // production pipeline never builds one with an empty list (ValidationPipelineBehavior only converts
        // when errors.Length != 0), so one error is the realistic minimum.
        var withErrors = resultType.GetMethod(
            nameof(ValidationResult.WithErrors),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(Error[])]);

        if (withErrors is not null)
        {
            return (BusinessResult)withErrors.Invoke(null, [new[] { BusinessError }])!;
        }

        if (!resultType.IsGenericType)
        {
            return BusinessResult.Failure(BusinessError);
        }

        var genericFailure = typeof(BusinessResult).GetMethods()
            .Single(method => method.Name == nameof(BusinessResult.Failure) && method.IsGenericMethodDefinition);

        return (BusinessResult)genericFailure
            .MakeGenericMethod(resultType.GenericTypeArguments[0])
            .Invoke(null, [BusinessError])!;
    }
}
