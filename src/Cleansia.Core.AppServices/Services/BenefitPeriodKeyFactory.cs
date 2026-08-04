using System.Globalization;
using Cleansia.Core.AppServices.Common;
using Cleansia.Core.AppServices.Services.Interfaces;
using Cleansia.Core.Domain.Repositories;

namespace Cleansia.Core.AppServices.Services;

/// <inheritdoc cref="IBenefitPeriodKeyFactory"/>
public sealed class BenefitPeriodKeyFactory(
    ICompanyInfoRepository companyInfoRepository,
    ICountryConfigurationRepository countryConfigurationRepository) : IBenefitPeriodKeyFactory
{
    public const string CalendarDiscriminator = "C:";

    // Scoped service, so this caches for the request — CreateOrder resolves the key twice (validator
    // and handler) and both must agree byte-for-byte.
    private TimeZoneInfo? _zone;

    public async Task<string> GetCalendarKeyAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var zone = _zone ??= await ResolvePlatformZoneAsync(cancellationToken);
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        return CalendarDiscriminator + local.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }

    private async Task<TimeZoneInfo> ResolvePlatformZoneAsync(CancellationToken cancellationToken)
    {
        var companyInfo = await companyInfoRepository.GetActiveCompanyInfoAsync(cancellationToken);
        if (companyInfo == null)
        {
            return TimeZoneInfo.Utc;
        }

        var configuration = await countryConfigurationRepository
            .GetByCountryIdAsync(companyInfo.CountryId, cancellationToken);

        return TimeZoneResolution.Resolve(configuration?.TimeZoneId);
    }
}
