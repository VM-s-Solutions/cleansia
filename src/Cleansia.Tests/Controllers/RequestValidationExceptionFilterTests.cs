using System.Reflection;
using System.Text.Json;
using Cleansia.Config.Abstractions;
using Cleansia.Config.Filters;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Infra.Common.Validations;
using Cleansia.Core.Domain.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;

namespace Cleansia.Tests.Controllers;

/// <summary>
/// A validation reject reaches a client through one of two arms — returned as a
/// <see cref="ValidationResult"/> and mapped by <c>HandleFailure</c>, or thrown as a
/// <see cref="RequestValidationException"/> (the response type could not carry it) and caught by the
/// filter on the controller base. The client must not be able to tell which: the two arms build the
/// same <see cref="ProblemDetails"/>, byte for byte, from the same errors.
/// </summary>
public class RequestValidationExceptionFilterTests
{
    private static readonly Error[] Errors =
    [
        new("Limit", BusinessErrorMessage.PageSizeExceeded),
        new("Offset", BusinessErrorMessage.Required),
        new("Limit", BusinessErrorMessage.Required)
    ];

    private sealed class TestController(IMediator mediator) : CleansiaApiController(mediator)
    {
        public IActionResult Invoke(BusinessResult result) => HandleFailure<object>(result);
    }

    [Fact]
    public void The_thrown_arm_answers_with_the_returning_arms_ProblemDetails()
    {
        var returning = Assert.IsType<BadRequestObjectResult>(
            new TestController(Mock.Of<IMediator>()).Invoke(ValidationResult.WithErrors(Errors)));
        var expected = Assert.IsType<ProblemDetails>(returning.Value);

        var context = ExceptionContextFor(new RequestValidationException(Errors));
        new RequestValidationExceptionFilterAttribute().OnException(context);

        Assert.True(context.ExceptionHandled);
        var thrown = Assert.IsType<BadRequestObjectResult>(context.Result);
        var actual = Assert.IsType<ProblemDetails>(thrown.Value);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(StatusCodes.Status400BadRequest, actual.Status);
        Assert.Equal("Validation Error", actual.Title);
        var errors = Assert.IsType<Dictionary<string, string>>(actual.Extensions["errors"]);
        Assert.Equal($"{BusinessErrorMessage.PageSizeExceeded}; {BusinessErrorMessage.Required}", errors["Limit"]);
    }

    /// <summary>
    /// A write a frozen company's books refused at the commit (ADR-0064 D3) answers 409 with the same
    /// body shape, one keyed error the clients localise, and never a 500.
    /// </summary>
    [Fact]
    public void The_archived_company_refusal_answers_409_with_one_keyed_error_on_the_tenant()
    {
        var context = ExceptionContextFor(new CompanyArchivedException("cleansia-sk"));

        new RequestValidationExceptionFilterAttribute().OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        var body = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status409Conflict, body.Status);
        Assert.Equal("Conflict", body.Title);
        Assert.Equal("TenantId", body.Type);
        Assert.Equal(BusinessErrorMessage.TenantArchived, body.Detail);
        var errors = Assert.IsType<Dictionary<string, string>>(body.Extensions["errors"]);
        Assert.Equal(new KeyValuePair<string, string>("TenantId", BusinessErrorMessage.TenantArchived), Assert.Single(errors));
        Assert.DoesNotContain("cleansia-sk", JsonSerializer.Serialize(body), StringComparison.Ordinal);
    }

    [Fact]
    public void Any_other_exception_is_left_for_the_host()
    {
        var context = ExceptionContextFor(new InvalidOperationException("stripe down"));

        new RequestValidationExceptionFilterAttribute().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    /// <summary>
    /// Every controller derives from the base, so the filter on it is what makes the throwing arm reach
    /// every route; a filter registered on one host or one controller would leave the others answering
    /// 500 to a rejected paged read.
    /// </summary>
    [Fact]
    public void The_filter_sits_on_the_controller_base()
    {
        Assert.NotNull(typeof(CleansiaApiController).GetCustomAttribute<RequestValidationExceptionFilterAttribute>(inherit: false));
    }

    private static ExceptionContext ExceptionContextFor(Exception exception)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        return new ExceptionContext(actionContext, []) { Exception = exception };
    }
}
