using Cleansia.Core.Domain.Orders;

namespace Cleansia.Core.Domain.Repositories;

public interface IExtraRepository : IRepository<Extra, string>
{
    /// <summary>True when any order line references the extra — the one reference that restricts its delete.</summary>
    Task<bool> IsInUseAsync(string extraId, CancellationToken cancellationToken);
}
