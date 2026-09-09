using Cleansia.Core.Domain.Orders;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests;

/// <summary>
/// An extras repository holding exactly the given rows.
///
/// <para>Every order-creation path now asks for them, because an order's extras are rows it owns
/// rather than a JSON map on the order — so a suite that is about something else entirely still has
/// to answer the question. <see cref="Empty"/> is the answer for the suites that book no extras,
/// which is nearly all of them.</para>
/// </summary>
internal static class ExtraRepositoryDouble
{
    public static IExtraRepository Holding(params Extra[] extras)
    {
        var mock = new Mock<IExtraRepository>();
        mock.Setup(r => r.GetAll()).Returns(extras.AsQueryable().BuildMock());
        return mock.Object;
    }

    public static IExtraRepository Empty() => Holding();

    /// <summary>
    /// Extras priced by slug, for the suites that do book one. The price becomes the order row's
    /// frozen <c>UnitPrice</c>, which is what the refund allocator then weights by.
    /// </summary>
    public static IExtraRepository Priced(params (string Slug, decimal Price)[] extras) =>
        Holding(extras.Select(e => Extra.Create(e.Slug, e.Slug, null, e.Price)).ToArray());
}
