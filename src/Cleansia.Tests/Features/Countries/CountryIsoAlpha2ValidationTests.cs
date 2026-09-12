using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Countries;

/// <summary>
/// The alpha-2 code is what the market chip prints ("CZ · CZK"), so it is exactly two upper-case
/// letters or it is refused. On update it is optional — absent means unchanged — because the shipped
/// admin form does not send it yet.
/// </summary>
public class CountryIsoAlpha2ValidationTests
{
    private const string CountryId = "country-1";

    private readonly Mock<ICountryRepository> _countries = new();

    public CountryIsoAlpha2ValidationTests()
    {
        _countries.Setup(r => r.ExistsAsync(CountryId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _countries.Setup(r => r.ExistsWithIsoCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
    }

    [Theory]
    [InlineData("CZ", true)]
    [InlineData("cz", false)]
    [InlineData("CZE", false)]
    [InlineData("C", false)]
    [InlineData("C1", false)]
    [InlineData("", false)]
    public async Task Create_Requires_Two_Upper_Case_Letters(string isoAlpha2, bool valid)
    {
        var command = new CreateCountry.Command("CZE", "Czechia", isoAlpha2);

        var result = await new CreateCountry.Validator(_countries.Object).ValidateAsync(command);

        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal(nameof(CreateCountry.Command.IsoAlpha2), error.PropertyName);
            Assert.Contains(error.ErrorMessage, new[] { BusinessErrorMessage.Required, BusinessErrorMessage.CountryIsoAlpha2Invalid });
        }
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("SK", true)]
    [InlineData("sk", false)]
    [InlineData("SVK", false)]
    public async Task Update_Validates_The_Code_Only_When_Sent(string? isoAlpha2, bool valid)
    {
        var command = new UpdateCountry.Command(CountryId, "Slovakia", isoAlpha2);

        var result = await new UpdateCountry.Validator(_countries.Object).ValidateAsync(command);

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(!valid, result.Errors.Any(e =>
            e.PropertyName == nameof(UpdateCountry.Command.IsoAlpha2)
            && e.ErrorMessage == BusinessErrorMessage.CountryIsoAlpha2Invalid));
    }
}
