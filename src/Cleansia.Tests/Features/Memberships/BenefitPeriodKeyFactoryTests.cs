using Cleansia.Core.AppServices.Services;
using Cleansia.Core.Domain.Company;
using Cleansia.Core.Domain.Configuration;
using Cleansia.Core.Domain.Repositories;
using Moq;

namespace Cleansia.Tests.Features.Memberships;

/// <summary>
/// <c>TC-BENEFIT-PERIOD-0</c> and <c>TC-BENEFIT-PERIOD-1</c> — the calendar boundary is evaluated in the
/// platform's zone, and every call site gets the SAME key for the same instant.
/// </summary>
public class BenefitPeriodKeyFactoryTests
{
    private const string CountryId = "country-cz";

    private readonly Mock<ICompanyInfoRepository> _companyInfo = new();
    private readonly Mock<ICountryConfigurationRepository> _countryConfiguration = new();

    private void ArrangeZone(string? timeZoneId)
    {
        var companyInfo = CompanyInfo.Create(
            legalName: "Cleansia s.r.o.",
            tradingName: "Cleansia",
            registrationNumber: "12345678",
            street: "Street 1",
            city: "Praha",
            zipCode: "11000",
            countryId: CountryId);
        _companyInfo
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyInfo);

        var configuration = CountryConfiguration.Create(
            CountryId, "CZK", "cs", standardVatRate: 21m, timeZoneId: timeZoneId);
        _countryConfiguration
            .Setup(r => r.GetByCountryIdAsync(CountryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
    }

    private BenefitPeriodKeyFactory CreateFactory()
        => new(_companyInfo.Object, _countryConfiguration.Object);

    [Fact]
    public async Task TheKeyIsTheCalendarDiscriminatorPlusTheLocalYearMonth()
    {
        ArrangeZone("Europe/Prague");

        var key = await CreateFactory().GetCalendarKeyAsync(
            new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal("C:2026-08", key);
    }

    /// <summary>
    /// The whole reason the zone is not bare UTC. 00:45 on 1 September in Prague is 22:45 on 31 August
    /// UTC: a bare-UTC key tells a member they have no credits on a day their own calendar calls the 1st,
    /// every month, forever. This test fails against a UTC implementation.
    /// </summary>
    [Fact]
    public async Task TheBoundaryIsTheLocalOneNotTheUtcOne()
    {
        ArrangeZone("Europe/Prague");
        var factory = CreateFactory();

        // 22:45Z on 31 Aug == 00:45 local on 1 Sep (CEST, UTC+2).
        var justAfterLocalMidnight = new DateTime(2026, 8, 31, 22, 45, 0, DateTimeKind.Utc);
        // 21:45Z on 31 Aug == 23:45 local on 31 Aug.
        var justBeforeLocalMidnight = new DateTime(2026, 8, 31, 21, 45, 0, DateTimeKind.Utc);

        Assert.Equal("C:2026-09", await factory.GetCalendarKeyAsync(justAfterLocalMidnight, CancellationToken.None));
        Assert.Equal("C:2026-08", await factory.GetCalendarKeyAsync(justBeforeLocalMidnight, CancellationToken.None));
    }

    /// <summary>
    /// One factory, one rule, so the quote, the create validator, the factory and the membership read
    /// cannot disagree — the defect an order-anchored key produced deterministically for the first two
    /// hours of every month, because three of those four sites have no address to anchor to.
    /// </summary>
    [Fact]
    public async Task RepeatedCallsForTheSameInstantAgree()
    {
        ArrangeZone("Europe/Prague");
        var factory = CreateFactory();
        var instant = new DateTime(2026, 9, 1, 0, 30, 0, DateTimeKind.Utc);

        var first = await factory.GetCalendarKeyAsync(instant, CancellationToken.None);
        var second = await factory.GetCalendarKeyAsync(instant, CancellationToken.None);
        var third = await factory.GetCalendarKeyAsync(instant, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
        // Resolved once and cached for the request; the read is not repeated per call site.
        _companyInfo.Verify(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A pricing call site must never throw over a time zone.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public async Task AnUnusableZoneFallsBackToUtcWithoutThrowing(string? timeZoneId)
    {
        ArrangeZone(timeZoneId);

        var key = await CreateFactory().GetCalendarKeyAsync(
            new DateTime(2026, 8, 31, 22, 45, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal("C:2026-08", key);
    }

    [Fact]
    public async Task NoCompanyInfoFallsBackToUtcWithoutThrowing()
    {
        _companyInfo
            .Setup(r => r.GetActiveCompanyInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyInfo?)null);

        var key = await CreateFactory().GetCalendarKeyAsync(
            new DateTime(2026, 8, 31, 22, 45, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal("C:2026-08", key);
    }
}
