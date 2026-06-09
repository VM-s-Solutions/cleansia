using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Packages;
using Cleansia.Core.Domain.Services;

namespace Cleansia.Tests.Features.Packages;

/// <summary>
/// Pins the additive <c>PackageDetails.IncludedServiceItems</c> the admin refund UI reads to build a
/// valid bundled-service refund line { ServiceId, PackageId } (ADR-0009 D5). The existing
/// <c>IncludedServices</c> (names only) stays byte-for-byte unchanged so the customer-app and admin
/// package-form consumers are untouched.
/// </summary>
public class PackageMapperIncludedServiceItemsTests
{
    private static Service BuildService(string name)
    {
        return Service.Create(
            categoryId: "cat-1",
            name: name,
            description: $"{name} desc",
            basePrice: 100m,
            perRoomPrice: 10m,
            estimatedTime: 30);
    }

    private static Package BuildPackage(params Service[] services)
    {
        var package = Package.Create("Deluxe", "Deluxe bundle", 500m);
        foreach (var service in services)
        {
            package.AddService(service);
        }

        return package;
    }

    [Fact]
    public void MapToDetails_PopulatesIncludedServiceItems_WithServiceIdAndName()
    {
        var windows = BuildService("Windows");
        var oven = BuildService("Oven");
        var package = BuildPackage(windows, oven);

        var details = package.MapToDetails("CZK");

        var items = details.IncludedServiceItems.ToList();
        Assert.Equal(2, items.Count);
        Assert.Collection(
            items,
            item =>
            {
                Assert.Equal(windows.Id, item.Id);
                Assert.Equal("Windows", item.Name);
            },
            item =>
            {
                Assert.Equal(oven.Id, item.Id);
                Assert.Equal("Oven", item.Name);
            });
    }

    [Fact]
    public void MapToDetails_LeavesIncludedServiceNames_Unchanged()
    {
        var package = BuildPackage(BuildService("Windows"), BuildService("Oven"));

        var details = package.MapToDetails("CZK");

        Assert.Equal(new[] { "Windows", "Oven" }, details.IncludedServices.ToList());
    }

    [Fact]
    public void MapToDetails_ItemIds_MatchTheNamesByPosition()
    {
        var windows = BuildService("Windows");
        var oven = BuildService("Oven");
        var package = BuildPackage(windows, oven);

        var details = package.MapToDetails("CZK");

        var itemNames = details.IncludedServiceItems.Select(i => i.Name).ToList();
        Assert.Equal(details.IncludedServices.ToList(), itemNames);
        Assert.All(details.IncludedServiceItems, item => Assert.False(string.IsNullOrEmpty(item.Id)));
    }
}
