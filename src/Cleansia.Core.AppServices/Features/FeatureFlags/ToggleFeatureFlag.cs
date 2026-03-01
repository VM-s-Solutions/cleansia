using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.FeatureFlags;

public static class ToggleFeatureFlag
{
    public record Command(string Id) : ICommand<Response>;

    internal class Validator : AbstractValidator<Command>
    {
        public Validator(IFeatureFlagRepository featureFlagRepository)
        {
            RuleFor(x => x.Id).NotEmpty()
                .MustAsync(featureFlagRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.FeatureFlagNotFound);
        }
    }

    internal class Handler(IFeatureFlagRepository featureFlagRepository) : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command request, CancellationToken cancellationToken)
        {
            var flag = await featureFlagRepository.GetByIdAsync(request.Id, cancellationToken);
            flag!.Toggle();
            return BusinessResult.Success(new Response(flag.Id, flag.IsEnabled));
        }
    }

    public record Response(string Id, bool IsEnabled);
}
