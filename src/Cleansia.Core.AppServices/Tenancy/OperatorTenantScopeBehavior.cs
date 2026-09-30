using Cleansia.Core.AppServices.Behaviors;
using Cleansia.Core.AppServices.Auditing;
using Cleansia.Core.AppServices.Features.Orders;
using Microsoft.EntityFrameworkCore;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Infra.Common.Validations;
using MediatR;

namespace Cleansia.Core.AppServices.Tenancy;

/// <summary>
/// Establishes operator scope before validation. Guest order requests prove their resource with the
/// complete secret key; other anonymous requests resolve their market and authenticated requests
/// retain their claim scope.
/// </summary>
public sealed class OperatorTenantScopeBehavior<TRequest, TResponse>(
    ITenantProvider tenantProvider,
    IOperatorTenantResolver resolver,
    GuestOrderAccess guestOrderAccess,
    IAuditContext auditContext)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly bool ResponseCarriesFailure = typeof(BusinessResult).IsAssignableFrom(typeof(TResponse));

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IGuestOrderScopedRequest guest)
        {
            var order = await guestOrderAccess.OrdersForKey(guest).AsNoTracking()
                .Select(o => new { o.Id, o.TenantId })
                .FirstOrDefaultAsync(cancellationToken);
            if (order is not null && !string.IsNullOrEmpty(order.TenantId))
            {
                tenantProvider.SetTenantOverride(order.TenantId);
                auditContext.RecordEvidence("Order", order.Id, null);
            }
            else if (tenantProvider.GetCurrentTenantId() is null)
            {
                // An unmatched secret proves no resource; the default only supplies failure-audit scope.
                var (_, fallbackTenantId) = await resolver.ResolveAsync(null, cancellationToken);
                if (!string.IsNullOrEmpty(fallbackTenantId))
                {
                    tenantProvider.SetTenantOverride(fallbackTenantId);
                }
            }
            return await next(cancellationToken);
        }

        if (request is not IOperatorScopedRequest scoped || tenantProvider.GetCurrentTenantId() is not null)
        {
            return await next(cancellationToken);
        }

        var (isMarket, operatorTenantId) = await resolver.ResolveAsync(scoped.CountryId, cancellationToken);
        if (!ResponseCarriesFailure && (!isMarket || operatorTenantId is null))
        {
            // A bare list cannot carry the refusal: it runs with no tenant, the filter shows it no
            // company's rows, and a country that is not an operated market gets an empty answer.
            return await next(cancellationToken);
        }

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
