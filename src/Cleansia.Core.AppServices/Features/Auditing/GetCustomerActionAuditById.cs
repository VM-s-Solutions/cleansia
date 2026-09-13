using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auditing;

public class GetCustomerActionAuditById
{
    public record Query(string AuditId) : IQuery<CustomerActionAuditDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(ICustomerActionAuditRepository customerActionAuditRepository)
        {
            RuleFor(x => x.AuditId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(customerActionAuditRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.AuditNotFound);
        }
    }

    internal class Handler(ICustomerActionAuditRepository customerActionAuditRepository)
        : IQueryHandler<Query, CustomerActionAuditDetailDto>
    {
        public async Task<BusinessResult<CustomerActionAuditDetailDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var audit = await customerActionAuditRepository.GetByIdAsync(request.AuditId, cancellationToken);

            if (audit is null)
            {
                return BusinessResult.Failure<CustomerActionAuditDetailDto>(
                    new Error(nameof(request.AuditId), BusinessErrorMessage.AuditNotFound));
            }

            return BusinessResult.Success(audit.MapToDetailDto());
        }
    }
}
