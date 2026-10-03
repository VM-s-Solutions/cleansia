using Cleansia.Core.Domain.Contracts;
using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// Turns "this cleaner accepts text T for this seat of this order" into a staged acceptance row, its
/// audit index row and the e-mail that sends the cleaner a copy of the contract, and freezes on the seat the
/// job figures its reward was priced from. The one writer of <see cref="WorkContractAcceptance"/> and of
/// <see cref="OrderEmployee.JobBasePay"/>; it stages and never commits —
/// the take commits itself so the seat, the status row and the acceptance are one transaction, and the
/// standalone accept rides the UnitOfWork pipeline.
/// </summary>
public interface IWorkContractAcceptor
{
    Task<WorkContractAcceptance> StageAsync(Order order, OrderEmployee seat, string textId, CancellationToken cancellationToken);
}
