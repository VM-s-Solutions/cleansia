namespace Cleansia.Core.AppServices.Features.Disputes.DTOs;

public record DisputeMessageDto(
    string Id,
    string Message,
    string AuthorId,
    string AuthorName,
    bool IsStaffMessage,
    DateTimeOffset CreatedOn
);
