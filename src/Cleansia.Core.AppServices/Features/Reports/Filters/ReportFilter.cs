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
    string? CurrencyId = null);