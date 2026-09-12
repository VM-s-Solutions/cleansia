using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Packages;

public class GetPackageById
{
    public record Query(string PackageId) : IQuery<AdminPackageDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IPackageRepository packageRepository)
        {
            RuleFor(x => x.PackageId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(packageRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PackageNotFound);
        }
    }

    internal class Handler(
        IPackageRepository packageRepository,
        IPackagePriceRepository packagePriceRepository)
        : IQueryHandler<Query, AdminPackageDetailDto>
    {
        public async Task<BusinessResult<AdminPackageDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var package = await packageRepository.GetByIdAsync(query.PackageId, cancellationToken);
            if (package is null)
            {
                return BusinessResult.Failure<AdminPackageDetailDto>(new Error(
                    nameof(query.PackageId), BusinessErrorMessage.PackageNotFound));
            }

            // EVERY currency's row, keyed by code -- see GetServiceById. Absent, not zero, so the edit
            // form can tell "not priced in this currency yet" from "priced at nothing".
            var rows = await packagePriceRepository.GetAll()
                .Where(p => p.PackageId == package.Id)
                .Include(p => p.Currency)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var prices = rows
                .Where(p => p.Currency != null)
                .ToDictionary(p => p.Currency!.Code, p => p.Price);

            return BusinessResult.Success(package.MapToAdminDetail(prices));
        }
    }
}