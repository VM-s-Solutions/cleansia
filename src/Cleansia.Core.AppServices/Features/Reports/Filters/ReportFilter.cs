#nullable enable

namespace Cleansia.Core.AppServices.Features.Reports.Filters;

public record ReportFilter(
    DateTime StartDate,
    DateTime EndDate);