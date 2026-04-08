using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.PayConfig.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.PayConfig;

public class GetPayConfigById
{
    public record Query(string PayConfigId) : IQuery<EmployeePayConfigDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IEmployeePayConfigRepository payConfigRepository)
        {
            RuleFor(x => x.PayConfigId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(payConfigRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.PayConfigNotFound);
        }
    }

    internal class Handler(
        IEmployeePayConfigRepository payConfigRepository)
        : IQueryHandler<Query, EmployeePayConfigDto>
    {
        public async Task<BusinessResult<EmployeePayConfigDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var payConfig = await payConfigRepository
                .GetAll()
                .Include(c => c.Service)
                .Include(c => c.Package)
                .Include(c => c.Currency)
                .Include(c => c.Employee)
                    .ThenInclude(e => e!.User)
                .FirstOrDefaultAsync(c => c.Id == query.PayConfigId, cancellationToken);

            return BusinessResult.Success(payConfig!.MapToDto());
        }
    }
}
