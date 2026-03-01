using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.FeatureFlags;

public static class CreateFeatureFlag
{
    public record Command(
        string Name,
        bool IsEnabled,
        string Scope,
        string? ScopeValue,
        string? Description
    ) : ICommand<Response>;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator(IFeatureFlagRepository featureFlagRepository)
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Scope).NotEmpty().Must(s => s is "global" or "country" or "tenant")
                .WithMessage("Scope must be 'global', 'country', or 'tenant'.");
            RuleFor(x => x.ScopeValue).MaximumLength(26);
            RuleFor(x => x.Description).MaximumLength(500);

            RuleFor(x => x)
                .MustAsync(async (cmd, ct) => !await featureFlagRepository.ExistsWithNameAndScopeAsync(
                    cmd.Name, cmd.Scope, cmd.ScopeValue, ct))
                .WithMessage(BusinessErrorMessage.FeatureFlagAlreadyExists);
        }
    }

    internal class Handler(IFeatureFlagRepository featureFlagRepository) : ICommandHandler<Command, Response>
    {
        public Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var flag = FeatureFlag.Create(request.Name, request.IsEnabled, request.Scope, request.ScopeValue, request.Description);
            featureFlagRepository.Add(flag);
            return Task.FromResult(BusinessResult.Success(new Response(flag.Id)));
        }
    }

    public record Response(string Id);
}
