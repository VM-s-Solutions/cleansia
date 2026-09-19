#nullable enable

namespace Cleansia.Core.AppServices.Features.Reports.Filters;

/// <param name="CurrencyId">
/// The ONE currency the report answers in. Null means the platform default. A report is a sum, and a
/// sum across two currencies is not a number, so the admin chooses which one — there is no
/// "all currencies" report.
/// </param>
public record ReportFilter(
    DateTime StartDate,
    DateTime EndDate,
    string? CurrencyId = null)
{
    /// <summary>
    /// The bounds as UTC instants. A bare date on the query string binds with Kind=Unspecified, which
    /// Npgsql refuses against a <c>timestamp with time zone</c> column; the report's bounds are UTC
    /// calendar bounds, so an unspecified kind is read as UTC and a local one is converted.
    /// </summary>
    public ReportFilter AsUtc() => this with { StartDate = ToUtc(StartDate), EndDate = ToUtc(EndDate) };

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };
}