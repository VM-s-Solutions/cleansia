using Cleansia.Core.Domain.Common;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Core.Domain.Packages;

public class PackageService : BaseEntity
{
    public string PackageId { get; private set; }
    public Package? Package { get; private set; }

    public string ServiceId { get; private set; }
    public Service? Service { get; private set; }

    public static PackageService Create(Package package, Service service) => new()
    {
        PackageId = package.Id,
        Package = package,
        ServiceId = service.Id,
        Service = service
    };
}