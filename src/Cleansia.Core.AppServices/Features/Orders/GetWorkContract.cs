using Cleansia.Core.AppServices.Abstractions;
using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Orders.DTOs;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using FluentValidation;

namespace Cleansia.Core.AppServices.Features.Orders;

/// <summary>
/// An accepted contract for work, keyed on the acceptance so the dropped contract of a re-take stays
/// readable and no employee id travels on the wire. The facts are the STORED ones, never the live order;
/// the text is the accepted document's in the requested language when it has one, else the accepted
/// text, with the accepted language on the DTO so a page can say "accepted in Czech".
///
/// <para>Access is derived from the row: its order must exist for the caller (owner-pinned for a
/// customer, the company's for staff) and, when the caller is a cleaner, the row must be theirs — an
/// ex-crew cleaner keeps reading the contract they accepted; anyone else answers not-found.</para>
/// </summary>
public class GetWorkContract
{
    public record Query(string AcceptanceId, string? Language = null) : IQuery<WorkContractDto>;

    public class Validator : AbstractValidator<Query>
    {
        private readonly IWorkContractAcceptanceRepository _acceptanceRepository;
        private readonly IOrderAccessService _orderAccessService;

        public Validator(
            IWorkContractAcceptanceRepository acceptanceRepository,
            IOrderAccessService orderAccessService)
        {
            _acceptanceRepository = acceptanceRepository;
            _orderAccessService = orderAccessService;

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(query => !string.IsNullOrWhiteSpace(query.AcceptanceId))
                .WithMessage(BusinessErrorMessage.Required)
                .Must(query => query.Language is null || query.Language.Length <= 10)
                .WithMessage(BusinessErrorMessage.MaxLength)
                .MustAsync(ExistsForCallerAsync)
                .WithMessage(BusinessErrorMessage.OrderNotFound);
        }

        private async Task<bool> ExistsForCallerAsync(Query query, CancellationToken cancellationToken)
        {
            var acceptance = await _acceptanceRepository.GetByIdIgnoringTenantAsync(query.AcceptanceId, cancellationToken);
            if (acceptance is null)
            {
                return false;
            }

            if (!await _orderAccessService.OrderExistsForCallerAsync(acceptance.OrderId, cancellationToken))
            {
                return false;
            }

            var callerEmployeeId = await _orderAccessService.GetCallerEmployeeIdAsync(cancellationToken);
            return string.IsNullOrEmpty(callerEmployeeId) || acceptance.EmployeeId == callerEmployeeId;
        }
    }

    public class Handler(
        IWorkContractAcceptanceRepository acceptanceRepository,
        ILegalDocumentRepository legalDocumentRepository) : IQueryHandler<Query, WorkContractDto>
    {
        public async Task<BusinessResult<WorkContractDto>> Handle(Query query, CancellationToken cancellationToken)
        {
            var acceptance = await acceptanceRepository.GetByIdIgnoringTenantAsync(query.AcceptanceId, cancellationToken);
            if (acceptance is null)
            {
                return BusinessResult.Failure<WorkContractDto>(
                    new Error(nameof(query.AcceptanceId), BusinessErrorMessage.OrderNotFound));
            }

            // The text row is FK-guaranteed (Restrict) by the acceptance that names it.
            var document = (await legalDocumentRepository.GetByTextIdWithTextsAsync(acceptance.LegalDocumentTextId, cancellationToken))!;
            var accepted = document.Texts.First(t => t.Id == acceptance.LegalDocumentTextId);

            var rendered = RequestedTextOrAccepted(document, accepted, query.Language);
            var details = new WorkContractAcceptanceDetails(
                AcceptedOn: acceptance.AcceptedOn,
                DocumentVersion: acceptance.DocumentVersion,
                AcceptedLanguage: accepted.Language,
                OrderEmployeeId: acceptance.OrderEmployeeId,
                EmployeeId: acceptance.EmployeeId);

            return BusinessResult.Success(
                document.MapToWorkContractDto(rendered, WorkContractFacts.FromJson(acceptance.FactsJson), details));
        }

        // The requested language by its primary subtag when the document has it; otherwise the text
        // that was accepted — never a third language the reader did not ask for.
        private static Domain.Legal.LegalDocumentText RequestedTextOrAccepted(
            Domain.Legal.LegalDocument document, Domain.Legal.LegalDocumentText accepted, string? language)
        {
            var primary = language is { Length: >= 2 } ? language[..2] : null;
            return (primary is null ? null : document.TextFor(primary)) ?? accepted;
        }
    }
}
