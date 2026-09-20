using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Turns "this cleaner accepts text T for this seat of this order" into a staged acceptance row and its
/// audit index row. The one writer of <see cref="WorkContractAcceptance"/>; it stages and never commits —
/// the take commits itself so the seat, the status row and the acceptance are one transaction, and the
/// standalone accept rides the UnitOfWork pipeline.
/// </summary>
public interface IWorkContractAcceptor
{
    Task<WorkContractAcceptance> StageAsync(Order order, OrderEmployee seat, string textId, CancellationToken cancellationToken);
}
