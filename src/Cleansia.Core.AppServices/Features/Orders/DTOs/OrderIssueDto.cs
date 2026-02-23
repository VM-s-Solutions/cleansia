namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderIssueDto(
    string Id,
    string ReportedByEmployeeId,
    string Description,
    bool IsResolved,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedOn);
