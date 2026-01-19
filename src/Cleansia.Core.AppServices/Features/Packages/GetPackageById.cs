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

    internal class Handler(IPackageRepository packageRepository)
        : IQueryHandler<Query, AdminPackageDetailDto>
    {
        public async Task<BusinessResult<AdminPackageDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var package = await packageRepository.GetByIdAsync(query.PackageId, cancellationToken);

            return BusinessResult.Success(package!.MapToAdminDetail());
        }
    }
}