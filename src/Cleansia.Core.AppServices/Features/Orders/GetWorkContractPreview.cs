using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Specifications;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The contract for work a cleaner reads before taking a job: the ORDER's document (stamped at booking),
/// its text in the requested language or the fallback, rendered with the order's currency and the identity
/// of the company that operates the order, and the job facts the acceptance will freeze — the price being
/// the caller's reward for a seat. The order must be readable by the caller as the board's floor and
/// the browse gate define it — on the crew, or offerable with a takeable seat, and open to the caller —
/// so a held order is a missing order to everyone but its beneficiary, and a cancelled, finished, full
/// or unpaid-card job the caller is not on says nothing.
/// </summary>
public class GetWorkContractPreview
{
    public record Query(string OrderId, string? Language = null) : IQuery<WorkContractDto>;

    public class Validator : AbstractValidator<Query>
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOrderAccessService _orderAccessService;
        private readonly ICurrencyResolutionService _currencyResolutionService;

        public Validator(
            IOrderRepository orderRepository,
            IOrderAccessService orderAccessService,
            ICurrencyResolutionService currencyResolutionService)
        {
            _orderRepository = orderRepository;
            _orderAccessService = orderAccessService;
            _currencyResolutionService = currencyResolutionService;

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(query => !string.IsNullOrWhiteSpace(query.OrderId))
                .WithMessage(BusinessErrorMessage.Required)
                .Must(query => query.Language is null || query.Language.Length <= 10)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(ExistsAndIsReadableByCallerAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }

        private async Task<bool> ExistsAndIsReadableByCallerAsync(Query query, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            if (string.IsNullOrEmpty(employeeId))
            {
                return false;
            }

            var cleanerCurrencyId = (await _currencyResolutionService.ResolveCurrencyForEmployeeAsync(employeeId, cancellationToken)).Id;

            // The board's floor for a browsing cleaner, not the hold alone: the facts this read discloses
            // are the job's, so an order the board would never show — cancelled, finished, crew-full,
            // unpaid card — must not be readable to anyone holding its id, while the crew term keeps an
            // admin-placed cleaner on a full or in-progress job able to read what they are asked to accept.
            var readable = OrderSpecification.Create(
                id: query.OrderId,
                restrictToEmployeeId: employeeId,
                notHeldFromEmployeeId: employeeId,
                nowUtc: DateTime.UtcNow,
                cleanerCurrencyId: cleanerCurrencyId);

            return await _orderRepository.GetQueryable().AnyAsync(readable.SatisfiedBy(), cancellationToken);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        ILegalDocumentRepository legalDocumentRepository,
        IWorkContractFactsBuilder factsBuilder,
        IOrderAccessService orderAccessService,
        ICompanyInfoRepository companyInfoRepository) : IQueryHandler<Query, WorkContractDto>
    {
        public async Task<BusinessResult<WorkContractDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var order = await orderRepository
                .GetQueryable()
                .Where(o => o.Id == query.OrderId)
                .Select(o => new { o.WorkContractDocumentId, o.TenantId })
                .FirstOrDefaultAsync(cancellationToken);

            var document = order?.WorkContractDocumentId is null
                ? null
                : await legalDocumentRepository.GetWithTextsAsync(order.WorkContractDocumentId, cancellationToken);
            var text = document?.TextForOrFallback(query.Language);
            if (document is null || text is null)
            {
                return BusinessResult.Failure<WorkContractDto>(
                    new Error(nameof(query.OrderId), BusinessErrorMessage.LegalDocumentNotFound));
            }

            var employeeId = (await orderAccessService.GetCallerEmployeeIdAsync(cancellationToken))!;
            var built = await factsBuilder.BuildAsync(query.OrderId, employeeId, cancellationToken);
            if (built is not { Facts: var facts })
            {
                return BusinessResult.Failure<WorkContractDto>(
                    new Error(nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            var company = order!.TenantId is { } operatorTenantId
                ? await companyInfoRepository.GetActiveForOperatorAsync(operatorTenantId, facts.CountryId, cancellationToken)
                : null;

            return BusinessResult.Success(document.MapToWorkContractDto(text, facts, acceptance: null, company));
        }
    }
}
