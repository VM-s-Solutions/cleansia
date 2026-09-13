using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Tenancy;

/// <summary>
/// Gives an anonymous <see cref="IOperatorScopedRequest"/> the ambient tenant of its market's operating
/// company BEFORE validation runs (ADR-0061 D3) — the validators' filtered pre-checks are the first
/// tenanted reads, so anything set later is too late. Steps aside when a claim exists: a claim is the
/// tenant, and the request can never override it (S1). Two refusals, and it is this behaviour rather
/// than a validator that owns them because it runs first: a country that is not a market is user input
/// (<c>country.not_serviced</c>); a market nobody operates is a configuration defect
/// (<c>tenant.not_found</c>).
/// </summary>
public sealed class OperatorTenantScopeBehavior<TRequest, TResponse>(
    ITenantProvider tenantProvider,
    IOperatorTenantResolver resolver)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : BusinessResult
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IOperatorScopedRequest scoped || tenantProvider.GetCurrentTenantId() is not null)
        {
            return await next(cancellationToken);
        }

        var (isMarket, operatorTenantId) = await resolver.ResolveAsync(scoped.CountryId, cancellationToken);
        if (!isMarket)
        {
            return Refuse(BusinessErrorMessage.CountryNotServiced);
        }

        if (operatorTenantId is null)
        {
            return Refuse(BusinessErrorMessage.TenantNotFound);
        }

        tenantProvider.SetTenantOverride(operatorTenantId);
        return await next(cancellationToken);
    }

    private static TResponse Refuse(string errorKey) =>
        ValidationPipelineBehavior<TRequest, TResponse>.CreateValidationResult<TResponse>(
            [new Error(nameof(IOperatorScopedRequest.CountryId), errorKey)]);
}
