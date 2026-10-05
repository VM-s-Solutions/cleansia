using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>
/// Catalogue repositories whose <c>GetByIds</c> answers from the rows given, filtered by the ids asked for.
/// <c>ExistActiveWithIdsAsync</c> refuses only an id naming a DEACTIVATED row given here: an id the fixture
/// did not populate is a selection outside its subject, as an empty catalogue is 0 minutes to <c>GetByIds</c>.
/// </summary>
internal static class CatalogueDoubles
{
    public static IServiceRepository Services(params Service[] services)
    {
        var mock = new Mock<IServiceRepository>();
        mock.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<string> ids) => services.Where(s => ids.Contains(s.Id)).AsQueryable().BuildMock());
        mock.Setup(r => r.ExistActiveWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
                !services.Any(s => !s.IsActive && ids.Contains(s.Id)));
        return mock.Object;
    }

    public static IPackageRepository Packages(params Package[] packages)
    {
        var mock = new Mock<IPackageRepository>();
        mock.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<string> ids) => packages.Where(p => ids.Contains(p.Id)).AsQueryable().BuildMock());
        mock.Setup(r => r.ExistActiveWithIdsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> ids, CancellationToken _) =>
                !packages.Any(p => !p.IsActive && ids.Contains(p.Id)));
        return mock.Object;
    }

    public static Service Service(string id, int minutes, int minutesPerRoom = 0)
    {
        var service = Core.Domain.Services.Service.Create("category-1", id, "Under test", minutes, minutesPerRoom);
        service.Id = id;
        return service;
    }

    public static Package Package(string id, params Service[] included)
    {
        var package = Core.Domain.Packages.Package.Create(id, "Under test");
        package.Id = id;
        foreach (var service in included)
        {
            package.AddService(service);
        }

        return package;
    }
}
