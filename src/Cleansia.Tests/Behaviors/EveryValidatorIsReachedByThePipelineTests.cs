using Cleansia.Config.Validation;
using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Tests.Behaviors;

/// <summary>
/// The container-side twin of <see cref="EveryCommandHasValidatorTests"/>. That one proves every
/// command has a validator; this one proves the pipeline can reach every validator there is. A
/// generic constraint on <c>ValidationPipelineBehavior&lt;,&gt;</c> is not a compile error for the
/// open-generic registration — the container simply leaves the behavior out of the chain for any
/// (request, response) pair the constraint rejects, and the validator registered for that request is
/// discoverable and never executed. That is how <c>GetAllGdprRequests.Validator</c> was dead: the
/// behavior was constrained to a <c>BusinessResult</c> response and the paged read's is
/// <c>PagedData&lt;T&gt;</c>. So the sweep resolves the behavior from the container, the way MediatR
/// does, for every request type the assembly's validators name.
/// </summary>
public sealed class EveryValidatorIsReachedByThePipelineTests
{
    [Fact]
    public void The_Validation_Behavior_Resolves_For_Every_Validated_Request_Type()
    {
        var registrations = new ServiceCollection().AddValidators();

        var validatedRequests = registrations
            .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(d => d.ServiceType.GetGenericArguments()[0])
            .Distinct()
            .Select(request => (Request: request, Response: ResponseTypeOf(request)))
            .Where(pair => pair.Response is not null)
            .OrderBy(pair => pair.Request.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(validatedRequests);
        Assert.Contains(validatedRequests, pair => !typeof(BusinessResult).IsAssignableFrom(pair.Response));

        // Only the behavior under test goes into the container: its validators resolve as an empty
        // set (the registrations are enumerated above, not constructed — several take repositories),
        // so what is exercised is exactly the container's decision whether the open generic closes.
        var behaviorRegistration = Assert.Single(registrations, d =>
            d.ServiceType == typeof(IPipelineBehavior<,>) && d.ImplementationType == typeof(ValidationPipelineBehavior<,>));
        var services = new ServiceCollection().AddLogging();
        services.Add(behaviorRegistration);
        using var provider = services.BuildServiceProvider();

        var unreached = validatedRequests
            .Where(pair => !provider
                .GetServices(typeof(IPipelineBehavior<,>).MakeGenericType(pair.Request, pair.Response!))
                .Any(behavior => behavior!.GetType().GetGenericTypeDefinition() == typeof(ValidationPipelineBehavior<,>)))
            .Select(pair => $"{pair.Request.FullName} -> {pair.Response!.Name}")
            .ToList();

        Assert.True(
            unreached.Count == 0,
            "A validator is registered for each of these request types, but the container will not close "
            + "ValidationPipelineBehavior<,> over them, so the validator never runs: "
            + string.Join(", ", unreached));
    }

    private static Type? ResponseTypeOf(Type request) =>
        request.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
            ?.GetGenericArguments()[0];
}
