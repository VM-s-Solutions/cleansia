using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// One read-only projection of the job facts a cleaner is shown before accepting the contract for work,
/// used by the preview and the acceptor so the screen and the row cannot differ. The price is the seat's
/// reward for <c>employeeId</c>, so the cleaner is the one the contract would bind. Null when the order is
/// not readable in the caller's scope.
/// </summary>
public interface IWorkContractFactsBuilder
{
    Task<WorkContractFacts?> BuildAsync(string orderId, string employeeId, CancellationToken cancellationToken);
}
