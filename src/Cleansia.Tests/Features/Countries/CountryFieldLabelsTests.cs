using Cleansia.Core.AppServices.Features.Countries;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Countries;

/// <summary>
/// The country's own word for its business identifiers.
///
/// <para><b>This existed in the database and nowhere else.</b> <c>CountryConfiguration</c> has carried
/// <c>RegistrationNumberLabel</c> since it was seeded — CZ and SK both set "IČO" — but no endpoint
/// returned it, so every client hardcoded the Czech word in its own translation files. A Ukrainian
/// partner saw "IČO"; a Polish one would too, and Poland has no IČO.</para>
/// </summary>
public class CountryFieldLabelsTests
{
    private readonly Mock<ICountryConfigurationRepository> _configurations = new();

    private GetCountryFieldLabels.Handler Handler() => new(_configurations.Object);

    private void Configured(string countryId, CountryConfiguration? configuration) =>
        _configurations
            .Setup(r => r.GetByCountryIdAsync(countryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

    [Fact]
    public async Task The_Country_Answers_With_Its_Own_Label_And_Format()
    {
        Configured("cze", CountryConfiguration.Create(
            "cze", "CZK", "cs", 0.21m,
            registrationNumberLabel: "IČO",
            registrationNumberFormat: @"^\d{8}$",
            registrationNumberRequired: true,
            vatNumberLabel: "DIČ",
            vatNumberFormat: @"^CZ\d{8,10}$",
            vatNumberRequired: false));

        var labels = await Handler().Handle(new GetCountryFieldLabels.Request("cze"), CancellationToken.None);

        Assert.NotNull(labels);
        Assert.Equal("IČO", labels!.RegistrationNumberLabel);
        Assert.Equal(@"^\d{8}$", labels.RegistrationNumberFormat);
        Assert.True(labels.RegistrationNumberRequired);
        Assert.Equal("DIČ", labels.VatNumberLabel);
        Assert.False(labels.VatNumberRequired);
    }

    /// <summary>
    /// Null, not an empty shell. A country with no configuration is one we know nothing about, and a
    /// client told "no label, not required" would render a business-identity field as optional for a
    /// jurisdiction that in fact demands one.
    /// </summary>
    [Fact]
    public async Task An_Unconfigured_Country_Answers_NULL_Rather_Than_An_Empty_Shell()
    {
        Configured("pol", null);

        Assert.Null(await Handler().Handle(
            new GetCountryFieldLabels.Request("pol"), CancellationToken.None));
    }

    /// <summary>
    /// A configuration that carries no label is not the same as no configuration. The client falls back
    /// to its own neutral string here — "Registration number", correct everywhere and precise nowhere —
    /// while <c>RegistrationNumberRequired</c> still tells it whether to demand a value.
    /// </summary>
    [Fact]
    public async Task A_Configured_Country_With_No_Label_Still_Reports_Whether_It_Demands_One()
    {
        Configured("deu", CountryConfiguration.Create(
            "deu", "EUR", "de", 0.19m,
            registrationNumberLabel: null,
            registrationNumberRequired: true));

        var labels = await Handler().Handle(new GetCountryFieldLabels.Request("deu"), CancellationToken.None);

        Assert.NotNull(labels);
        Assert.Null(labels!.RegistrationNumberLabel);
        Assert.True(labels.RegistrationNumberRequired);
    }
}
