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
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkPipelineBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);
        return services;
    }
}