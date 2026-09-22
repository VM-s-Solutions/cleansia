using Cleansia.Core.AppServices.Features.Orders;

namespace Cleansia.Core.AppServices.Services.Interfaces;

/// <summary>
/// One read-only projection of the job facts a cleaner is shown before accepting the contract for work,
/// used by the preview and the acceptor so the screen and the row cannot differ. Null when the order is
/// not readable in the caller's scope.
/// </summary>
public interface IWorkContractFactsBuilder
{
    Task<WorkContractFacts?> BuildAsync(string orderId, CancellationToken cancellationToken);
}
