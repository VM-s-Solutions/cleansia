using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// The contract for work a cleaner reads before taking a job: the ORDER's document (stamped at booking),
/// its text in the requested language or the fallback, rendered with the order's currency, and the job
/// facts the acceptance will freeze. The order must be open to the caller exactly as the take requires
/// it, so a held order is a missing order to everyone but its beneficiary.
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
                .MustAsync(ExistsAndIsOpenToCallerAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }

        private async Task<bool> ExistsAndIsOpenToCallerAsync(Query query, CancellationToken cancellationToken)
        {
            var employeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            var cleanerCurrencyId = string.IsNullOrEmpty(employeeId)
                ? null
                : (await _currencyResolutionService.ResolveCurrencyForEmployeeAsync(employeeId, cancellationToken)).Id;

            return await _orderRepository
                .GetQueryable()
                .Where(OrderVisibility.OpenTo(employeeId, cleanerCurrencyId, DateTime.UtcNow))
                .AnyAsync(o => o.Id == query.OrderId, cancellationToken);
        }
    }

    public class Handler(
        IOrderRepository orderRepository,
        ILegalDocumentRepository legalDocumentRepository,
        IWorkContractFactsBuilder factsBuilder) : IQueryHandler<Query, WorkContractDto>
    {
        public async Task<BusinessResult<WorkContractDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var documentId = await orderRepository
                .GetQueryable()
                .Where(o => o.Id == query.OrderId)
                .Select(o => o.WorkContractDocumentId)
                .FirstOrDefaultAsync(cancellationToken);

            var document = documentId is null
                ? null
                : await legalDocumentRepository.GetWithTextsAsync(documentId, cancellationToken);
            var text = document?.TextForOrFallback(query.Language);
            if (document is null || text is null)
            {
                return BusinessResult.Failure<WorkContractDto>(
                    new Error(nameof(query.OrderId), BusinessErrorMessage.LegalDocumentNotFound));
            }

            var facts = await factsBuilder.BuildAsync(query.OrderId, cancellationToken);
            if (facts is null)
            {
                return BusinessResult.Failure<WorkContractDto>(
                    new Error(nameof(query.OrderId), BusinessErrorMessage.OrderNotFound));
            }

            return BusinessResult.Success(document.MapToWorkContractDto(text, facts, acceptance: null));
        }
    }
}
