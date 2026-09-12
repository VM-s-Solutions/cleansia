using Cleansia.Core.Domain.EmployeePayroll;
using Cleansia.Core.Domain.Repositories;
using MockQueryable;
using Moq;

namespace Cleansia.Tests;

/// <summary>
/// A pay-config repository holding exactly the given configs. Every order-creation and approval path
/// now asks the coverage question, so a suite that is about something else still has to answer it.
/// </summary>
internal static class PayConfigRepositoryDouble
{
    public static IEmployeePayConfigRepository Holding(params EmployeePayConfig[] configs)
    {
        var mock = new Mock<IEmployeePayConfigRepository>();
        mock.Setup(r => r.GetAll()).Returns(configs.AsQueryable().BuildMock());
        return mock.Object;
    }

    /// <summary>
    /// Platform-wide rates for the given entries IN ONE CURRENCY. Required first, not defaulted: the
    /// gate filters on it, so a fixture whose order and configs disagree on the id is refused -- which
    /// is the fixture being wrong, not the gate.
    /// </summary>
    public static IEmployeePayConfigRepository CoveringServices(string currencyId, params string[] serviceIds) =>
        Holding(serviceIds
            .Select(id => EmployeePayConfig.CreateForService(id, 100m, currencyId))
            .ToArray());

    public static IEmployeePayConfigRepository Covering(string currencyId, string[] serviceIds, string[] packageIds) =>
        Holding(serviceIds
            .Select(id => EmployeePayConfig.CreateForService(id, 100m, currencyId))
            .Concat(packageIds.Select(id => EmployeePayConfig.CreateForPackage(id, 100m, currencyId)))
            .ToArray());
}
