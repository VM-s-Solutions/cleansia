using Cleansia.Core.AppServices.Features.Catalog;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Services.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Services;

public class GetServiceById
{
    public record Query(string ServiceId) : IQuery<AdminServiceDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IServiceRepository serviceRepository)
        {
            RuleFor(x => x.ServiceId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(serviceRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ServiceNotFound);
        }
    }

    internal class Handler(
        IServiceRepository serviceRepository,
        IServicePriceRepository servicePriceRepository,
        ICurrencyRepository currencyRepository)
        : IQueryHandler<Query, AdminServiceDetailDto>
    {
        public async Task<BusinessResult<AdminServiceDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var service = await serviceRepository.GetByIdAsync(query.ServiceId, cancellationToken);
            if (service is null)
            {
                return BusinessResult.Failure<AdminServiceDetailDto>(new Error(
                    nameof(query.ServiceId), BusinessErrorMessage.ServiceNotFound));
            }

            // The platform default currency's price -- 0 when there is no row, for the reason given
            // in GetPagedServices.
            var currency = await currencyRepository.GetDefaultAsync(cancellationToken);
            var prices = await CataloguePriceLookup.ForServicesAsync(
                servicePriceRepository, [service.Id], currency.Id, cancellationToken);
            var price = prices.GetValueOrDefault(service.Id);

            return BusinessResult.Success(service.MapToAdminDetail(price.BasePrice, price.PerRoomPrice));
        }
    }
}