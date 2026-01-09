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

    internal class Handler(IServiceRepository serviceRepository)
        : IQueryHandler<Query, AdminServiceDetailDto>
    {
        public async Task<BusinessResult<AdminServiceDetailDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var service = await serviceRepository.GetByIdAsync(query.ServiceId, cancellationToken);

            return BusinessResult.Success(service!.MapToAdminDetail());
        }
    }
}