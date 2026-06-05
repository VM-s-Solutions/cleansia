using FluentValidation;
using Cleansia.Core.AppServices;
using Cleansia.Core.AppServices.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cleansia.Config.Validation;

public static class FluentValidationExtensions
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        // ADR-0002 D4 (F11) + D1: registration order = outer → inner for MediatR. The order MUST be
        // PostCommitDispatch → Validation → UnitOfWork → Handler.
        //   • PostCommitDispatch is OUTERMOST (registered first): it drains IPendingDispatch and puts
        //     messages on the wire ONLY after the inner pipeline returns a committed success — never
        //     before the commit (the F2/SEC-W1 fix), never on a commit-throw (the guard is unreached).
        //   • Validation is OUTER to UnitOfWork: a failing validator returns the failure result
        //     without calling next(), so control never reaches the UoW commit on a rejected command.
        // A re-swap that breaks this order is caught by the pipeline-order unit test (verify #4).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PostCommitDispatchBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkPipelineBehavior<,>));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        return services;
    }
}