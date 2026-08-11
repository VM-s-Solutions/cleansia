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

    public static IEmployeePayConfigRepository CoveringServices(params string[] serviceIds) =>
        Holding(serviceIds
            .Select(id => EmployeePayConfig.CreateForService(id, 100m, "czk"))
            .ToArray());

    public static IEmployeePayConfigRepository Covering(string[] serviceIds, string[] packageIds) =>
        Holding(serviceIds
            .Select(id => EmployeePayConfig.CreateForService(id, 100m, "czk"))
            .Concat(packageIds.Select(id => EmployeePayConfig.CreateForPackage(id, 100m, "czk")))
            .ToArray());
}
