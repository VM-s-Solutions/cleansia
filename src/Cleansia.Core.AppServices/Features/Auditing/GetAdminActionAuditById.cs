using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Auditing;

public class GetAdminActionAuditById
{
    public record Query(string AuditId) : IQuery<AdminActionAuditDetailDto>;

    public class Validator : AbstractValidator<Query>
    {
        public Validator(IAdminActionAuditRepository adminActionAuditRepository)
        {
            RuleFor(x => x.AuditId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(adminActionAuditRepository.ExistsAsync)
                .WithMessage(BusinessErrorMessage.AuditNotFound);
        }
    }

    internal class Handler(IAdminActionAuditRepository adminActionAuditRepository)
        : IQueryHandler<Query, AdminActionAuditDetailDto>
    {
        public async Task<BusinessResult<AdminActionAuditDetailDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var audit = await adminActionAuditRepository.GetByIdAsync(request.AuditId, cancellationToken);

            if (audit is null)
            {
                return BusinessResult.Failure<AdminActionAuditDetailDto>(
                    new Error(nameof(request.AuditId), BusinessErrorMessage.AuditNotFound));
            }

            return BusinessResult.Success(audit.MapToDetailDto());
        }
    }
}
