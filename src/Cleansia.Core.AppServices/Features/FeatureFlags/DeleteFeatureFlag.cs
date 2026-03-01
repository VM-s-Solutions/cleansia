using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.FeatureFlags;

public static class DeleteFeatureFlag
{
    public record Command(string Id) : ICommand;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator(IFeatureFlagRepository featureFlagRepository)
        {
            RuleFor(x => x.Id).NotEmpty()
                .MustAsync(featureFlagRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.FeatureFlagNotFound);
        }
    }

    internal class Handler(IFeatureFlagRepository featureFlagRepository) : ICommandHandler<Command>
    {
        public async Task<BusinessResult> Handle(Command request, CancellationToken cancellationToken)
        {
            var flag = await featureFlagRepository.GetByIdAsync(request.Id, cancellationToken);
            featureFlagRepository.Remove(flag!);
            return BusinessResult.Success();
        }
    }
}
