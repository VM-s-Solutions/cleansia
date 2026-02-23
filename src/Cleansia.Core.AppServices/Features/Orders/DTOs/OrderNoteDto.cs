namespace Cleansia.Core.AppServices.Features.Orders.DTOs;

public record OrderNoteDto(
    string Id,
    string EmployeeId,
    string Content,
    DateTimeOffset CreatedOn);
