using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Packages.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Packages;

public class GetPagedPackages
{
    public record Request(
        int Page,
        int PageSize,
        string? SearchTerm,
        string? SortField,
        bool SortAscending = true) : IQuery<PagedData<PackageListItem>>;

    public class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Page)
                .GreaterThanOrEqualTo(1)
                .WithMessage(BusinessErrorMessage.MustBePositive);

            RuleFor(x => x.PageSize)
                .GreaterThanOrEqualTo(1)
                .LessThanOrEqualTo(100)
                .WithMessage(BusinessErrorMessage.MustBePositive);
        }
    }

    internal class Handler(IPackageRepository packageRepository)
        : IQueryHandler<Request, PagedData<PackageListItem>>
    {
        public async Task<BusinessResult<PagedData<PackageListItem>>> Handle(Request request, CancellationToken cancellationToken)
        {
            var query = packageRepository.GetAll();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(searchTerm) ||
                    p.Description.ToLower().Contains(searchTerm));
            }

            query = request.SortField?.ToLower() switch
            {
                "name" => request.SortAscending ? query.OrderBy(p => p.Name) : query.OrderByDescending(p => p.Name),
                "price" => request.SortAscending ? query.OrderBy(p => p.Price) : query.OrderByDescending(p => p.Price),
                "createdon" => request.SortAscending ? query.OrderBy(p => p.CreatedOn) : query.OrderByDescending(p => p.CreatedOn),
                _ => query.OrderBy(p => p.Name)
            };

            var totalCount = await query.CountAsync(cancellationToken);
            var packages = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => p.MapToDto())
                .ToListAsync(cancellationToken);

            var pagedData = new PagedData<PackageListItem>(
                request.Page,
                request.PageSize,
                totalCount,
                packages);

            return BusinessResult.Success(pagedData);
        }
    }
}