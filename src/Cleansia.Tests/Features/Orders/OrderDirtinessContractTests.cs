using System.Text.Json;
using Cleansia.Config.Abstractions;
using Cleansia.Core.AppServices.Features.Orders;
using Cleansia.Core.AppServices.Mappers;
using Cleansia.Core.Domain.Enums;
using Cleansia.Core.Domain.Orders;
using Cleansia.TestUtilities.MockDataFactories.Orders;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The customer, admin and partner hosts all read an order through <c>OrderItem</c> and
/// <c>OrderListItem</c>, so the level the customer booked at and the surcharge it added reach every
/// client from these two shapes: the receipt itemises the surcharge, and a cleaner decides on the job
/// knowing the level.
/// </summary>
public class OrderDirtinessContractTests
{
    private const decimal Surcharge = 229.99m;

    private static Order HeavyOrder()
    {
        var order = OrderMockFactory.Generate();
        order.SetDirtinessSurcharge(DirtinessLevel.Heavy, Surcharge, BookingPolicy.HeavyDirtinessSurchargeRate);
        return order;
    }

    [Fact]
    public void The_Detail_States_The_Level_And_The_Stored_Surcharge_On_The_Wire()
    {
        var detail = HeavyOrder().MapToDetail();

        Assert.Equal(DirtinessLevel.Heavy, detail.DirtinessLevel);
        Assert.Equal(Surcharge, detail.DirtinessSurchargeAmount);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        CleansiaStartupBase.ConfigureJsonSerialization(options);
        var json = JsonSerializer.Serialize(detail, options);
        Assert.Contains("\"dirtinessLevel\":2", json);
        Assert.Contains("\"dirtinessSurchargeAmount\":229.99", json);
    }

    [Fact]
    public void The_List_Row_States_The_Level_And_The_Stored_Surcharge()
    {
        var row = HeavyOrder().MapToDto();

        Assert.Equal(DirtinessLevel.Heavy, row.DirtinessLevel);
        Assert.Equal(Surcharge, row.DirtinessSurchargeAmount);
    }
}
