using Cleansia.Core.AppServices.Features.Packages;

namespace Cleansia.Tests.Features.Packages;

public class PackagePricingTests
{
    [Fact]
    public void Even_Weights_Split_Package_Price_Into_Thirds_With_Last_Absorbing_Residual()
    {
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(
            priceWeights: [1m, 1m, 1m],
            packageLineGross: 100m);

        Assert.Equal([33.33m, 33.33m, 33.34m], grosses);
        Assert.Equal(100m, grosses.Sum());
    }

    [Fact]
    public void Sum_Of_Derived_Grosses_Always_Equals_Package_Line_Gross()
    {
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(
            priceWeights: [1m, 1m, 1m, 1m, 1m, 1m, 1m],
            packageLineGross: 100m);

        Assert.Equal(100m, grosses.Sum());
    }

    [Fact]
    public void Non_Even_Weights_Distribute_Proportionally()
    {
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(
            priceWeights: [1m, 3m],
            packageLineGross: 80m);

        Assert.Equal([20m, 60m], grosses);
        Assert.Equal(80m, grosses.Sum());
    }

    [Fact]
    public void Single_Included_Service_Takes_The_Whole_Package_Price()
    {
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(
            priceWeights: [5m],
            packageLineGross: 49.99m);

        Assert.Equal([49.99m], grosses);
    }

    [Fact]
    public void Residual_Is_Absorbed_By_The_Last_Included_Service_Only()
    {
        var grosses = PackagePricing.DeriveIncludedServiceGrosses(
            priceWeights: [1m, 1m, 1m],
            packageLineGross: 10m);

        Assert.Equal([3.33m, 3.33m, 3.34m], grosses);
        Assert.Equal(10m, grosses.Sum());
    }

    [Fact]
    public void Reweighting_Redistributes_Shares_But_Sum_Still_Equals_Package_Price()
    {
        var even = PackagePricing.DeriveIncludedServiceGrosses([1m, 1m, 1m], 100m);
        var skewed = PackagePricing.DeriveIncludedServiceGrosses([2m, 1m, 1m], 100m);

        Assert.NotEqual(even[0], skewed[0]);
        Assert.Equal(100m, even.Sum());
        Assert.Equal(100m, skewed.Sum());
    }

    [Fact]
    public void Zero_Package_Price_Yields_All_Zero_Grosses()
    {
        var grosses = PackagePricing.DeriveIncludedServiceGrosses([1m, 1m, 1m], 0m);

        Assert.Equal([0m, 0m, 0m], grosses);
    }

    [Fact]
    public void Empty_Weights_Yield_An_Empty_Result()
    {
        var grosses = PackagePricing.DeriveIncludedServiceGrosses([], 100m);

        Assert.Empty(grosses);
    }

    [Fact]
    public void Non_Positive_Total_Weight_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PackagePricing.DeriveIncludedServiceGrosses([0m, 0m], 100m));
    }
}
