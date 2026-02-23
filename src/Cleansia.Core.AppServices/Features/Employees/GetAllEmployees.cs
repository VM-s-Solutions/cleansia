using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Employees.DTOs;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Employees;

public class GetAllEmployees
{
    public class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0).WithMessage(BusinessErrorMessage.PageMustBePositive);

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage(BusinessErrorMessage.PageSizeMustBePositive)
                .LessThanOrEqualTo(100).WithMessage(BusinessErrorMessage.PageSizeExceeded);

            When(x => !string.IsNullOrEmpty(x.ContractStatus), () =>
            {
                RuleFor(x => x.ContractStatus)
                    .Must(value => Enum.TryParse<Core.Domain.Enums.ContractStatus>(value, out _))
                    .WithMessage(BusinessErrorMessage.InvalidContractStatus);
            });
        }
    }

    public record Query(
        int Page = 1,
        int PageSize = 20,
        string? ContractStatus = null
    ) : IQuery<Response>;

    public record Response(
        List<AdminEmployeeListItem> Employees,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages
    );

    public class Handler(IEmployeeRepository employeeRepository)
        : IQueryHandler<Query, Response>
    {
        public async Task<BusinessResult<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var dbQuery = employeeRepository
                .GetQueryable()
                .Include(e => e.User)
                .Include(e => e.Nationality)
                .AsNoTracking();

            // Filter by contract status if provided
            if (!string.IsNullOrEmpty(query.ContractStatus) &&
                Enum.TryParse<Core.Domain.Enums.ContractStatus>(query.ContractStatus, out var status))
            {
                dbQuery = dbQuery.Where(e => e.ContractStatus == status);
            }

            var totalCount = await dbQuery.CountAsync(cancellationToken);

            var employees = await dbQuery
                .OrderByDescending(e => e.User!.CreatedOn)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(e => new AdminEmployeeListItem(
                    e.Id,
                    e.User!.FirstName,
                    e.User!.LastName,
                    e.User!.Email,
                    e.User!.PhoneNumber,
                    e.ContractStatus.ToString(),
                    e.AverageRating,
                    e.ComplaintsCount,
                    e.Nationality != null ? e.Nationality.Name : null,
                    e.User!.CreatedOn,
                    e.IsProfileComplete()))
                .ToListAsync(cancellationToken);

            var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

            var response = new Response(
                employees,
                totalCount,
                query.Page,
                query.PageSize,
                totalPages
            );

            return BusinessResult.Success(response);
        }
    }
}
