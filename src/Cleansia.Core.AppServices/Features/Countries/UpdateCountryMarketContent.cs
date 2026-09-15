using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Countries;

/// <summary>
/// The per-country figures customer copy interpolates (ADR-0060 D2). The configuration row must
/// already exist: creating one needs a currency, a language and a VAT rate, which is not this
/// command's business.
/// </summary>
public class UpdateCountryMarketContent
{
    public record Command(string CountryId, decimal? InsuranceCoverageAmount) : ICommand<Response>;

    public record Response(string CountryId);

    public class Validator : AbstractValidator<Command>
    {
        public Validator(ICountryConfigurationRepository countryConfigurationRepository)
        {
            RuleFor(x => x.CountryId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(async (id, ct) => await countryConfigurationRepository.ExistsForCountryAsync(id, ct))
                .WithMessage(BusinessErrorMessage.CountryConfigurationMissing);

            RuleFor(x => x.InsuranceCoverageAmount)
                .GreaterThanOrEqualTo(0m)
                .When(x => x.InsuranceCoverageAmount.HasValue)
                .WithMessage(BusinessErrorMessage.MustBePositive);
        }
    }

    internal class Handler(ICountryConfigurationRepository countryConfigurationRepository)
        : ICommandHandler<Command, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var configuration = await countryConfigurationRepository.GetByCountryIdAsync(command.CountryId, cancellationToken);
            if (configuration is null)
            {
                return BusinessResult.Failure<Response>(new Error(
                    nameof(command.CountryId), BusinessErrorMessage.CountryConfigurationMissing));
            }

            configuration.UpdateMarketContent(command.InsuranceCoverageAmount);

            return BusinessResult.Success(new Response(configuration.CountryId));
        }
    }
}
