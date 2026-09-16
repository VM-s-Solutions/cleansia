#nullable enable
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Auditing.DTOs;
using Cleansia.Core.AppServices.Features.Auditing.Filters;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Shared.DTOs.RequestModels;
using Cleansia.Core.AppServices.Shared.DTOs.ResponseModels;
using Cleansia.Core.Domain.Auditing;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Sorting;
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SortDefinition = Cleansia.Core.Domain.Sorting.Common.SortDefinition;
using SortDirection = Cleansia.Core.Domain.Sorting.Common.SortDirection;

namespace Cleansia.Core.AppServices.Features.Auditing;

public class GetPagedCustomerActionAudits
{
    public class Request : DataRangeRequest, IRequest<PagedData<CustomerActionAuditDto>>
    {
        public CustomerActionAuditFilter? Filter { get; init; }
    }

    public class Validator : AbstractValidator<Request>
    {
        public Validator(IUserRepository userRepository)
        {
            When(r => !string.IsNullOrWhiteSpace(r.Filter?.UserId), () =>
                RuleFor(r => r.Filter!.UserId!).MustAsync(userRepository.ExistsAsync)
                    .WithMessage(BusinessErrorMessage.NotExistingUserWithId));
        }
    }

    internal class Handler(ICustomerActionAuditRepository customerActionAuditRepository)
        : IRequestHandler<Request, PagedData<CustomerActionAuditDto>>
    {
        public async Task<PagedData<CustomerActionAuditDto>> Handle(Request request, CancellationToken cancellationToken)
        {
            var specification = request.Filter.MapToDomain();
            var filter = specification.SatisfiedBy();

            // Filtered to one customer, the list is that person's history in every operating company —
            // their act on a booking made across the border is stamped with that market's operator
            // (ADR-0062 D7). Without a customer named it stays the admin's own company's feed.
            var userId = request.Filter?.UserId;
            var totalItems = string.IsNullOrWhiteSpace(userId)
                ? await customerActionAuditRepository.GetCountAsync(filter, cancellationToken)
                : await customerActionAuditRepository.GetCountForUserAsync(userId, filter, cancellationToken);
            var page = string.IsNullOrWhiteSpace(userId)
                ? customerActionAuditRepository.GetPagedSort<CustomerActionAuditSort>(request.Offset, request.Limit, filter, ResolveSort(request))
                : customerActionAuditRepository.GetPagedSortForUser<CustomerActionAuditSort>(userId, request.Offset, request.Limit, filter, ResolveSort(request));
            var items = await page
                .AsNoTracking()
                .Select(audit => audit.MapToDto())
                .ToListAsync(cancellationToken);

            return items.MapToDto(totalItems, request);
        }

        private static IEnumerable<SortDefinition> ResolveSort(Request request)
        {
            var sort = request.Sort.MapToDomain().ToList();
            return sort.Count > 0
                ? sort
                : [new SortDefinition { Field = nameof(CustomerActionAudit.OccurredOn), Direction = SortDirection.Descending }];
        }
    }
}
