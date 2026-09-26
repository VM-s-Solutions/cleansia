using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Repositories;
using Cleansia.Core.Domain.Services;
using MockQueryable;
using Moq;

namespace Cleansia.Tests.Features.Bookings;

/// <summary>Catalogue repositories whose <c>GetByIds</c> answers from the rows given, filtered by the ids asked for.</summary>
internal static class CatalogueDoubles
{
    public static IServiceRepository Services(params Service[] services)
    {
        var mock = new Mock<IServiceRepository>();
        mock.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<string> ids) => services.Where(s => ids.Contains(s.Id)).AsQueryable().BuildMock());
        return mock.Object;
    }

    public static IPackageRepository Packages(params Package[] packages)
    {
        var mock = new Mock<IPackageRepository>();
        mock.Setup(r => r.GetByIds(It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<string> ids) => packages.Where(p => ids.Contains(p.Id)).AsQueryable().BuildMock());
        return mock.Object;
    }

    public static Service Service(string id, int minutes)
    {
        var service = Core.Domain.Services.Service.Create("category-1", id, "Under test", minutes);
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
