using Cleansia.Core.AppServices.Mappers;

namespace Cleansia.Tests.Features.Orders;

/// <summary>
/// The pre-take location label: a valid Prague postcode becomes the district its second digit names
/// ("Praha 4"), however the city spells Prague; every other address is the trimmed city alone. No case
/// carries a postal digit, so each expectation is the whole label rather than a prefix check.
/// </summary>
public class ApproximateAddressTests
{
    [Theory]
    [InlineData("Praha", "140 00", "Praha 4")]
    [InlineData("Praha", "149 00", "Praha 4")]
    [InlineData("Praha", "100 00", "Praha 10")]
    [InlineData("Praha", "102 00", "Praha 10")]
    [InlineData("Praha", "110 00", "Praha 1")]
    [InlineData("Prague", "11000", "Praha 1")]
    [InlineData("Praha 8", "180 00", "Praha 8")]
    [InlineData("Praha 8 - Karlín", "186 00", "Praha 8")]
    [InlineData("Praha 11", "149 00", "Praha 4")]
    [InlineData("Praha-Libuš", "142 00", "Praha 4")]
    public void A_Prague_Postcode_Shows_The_District(string city, string zipCode, string expected)
    {
        Assert.Equal(expected, OrderMappers.BuildApproximateAddress(city, zipCode));
    }

    [Theory]
    [InlineData("Brno", "602 00", "Brno")]
    [InlineData("  Brno ", "602 00", "Brno")]
    [InlineData("Praha", "602 00", "Praha")]
    [InlineData("Praha", null, "Praha")]
    [InlineData("Praha", "1100", "Praha")]
    [InlineData("Praha 8", null, "Praha 8")]
    [InlineData("Прага", "110 00", "Прага")]
    [InlineData("Berlin", "10115", "Berlin")]
    [InlineData("Praha-západ", "25001", "Praha-západ")]
    public void Any_Other_Address_Shows_The_City_Alone(string city, string? zipCode, string expected)
    {
        Assert.Equal(expected, OrderMappers.BuildApproximateAddress(city, zipCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Blank_City_Shows_Nothing(string? city)
    {
        Assert.Equal(string.Empty, OrderMappers.BuildApproximateAddress(city, "140 00"));
    }
}
