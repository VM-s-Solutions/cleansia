using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Extras.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Extras;

public class GetExtraById
{
    public record Query(string ExtraId) : IQuery<AdminExtraDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IExtraRepository extraRepository)
        {
            RuleFor(x => x.ExtraId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(extraRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.ExtraNotFound);
        }
    }

    internal class Handler(
        IExtraRepository extraRepository,
        IExtraPriceRepository extraPriceRepository)
        : IQueryHandler<Query, AdminExtraDetailDto>
    {
        public async Task<BusinessResult<AdminExtraDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var extra = await extraRepository.GetByIdAsync(query.ExtraId, cancellationToken);
            if (extra is null)
            {
                return BusinessResult.Failure<AdminExtraDetailDto>(new Error(
                    nameof(query.ExtraId), BusinessErrorMessage.ExtraNotFound));
            }

            // EVERY currency's row, keyed by code -- see GetServiceById. Absent, not zero.
            var rows = await extraPriceRepository.GetAll()
                .Where(p => p.ExtraId == extra.Id)
                .Include(p => p.Currency)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var prices = rows
                .Where(p => p.Currency != null)
                .ToDictionary(p => p.Currency!.Code, p => p.Price);

            return BusinessResult.Success(extra.MapToAdminDetail(prices));
        }
    }
}
