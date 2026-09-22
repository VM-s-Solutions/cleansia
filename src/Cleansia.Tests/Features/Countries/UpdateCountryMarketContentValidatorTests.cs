using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Countries;

/// <summary>
/// The insurance ceiling hangs off the configuration row (ADR-0060 D2): a country without one has
/// nowhere to hold it, and creating the row needs a currency, a language and a VAT rate that this
/// command does not take.
/// </summary>
public class UpdateCountryMarketContentValidatorTests
{
    private const string ConfiguredCountryId = "country-configured";
    private const string BareCountryId = "country-bare";

    private readonly Mock<ICountryConfigurationRepository> _configurations = new();

    public UpdateCountryMarketContentValidatorTests()
    {
        _configurations.Setup(r => r.ExistsForCountryAsync(ConfiguredCountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _configurations.Setup(r => r.ExistsForCountryAsync(BareCountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private UpdateCountryMarketContent.Validator Validator() => new(_configurations.Object);

    [Fact]
    public async Task A_Country_With_No_Configuration_Is_Refused()
    {
        var result = await Validator().ValidateAsync(new UpdateCountryMarketContent.Command(BareCountryId, 1_000_000m));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BusinessErrorMessage.CountryConfigurationMissing, error.ErrorMessage);
        Assert.Equal(nameof(UpdateCountryMarketContent.Command.CountryId), error.PropertyName);
    }

    [Theory]
    [InlineData(-1.0, false)]
    [InlineData(0.0, true)]
    [InlineData(null, true)]
    [InlineData(1000000.0, true)]
    public async Task The_Figure_Is_Zero_Or_More_When_Set(double? amount, bool valid)
    {
        var result = await Validator().ValidateAsync(
            new UpdateCountryMarketContent.Command(ConfiguredCountryId, (decimal?)amount));

        Assert.Equal(valid, result.IsValid);
        Assert.Equal(!valid, result.Errors.Any(e =>
            e.PropertyName == nameof(UpdateCountryMarketContent.Command.InsuranceCoverageAmount)
            && e.ErrorMessage == BusinessErrorMessage.MustBePositive));
    }
}
