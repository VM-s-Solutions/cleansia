using Cleansia.Core.AppServices.Authentication;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Common;

/// <summary>
/// A real <see cref="OrderAccessService"/> over a suite's repository mock and session, for handlers that
/// read their order through the caller-scoped seam. A session with no Customer role claim resolves to the
/// mock's <c>GetQueryable()</c> / <c>GetByIdAsync()</c> — what every suite written before the seam set up;
/// one carrying the Customer role resolves to the owner-pinned reads, which such a suite sets up itself.
/// </summary>
public static class OrderAccessDoubles
{
    public static OrderAccessService Over(Mock<IOrderRepository> orders, Mock<IUserSessionProvider> session) =>
        new(session.Object, Mock.Of<IEmployeeRepository>(), orders.Object, Mock.Of<ICurrencyResolutionService>());
}
