using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Employees;

public class GetEmployeeDetail
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator(IEmployeeRepository employeeRepository)
        {
            RuleFor(x => x.EmployeeId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage(BusinessErrorMessage.Required)
                .MustAsync(employeeRepository.ExistsAsync)
                .WithErrorCode(nameof(Query.EmployeeId))
                .WithMessage(BusinessErrorMessage.EmployeeNotFound);
        }
    }

    public record Query(string EmployeeId) : IQuery<AdminEmployeeDetail>;

    internal class Handler(IEmployeeRepository employeeRepository)
        : IQueryHandler<Query, AdminEmployeeDetail>
    {
        public async Task<BusinessResult<AdminEmployeeDetail>> Handle(Query query, CancellationToken cancellationToken)
        {
            var employee = await employeeRepository
                .GetQueryable()
                .Include(e => e.User)
                .Include(e => e.Nationality)
                .Include(e => e.Address)
                .ThenInclude(a => a!.Country)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == query.EmployeeId, cancellationToken);

            return BusinessResult.Success(employee!.MapToAdminDetailDto());
        }
    }
}
