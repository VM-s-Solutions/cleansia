using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Tests.Features.Packages;

public class PackageServiceWeightTests
{
    [Fact]
    public void New_Included_Service_Defaults_To_The_Even_Split_Weight()
    {
        var package = Package.Create("Deep Clean", "desc", 99m);
        var service = Service.Create("cat", "Windows", "desc", 10m, 0m);

        var included = PackageService.Create(package, service);

        Assert.Equal(PackageService.DefaultPriceWeight, included.PriceWeight);
    }

    [Fact]
    public void A_Three_Service_Bundle_With_Default_Weights_Splits_Price_Into_Thirds()
    {
        var package = Package.Create("Trio", "desc", 100m);
        package
            .AddService(Service.Create("cat", "A", "desc", 1m, 0m))
            .AddService(Service.Create("cat", "B", "desc", 1m, 0m))
            .AddService(Service.Create("cat", "C", "desc", 1m, 0m));

        var weights = package.IncludedServices.Select(ps => ps.PriceWeight).ToList();
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(weights, package.Price);

        Assert.Equal([33.33m, 33.33m, 33.34m], grosses);
        Assert.Equal(package.Price, grosses.Sum());
    }

    [Fact]
    public void Setting_A_Weight_Redistributes_Shares_Without_Touching_Package_Price()
    {
        var package = Package.Create("Duo", "desc", 100m);
        package
            .AddService(Service.Create("cat", "A", "desc", 1m, 0m))
            .AddService(Service.Create("cat", "B", "desc", 1m, 0m));

        var first = package.IncludedServices.First();
        first.SetPriceWeight(3m);

        var weights = package.IncludedServices.Select(ps => ps.PriceWeight).ToList();
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(weights, package.Price);

        Assert.Equal([75m, 25m], grosses);
        Assert.Equal(100m, package.Price);
        Assert.Equal(package.Price, grosses.Sum());
    }
}
