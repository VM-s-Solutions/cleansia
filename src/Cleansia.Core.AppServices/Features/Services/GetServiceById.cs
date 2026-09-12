using Microsoft.EntityFrameworkCore;
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
        IServicePriceRepository servicePriceRepository)
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

            // EVERY currency's row, keyed by code, because this is what the edit form patches. An
            // entry the admin has not priced in some currency simply has no key for it — absent, not
            // zero, so the form can tell "not priced yet" from "priced at nothing".
            var rows = await servicePriceRepository.GetAll()
                .Where(p => p.ServiceId == service.Id)
                .Include(p => p.Currency)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var prices = rows
                .Where(p => p.Currency != null)
                .ToDictionary(
                    p => p.Currency!.Code,
                    p => new AdminServicePriceDto(p.BasePrice, p.PerRoomPrice));

            return BusinessResult.Success(service.MapToAdminDetail(prices));
        }
    }
}