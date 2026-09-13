using System.Text.Json;
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

    internal class Handler(
        ICustomerActionAuditRepository customerActionAuditRepository,
        ICurrencyRepository currencyRepository)
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

            string? currencyCode = null;
            var currencyId = ReadCurrencyId(audit.PayloadJson);
            if (currencyId is not null)
            {
                var currency = await currencyRepository.GetByIdAsync(currencyId, cancellationToken);
                currencyCode = currency?.Code;
            }

            return BusinessResult.Success(audit.MapToDetailDto(currencyCode));
        }
    }

    /// <summary>
    /// The evidence writer records the currency id it already holds (ADR-0062 D3: no extra include on
    /// the act); the reader turns it into a code. Only a top-level <c>currencyId</c> string counts.
    /// </summary>
    public static string? ReadCurrencyId(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payloadJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("currencyId", out var currencyId)
            || currencyId.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = currencyId.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
