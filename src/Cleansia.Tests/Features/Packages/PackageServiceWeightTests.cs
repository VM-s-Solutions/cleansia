using Cleansia.Core.AppServices.Features.Packages;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Tests.Features.Packages;

public class PackageServiceWeightTests
{
    /// <summary>
    /// The bundle's price in the currency under test. It is a local constant now rather than a column
    /// on the package, because a package has a price per currency — and the split is derived from the
    /// weights and THIS number, which is exactly what the cases below hand-derive against.
    /// </summary>
    private const decimal PackagePrice = 100m;

    [Fact]
    public void New_Included_Service_Defaults_To_The_Even_Split_Weight()
    {
        var package = Package.Create("Deep Clean", "desc");
        var service = Service.Create("cat", "Windows", "desc");

        var included = PackageService.Create(package, service);

        Assert.Equal(PackageService.DefaultPriceWeight, included.PriceWeight);
    }

    [Fact]
    public void A_Three_Service_Bundle_With_Default_Weights_Splits_Price_Into_Thirds()
    {
        var package = Package.Create("Trio", "desc");
        package
            .AddService(Service.Create("cat", "A", "desc"))
            .AddService(Service.Create("cat", "B", "desc"))
            .AddService(Service.Create("cat", "C", "desc"));

        var weights = package.IncludedServices.Select(ps => ps.PriceWeight).ToList();
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(weights, PackagePrice);

        Assert.Equal([33.33m, 33.33m, 33.34m], grosses);
        Assert.Equal(PackagePrice, grosses.Sum());
    }

    [Fact]
    public void Setting_A_Weight_Redistributes_Shares_Without_Touching_Package_Price()
    {
        var package = Package.Create("Duo", "desc");
        package
            .AddService(Service.Create("cat", "A", "desc"))
            .AddService(Service.Create("cat", "B", "desc"));

        var first = package.IncludedServices.First();
        first.SetPriceWeight(3m);

        var weights = package.IncludedServices.Select(ps => ps.PriceWeight).ToList();
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(weights, PackagePrice);

        Assert.Equal([75m, 25m], grosses);
        Assert.Equal(PackagePrice, grosses.Sum());
    }
}
